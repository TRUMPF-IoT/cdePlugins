// SPDX-FileCopyrightText: 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

﻿using System;
using System.Collections.Generic;

using nsCDEngine.BaseClasses;
using nsCDEngine.Engines;
using nsCDEngine.Engines.NMIService;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.ViewModels;
using CDMyLogger.ViewModel;
using nsCDEngine.Communication;
using nsCDEngine.Engines.StorageService;

namespace CDMyLogger
{
    public class eTheLoggerServiceTypes : TheDeviceTypeEnum
    {
        public const string GELF = "Graylog GELF";
        public const string StatusLogger = "Status Logger";
        public const string TextLogger = "Text Logger";
        public const string IISLogger = "IIS Logger";
        public const string RSSLogger = "RSS Logger";
        public const string InternalLogger = "Internal Logger";
        public const string DiscordLogger = "Discord Logger";
    }

    class LoggerService : ThePluginBase, ICDELoggerEngine
    {
        // Initialization flags
        protected bool mIsInitStarted = false;
        protected bool mIsInitCompleted = false;
        protected bool mIsUXInitStarted = false;
        protected bool mIsUXInitCompleted = false;

        Guid guidEngineID = new Guid("{F389DF53-8424-4806-A8CD-B63F81436B2C}"); // TODO: Set GUID value for InitEngineAssets (in the next block)
        String strFriendlyName = "Logging Service";               // TODO: Set plugin friendly name for InitEngineAssets (optional)

        [ConfigProperty(Required = true, Generalize = true)]
        public bool PublishEvents
        {
            get { return TheThing.MemberGetSafePropertyBool(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyBool(MyBaseThing, value); }
        }

        public override void InitEngineAssets(IBaseEngine pBase)
        {
            base.InitEngineAssets(pBase);
            MyBaseEngine.SetEngineID(guidEngineID);
            MyBaseEngine.SetFriendlyName(strFriendlyName);
            MyBaseEngine.SetPluginInfo("This service will push C-DEngine logs to differnet loggin services",       // Describe plugin for Plugin Store
                                       0,                       // pPrice - retail price (default = 0)
                                       null,                    // Custom home page - default = /ServiceID
                                       "toplogo-150.png",       // pIcon - custom icon.
                                       "C-Labs",                // pDeveloper - name of the plugin developer.
                                       "http://www.c-labs.com", // pDeveloperUrl - URL to developer home page.
                                       new List<string>() { }); // pCategories - Search categories for service.
            MyBaseEngine.AddManifestFiles(new List<string> { "Discord.Net.Core.dll", "Discord.Net.Rest.dll", "Discord.Net.Webhook.dll", "Newtonsoft.Json.dll" });
            MyBaseEngine.AddCapability(eThingCaps.LoggerEngine);
            MyBaseEngine.AddCapability(eThingCaps.ConfigManagement);
        }

        public override bool Init()
        {
            if (!mIsInitStarted)
            {
                mIsInitStarted = true;
                MyBaseThing.StatusLevel = 4;
                MyBaseThing.LastMessage = "Logger Service has started";

                MyBaseThing.RegisterEvent(eEngineEvents.IncomingMessage, HandleMessage);
                MyBaseEngine.RegisterEvent(eEngineEvents.ThingDeleted, OnThingDeleted);

                cdeP tP = null;
                if (TheBaseAssets.MyServiceHostInfo.DisableConsole)
                    TheThing.SetSafePropertyBool(MyBaseThing, "DisableStandardLog", TheBaseAssets.MyServiceHostInfo.DisableConsole);
                else
                    tP = GetProperty("DisableStandardLog", true);
                tP.RegisterEvent(eThingEvents.PropertyChanged, sinkDisableChanged);
                if (TheCommonUtils.CBool(tP.ToString()))
                    TheBaseAssets.MyServiceHostInfo.DisableConsole = true;

                if (TheBaseAssets.MyServiceHostInfo.UseGELFLoggingFormat)
                    tP = TheThing.SetSafePropertyBool(MyBaseThing, "UseGELF", TheBaseAssets.MyServiceHostInfo.UseGELFLoggingFormat);
                else
                    tP = GetProperty("UseGELF", true);
                tP.RegisterEvent(eThingEvents.PropertyChanged, sinkGELF);
                if (TheCommonUtils.CBool(tP.ToString()))
                    TheBaseAssets.MyServiceHostInfo.UseGELFLoggingFormat = true;

                bool DoLogKPIs = TheCommonUtils.CBool(TheBaseAssets.MySettings.GetSetting("LogKPIs"));
                if (DoLogKPIs)
                    TheThing.SetSafePropertyBool(MyBaseThing, "LogKPIs", true);
                bool DoPublish = TheCommonUtils.CBool(TheBaseAssets.MySettings.GetSetting("PublishEvents"));
                if (DoPublish)
                    PublishEvents = DoPublish;

                TheQueuedSenderRegistry.RegisterHealthTimer(sinkTimer);
                // If not lengthy initialized you can remove cdeRunasync and call this synchronously
                TheCommonUtils.cdeRunAsync(MyBaseEngine.GetEngineName() + " Init Services", true, (o) =>
                {
                    // Perform any long-running initialization (i.e. network access, file access) here
                    InitServices();
                    MyBaseEngine.ProcessInitialized(); //Set the status of the Base Engine according to the status of the Things it manages
                    mIsInitCompleted = true;
                });
            }
            return false;
        }



        void sinkTimer(long timer)
        {
            if ((timer % 5) == 0)
            {
                if (TheThing.GetSafePropertyBool(MyBaseThing, "LogKPIs"))
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(23099, new TSM(MyBaseEngine.GetEngineName(), TheCDEKPIs.GetKPIs(true), eMsgLevel.l4_Message));
                }
            }
        }

