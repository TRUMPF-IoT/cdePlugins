// SPDX-FileCopyrightText: 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

﻿using nsCDEngine.BaseClasses;
using nsCDEngine.Engines;
using nsCDEngine.Engines.NMIService;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.ViewModels;
using System;
using System.Collections.Generic;

namespace CDMyVThings.ViewModel
{
    [DeviceType(DeviceType = eVThings.eDataGenerator, Capabilities = new [] {  eThingCaps.ConfigManagement, eThingCaps.SensorContainer })]
    public class TheDataGenerator : ICDEThing
    {
        #region ICDEThing Methods
        public void SetBaseThing(TheThing pThing)
        {
            MyBaseThing = pThing;
        }
        public TheThing GetBaseThing()
        {
            return MyBaseThing;
        }
        public virtual cdeP GetProperty(string pName, bool DoCreate)
        {
            return MyBaseThing?.GetProperty(pName, DoCreate);
        }
        public virtual cdeP SetProperty(string pName, object pValue)
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

        public virtual void HandleMessage(ICDEThing sender, object pMsg)
        { }


        protected TheThing MyBaseThing;

        protected bool mIsUXInitCalled;
        protected bool mIsUXInitialized;
        protected bool mIsInitCalled;
        protected bool mIsInitialized;
        public bool IsUXInit()
        { return mIsUXInitialized; }
        public bool IsInit()
        { return mIsInitialized; }

        /// <summary>
        /// The possible types of WeMo devices that can be detected
        /// </summary>
        #endregion

        private IBaseEngine MyBaseEngine;

