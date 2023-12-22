// SPDX-FileCopyrightText: 2009-2023 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using System;
using System.Collections.Generic;

using nsCDEngine.Engines;
using nsCDEngine.BaseClasses;
using nsCDEngine.ViewModels;
using nsCDEngine.Engines.NMIService;
using nsCDEngine.Engines.ThingService;

using CDMyNetwork.ViewModel;

namespace CDMyNetwork
{
    public class eNetworkServiceTypes : TheDeviceTypeEnum
    {
        public const string PingService = "Ping Service";
    }

    [EngineAssetInfo(
        FriendlyName ="Network Services",
        EngineID = "{2592E784-2EDF-4691-A0CD-B2BBCFDAF9DD}",
        //AllowIsolation = true, AllowNodeHop = true,
        LongDescription = "This service allows to monitors network services",
        IconUrl = "FA3:f6ff",
        Developer = "C-Labs",
        Categories = ["Service"],
        DeveloperUrl = "http://www.c-labs.com"
        )]
    partial class MyNetworkServices : ThePluginBase
    {
        #region ICDEPlugin
        /// <summary>
        /// This constructor is called by The C-DEngine during initialization in order to register this service
        /// </summary>
        /// <param name="pBase">The C-DEngine is creating a Host for this engine and hands it to the Plugin Service</param>
        public override void InitEngineAssets(IBaseEngine pBase)
        {
            base.InitEngineAssets(pBase);
            MyBaseEngine.SetIsolationFlags(true, true);
        }
        #endregion

        public override bool Init()
        {
            if (mIsInitCalled) return false;
            mIsInitCalled = true;

            MyBaseThing.RegisterStatusChanged(sinkStatChanged);
            MyBaseThing.StatusLevel = 4;
            MyBaseThing.Value = "0";
            MyBaseThing.RegisterEvent(eEngineEvents.IncomingMessage, HandleMessage);
            MyBaseEngine.RegisterEvent(eEngineEvents.ThingDeleted, OnThingDeleted);
            MyBaseEngine.RegisterEvent(eEngineEvents.ThingRegistered, OnThingRegisterd);

            if (!TheBaseAssets.MyServiceHostInfo.IsCloudService && !TheThing.GetSafePropertyBool(MyBaseThing, "RanBefore"))
            {
                TheThing.SetSafePropertyBool(MyBaseThing, "RanBefore", true);
                string tAutoPing = TheBaseAssets.MySettings.GetSetting("AutoPing");
                if (!string.IsNullOrEmpty(tAutoPing))
                {
                    TheThing tThing = new TheThing();
                    tThing.EngineName = MyBaseEngine.GetEngineName();
                    tThing.DeviceType = eNetworkServiceTypes.PingService;
                    tThing.Address = tAutoPing;
                    tThing.FriendlyName = tAutoPing;
                    TheThing.SetSafePropertyBool(tThing, "AllowRTT", true);
                    TheThing.SetSafePropertyBool(tThing, "AutoConnect", true);
                    TheThingRegistry.RegisterThing(tThing);
                }
            }

            if (MyBaseEngine.GetEngineState().IsService)
            {
                TheCommonUtils.cdeRunAsync("Init Networkers", true, (o) =>
                  {
                      InitNetworkServices();
                      mIsInitialized = true;
                      FireEvent(eThingEvents.Initialized, this, true, true);
                      MyBaseEngine.ProcessInitialized();
                  });
            }
            return false;
        }

        public override bool Delete()
        {
            OnThingDeleted(this, this);
            mIsInitialized = false;
            return true;
        }

        void OnThingRegisterd(ICDEThing thing, object para)
        {
            InitNetworkServices();
        }

        void OnThingDeleted(ICDEThing tThing, object deletedThing)
        {
            if (deletedThing is ICDEThing thing)
            {
                var fileServer = thing.GetBaseThing().GetObject() as TheNetworkServiceBase;
                fileServer?.Disconnect();
            }
        }

        void sinkStatChanged(cdeP prop)
        {
            RootDashPanel?.SetUXProperty(Guid.Empty, string.Format("Background={0}", TheNMIEngine.GetStatusColor(MyBaseThing.StatusLevel)));
        }

        private void InitNetworkServices()
        {
            List<TheThing> tDeviceList = TheThingRegistry.GetThingsOfEngine(MyBaseThing.EngineName);

            foreach (TheThing tDevice in tDeviceList)
            {
                switch (tDevice.DeviceType)
                {
                    case eNetworkServiceTypes.PingService:
                        CreateOrUpdateService<ThePingService>(tDevice, true);
                        break;
                }
            }
            MyBaseEngine.SetStatusLevel(-1);
            sinkStatChanged(null);
        }

        T CreateOrUpdateService<T>(TheThing tDevice, bool bRegisterThing) where T : TheNetworkServiceBase
        {
            T tServer;
            if (tDevice == null || !tDevice.HasLiveObject)
            {
                tServer = (T)Activator.CreateInstance(typeof(T), tDevice, this);
                if (bRegisterThing)
                {
                    TheThingRegistry.RegisterThing(tServer);
                    tServer.GetBaseThing().RegisterOnChange("DeviceType", OnServerDeviceTypeChanged);
                }
            }
            else
            {
                tServer = tDevice.GetObject() as T;
                if (tServer != null)
                {
                    //tServer.InitServer(null)
                }
                else
                {
                    tServer = (T)Activator.CreateInstance(typeof(T), tDevice, this);
                }
            }
            return tServer;
        }

        void OnServerDeviceTypeChanged(cdeP prop)
        {
            // Brute force: reinitialize all servers
            InitNetworkServices();
        }

        #region Message Handling
        public override void HandleMessage(ICDEThing sender, object pIncoming)
        {
            TheProcessMessage pMsg = pIncoming as TheProcessMessage;
            if (pMsg == null) return;

            string[] cmd = pMsg.Message.TXT.Split(':');

            switch (cmd[0])
            {
                case "UPDATE_VALUE":
                    ScanAllServices();
                    break;
                case "REFRESH_DASH":
                    InitNetworkServices();
                    mMyDashboard.Reload(pMsg, false);
                    break;
                case "CDE_INITIALIZED":
                    MyBaseEngine.SetInitialized(pMsg.Message);
                    break;
                case "CDE_INITIALIZE":
                    if (MyBaseEngine.GetEngineState().IsService)
                    {
                        if (!MyBaseEngine.GetEngineState().IsEngineReady)
                            MyBaseEngine.SetEngineReadiness(true, null);
                        MyBaseEngine.ReplyInitialized(pMsg.Message);
                    }
                    break;
                default:
                    TheThing tt = TheThingRegistry.GetThingByProperty(MyBaseEngine.GetEngineName(), Guid.Empty, "ID", pMsg.Message.PLS);
                    tt?.HandleMessage(sender, pMsg);
                    break;
            }
        }

        private void ScanAllServices()
        {
            int CombinedCode = 0;
            List<TheThing> tDeviceList = TheThingRegistry.GetThingsOfEngine(MyBaseThing.EngineName);
            foreach (TheThing tDevice in tDeviceList)
            {
                if (tDevice.HasLiveObject)
                {
                    TheNetworkServiceBase tHid = tDevice.GetObject() as TheNetworkServiceBase;
                    if (tHid?.GetBaseThing().StatusLevel > 1)
                        CombinedCode++; 
                }
            }
            MyBaseEngine.SetStatusLevel(-1);
            MyBaseEngine.LastMessage = "Scanning done";
            MyBaseThing.Value = CombinedCode.ToString();
            sinkStatChanged(null);
        }
        #endregion
    }
}