        void sinkDisableChanged(cdeP p)
        {
            TheBaseAssets.MyServiceHostInfo.DisableConsole = TheCommonUtils.CBool(p);
        }
        void sinkGELF(cdeP p)
        {
            TheBaseAssets.MyServiceHostInfo.UseGELFLoggingFormat = TheCommonUtils.CBool(p);
        }

        public override bool CreateUX()
        {
            if (!mIsUXInitStarted)
            {
                mIsUXInitStarted = true;

                mMyDashboard = TheNMIEngine.AddDashboard(MyBaseThing, new TheDashboardInfo(MyBaseEngine, "System Logger") { PropertyBag = new ThePropertyBag() { "Category=Diagnostics", "Caption=<i class='fa faIcon fa-5x'>&#xf0f6;</i><br>System Logger" } });

                var tFlds = TheNMIEngine.CreateEngineForms(MyBaseThing, TheThing.GetSafeThingGuid(MyBaseThing, "LOGGER"), "Logger Services", null, 20, 0x0F, 0xF0, TheNMIEngine.GetNodeForCategory(), "REFFRESHME", true, new eTheLoggerServiceTypes(), eTheLoggerServiceTypes.GELF);
                (tFlds["DashIcon"] as TheDashPanelInfo).PanelTitle = "Logger Services";
                var tForm = tFlds["Form"] as TheFormInfo;
                tForm.AddButtonText = "Add new Logger Service";


                var tF = TheNMIEngine.AddStandardForm(MyBaseThing, "Global Logger Settings", 6, TheThing.GetSafeThingGuid(MyBaseThing, "LOGGERSETTINGS").ToString(), null, 0xF0, TheNMIEngine.GetNodeForCategory());
                var tMyUserSettingsForm = tF["Form"] as TheFormInfo;
                TheNMIEngine.AddSmartControl(MyBaseThing, tMyUserSettingsForm, eFieldType.SingleCheck, 10, 2, 0x80, "Disable CDE-Log to Console", "DisableStandardLog", new nmiCtrlSingleCheck() { ParentFld = 1, TileWidth = 3 });
                TheNMIEngine.AddSmartControl(MyBaseThing, tMyUserSettingsForm, eFieldType.SingleCheck, 20, 2, 0x80, "Use GELF to Console", "UseGELF", new nmiCtrlSingleCheck() { ParentFld = 1, TileWidth = 3 });
                TheNMIEngine.AddSmartControl(MyBaseThing, tMyUserSettingsForm, eFieldType.SingleCheck, 40, 2, 0, "Log KPIs", "LogKPIs", new nmiCtrlSingleCheck() { ParentFld = 1, TileWidth = 3 });
                TheNMIEngine.AddSmartControl(MyBaseThing, tMyUserSettingsForm, eFieldType.SingleCheck, 50, 2, 0, "Publish Events", nameof(PublishEvents), new nmiCtrlSingleCheck() { ParentFld = 1, TileWidth = 3 });
                var but=TheNMIEngine.AddSmartControl(MyBaseThing, tMyUserSettingsForm, eFieldType.TileButton, 60, 2, 0, "Test Log Entry", null, new nmiCtrlTileButton { NoTE = true, ClassName = "cdeGoodActionButton", ParentFld=1 });
                but.RegisterUXEvent(MyBaseThing, eUXEvents.OnClick, "TestLog", (sender, obj) => { 
                    SetMessage("Test Event Log Entry","TESTLOG",-1,DateTimeOffset.Now,178001,eMsgLevel.l6_Debug);
                });
                TheNMIEngine.RegisterEngine(MyBaseEngine);      //Registers this engine and its "SmartPage" with the System
                mIsUXInitCompleted = true;
            }
            return true;
        }

