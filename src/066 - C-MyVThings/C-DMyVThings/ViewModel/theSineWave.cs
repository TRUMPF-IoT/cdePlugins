// SPDX-FileCopyrightText: 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

﻿using nsCDEngine.Engines.ThingService;
using nsCDEngine.Engines;
using nsCDEngine.Engines.NMIService;
using nsCDEngine.ViewModels;
using System;
using System.Globalization;
using System.Threading;
using nsCDEngine.BaseClasses;

namespace CDMyVThings.ViewModel
{
    internal class TheSineWave : TheNMILiveTag
    {
        [ConfigProperty(DefaultValue = false)]
        public bool IsDisabled
        {
            get { return TheThing.MemberGetSafePropertyBool(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyBool(MyBaseThing, value); }
        }


        private IBaseEngine MyBaseEngine;

        public TheSineWave(TheThing pThing, IBaseEngine pEngine) : base(pThing)
        {
            MyBaseThing = pThing ?? new TheThing();
            MyBaseEngine = pEngine;
            MyBaseThing.DeviceType = eVThings.eSineWave;
            MyBaseThing.EngineName = pEngine.GetEngineName();
            MyBaseThing.SetIThingObject(this);
        }

        public override cdeP SetProperty(string pName, object pValue)
        {
            var tProp = base.SetProperty(pName, pValue);
            if (pName == "Value")
                SinkValueReset(tProp);

            return tProp;
        }

        public override bool DoInit()
        {
            if (TheThing.GetSafePropertyNumber(MyBaseThing, "Frequency") < 100)
            {
                TheThing.SetSafePropertyNumber(MyBaseThing, "Frequency", 1000);
            }
            if (TheThing.GetSafePropertyBool(MyBaseThing, "AutoStart"))
            {
                SinkTriggered(GetProperty("Value", false));
            }
            GetProperty("Value", true).RegisterEvent(eThingEvents.PropertyChanged, SinkValueReset);

            return true;
        }

        private Timer _mTimer;
        protected TheFieldInfo CountBar;
        protected TheFieldInfo MyGauge=null;

        public override bool DoCreateUX()
        {

            var tFlds = TheNMIEngine.AddStandardForm(MyBaseThing, null, 12, null, "Value");
            MyStatusForm = tFlds["Form"] as TheFormInfo;
            SummaryForm = tFlds["DashIcon"] as TheDashPanelInfo;
            SummaryForm.PropertyBag = new ThePropertyBag() { string.Format("Format={0}<br>{{0}}", MyBaseThing.FriendlyName) };

            var ts=TheNMIEngine.AddStatusBlock(MyBaseThing, MyStatusForm, 10);
            ts["Group"].SetParent(1);

            // loading spinner
            //var tDash = TheThingRegistry.IsEngineRegistered("CDMyNMIControls.TheNMIctrls")
            var tDash=new nmiDashboardTile { ClassName = "cdeDashPlate", Format = "Loading... <i class='fa fa-spinner fa-pulse'></i>", TileWidth = 2, TileHeight = 2, HTML = "<div><p><%C20:FriendlyName%></p><div id='COUNTD%cdeMID%'></div></div>", Style = "background-image:url('GlasButton.png');color:white;background-color:gray", RSB = true };
                //: new ThePropertyBag { $"Format={MyBaseThing.FriendlyName}<br>{{0}}", "Foreground=white" };

            SummaryForm.PropertyBag = tDash;
            SummaryForm.RegisterPropertyChanged(SinkStatChanged);

            var tControl = TheCommonUtils.CInt(TheThing.GetSafePropertyNumber(MyBaseThing, "ControlType"));
            var tBag = ThePropertyBag.CreateUXBagFromProperties(MyBaseThing);
            tBag.Add("ParentFld=1");

            CountBar = tControl > 0
             ? TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, (eFieldType)tControl, 21, 2, 0, "Current Value", "Value", tBag)
             : TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.BarChart, 21, 2, 0, "Current Value", "Value", new nmiCtrlBarChart { ParentFld = 1 });  

            //if (TheThingRegistry.IsEngineRegistered("CDMyNMIControls.TheNMIctrls"))
            {
                //MyGauge=TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.UserControl, 4000, 2, 0, "Countdown", "Value", new ThePropertyBag { "TileWidth=2", "NoTE=true", "EngineName=CDMyNMIControls.TheNMIctrls", "RenderTarget=COUNTD%cdeMID%", "ControlType=cdeNMI.ctrlCircularGauge", "LabelForeground=#00b9ff", "Foreground=#FFFFFF" });
                MyGauge = TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.CircularGauge, 4000, 2, 0, "Countdown", "Value", new ThePropertyBag { "TileWidth=2", "NoTE=true", "RenderTarget=COUNTD%cdeMID%", "LabelForeground=#00b9ff", "Foreground=#FFFFFF" });
            }

