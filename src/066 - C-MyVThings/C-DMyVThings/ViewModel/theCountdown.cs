// SPDX-FileCopyrightText: 2015-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

﻿/*********************************************************************
*
* Project Name:CMyVThings
*
* Description: 
*
* Date of creation: 2015/28/07
*
* Author:
*
* NOTES:
*        UX "pOrder" for this file is  100-299 and  1000-1999

*********************************************************************/
using nsCDEngine.BaseClasses;
using nsCDEngine.Engines;
using nsCDEngine.Engines.NMIService;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.ViewModels;
using System;
using System.Collections.Generic;
using System.Threading;

namespace CDMyVThings.ViewModel
{
    [DeviceType(DeviceType = eVThings.eVCountdown, Description = "", Capabilities = new[] { eThingCaps.ConfigManagement, eThingCaps.SensorContainer })]
    class TheVCountdown : TheNMILiveTag
    {
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

        [ConfigProperty(DefaultValue = false)]
        public bool Restart
        {
            get { return TheThing.MemberGetSafePropertyBool(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyBool(MyBaseThing, value); }
        }

        [ConfigProperty]
        public double StartValue
        {
            get { return TheThing.MemberGetSafePropertyNumber(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyNumber(MyBaseThing, value); }
        }

        [ConfigProperty]
        public int Frequency
        {
            get { return TheCommonUtils.CInt(TheThing.MemberGetSafePropertyNumber(MyBaseThing)); }
            set { TheThing.MemberSetSafePropertyNumber(MyBaseThing, value); }
        }

        public override cdeP SetProperty(string pName, object pValue)
        {
            cdeP tProp = base.SetProperty(pName, pValue);
            if (pName == nameof(StartValue))
                sinkTriggered(tProp);
            if (pName == "Value")
                sinkValueReset(tProp);
            return tProp;
        }

        private readonly object TriggerLock = new object();
        private void sinkTriggered(cdeP pProp)
        {
            if (TheCommonUtils.cdeIsLocked(TriggerLock) || IsDisabled) return;
            lock (TriggerLock)
            {
                if (pProp == null) return;
                int tTime = TheCommonUtils.CInt(pProp.ToString());
                if (tTime <= 0 || mTimer != null) return;
                mTimer?.Dispose();
                if (Frequency < 100)
                    Frequency = 100;
                mTimer = new Timer(sinkTriggerTimeout, null, 0, Frequency);
                MyBaseThing.Value = tTime.ToString();
                IsActive = true;
                CountBar?.SetUXProperty(Guid.Empty, $"MaxValue={tTime}");
                tGauge?.SetUXProperty(Guid.Empty, $"MaxValue={tTime}");
                MyBaseThing.LastMessage = "Countdown started: " + MyBaseThing.FriendlyName;
                MyBaseThing.StatusLevel = 1;
            }
        }

        private void sinkValueReset(cdeP pProp)
        {
            if (mTimer == null && TheCommonUtils.CInt(pProp.ToString())>0)
                this.SetProperty(nameof(StartValue), pProp.ToString());
        }

        private void sinkStartValueChanged(cdeP pProp)
        {
            this.MyBaseThing.DeclareSensorProperty("Value", ePropertyTypes.TNumber, new cdeP.TheSensorMeta { RangeMax = StartValue, RangeMin = 0, RangeAverage = (StartValue / 2), Units = "ticks", Kind = "analog" });
        }


        private void UpdateUx()
        {
            CountBar.PropertyBag = ThePropertyBag.CreateUXBagFromProperties(MyBaseThing);
            CountBar.Type = (eFieldType)TheCommonUtils.CInt(ThePropertyBag.PropBagGetValue(CountBar.PropertyBag, "ControlType", "="));
            string t = TheCommonUtils.CListToString(CountBar.PropertyBag, ":;:");
            CountBar.SetUXProperty(Guid.Empty, t);
        }

        void sinkTriggerTimeout(object pTime)
        {
            if (!TheBaseAssets.MasterSwitch)
            {
                mTimer?.Dispose();
                mTimer = null;
                return;
            }
            if (IsDisabled)
            {
                if (MyBaseThing.StatusLevel != 0)
                {
                    MyBaseThing.StatusLevel = 0;
                    MyBaseThing.LastMessage = "Countdown disabled";
                }
                return;
            }
            else if (MyBaseThing.StatusLevel == 0)
            {
                MyBaseThing.StatusLevel = 1;
                MyBaseThing.LastMessage = "Countdown enabled";
            }
                if (TheCommonUtils.cdeIsLocked(TriggerLock)) return;
            lock (TriggerLock)
            {
                int tTIme = (TheCommonUtils.CInt(MyBaseThing.Value) - 1);
                if (tTIme >= 0)
                {
                    MyBaseThing.Value = tTIme.ToString();
                    if (TheCommonUtils.CBool(TheBaseAssets.MySettings.GetSetting("VThing-SimTest")) == true && MyBaseThing.ID== "TESTSIM")
                    {
                        var response = new CDMyMeshManager.Contracts.MsgReportTestStatus
                        {
                            NodeId = TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID,
                            PercentCompleted = 100 - tTIme,
                            SuccessRate = 100 - tTIme,
                            Status = CDMyMeshManager.Contracts.eTestStatus.Running,
                            TestRunId = TheCommonUtils.CGuid(TheBaseAssets.MySettings.GetSetting("TestRunID")),
                            Timestamp = DateTimeOffset.Now,
                            ResultDetails = new Dictionary<string, object>
                         {
                         {"SomeKPI", 123 },
                         },
                        }.Publish();
                    }
                }
                if (tTIme <= 0 && mTimer != null)
                {
                    mTimer.Dispose();
                    mTimer = null;
                    IsActive = false;
                    MyBaseThing.StatusLevel = 0;

                    if (TheCommonUtils.CBool(TheBaseAssets.MySettings.GetSetting("VThing-SimTest")) == true && MyBaseThing.ID == "TESTSIM")
                    {
                        TheCommonUtils.TaskDelayOneEye(2000, 100).ContinueWith(t =>
                        {
                            var response = new CDMyMeshManager.Contracts.MsgReportTestStatus
                            {
                                NodeId = TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID,
                                PercentCompleted = 100,
                                SuccessRate = 100,
                                Status = CDMyMeshManager.Contracts.eTestStatus.Success,
                                TestRunId = TheCommonUtils.CGuid(TheBaseAssets.MySettings.GetSetting("TestRunID")),
                                Timestamp = DateTimeOffset.Now,
                                ResultDetails = new Dictionary<string, object>
                         {
                         {"SomeKPI", 123 },
                         },
                            }.Publish();
                        });
                    }

                    if (Restart)
                        sinkTriggered(this.GetProperty(nameof(StartValue), false));
                    else
                    {
                        CountBar?.SetUXProperty(Guid.Empty, string.Format("MaxValue=100"));
                    }
                }
            }
        }

        Timer mTimer = null;

        public TheVCountdown(TheThing pThing, IBaseEngine pEngine)
            : base(pThing)
        {
            if (pThing != null)
                MyBaseThing = pThing;
            else
                MyBaseThing = new TheThing();

            MyBaseThing.DeviceType = eVThings.eVCountdown;
            MyBaseThing.EngineName = pEngine.GetEngineName();
            MyBaseThing.SetIThingObject(this);
            IsActive = false;
        }

        protected TheFieldInfo CountBar;

        public override bool DoInit()
        {
            if (Frequency < 100)
                Frequency = 100;

            GetProperty(nameof(StartValue), true).RegisterEvent(eThingEvents.PropertyChanged, sinkTriggered);
            GetProperty(nameof(StartValue), true).RegisterEvent(eThingEvents.PropertyChanged, sinkStartValueChanged);
            GetProperty("Value", true).RegisterEvent(eThingEvents.PropertyChanged, sinkValueReset);

            this.MyBaseThing.DeclareSensorProperty("Value", ePropertyTypes.TNumber, new cdeP.TheSensorMeta { RangeMax = 100, RangeMin = 0, RangeAverage = 25, Units = "ticks", Kind = "analog" });

            if (!TheThing.GetSafePropertyBool(MyBaseThing, "IsStateSensor"))
            {
                TheThing.SetSafePropertyBool(MyBaseThing, "IsStateSensor", true);
                TheThing.SetSafePropertyString(MyBaseThing, "StateSensorType", "analog");
                TheThing.SetSafePropertyString(MyBaseThing, "StateSensorUnit", "ticks");
                TheThing.SetSafePropertyNumber(MyBaseThing, "StateSensorMaxValue", 100);
                TheThing.SetSafePropertyNumber(MyBaseThing, "StateSensorAverage", 50);
                TheThing.SetSafePropertyNumber(MyBaseThing, "StateSensorMinValue", 0);
            }
            if (!IsActive)
            {
                IsActive = false;
                MyBaseThing.StatusLevel = 0;
                MyBaseThing.LastMessage = "Countdown ready";
                if (AutoStart && mTimer == null)
                    sinkTriggered(this.GetProperty("Value", false));
            }
            return true;
        }

        TheFieldInfo tGauge;
        public override bool DoCreateUX()
        {

            var tFlds = TheNMIEngine.AddStandardForm(MyBaseThing, null, null,0, new nmiStandardForm { IconUpdateName = "Value", MaxTileWidth = 12, UseMargin = true });
            MyStatusForm = tFlds["Form"] as TheFormInfo;
            MyStatusForm.ModelID = "CountdownForm";

            ThePropertyBag tDash = new nmiDashboardTile()
            {
                ClassName = "cdeDashPlate",
                Caption = "Loading... <i class='fa fa-spinner fa-pulse'></i>",
                TileWidth = 2,
                TileHeight = 2,
                HTML = "<div><div id='COUNTD%cdeMID%'></div><p><%C20:FriendlyName%></p></div>",    //TODO:SETP Move to Generate Screen for all HTML blocks!
                Style = "background-image:none;color:white;background-color:gray",
                RSB = true,
                RenderTarget="HomeCenterStage"
            };

            SummaryForm = tFlds["DashIcon"] as TheDashPanelInfo;
            SummaryForm.PropertyBag = tDash;
            SummaryForm.RegisterPropertyChanged(sinkStatChanged);

            tGauge = TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.CircularGauge, 4000, 2, 0, "Countdown", "Value", new ThePropertyBag() { "TileWidth=2", "NoTE=true", "RenderTarget=COUNTD%cdeMID%", "LabelForeground=#00b9ff", "Foreground=#FFFFFF" });

            var ts = TheNMIEngine.AddStatusBlock(MyBaseThing, MyStatusForm, 10);
            ts["Group"].SetParent(1);

            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.CollapsibleGroup, 150, 2, 128, "###CDMyVThings.TheVThings#Settings796959#Settings...###", null, ThePropertyBag.Create(new nmiCtrlCollapsibleGroup() { DoClose = true, TileWidth = 6, IsSmall = true, ParentFld = 1 }));

            TheFieldInfo mSendbutton = TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.TileButton, 151, 2, 0, "###CDMyVThings.TheVThings#Trigger796959#Trigger###", false, "", null, new nmiCtrlTileButton() { NoTE = true, ParentFld = 150, ClassName = "cdeGoodActionButton" });
            mSendbutton.RegisterUXEvent(MyBaseThing, eUXEvents.OnClick, "", (pThing, pPara) =>
            {
                if (mTimer != null)
                {
                    mTimer.Dispose();
                    mTimer = null;
                }
                sinkTriggered(this.GetProperty(nameof(StartValue), false));
                //UpdateUx();
            });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.Number, 152, 2, MyBaseThing.cdeA, "###CDMyVThings.TheVThings#StartValue633339#Start Value###", nameof(StartValue), new nmiCtrlNumber { TileWidth = 3, TileHeight = 1, ParentFld = 150 });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.Number, 153, 2, MyBaseThing.cdeA, "###CDMyVThings.TheVThings#Ticktimeinms633339#Tick time in ms###", nameof(Frequency), new nmiCtrlNumber { TileWidth = 3, TileHeight = 1, ParentFld = 150 });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.SingleCheck, 154, 2, MyBaseThing.cdeA, "###CDMyVThings.TheVThings#ContinueonRestart992144#Continue on Restart###", nameof(AutoStart), new nmiCtrlNumber { TileWidth = 3, TileHeight = 1, ParentFld = 150 });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.SingleCheck, 155, 2, MyBaseThing.cdeA, "###CDMyVThings.TheVThings#StartOverwhenzero992144#Start Over when zero###", nameof(Restart), new nmiCtrlNumber { TileWidth = 3, TileHeight = 1, ParentFld = 150 });

            int tControl = TheCommonUtils.CInt(TheThing.GetSafePropertyNumber(MyBaseThing, "ControlType"));
            ThePropertyBag tBag = ThePropertyBag.CreateUXBagFromProperties(MyBaseThing);
            tBag.Add("ParentFld=1");
            if (tControl > 0)
                CountBar = TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, (eFieldType)tControl, 100, 2, MyBaseThing.cdeA, "CurrentValue", "Value", tBag);
            else
            {
                TheThing.SetSafePropertyString(MyBaseThing, "ControlType", "34");
                TheThing.SetSafePropertyString(MyBaseThing, "NoTE", "true");
                TheThing.SetSafePropertyString(MyBaseThing, "TileWidth", "6");
                TheThing.SetSafePropertyString(MyBaseThing, "TileHeight", "1");
                CountBar = TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.BarChart, 100, 2, MyBaseThing.cdeA, "CurrentValue", "Value", new nmiCtrlBarChart() { NoTE = true, TileWidth = 6, TileHeight = 1, ParentFld = 10 });
            }
            return true;
        }

        void sinkStatChanged(TheDashPanelInfo tDas, cdeP prop)
        {
            int tLevel = 1;
            string tCol = "green";
            int i = TheCommonUtils.CInt(MyBaseThing.Value);
            if (prop != null)
                i = TheCommonUtils.CInt(prop.ToString());
            if (i < 3) { tCol = "orange"; tLevel = 2; }
            if (i == 0) { tCol = "gray"; tLevel = 0; }
            SummaryForm.SetUXProperty(Guid.Empty, string.Format("Background={0}", tCol));
            if (MyBaseThing.StatusLevel != tLevel)
            {
                MyBaseThing.StatusLevel = tLevel;
            }
        }
    }
}