        TheDashboardInfo mMyDashboard;
        void InitServices()
        {
            bool CreatedATextLogger = false;
            List<TheThing> tDevList = TheThingRegistry.GetThingsOfEngine(MyBaseThing.EngineName);
            if (tDevList.Count > 0)
            {
                foreach (TheThing tDev in tDevList)
                {
                    switch (tDev.DeviceType)
                    {
                        case eTheLoggerServiceTypes.TextLogger:
                            var tt = CreateOrUpdateService<TheTextLogger>(tDev, true);
                            CreatedATextLogger = true;
                            break;
                        case eTheLoggerServiceTypes.GELF:
                            CreateOrUpdateService<TheGELF>(tDev, true);
                            break;
                        case eTheLoggerServiceTypes.StatusLogger:
                            CreateOrUpdateService<TheStatusLogger>(tDev, true);
                            break;
                        case eTheLoggerServiceTypes.IISLogger:
                            CreateOrUpdateService<TheIISLogger>(tDev, true);
                            break;
                        case eTheLoggerServiceTypes.RSSLogger:
                            CreateOrUpdateService<TheRSSFeed>(tDev, true);
                            break;
                        case eTheLoggerServiceTypes.InternalLogger:
                            CreateOrUpdateService<TheInternalLogger>(tDev, true);
                            break;
                        case eTheLoggerServiceTypes.DiscordLogger:
                            CreateOrUpdateService<TheDiscordLogger>(tDev, true);
                            break;
                    }
                }
            }
            if (!CreatedATextLogger && TheBaseAssets.MyCmdArgs?.ContainsKey("CreateEventLog") == true)
            {
                var tLogName = TheBaseAssets.MyCmdArgs["CreateEventLog"];
                TheTextLogger tThin = null;
                try
                {
                    tThin = CreateOrUpdateService<TheTextLogger>(null, true);
                    tThin.FriendlyName = $"Auto Event Log: {(string.IsNullOrEmpty(tLogName) ? "EventLog" : tLogName)}";
                    tThin.Address = string.IsNullOrEmpty(tLogName) ? "EventLog" : tLogName;
                    tThin.AutoConnect = true;
                    tThin.EachDayInOwnFile = true;
                    tThin.Connect(null);
                }
                catch (Exception e)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(25010, new TSM(MyBaseEngine.GetEngineName(), $"Could not auto-create Event Log. Address {tLogName} - Path: {tThin?.MyCurLog}", eMsgLevel.l1_Error, e.ToString()));
                }
            }
            MyBaseEngine.SetStatusLevel(-1); //Calculates the current statuslevel of the service/engine
        }

        T CreateOrUpdateService<T>(TheThing tDevice, bool bRegisterThing) where T : TheLoggerBase
        {
            T tServer;
            if (tDevice == null || !tDevice.HasLiveObject)
            {
                tServer = (T)Activator.CreateInstance(typeof(T), tDevice, this);
                if (bRegisterThing)
                    TheThingRegistry.RegisterThing((ICDEThing)tServer);
            }
            else
            {
                tServer = tDevice.GetObject() as T;
                if (tServer != null)
                    tServer.Connect(null);    //Expose a Connect(TheProcessMessage) method on TheThing
                else
                    tServer = (T)Activator.CreateInstance(typeof(T), tDevice, this);
            }
            return tServer;
        }

        void OnThingDeleted(ICDEThing pEngine, object pDeletedThing)
        {
            if (pDeletedThing is ICDEThing thing)
            {
                thing.FireEvent(eEngineEvents.ShutdownEvent, pEngine, null, false);
            }
        }

        public override void HandleMessage(ICDEThing sender, object pIncoming)
        {
            TheProcessMessage pMsg = pIncoming as TheProcessMessage;
            if (pMsg == null) return;

            string[] cmd = pMsg.Message.TXT.Split(':');
            switch (cmd[0])
            {
                case "CDE_INITIALIZED":
                    MyBaseEngine.SetInitialized(pMsg.Message);      //Sets the Service to "Ready". ProcessInitialized() internally contains a call to this Handler and allows for checks right before SetInitialized() is called. 
                    break;
                case "REFFRESHME":
                    InitServices();
                    mMyDashboard.Reload(pMsg, false);
                    break;
            }
        }

        public bool LogEvent(TheEventLogData pItem)
        {
            if (pItem == null)
                return false;
            bool bRet = false;
            List<TheThing> tDevList = TheThingRegistry.GetThingsOfEngine(MyBaseThing.EngineName);
            if (tDevList?.Count > 0)
            {
                foreach (TheThing tDev in tDevList)
                {
                    var tTwin = tDev.GetObject() as ICDELoggerEngine;
                    var tRes = tTwin?.LogEvent(pItem);
                    if (!bRet && tRes == true)
                        bRet = true;
                }
            }
            if (PublishEvents) //This allows to have a logger on a different node
                TheCommCore.PublishCentral(new TSM(eEngineName.ContentService, eEngineEvents.NewEventLogEntry, pItem.EventLevel, TheCommonUtils.SerializeObjectToJSONString(pItem)), true);
            return bRet;
        }

        public List<TheEventLogData> GetEvents(int PageNo, int PageSize, bool LatestFirst)
        {
            return new();
        }

    }
}
