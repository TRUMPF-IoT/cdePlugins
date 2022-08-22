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

namespace CDMyLogger.ViewModel
{
    class TheLoggerBase : TheThingBase, ICDELoggerEngine
    {
        // Base object references 
        protected IBaseEngine MyBaseEngine;    // Base engine (service)

        // User-interface defintion
        protected TheFormInfo MyStatusForm;
        protected TheDashPanelInfo MyStatusFormDashPanel;

        //
        // C-DEngine properties are wrapped inside C# properties.
        // This is a recommended practice.
        // Also recommended, the use of the 'GetSafe...' and 'SetSafe...' methods.
        public bool IsConnected
        {
            get { return TheThing.MemberGetSafePropertyBool(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyBool(MyBaseThing, value); }
        }

        [ConfigProperty(Generalize = true)]
        public bool AutoConnect
        {
            get { return TheThing.MemberGetSafePropertyBool(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyBool(MyBaseThing, value); }

        }
        [ConfigProperty(Generalize = true)]
        public bool LogRemoteEvents
        {
            get { return TheThing.MemberGetSafePropertyBool(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyBool(MyBaseThing, value); }
        }

        [ConfigProperty(Required = true, Generalize = true)]
        public string Address
        {
            get { return TheThing.MemberGetSafePropertyString(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyString(MyBaseThing, value); }
        }
        [ConfigProperty(Generalize = true)]
        public string FriendlyName
        {
            get { return TheThing.MemberGetSafePropertyString(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyString(MyBaseThing, value); }
        }

        public TheLoggerBase()
        {
        }

        public TheLoggerBase(TheThing tBaseThing, ICDEPlugin pPluginBase)
        {
            MyBaseThing = tBaseThing ?? new TheThing();
            MyBaseEngine = pPluginBase.GetBaseEngine();
            MyBaseThing.EngineName = MyBaseEngine.GetEngineName();
            MyBaseThing.SetIThingObject(this);
        }

        public override bool Init()
        {
            if (!mIsInitCalled)
            {
                mIsInitCalled = true;
                IsConnected = false;
                MyBaseEngine.RegisterEvent(eEngineEvents.ShutdownEvent, DoEndMe);
                MyBaseThing.LastMessage = "Logger Ready";
                MyBaseThing.StatusLevel = 0;
                DoInit();
                if (AutoConnect)
                    Connect(null);
                mIsInitialized = true;
            }
            return true;
        }

        protected virtual void DoInit()
        {

        }

        protected virtual void DoEndMe(ICDEThing pEngine, object notused)
        {

        }


        public override bool CreateUX()
        {
            if (!mIsUXInitCalled)
            {
                mIsUXInitCalled = true;

                var tHead = TheNMIEngine.AddStandardForm(MyBaseThing, MyBaseThing.FriendlyName,12, null,null,0,$"..Event Logs on {TheCommonUtils.GetMyNodeName()}");
                MyStatusForm = tHead["Form"] as TheFormInfo; 
                MyStatusFormDashPanel = tHead["DashIcon"] as TheDashPanelInfo;
                var tBlock = TheNMIEngine.AddStatusBlock(MyBaseThing, MyStatusForm, 2);
                tBlock["Group"].SetParent(1);

                tBlock = TheNMIEngine.AddConnectivityBlock(MyBaseThing, MyStatusForm, 120, sinkConnect);
                TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.SingleCheck, 135, 2, 0xf0, "Log Remote Events", nameof(LogRemoteEvents), new nmiCtrlSingleCheck() { TileWidth = 3, ParentFld = 120 });
                tBlock["Group"].SetParent(1);
                DoCreateUX(tHead["Form"] as TheFormInfo);
                mIsUXInitialized = true;
            }
            return true;
        }

        protected virtual void DoCreateUX(TheFormInfo pForm)
        {

        }

        void sinkConnect(TheProcessMessage pMsg, bool DoConnect)
        {
            if (DoConnect)
                Connect(pMsg);
            else
                Disconnect(pMsg);
        }

        public virtual void Connect(TheProcessMessage pMsg)
        {
            IsConnected = true;
            MyBaseThing.StatusLevel = 1;
            MyBaseThing.LastMessage = $"Connected to Logger at {DateTimeOffset.Now}";
            TheBaseAssets.MySYSLOG.RegisterEvent2("NewLogEntry", OnNewEvent);
            if (LogRemoteEvents)
                TheCDEngines.MyContentEngine.RegisterEvent(eEngineEvents.NewEventLogEntry, sinkLogMe);
        }
        public virtual void Disconnect(TheProcessMessage pMsg)
        {
            IsConnected = false;
            MyBaseThing.StatusLevel = 0;
            MyBaseThing.LastMessage = $"Disconnected from Logger at {DateTimeOffset.Now}";
            TheBaseAssets.MySYSLOG.UnregisterEvent2("NewLogEntry", OnNewEvent);
            if (LogRemoteEvents)
                TheCDEngines.MyContentEngine.UnregisterEvent(eEngineEvents.NewEventLogEntry, sinkLogMe);
        }

        protected virtual void sinkLogMe(ICDEThing sender, object para)
        {
            if (para is not TheEventLogData tData)
                return;
            if (!TheCommonUtils.IsLocalhost(tData.cdeN))
                LogEvent(para as TheEventLogData);
        }

        public virtual void OnNewEvent(TheProcessMessage timer, object unused)
        {
        }

        #region Message Handling
        public virtual bool LogEvent(TheEventLogData pItem)
        {
            return false;
        }

        public virtual List<TheEventLogData> GetEvents(int PageNo, int PageSize, bool LatestFirst)
        {
            return new();
        }
        #endregion
    }
}