            var mSendbutton = TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.TileButton, 120, 2, 0x80, "Trigger", false, "", null, new nmiCtrlTileButton { NoTE=true, TileWidth=3, ParentFld=10, ClassName="cdeGoodActionButton" });
            mSendbutton.RegisterUXEvent(MyBaseThing, eUXEvents.OnClick, "", (pThing, pPara) =>
            {
                if (_mTimer != null)
                {
                    _mTimer.Dispose();
                    _mTimer = null;
                }
                SinkTriggered(GetProperty("StartValue", false));
            });
            var mSP = TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.TileButton, 130, 2, 0x80, "Stop", false, "", null, new nmiCtrlTileButton { NoTE=true, TileWidth=3, ParentFld=10, ClassName = "cdeBadActionButton" });
            mSP.RegisterUXEvent(MyBaseThing, eUXEvents.OnClick, "", (pThing, pPara) =>
            {
                if (_mTimer != null)
                {
                    _mTimer.Dispose();
                    _mTimer = null;
                }
            });
            // Settings Controls & User Input
            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.CollapsibleGroup, 151, 2, 0x80, "Settings...", null, ThePropertyBag.Create(new nmiCtrlCollapsibleGroup { ParentFld=1,TileWidth=6, IsSmall=true, DoClose = true }));
            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.Number, 153, 2, 0x80, "Tick time in ms", "Frequency", new nmiCtrlNumber { TileWidth = 3, TileHeight = 1, ParentFld = 151 });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.Number, 156, 2, 0x80, "Step", "Step", new nmiCtrlNumber { TileWidth = 3, TileHeight = 1, ParentFld = 151 });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.Number, 157, 2, 0x80, "Amplitude", "Amplitude", new nmiCtrlNumber { TileWidth = 3, TileHeight = 1, ParentFld = 151 });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.Number, 158, 2, 0x80, "Shift", "Shift", new nmiCtrlNumber { TileWidth = 3, TileHeight = 1, ParentFld = 151 });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.SingleCheck, 180, 2, 0x80, "Autostart", "AutoStart", new nmiCtrlNumber { TileWidth = 3, TileHeight = 1, ParentFld = 151 });

            return true;
        }

        private void SinkStatChanged(TheDashPanelInfo tDas, cdeP prop)
        {
            var i = (prop != null)
                ? TheCommonUtils.CInt(prop.ToString())
                : TheCommonUtils.CInt(MyBaseThing.Value);

            var tCol = i < 3 ? "orange" : "green";
            var tLevel = i < 3 ? 2 : 1;

            if (i == 0)
            {
                tCol = "gray";
                tLevel = 0;
            }

            SummaryForm.SetUXProperty(Guid.Empty, $"Background={tCol}");

            if (MyBaseThing.StatusLevel != tLevel)
            {
                MyBaseThing.StatusLevel = tLevel;
            }
        }

        private void SinkTriggered(cdeP pProp)
        {
            _index = 0;
            _mTimer?.Dispose();

            _mTimer = new Timer(SinkTriggerTimeout, null, 0, TheCommonUtils.CInt(TheThing.GetSafePropertyNumber(MyBaseThing, "Frequency")));
            MyBaseThing.Value = _index.ToString();
            IsActive = true;

            var xMax = GetProperty("Amplitude", false);
            CountBar?.SetUXProperty(Guid.Empty, $"MaxValue={xMax}");
            MyGauge?.SetUXProperty(Guid.Empty, $"MaxValue={xMax}");
            MyBaseThing.LastMessage = "Countdown started: " + MyBaseThing.FriendlyName;
        }

        private void SinkValueReset(cdeP pProp)
        {
            if (_mTimer == null)
            {
                SetProperty("StartValue", pProp.ToString());
            }
        }

        private double _index;
        private void SinkTriggerTimeout(object pTime)
        {
            // reset the timer if the CDEngine is shutting down
            if (!TheBaseAssets.MasterSwitch)
            {
                _mTimer?.Dispose();
                _mTimer = null;
                return;
            }
            if (IsDisabled)
            {
                if (MyBaseThing.StatusLevel != 0)
                {
                    MyBaseThing.StatusLevel = 0;
                    MyBaseThing.LastMessage = "Sinewave disabled";
                }
                return;
            }
            else if (MyBaseThing.StatusLevel==0)
            {
                MyBaseThing.StatusLevel = 1;
                MyBaseThing.LastMessage = "Sinewave enabled";
            }

            var y = CalculateYSine(CalculateX());
            MyBaseThing.Value = y.ToString(CultureInfo.InvariantCulture);
        }

        private double CalculateX()
        {
            var step= TheThing.GetSafePropertyNumber(MyBaseThing, "Step");
            if (step == 0) step = 1;
            return _index += step;
        }

        private double CalculateYSine(double x)
        {
            //const int amplitude = 10;
            //const int verticalShift = 10;
            var amplitude = TheThing.GetSafePropertyNumber(MyBaseThing, "Amplitude");
            if (amplitude < 10) amplitude = 10;
            var verticalShift = TheThing.GetSafePropertyNumber(MyBaseThing, "Shift");
            return (amplitude/2) * Math.Sin(x) + verticalShift;
        }
    }
}
