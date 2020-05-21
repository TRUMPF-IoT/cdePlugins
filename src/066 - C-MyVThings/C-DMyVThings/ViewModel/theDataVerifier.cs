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
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CDMyVThings.ViewModel
{
    public class TheDataVerifier : ICDEThing
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
            if (MyBaseThing != null)
                return MyBaseThing.GetProperty(pName, DoCreate);
            return null;
        }
        public virtual cdeP SetProperty(string pName, object pValue)
        {
            if (MyBaseThing != null)
                return MyBaseThing.SetProperty(pName, pValue);
            return null;
        }

        public void RegisterEvent(string pName, Action<ICDEThing, object> pCallBack)
        {
            if (MyBaseThing != null)
                MyBaseThing.RegisterEvent(pName, pCallBack);
        }
        public void UnregisterEvent(string pName, Action<ICDEThing, object> pCallBack)
        {
            if (MyBaseThing != null)
                MyBaseThing.UnregisterEvent(pName, pCallBack);
        }
        public void FireEvent(string pEventName, ICDEThing sender, object pPara, bool FireAsync)
        {
            if (MyBaseThing != null)
                MyBaseThing.FireEvent(pEventName, sender, pPara, FireAsync);
        }
        public bool HasRegisteredEvents(string pEventName)
        {
            if (MyBaseThing != null)
                return MyBaseThing.HasRegisteredEvents(pEventName);
            return false;
        }

        public virtual void HandleMessage(ICDEThing sender, object pMsg)
        { }

        protected TheThing MyBaseThing = null;

        protected bool mIsUXInitCalled = false;
        protected bool mIsUXInitialized = false;
        protected bool mIsInitCalled = false;
        protected bool mIsInitialized = false;
        public bool IsUXInit()
        { return mIsUXInitialized; }
        public bool IsInit()
        { return mIsInitialized; }

        /// <summary>
        /// The possible types of WeMo devices that can be detected
        /// </summary>
        #endregion

        private IBaseEngine MyBaseEngine;

        public long Config_NumberOfActiveProperties
        {
            get { return (long)TheThing.GetSafePropertyNumber(MyBaseThing, "Gen_Config_NumberOfActiveProperties"); }
            set
            {
                TheThing.SetSafePropertyNumber(MyBaseThing, "Gen_Config_NumberOfActiveProperties", value);
            }
        }
        [ConfigProperty(DefaultValue = false)]
        public bool IsDisabled
        {
            get { return TheThing.MemberGetSafePropertyBool(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyBool(MyBaseThing, value); }
        }


        public string ThingToVerify
        {
            get { return TheThing.GetSafePropertyString(MyBaseThing, "ThingToVerify"); }
            set { TheThing.SetSafePropertyString(MyBaseThing, "ThingToVerify", value); }
        }

        public double Stats_PropertiesPerSecond
        {
            get { return TheThing.GetSafePropertyNumber(MyBaseThing, "Gen_Stats_PropertiesPerSecond"); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, "Gen_Stats_PropertiesPerSecond", value); }
        }

        public double Stats_PropertyCounter
        {
            get { return TheThing.GetSafePropertyNumber(MyBaseThing, "Gen_Stats_PropertyCounter"); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, "Gen_Stats_PropertyCounter", value); }
        }

        public double MinLatency
        {
            get { return TheThing.GetSafePropertyNumber(MyBaseThing, "Gen_MinLatency"); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, "Gen_MinLatency", value); }
        }

        public double MaxLatency
        {
            get { return TheThing.GetSafePropertyNumber(MyBaseThing, "Gen_MaxLatency"); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, "Gen_MaxLatency", value); }
        }

        public bool IsStarted
        {
            get { return TheThing.GetSafePropertyBool(MyBaseThing, "IsStarted"); }
            set { TheThing.SetSafePropertyBool(MyBaseThing, "IsStarted", value); }
        }

        System.Diagnostics.Stopwatch g_sw = null;

        long count = 0;
        long _maxLatency = 0;
        long _minLatency = long.MaxValue;


        class Stats
        {
            public long count;
        };

        ConcurrentDictionary<string, Stats> thingStats = new ConcurrentDictionary<string, Stats>();
        long lastGeneratorPropertyCounter = 0;

        void OnThingUpdate(ICDEThing thing, object param)
        {
            try
            {
                if (IsDisabled)
                {
                    StopVerifier();
                    return;
                }
                {
                    var property = param as cdeP;

                    if (property == null)
                    {
                        return;
                    }

                    string genName = property.Name;
                    var indexGen = genName.IndexOf("Gen_");
                    if (indexGen >= 0)
                    {
                        genName = property.Name.Substring(indexGen + "Gen_".Length);
                    }

                    switch (genName)
                    {
                        case "Stats_PropertyCounter":
                            long generatorPropertyCounter = TheCommonUtils.CLng(property.Value);
                            if (generatorPropertyCounter + this.Config_NumberOfActiveProperties < lastGeneratorPropertyCounter)
                            {
                                this.Stats_PropertyCounter = generatorPropertyCounter;
                                thingStats = new ConcurrentDictionary<string, Stats>();
                                TheBaseAssets.MySYSLOG.WriteToLog(700, TSM.L(eDEBUG_LEVELS.FULLVERBOSE) ? null : new TSM("Data Verifier", "Verifier", eMsgLevel.l6_Debug, "Resetting property counter"));
                            }
                            lastGeneratorPropertyCounter = generatorPropertyCounter;
                            break;
                        case "Config_NumberOfActiveProperties":
                        case "Stats_PropertiesPerSecond":
                        case "Config_PropertyUpdateInterval":
                        case "Stats_UpdateTime":
                        case "Config_StatsUpdateInterval":
                            break;
                        case "Value":
                            MyBaseThing.SetProperty("Value", property.Value);
                            System.Threading.Interlocked.Increment(ref count);
                            break;
                        default:
                            if (!genName.StartsWith("Prop"))
                            {
                                TheBaseAssets.MySYSLOG.WriteToLog(700, TSM.L(eDEBUG_LEVELS.FULLVERBOSE) ? null : new TSM("Data Verifier", "Verifier", eMsgLevel.l6_Debug, String.Format("Unexpected property: {0}", property.Name)));
                            }
                            System.Threading.Interlocked.Increment(ref count);
                            break;

                    }

                    thingStats.AddOrUpdate(property.Name, new Stats { count = 1 },
                        (name, currentStats) =>
                        {
                            currentStats.count++;
                            return currentStats;
                        }
                        );

                    var lastUpdate = property.cdeCTIM;

                    var now = DateTime.UtcNow;
                    var latency = (now.Ticks - lastUpdate.Ticks);
                    if (latency >= 0)
                    {
                        if (latency > _maxLatency) _maxLatency = latency;
                        if (latency < _minLatency) _minLatency = latency;
                    }
                    else
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(700, TSM.L(eDEBUG_LEVELS.FULLVERBOSE) ? null : new TSM("Data Verifier", "Verifier", eMsgLevel.l6_Debug, "Negative latency!!??"));
                    }
                }
                if (g_sw.ElapsedMilliseconds > 5000)
                {
                    long elapsed = 0;
                    long newCount = -1;
                    lock (g_sw)
                    {
                        elapsed = g_sw.ElapsedMilliseconds;
                        if (elapsed > 5000)
                        {
                            newCount = System.Threading.Interlocked.Exchange(ref count, 0);
                            g_sw.Restart();
                        }
                    }


                    if (elapsed > 5000)
                    {
                        this.Stats_PropertyCounter += newCount;
                        this.Stats_PropertiesPerSecond = newCount / (elapsed / 1000.0);
                        // this.MinLatency = _minLatency / 1000000;
                        // this.MaxLatency = _maxLatency / 1000000;
                        this.MinLatency = _minLatency / 100000000;
                        this.MaxLatency = _maxLatency / 100000000;
                        _maxLatency = 0;
                        _minLatency = long.MaxValue;
                        TheBaseAssets.MySYSLOG.WriteToLog(700, TSM.L(eDEBUG_LEVELS.FULLVERBOSE) ? null : new TSM("Data Verifier", "Verifier", eMsgLevel.l6_Debug, String.Format("Verify Rate: {0,11:N2} properties/s", this.Stats_PropertiesPerSecond)));
                    }
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
                MyBaseThing.ID = Guid.NewGuid().ToString();
            MyBaseThing.Value = "0";
            TheThing.SetSafePropertyBool(MyBaseThing, "IsActive", true);

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

            var ts = TheNMIEngine.AddStatusBlock(MyBaseThing, MyStatusForm, 10);
            ts["Group"].SetParent(1);

            var tc = TheNMIEngine.AddStartingBlock(MyBaseThing, MyStatusForm, 20, (pMsg, DoStart) =>
            {
                if (DoStart)
                {
                    StartVerifier();
                }
                else
                {
                    StopVerifier();
                }
            });
            tc["Group"].SetParent(1);
            ts["Group"].Header = "Settings...";

                TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.Number, 41, 2, 0x0, "###CDMyVThings.TheVThings#NumberProperties295308#Number Properties###", "Gen_Config_NumberOfActiveProperties", new nmiCtrlNumber() { ParentFld=20  });
                TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.ThingPicker, 42, 2, 0xC0, "###CDMyVThings.TheVThings#ThingforPropertySubs849298#Thing for Property Subs###", "ThingToVerify", new nmiCtrlThingPicker() { IncludeEngines=true, ParentFld=20 });
                TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.Number, 43, 0, 0x0, "###CDMyVThings.TheVThings#PropertiesperSecond849298#Properties per Second###", "Gen_Stats_PropertiesPerSecond", new nmiCtrlNumber() { TileWidth=3, ParentFld = 20 });
                TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.Number, 44, 0, 0x0, "###CDMyVThings.TheVThings#Propertycounter208103#Property Counter###", "Gen_Stats_PropertyCounter", new nmiCtrlNumber() { TileWidth = 3, ParentFld = 20 });
                TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.Number, 45, 2, 0x0, "###CDMyVThings.TheVThings#MinimumLatencyms44483#Minimum Latency (ms)###", "Gen_MinLatency", new nmiCtrlNumber() { TileWidth = 3, ParentFld = 20 });
                TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.Number, 46, 2, 0x0, "###CDMyVThings.TheVThings#MaximumLatencyms44483#Maximum Latency (ms)###", "Gen_MaxLatency", new nmiCtrlNumber() { TileWidth = 3, ParentFld = 20 });
                TheFieldInfo mSendbutton = TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.TileButton, 60, 2, 0, "###CDMyVThings.TheVThings#ResetCounter741318#Reset Counter###", false, "", null, new nmiCtrlTileButton() { TileWidth = 3, ParentFld = 20, ClassName = "cdeGoodActionButton", NoTE = true });
                mSendbutton.RegisterUXEvent(MyBaseThing, eUXEvents.OnClick, "", (pThing, pPara) =>
                {
                    TheProcessMessage pMsg = pPara as TheProcessMessage;
                    if (pMsg == null || pMsg.Message == null) return;
                    this.Stats_PropertyCounter = 0;
                    this.Stats_PropertiesPerSecond = 0;
                    count = 0;
                    _maxLatency = 0;
                    _minLatency = long.MaxValue;

                    // sinkStatChanged(null, null);
                });

            PropTable = TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.Table, 120, 0xA2, 0x80, "All Properties", "mypropertybag;0", new ThePropertyBag() { "ParentFld=1", "TileWidth=12", "TileHeight=6" });

            mIsUXInitialized = true;
            return true;
        }

        ICDEThing m_thingToVerify = null;

        private void StopVerifier()
        {
            if (m_thingToVerify != null)
            {
                m_thingToVerify.UnregisterEvent(eThingEvents.PropertySet, OnThingUpdate);
                m_thingToVerify = null;
                MyBaseThing.StatusLevel = 0;
                IsStarted = false;
                MyBaseThing.LastMessage = "Verifier stopped";
            }
        }

        private void StartVerifier()
        {
            var thing = TheThingRegistry.GetThingByMID("*", TheCommonUtils.CGuid(this.ThingToVerify));
            if (thing != null)
            {
                thing.RegisterEvent(eThingEvents.PropertySet, OnThingUpdate);
                m_thingToVerify = thing;
                g_sw = new System.Diagnostics.Stopwatch();
                g_sw.Start();
                MyBaseThing.StatusLevel = 1;
                IsStarted = true;
                MyBaseThing.LastMessage = "Verifier started";
            }
        }

        public TheDataVerifier(TheThing pThing, IBaseEngine pEngine)
        {
            MyBaseThing = pThing ?? new TheThing();
            MyBaseEngine = pEngine;
            MyBaseThing.DeviceType = eVThings.eDataVerifier;
            MyBaseThing.EngineName = pEngine.GetEngineName();
            MyBaseThing.SetIThingObject(this);
            if (TheThing.GetSafePropertyBool(MyBaseThing,"AutoStart"))
                StartVerifier();
        }
    }
}
