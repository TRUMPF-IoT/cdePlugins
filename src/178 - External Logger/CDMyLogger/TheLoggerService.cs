// SPDX-FileCopyrightText: 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

﻿using System;
using System.Collections.Generic;

// TODO: Add reference for C-DEngine.dll
// TODO: Make sure plugin file name starts with either CDMy or C-DMy
using nsCDEngine.BaseClasses;
using nsCDEngine.Engines;
using nsCDEngine.Engines.NMIService;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.ViewModels;
using CDMyLogger.ViewModel;
using nsCDEngine.Communication;

namespace CDMyLogger
{
    public class eTheLoggerServiceTypes : TheDeviceTypeEnum
    {
        public const string GELF = "Graylog GELF";
        public const string StatusLogger = "Status Logger";
        public const string TextLogger = "Text Logger";
        public const string IISLogger = "IIS Logger";
        public const string RSSLogger = "RSS Logger";
    }

    class LoggerService : ICDEPlugin, ICDEThing, ICDELoggerEngine
    {
        // Base object references 
        protected TheThing MyBaseThing;      // Base thing
        private IBaseEngine MyBaseEngine;    // Base engine (service)

        // Initialization flags
        protected bool mIsInitStarted = false;
        protected bool mIsInitCompleted = false;
        protected bool mIsUXInitStarted = false;
        protected bool mIsUXInitCompleted = false;

        Guid guidEngineID = new Guid("{F389DF53-8424-4806-A8CD-B63F81436B2C}"); // TODO: Set GUID value for InitEngineAssets (in the next block)
        String strFriendlyName = "Logging Service";               // TODO: Set plugin friendly name for InitEngineAssets (optional)

        #region ICDEPlugin - interface methods for service (engine)
        public IBaseEngine GetBaseEngine()
        {
            return MyBaseEngine;
        }

        /// <summary>
        /// InitEngineAssets - The C-DEngine calls this initialization
        /// function as part of registering this service (engine)
        /// </summary>
        /// <param name="pBase">The C-DEngine creates a base engine object.
        /// This parameter is a reference to that base engine object.
        /// We keep a copy because it will be very useful to us.
        /// </param>
        public void InitEngineAssets(IBaseEngine pBase)
        {
            MyBaseEngine = pBase;

            MyBaseEngine.SetEngineID(guidEngineID);
            MyBaseEngine.SetFriendlyName(strFriendlyName);

            MyBaseEngine.SetEngineName(GetType().FullName);  // Can be any arbitrary name - recommended is the class name
            MyBaseEngine.SetEngineType(GetType());           // Has to be the type of this class
            MyBaseEngine.SetEngineService(true);             // Keep True if this class is a service

            MyBaseEngine.SetPluginInfo("This service will push C-DEngine logs to differnet loggin services",       // Describe plugin for Plugin Store
                                       0,                       // pPrice - retail price (default = 0)
                                       null,                    // Custom home page - default = /ServiceID
                                       "toplogo-150.png",       // pIcon - custom icon.
                                       "C-Labs",                // pDeveloper - name of the plugin developer.
                                       "http://www.c-labs.com", // pDeveloperUrl - URL to developer home page.
                                       new List<string>() { }); // pCategories - Search categories for service.

            MyBaseEngine.AddCapability(eThingCaps.LoggerEngine);
            MyBaseEngine.AddCapability(eThingCaps.ConfigManagement);
        }
        #endregion

        #region ICDEThing - interface methods (rare to override)
        public bool IsInit()
        {
            return mIsInitCompleted;
        }
        public bool IsUXInit()
        {
            return mIsUXInitCompleted;
        }

        public void SetBaseThing(TheThing pThing)
        {
            MyBaseThing = pThing;
        }
        public TheThing GetBaseThing()
        {
            return MyBaseThing;
        }

        public cdeP GetProperty(string pName, bool DoCreate)
        {
            return MyBaseThing?.GetProperty(pName, DoCreate);
        }
        public cdeP SetProperty(string pName, object pValue)
        {
            return MyBaseThing?.SetProperty(pName, pValue);
        }

        public void RegisterEvent(string pName, Action<ICDEThing, object> pCallBack)
        {
            MyBaseThing?.RegisterEvent(pName, pCallBack);
        }
        public void UnregisterEvent(string pName, Action<ICDEThing, object> pCallBack)
        {
            MyBaseThing?.UnregisterEvent(pName, pCallBack);
        }
        public void FireEvent(string pEventName, ICDEThing sender, object pPara, bool FireAsync)
        {
            MyBaseThing?.FireEvent(pEventName, sender, pPara, FireAsync);
        }
        public bool HasRegisteredEvents(string pEventName)
        {
            if (MyBaseThing != null)
                return MyBaseThing.HasRegisteredEvents(pEventName);
            return false;
        }
        #endregion