        [ConfigProperty]
        public long Gen_Config_NumberOfActiveProperties
        {
            get { return (long)TheThing.MemberGetSafePropertyNumber(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyNumber(MyBaseThing, value); }
        }

        [ConfigProperty]
        public long Gen_Config_PropertyUpdateInterval
        {
            get { return (long)TheThing.MemberGetSafePropertyNumber(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyNumber(MyBaseThing, value); }
        }

        public double Gen_Stats_PropertiesPerSecond
        {
            get { return TheThing.MemberGetSafePropertyNumber(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyNumber(MyBaseThing, value); }
        }
        public double Gen_Stats_PropertyCounter
        {
            get { return TheThing.MemberGetSafePropertyNumber(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyNumber(MyBaseThing, value); }
        }
        public DateTimeOffset Gen_Stats_UpdateTime
        {
            get { return TheThing.MemberGetSafePropertyDate(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyDate(MyBaseThing, value); }
        }

        public long Gen_Config_StatsUpdateInterval
        {
            get { return (long)TheThing.MemberGetSafePropertyNumber(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyNumber(MyBaseThing, value); }
        }

        public bool IsStarted
        {
            get { return TheThing.MemberGetSafePropertyBool(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyBool(MyBaseThing, value); }
        }

        [ConfigProperty(DefaultValue = false)]
        public bool IsDisabled
        {
            get { return TheThing.MemberGetSafePropertyBool(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyBool(MyBaseThing, value); }
        }
        [ConfigProperty(DefaultValue = false)]
        public bool AutoStart
        {
            get { return TheThing.MemberGetSafePropertyBool(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyBool(MyBaseThing, value); }
        }

        System.Threading.Timer m_Timer;

        void OnChangeTimer(object t)
        {
            bFirstRun = true;
            if (IsDisabled)
            {
                StopGenerator();
                return;
            }
            if (Gen_Config_PropertyUpdateInterval > 0)
            {
                if (m_Timer == null)
                {
                    m_Timer = new System.Threading.Timer(GenerateStressThingData, this, 5000, System.Threading.Timeout.Infinite); // Wait 5 s to give receivers time to initialize. 
                }
                else
                {
                    m_Timer.Change(Gen_Config_PropertyUpdateInterval, System.Threading.Timeout.Infinite);
                }
                if (!g_sw.IsRunning)
                {
                    g_sw.Start();
                }
                IsStarted = true;
                MyBaseThing.StatusLevel = 1;
                MyBaseThing.LastMessage = "Generator started";
            }
            else
            {
                StopGenerator();
            }
        }

        private void StopGenerator()
        {
            if (m_Timer != null)
            {
                m_Timer.Dispose();
            }
            m_Timer = null;
            IsStarted = false;
            MyBaseThing.StatusLevel = 0;
            MyBaseThing.LastMessage = "Generator stopped";
        }

        long propGenerateCounter;
        int valueCounter;

        System.Diagnostics.Stopwatch g_sw = new System.Diagnostics.Stopwatch();

        bool b35_Running = false;
        bool bFirstRun = true;
        void GenerateStressThingData(object stressThingObj)
        {
            if (!TheBaseAssets.MasterSwitch)
            {
                m_Timer?.Change(System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);
                m_Timer?.Dispose();
                m_Timer = null;
                return;
            }
            var sw = new System.Diagnostics.Stopwatch();
            sw.Start();
            var now = DateTimeOffset.UtcNow;
            for (int i = 1; i <= this.Gen_Config_NumberOfActiveProperties; i++)
            {
                var value = (double)valueCounter++; //r.Next(0, 1000);
                if (valueCounter % 25 == 0)
                {
                    value = double.NaN;
                }
                var propName = String.Format("Gen_Prop{0:D4}", i);
                if (bFirstRun && this.MyBaseThing.GetProperty(propName, false)?.IsSensor != true)
                {
                    this.MyBaseThing.DeclareSensorProperty(propName, ePropertyTypes.TNumber, new cdeP.TheSensorMeta { });
                }
                this.MyBaseThing.SetProperty(propName, value, now);

                var newCount = System.Threading.Interlocked.Increment(ref propGenerateCounter);
            }
            bFirstRun = false;

            if (TheThing.GetSafePropertyBool(MyBaseThing, "Gen_Config_35Running"))
            {
                MyBaseThing.SetProperties(new Dictionary<string, object>
                {
                    {  "35.Running", b35_Running },
                    {  "35.Ended", !b35_Running },
                    {  "35.Aborted", false },
                    {  "35.StoppedOperator", false },
                    {  "35.StoppedMalfunction", false },
                }, now);
                b35_Running = !b35_Running;
            }

            if (g_sw.ElapsedMilliseconds > this.Gen_Config_StatsUpdateInterval)
            {
                long elapsed = 0;
                lock (g_sw)
                {
                    elapsed = g_sw.ElapsedMilliseconds;
                    if (elapsed > this.Gen_Config_StatsUpdateInterval)
                    {
                        g_sw.Restart();
                    }
                }
                if (elapsed > this.Gen_Config_StatsUpdateInterval)
                {
                    var newCount = System.Threading.Interlocked.Exchange(ref propGenerateCounter, 0);
                    this.Gen_Stats_PropertyCounter += newCount;
                    this.Gen_Stats_PropertiesPerSecond = newCount / (elapsed / 1000.0);
                    this.Gen_Stats_UpdateTime = DateTimeOffset.Now;
                    TheBaseAssets.MySYSLOG.WriteToLog(700, TSM.L(eDEBUG_LEVELS.FULLVERBOSE) ? null : new TSM("Data Generator", "Generate", eMsgLevel.l6_Debug, String.Format("Generate Rate: {0,11:N2} properties/s", this.Gen_Stats_PropertiesPerSecond)));
                }

            }
            sw.Stop();
            long newDueTime = this.Gen_Config_PropertyUpdateInterval - sw.ElapsedMilliseconds;
            if (newDueTime < 0)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(700, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("Data Generator", "Generate", eMsgLevel.l6_Debug, String.Format("Falling behing by {0} ms", -newDueTime)));
                newDueTime = 0;
            }
            try
            {
                if (m_Timer != null)
                {
                    m_Timer.Change(newDueTime, System.Threading.Timeout.Infinite);
                }
            }
            catch (Exception)
            {

            }
        }


        public virtual bool Init()
        {
            if (mIsInitCalled) return false;
            mIsInitCalled = true;

            if (string.IsNullOrEmpty(MyBaseThing.ID))
            {
                MyBaseThing.ID = Guid.NewGuid().ToString();
                this.Gen_Config_StatsUpdateInterval = 5000;
            }
            MyBaseThing.Value = "0";
            TheThing.SetSafePropertyBool(MyBaseThing, "IsActive", true);
            MyBaseThing.GetProperty(nameof(Gen_Config_PropertyUpdateInterval), true).RegisterEvent(eThingEvents.PropertyChanged, OnChangeTimer);
            MyBaseThing.GetProperty(nameof(Gen_Config_NumberOfActiveProperties), true).RegisterEvent(eThingEvents.PropertyChanged, OnChangeTimer);
            MyBaseThing.StatusLevel = 0;
            if (TheThing.GetSafePropertyBool(MyBaseThing, "AutoStart"))
                OnChangeTimer(null);
            mIsInitialized = true;
            return true;
        }

        public bool Delete()
        {
            mIsInitialized = false;
            // TODO Properly implement delete
            return true;
        }

        protected TheFormInfo MyStatusForm;
        protected TheDashPanelInfo SummaryForm;
        protected TheFieldInfo CountBar;
        protected TheFieldInfo PropTable;
        public virtual bool CreateUX()
        {
            if (mIsUXInitCalled) return false;
            mIsUXInitCalled = true;

            var tFlds = TheNMIEngine.AddStandardForm(MyBaseThing, null, 12, null, "Value");
            MyStatusForm = tFlds["Form"] as TheFormInfo;
            SummaryForm = tFlds["DashIcon"] as TheDashPanelInfo;
            SummaryForm.PropertyBag = new ThePropertyBag() { string.Format("Format={0}<br>{{0}} Properties", MyBaseThing.FriendlyName), "TileWidth=2" };
            SummaryForm.RegisterPropertyChanged(sinkStatChanged);

            var ts = TheNMIEngine.AddStatusBlock(MyBaseThing, MyStatusForm, 10);
            ts["Group"].SetParent(1);

            var tc = TheNMIEngine.AddStartingBlock(MyBaseThing, MyStatusForm, 20, (pMsg, DoStart) =>
            {
                if (DoStart)
                {
                    OnChangeTimer(null);
                }
                else
                {
                    StopGenerator();
                }
            });
            tc["Group"].SetParent(1);
            ts["Group"].Header = "Settings...";

            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.Number, 51, 2, 0x0, "###CDMyVThings.TheVThings#NumberProperties187329#Number Properties###", nameof(Gen_Config_NumberOfActiveProperties), new nmiCtrlNumber() { TileWidth=3, ParentFld = 20 });
            CountBar = TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.Number, 52, 2, 0x0, "###CDMyVThings.TheVThings#Frequencyms741318#Frequency###", nameof(Gen_Config_PropertyUpdateInterval), new nmiCtrlNumber() { TileWidth = 3, ParentFld = 20 });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.Number, 53, 0, 0x0, "###CDMyVThings.TheVThings#PropertiesperSecond546134#Properties per Second###", nameof(Gen_Stats_PropertiesPerSecond), new nmiCtrlNumber() { TileWidth=3, ParentFld = 20 });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.Number, 54, 0, 0x0, "###CDMyVThings.TheVThings#Propertycount904939#Property Counter###", nameof(Gen_Stats_PropertyCounter), new nmiCtrlNumber() { TileWidth = 3, ParentFld = 20 });
            TheFieldInfo mSendbutton = TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.TileButton, 60, 2, 0, "###CDMyVThings.TheVThings#ResetCounter741318#Reset Counter###", false, "", null, new nmiCtrlTileButton() { TileWidth = 3, ParentFld = 20, ClassName = "cdeGoodActionButton", NoTE = true });
            mSendbutton.RegisterUXEvent(MyBaseThing, eUXEvents.OnClick, "", (pThing, pPara) =>
            {
                TheProcessMessage pMsg = pPara as TheProcessMessage;
                if (pMsg == null || pMsg.Message == null) return;
                this.Gen_Stats_PropertyCounter = 0;
                this.Gen_Stats_PropertiesPerSecond = 0;
                sinkStatChanged(null, null);
            });

            //PropTable = TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.Table, 100, 0xA2, 0x80, "###CDMyVThings.TheVThings#AllProperties100123#All Properties###", "mypropertybag;1", new TheNMIBaseControl() { TileWidth = 12, TileHeight = 6, ParentFld = 1 });
            PropTable = TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.Table, 100, 0xA2, 0x80, "###CDMyVThings.TheVThings#AllProperties100123#All Properties###", "mypropertybag;0", new TheNMIBaseControl() { NoTE=true, TileWidth = 12, TileHeight = 6, ParentFld = 1 });
            mIsUXInitialized = true;
            return true;
        }

        void sinkStatChanged(TheDashPanelInfo tDas, cdeP prop)
        {
            MyBaseThing.Value = MyBaseThing.PropertyCount.ToString();
            SummaryForm.SetUXProperty(Guid.Empty, string.Format("iValue={0}", MyBaseThing.Value));
        }

        public TheDataGenerator(TheThing pThing, IBaseEngine pEngine)
        {
            if (pThing != null)
                MyBaseThing = pThing;
            else
                MyBaseThing = new TheThing();
            MyBaseEngine = pEngine;
            MyBaseThing.DeviceType = eVThings.eDataGenerator;
            MyBaseThing.EngineName = pEngine.GetEngineName();
            MyBaseThing.SetIThingObject(this);
        }
    }
}
