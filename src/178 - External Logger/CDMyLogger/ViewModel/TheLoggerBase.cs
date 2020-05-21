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

namespace CDMyLogger.ViewModel
{
    class TheLoggerBase : ICDEThing, ICDELoggerEngine
    {
        // Base object references 
        protected TheThing MyBaseThing;      // Base thing
        protected IBaseEngine MyBaseEngine;    // Base engine (service)

        // Initialization flags
        protected bool mIsInitStarted = false;
        protected bool mIsInitCompleted = false;
        protected bool mIsUXInitStarted = false;
        protected bool mIsUXInitCompleted = false;

        // User-interface defintion
        protected TheFormInfo MyStatusForm;
        protected TheDashPanelInfo MyStatusFormDashPanel;

        //
        // C-DEngine properties are wrapped inside C# properties.
        // This is a recommended practice.
        // Also recommended, the use of the 'GetSafe...' and 'SetSafe...' methods.
        public bool IsConnected
        {
            get { return TheThing.GetSafePropertyBool(MyBaseThing, "IsConnected"); }
            set { TheThing.SetSafePropertyBool(MyBaseThing, "IsConnected", value); }
        }

        [ConfigProperty(Required = true, Generalize = true)]
        public bool AutoConnect
        {
            get { return TheThing.GetSafePropertyBool(MyBaseThing, "AutoConnect"); }
            set { TheThing.SetSafePropertyBool(MyBaseThing, "AutoConnect", value); }
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
                IsConnected = false;
                MyBaseEngine.RegisterEvent(eEngineEvents.ShutdownEvent, DoEndMe);
                MyBaseThing.LastMessage = "Logger Ready";
                MyBaseThing.StatusLevel = 0;
                DoInit();
                if (AutoConnect)
                    Connect(null);
                mIsInitCompleted = true;
            }
            return true;
        }

        protected virtual void DoInit()
        {

        }

        protected virtual void DoEndMe(ICDEThing pEngine, object notused)
        {

        }


        public bool CreateUX()
        {
            if (!mIsUXInitStarted)
            {
                mIsUXInitStarted = true;

                var tHead = TheNMIEngine.AddStandardForm(MyBaseThing, MyBaseThing.FriendlyName);
                MyStatusForm = tHead["Form"] as TheFormInfo; // TheNMIEngine.AddForm(new TheFormInfo(MyBaseThing) { FormTitle = MyBaseThing.DeviceType, DefaultView = eDefaultView.Form, PropertyBag = new ThePropertyBag { "MaxTileWidth=6" } });
                MyStatusFormDashPanel = tHead["DashIcon"] as TheDashPanelInfo;
                var tBlock = TheNMIEngine.AddStatusBlock(MyBaseThing, MyStatusForm, 2);
                tBlock["Group"].SetParent(1);

                tBlock = TheNMIEngine.AddConnectivityBlock(MyBaseThing, MyStatusForm, 120, sinkConnect);
                tBlock["Group"].SetParent(1);
                DoCreateUX(tHead["Form"] as TheFormInfo);
                mIsUXInitCompleted = true;
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
        }
        public virtual void Disconnect(TheProcessMessage pMsg)
        {
            IsConnected = false;
            MyBaseThing.StatusLevel = 0;
            MyBaseThing.LastMessage = $"Disconnected from Logger at {DateTimeOffset.Now}";
            TheBaseAssets.MySYSLOG.UnregisterEvent2("NewLogEntry", OnNewEvent);
        }


        public virtual void OnNewEvent(TheProcessMessage timer, object unused)
        {
        }

        public virtual bool Delete()
        {
            return true;
        }

        #region Message Handling
        public virtual void HandleMessage(ICDEThing sender, object pIncoming)
        {
            TheProcessMessage pMsg = pIncoming as TheProcessMessage;
            if (pMsg == null) return;

            string[] cmd = pMsg.Message.TXT.Split(':');
            switch (cmd[0])
            {
                default:
                    break;
            }
        }

        public virtual bool LogEvent(TheEventLogData pItem)
        {
            return false;
        }

        public virtual List<TheEventLogData> GetEvents(int PageNo, int PageSize, bool LatestFirst)
        {
            return null;
        }
        #endregion
    }
}