        public bool Init()
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

                bool DoLogKPIs =TheCommonUtils.CBool(TheBaseAssets.MySettings.GetSetting("LogKPIs"));
                if (DoLogKPIs)
                    TheThing.SetSafePropertyBool(MyBaseThing, "LogKPIs", true);

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
            if ((timer%5)==0)
            {
                if (TheThing.GetSafePropertyBool(MyBaseThing,"LogKPIs"))
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

        public bool CreateUX()
        {
            if (!mIsUXInitStarted)
            {
                mIsUXInitStarted = true;

                mMyDashboard = TheNMIEngine.AddDashboard(MyBaseThing, new TheDashboardInfo(MyBaseEngine, "System Logger") { PropertyBag = new ThePropertyBag() { "Category=Diagnostics", "Caption=<i class='fa faIcon fa-5x'>&#xf0f6;</i><br>System Logger" } });

                var tFlds = TheNMIEngine.CreateEngineForms(MyBaseThing, TheThing.GetSafeThingGuid(MyBaseThing, "LOGGER"), "Logger Services", null, 20, 0x0F, 0xF0, TheNMIEngine.GetNodeForCategory(), "REFFRESHME", true, new eTheLoggerServiceTypes(), eTheLoggerServiceTypes.GELF);
                (tFlds["DashIcon"] as TheDashPanelInfo).PanelTitle = "Logger Services";
                var tForm = tFlds["Form"] as TheFormInfo;
                tForm.AddButtonText = "Add new Logger Service";


                var tF = TheNMIEngine.AddStandardForm(MyBaseThing, "Global Logger Settings", 6, TheThing.GetSafeThingGuid(MyBaseThing, "LOGGERSETTINGS").ToString(), null, 0xF0,TheNMIEngine.GetNodeForCategory());
                var tMyUserSettingsForm = tF["Form"] as TheFormInfo;
                TheNMIEngine.AddSmartControl(MyBaseThing, tMyUserSettingsForm, eFieldType.SingleCheck, 10, 2, 0x80, "Disable CDE-Log to Console", "DisableStandardLog", new nmiCtrlSingleCheck() { ParentFld = 1, TileWidth=3 });
                TheNMIEngine.AddSmartControl(MyBaseThing, tMyUserSettingsForm, eFieldType.SingleCheck, 20, 2, 0x80, "Use GELF to Console", "UseGELF", new nmiCtrlSingleCheck() { ParentFld = 1, TileWidth=3 });
                TheNMIEngine.AddSmartControl(MyBaseThing, tMyUserSettingsForm, eFieldType.SingleCheck, 40, 2, 0, "Log KPIs", "LogKPIs", new nmiCtrlSingleCheck() { ParentFld = 1, TileWidth = 3 });

                TheNMIEngine.RegisterEngine(MyBaseEngine);      //Registers this engine and its "SmartPage" with the System
                mIsUXInitCompleted = true;
            }
            return true;
        }
        public bool Delete()
        {
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
                            var tt=CreateOrUpdateService<TheTextLogger>(tDev, true);
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
                    TheBaseAssets.MySYSLOG.WriteToLog(25010, new TSM(MyBaseEngine.GetEngineName(), $"Could not auto-create Event Log. Address {tLogName} - Path: {tThin?.MyCurLog}", eMsgLevel.l1_Error,e.ToString()));
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
            if (pDeletedThing != null && pDeletedThing is ICDEThing)
            {
                //TODO: Stop Resources, Thread etc associated with this Thing
                ((ICDEThing)pDeletedThing).FireEvent(eEngineEvents.ShutdownEvent, pEngine, null, false);
            }
        }

        //TODO: Step 4: Write your Business Logic

        #region Message Handling
        public void HandleMessage(ICDEThing sender, object pIncoming)
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
        #endregion

        public bool LogEvent(TheEventLogData pItem)
        {
            bool bRet = false;
            List<TheThing> tDevList = TheThingRegistry.GetThingsOfEngine(MyBaseThing.EngineName);
            if (tDevList?.Count > 0)
            {
                foreach (TheThing tDev in tDevList)
                {
                    var tTwin = tDev.GetObject() as ICDELoggerEngine;
                    var tRes=tTwin?.LogEvent(pItem);
                    if (!bRet && tRes==true)
                        bRet = true;
                }
            }
            return bRet;
        }

        public List<TheEventLogData> GetEvents(int PageNo, int PageSize, bool LatestFirst)
        {
            return null; //TODO implement
        }

    }
}
