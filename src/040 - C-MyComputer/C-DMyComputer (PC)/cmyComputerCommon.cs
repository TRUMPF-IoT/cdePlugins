// SPDX-FileCopyrightText: 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

﻿using System;
using System.Collections.Generic;

using nsCDEngine.Engines;
using nsCDEngine.BaseClasses;
using nsCDEngine.ViewModels;

using nsCDEngine.Engines.NMIService;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.Security;
using nsCDEngine.Communication;
using System.IO;
using nsCDEngine.Engines.StorageService;

namespace CDMyComputer
{
    public partial class TheCDMyComputerEngine : ThePluginBase
    {
        public override void InitEngineAssets(IBaseEngine pBase)
        {
            base.InitEngineAssets(pBase);
            MyBaseEngine.SetFriendlyName("Computer Management");
            MyBaseEngine.SetEngineID(new Guid("{8FB08B63-1A2D-4DF6-8DC8-BADB12500F94}"));
            MyBaseEngine.GetEngineState().IsAcceptingFilePush = true;
           // MyBaseEngine.RegisterJSEngine(null);
            MyBaseEngine.AddCapability(eThingCaps.ComputerHealth);
            MyBaseEngine.AddCapability(eThingCaps.HardwareAccess);
            MyBaseEngine.AddCapability(eThingCaps.DoNotIsolate);
            MyBaseEngine.SetPluginInfo("Monitors the health of all your Nodes", 0, null, "FA3:f108", "C-Labs and its licensors", "http://www.c-labs.com", new List<string>());
        }

        public int HealthCollectionCycle
        {
            get { return TheCommonUtils.CInt(TheThing.GetSafePropertyString(MyBaseThing, "HealthCollectionCycle")); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, "HealthCollectionCycle", value); }
        }

        public bool IsHealthCollectionOff
        {
            get { return TheThing.GetSafePropertyBool(MyBaseThing, "IsHealthCollectionOff"); }
            set { TheThing.SetSafePropertyBool(MyBaseThing, "IsHealthCollectionOff", value); }
        }
        public int SensorDelay
        {
            get { return TheCommonUtils.CInt(TheThing.GetSafePropertyString(MyBaseThing, "SensorDelay")); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, "SensorDelay", value); }
        }
        public double SensorAccelDeadband
        {
            get { return TheCommonUtils.CDbl(TheThing.GetSafePropertyString(MyBaseThing, "SensorAccelDeadband")); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, "SensorAccelDeadband", value); }
        }

        public override bool Init()
        {
            if (mIsInitCalled) return false;
            mIsInitCalled = true;

            MyBaseThing.StatusLevel = 4;
            string temp;
            TheBaseAssets.MyCmdArgs.TryGetValue("IsHealthCollectionOff", out temp);
            if (!string.IsNullOrEmpty(temp))
                IsHealthCollectionOff = TheCommonUtils.CBool(temp);
            if (HealthCollectionCycle == 0)
            {
                TheBaseAssets.MyCmdArgs.TryGetValue("HealthCollectionCycle", out temp);
                if (!string.IsNullOrEmpty(temp))
                    HealthCollectionCycle = TheCommonUtils.CInt(temp);
                if (HealthCollectionCycle == 0)
                    HealthCollectionCycle = 15;
            }

            if (SensorDelay == 0)
            {
                TheBaseAssets.MyCmdArgs.TryGetValue("SensorDelay", out temp);
                if (!string.IsNullOrEmpty(temp))
                    SensorDelay = TheCommonUtils.CInt(temp);
                if (SensorDelay == 0)
                    SensorDelay = 500;
            }

            if (SensorAccelDeadband < 1)
            {
                TheBaseAssets.MyCmdArgs.TryGetValue("SensorAccelDeadband", out temp);
                if (!string.IsNullOrEmpty(temp))
                    SensorAccelDeadband = TheCommonUtils.CDbl(temp);
                if (SensorAccelDeadband < 0.1)
                    SensorAccelDeadband = 3.0;
            }
            int tBS = (int)TheThing.GetSafePropertyNumber(MyBaseThing, "ChartValues");
            if (tBS < 10)
            {
                tBS = 1000;
                TheThing.SetSafePropertyNumber(MyBaseThing, "ChartValues", tBS);
            }
            tBS = (int)TheThing.GetSafePropertyNumber(MyBaseThing, "ChartSize");
            if (tBS < 6)
            {
                tBS = 18;
                TheThing.SetSafePropertyNumber(MyBaseThing, "ChartSize", tBS);
            }

            TheUserManager.RegisterNewRole(new TheUserRole(new Guid("{0A254170-D4D4-4B2D-9E05-D471729BE739}"), "ComputerManager", 1, new Guid("{3FB56264-9AA8-4AC9-9208-A01F1142B153}"), true, "Person allowed to view Computer Details"));
            MyBaseThing.RegisterEvent(eEngineEvents.IncomingMessage, HandleMessage);        //Event when C-DEngine has new Telegram for this service as a subscriber (Client Side)
            MyBaseThing.RegisterEvent("FileReceived", sinkFileReceived);
            MyBaseThing.RegisterEvent(eEngineEvents.ShutdownEvent, sinkEngineShutdown);
            StartEngineServices();
            if (MyBaseThing.StatusLevel == 4)
                MyBaseThing.StatusLevel = 1;
            mIsInitialized = true;
            return true;
        }

        void sinkFileReceived(ICDEThing pThing, object pFileName)
        {
            TheProcessMessage pMsg = pFileName as TheProcessMessage;
            if (pMsg?.Message == null) return;
            try
            {
                string tGuid = $"THH{Guid.NewGuid()}";
                //File.Copy(TheCommonUtils.cdeFixupFileName(pMsg.Message.TXT), TheCommonUtils.cdeFixupFileName($"/cache/{tGuid}"));
                CreateTHHUx(tGuid, TheCommonUtils.cdeFixupFileName(pMsg.Message.TXT));
                TheCommCore.PublishToOriginator(pMsg.Message, new TSM(eEngineName.NMIService, "NMI_TOAST", string.Format("KPI File ({0}) received - Creating UX", pMsg.Message.TXT)));
                MyPCVitalsDashboard.Reload(pMsg, true);
            }
            catch (Exception)
            {
                TheCommCore.PublishToOriginator(pMsg.Message, new TSM(eEngineName.NMIService, "NMI_ERROR", $"KPI File ({pMsg.Message.TXT}) received but creating UX failed!"));
            }
        }

        private void CreateTHHUx(string tGuid, string FileToImport)
        {
            var MyServiceHealthDataStore = new TheStorageMirror<TheThingStore>(TheCDEngines.MyIStorageService);
            MyServiceHealthDataStore.IsRAMStore = true;
            MyServiceHealthDataStore.IsCachePersistent = true;
            MyServiceHealthDataStore.CacheStoreInterval = 200;
            MyServiceHealthDataStore.IsStoreIntervalInSeconds = true;
            MyServiceHealthDataStore.CacheTableName = tGuid;
            MyServiceHealthDataStore.InitializeStore(true, false, FileToImport);

            int tBS = (int)TheThing.GetSafePropertyNumber(MyBaseThing, "ChartValues");
            if (tBS < 10)
                tBS = 10;
            if (tBS > 10000)
                tBS = 10000;


            int tCS = (int)TheThing.GetSafePropertyNumber(MyBaseThing, "ChartSize");
            if (tCS < 6)
                tCS = 6;

            TheNMIEngine.AddChartScreen(MyBaseThing, new TheChartDefinition(TheThing.GetSafeThingGuid(MyBaseThing, tGuid), tGuid, tBS, tGuid, true, "", "PB.HostAddress", "PB.QSenders,PB.QSLocalProcessed,PB.QSSent,PB.QKBSent,PB.QKBReceived,PB.QSInserted,PB.EventTimeouts,PB.TotalEventTimeouts,PB.CCTSMsRelayed,PB.CCTSMsReceived,PB.CCTSMsEvaluated,PB.HTCallbacks,PB.KPI1,PB.KPI2,PB.KPI3,PB.KPI4,PB.KPI5,PB.KPI10") { GroupMode = 0, IntervalInMS = 0 }, 5, 3, 0, "Customer KPIs", false, new ThePropertyBag() { ".TileHeight=8", ".NoTE=true", ".TileWidth=" + tCS });
        }

        void FindTHHFiles()
        {
            //TODO: these files should not be in the Cache folder but in the CMYC folder, then copied to the cache folder with new APIs
            var tCacheDir = TheCommonUtils.cdeFixupFileName("cccccccc");
            tCacheDir = tCacheDir.Replace("cccccccc", "cache");
            DirectoryInfo di = new DirectoryInfo(tCacheDir);
            List<string> tTHHs = new List<string>();
            FileInfo[] fileInfo = di.GetFiles();
            foreach (FileInfo fiNext in fileInfo)
            {
                if (!fiNext.Name.StartsWith("THH")) continue;
                CreateTHHUx(fiNext.Name, null);
            }
        }
    }
}
