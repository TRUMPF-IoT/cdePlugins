// SPDX-FileCopyrightText: 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

﻿using System;
using nsCDEngine.BaseClasses;
using nsCDEngine.Engines;
using nsCDEngine.Engines.NMIService;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.ViewModels;
using nsCDEngine.Communication;
using System.Text.RegularExpressions;
using TheSensorTemplate;

namespace CDMyVThings.ViewModel
{
    class TheVStateSensor : TheNMILiveTag
    {

        string ValueFilterPattern
        {
            get { return TheThing.GetSafePropertyString(MyBaseThing, nameof(ValueFilterPattern)); }
            set { TheThing.SetSafePropertyString(MyBaseThing, nameof(ValueFilterPattern), value); }
        }

        string ValueMatchPattern
        {
            get { return TheThing.GetSafePropertyString(MyBaseThing, nameof(ValueMatchPattern)); }
            set { TheThing.SetSafePropertyString(MyBaseThing, nameof(ValueMatchPattern), value); }
        }

        public TheVStateSensor(TheThing pThing, IBaseEngine pEngine)
            : base(pThing)
        {
            MyBaseThing = pThing ?? new TheThing();
            MyBaseThing.DeviceType = eVThings.eVStateSensor;
            MyBaseThing.EngineName = pEngine.GetEngineName();
            MyBaseThing.SetIThingObject(this);
        }

        public override bool DoInit()
        {
            base.DoInit();
            IsActive = false;
            TheThing.SetSafePropertyBool(MyBaseThing, "IsStateSensor", true);
            MyBaseThing.StatusLevel = 4;
            if (string.IsNullOrEmpty(MyBaseThing.ID))
            {
                MyBaseThing.ID = Guid.NewGuid().ToString();

                TheThing.SetSafePropertyString(MyBaseThing, "StateSensorType", "analog");
                TheThing.SetSafePropertyString(MyBaseThing, "StateSensorUnit", "units");
                TheThing.SetSafePropertyNumber(MyBaseThing, "StateSensorMaxValue", 100);
                TheThing.SetSafePropertyNumber(MyBaseThing, "StateSensorAverage", 50);
                TheThing.SetSafePropertyNumber(MyBaseThing, "StateSensorMinValue", 0);
                TheThing.SetSafePropertyNumber(MyBaseThing, "Interval", 500);
            }
            TheThing.SetSafePropertyString(MyBaseThing, "StateSensorIcon", "/P066/Images/iconVThingsRest.png");
            GetProperty("FriendlyName",true).RegisterEvent(eThingEvents.PropertyChanged, sinkNameChanged);
            cdeP tRW = GetProperty("RawValue", true);
            tRW.RegisterEvent(eThingEvents.PropertyChanged, sinkPrePro);
            cdeP.SetSafePropertyBool(tRW, "IsStateSensor", true);
            cdeP.SetSafePropertyString(tRW, "StateSensorType", "analog");
            cdeP.SetSafePropertyString(tRW, "StateSensorUnit", "&#176;F");
            cdeP.SetSafePropertyNumber(tRW, "StateSensorMaxValue", 100);
            cdeP.SetSafePropertyNumber(tRW, "StateSensorAverage", 50);
            cdeP.SetSafePropertyNumber(tRW, "StateSensorMinValue", 0);

            if (!string.IsNullOrEmpty(TheThing.GetSafePropertyString(MyBaseThing,"RealSensorThing")) && !string.IsNullOrEmpty(TheThing.GetSafePropertyString(MyBaseThing, "RealSensorProperty")))
            {
                EngageMapper();
            }

            GetProperty("IsGlobal", true).RegisterEvent(eThingEvents.PropertyChanged, (p) =>
            {
                if (TheCommonUtils.CBool(p.ToString()))
                    TheThingRegistry.RegisterThingGlobally(MyBaseThing);
                else
                    TheThingRegistry.UnregisterThingGlobally(MyBaseThing);
            });
            GetProperty("Interval", true).RegisterEvent(eThingEvents.PropertyChanged, (p) =>
            {
                changeInterval(TheCommonUtils.CInt(p.ToString()));
            });
            MyBaseThing.SetPublishThrottle((int)TheThing.GetSafePropertyNumber(MyBaseThing,"Interval"));

            //TheQueuedSenderRegistry.RegisterHealthTimer(checkMapperHealth);
            return true;
        }

        private void EngageMapper()
        {
            TheThingRegistry.UnmapPropertyMapper(mRealThingGuid);
            mRealThingGuid = TheThingRegistry.PropertyMapper(TheThing.GetSafePropertyGuid(MyBaseThing, "RealSensorThing"), TheThing.GetSafePropertyString(MyBaseThing, "RealSensorProperty"), MyBaseThing.cdeMID, "RawValue", false);
            if (mRealThingGuid == Guid.Empty)
            {
                MyBaseThing.StatusLevel = 0;
                MyBaseThing.LastMessage = "Mapper not active";
                IsActive = false;
            }
            else
            {
                MyBaseThing.LastMessage = "Mapper engaged";
                MyBaseThing.StatusLevel = 1;
                TheDefaultSensor.CopyStateSensorInfo(MyBaseThing);
                SummaryForm?.SetUXProperty(Guid.Empty, string.Format("Background={0}", TheNMIEngine.GetStatusColor(MyBaseThing.StatusLevel)));
                IsActive = true;
            }
        }

        //void checkMapperHealth(long pTime)
        //{
        //    if (pTime % 5 != 0) return;
        //}

        void changeInterval(int iVal)
        {
            MyBaseThing.SetPublishThrottle(iVal);
        }

        void sinkNameChanged(cdeP pProp)
        {
            //TheThing.SetSafePropertyString(MyBaseThing, "StateSensorName", MyBaseThing.FriendlyName);
        }

        double min = double.MaxValue;
        double max = double.MinValue;
        int aveCnt = 0;
        double ave = 0;
        DateTimeOffset lastWrite;

        Regex matchRegex;
        string matchPattern;
        Regex filterRegex;
        string filterPattern;


        void sinkPrePro(cdeP pProp)
        {
            if (TheThing.GetSafePropertyString(MyBaseThing, "StateSensorType") != "analog")
            {
                SetProperty("Value", pProp.Value);
                return;
            }
            int prepro = TheCommonUtils.CInt(TheThing.GetSafePropertyNumber(MyBaseThing, "PrePro"));
            int preprotime = TheCommonUtils.CInt(TheThing.GetSafePropertyNumber(MyBaseThing, "PreProTime"));
            double valScale = TheCommonUtils.CDbl(TheThing.GetSafePropertyNumber(MyBaseThing, "StateSensorScaleFactor"));


            var pPropString = TheCommonUtils.CStr(pProp);

            if (!string.IsNullOrEmpty(ValueFilterPattern))
            {
                try
                {
                    if (ValueFilterPattern != filterPattern || filterRegex == null)
                    {
#if !CDE_NET4 
                        filterRegex = new Regex(ValueFilterPattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, new TimeSpan(0, 0, 5));
#else
                        filterRegex = new Regex(ValueFilterPattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);
#endif
                        filterPattern = ValueFilterPattern;
                    }
                    pPropString = filterRegex.Match(pPropString).Value;
                }
                catch (Exception e)
                {
                    LastMessage = $"Error applying filter pattern: {e.Message}";
                    return; // CODE REVIEW: Or ist it better to treat this as 0, so other time processing doesn not get confused?
                }
            }

            double pValue;

            if (!string.IsNullOrEmpty(ValueMatchPattern))
            {
                try
                {
                    if (ValueMatchPattern != matchPattern || matchRegex == null)
                    {
#if !CDE_NET4
                        matchRegex = new Regex(ValueMatchPattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, new TimeSpan(0, 0, 5));
#else
                        matchRegex = new Regex(ValueMatchPattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);
#endif
                        matchPattern = ValueMatchPattern;
                    }
                    if (matchRegex.IsMatch(pProp.ToString()))
                    {
                        pValue = 1;
                    }
                    else
                    {
                        pValue = 0;
                    }
                }
                catch (Exception e)
                {
                    LastMessage = $"Error applying match pattern: {e.Message}";
                    return; // CODE REVIEW: Or ist it better to treat this as 0, so other time processing doesn not get confused?
                }
            }
            else
            {
                pValue = TheCommonUtils.CDbl(pPropString);
            }
            if (valScale != 0)
                pValue /= valScale;
            if (pValue < 0 && TheThing.GetSafePropertyBool(MyBaseThing, "StateSensorIsAbs"))
                pValue = Math.Abs(pValue);
            if (preprotime > 0 && prepro > 0)
            {
                if (pValue < min) min = pValue;
                if (pValue > max) max = pValue;
                ave += pValue;
                aveCnt++;
                if (DateTimeOffset.Now.Subtract(lastWrite).TotalSeconds < preprotime)
                    return;
                lastWrite = DateTimeOffset.Now;
                switch (prepro)
                {
                    case 1:
                        if (min == double.MaxValue)
                            return;
                        pValue = min;
                        min = double.MaxValue;
                        break;
                    case 2:
                        if (max == double.MinValue)
                            return;
                        pValue = max;
                        min = double.MinValue;
                        break;
                    case 3:
                        if (aveCnt == 0)
                            return;
                        pValue = ave / aveCnt;
                        ave = 0;
                        aveCnt = 0;
                        break;
                }
            }
            if (GetProperty("StateSensorDigits", false) != null)
            {
                int tDigits = (int)TheThing.GetSafePropertyNumber(MyBaseThing, "StateSensorDigits");
                SetProperty("Value", decimal.Round((decimal)pValue, tDigits, MidpointRounding.AwayFromZero));
            }
            else
                SetProperty("Value", pValue);
        }





















        Guid mRealThingGuid = Guid.Empty;
        public override bool DoCreateUX()
        {
            var tFlds = TheNMIEngine.AddStandardForm(MyBaseThing, "FACEPLATE", 18, null, null, 0, "...Virtual State Sensors");
            SummaryForm = (tFlds["DashIcon"] as TheDashPanelInfo);
            MyStatusForm = tFlds["Form"] as TheFormInfo;
            MyStatusForm.ModelID = "VStateSensorForm";
            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.TileButton, 1112, 2, 0, "Details...", null, new nmiCtrlTileButton { OnClick = $"TTS:%RealSensorThing%", RenderTarget = "VSDETBUT%cdeMID%", NoTE = true, Background = "transparent", TileWidth = 3, TileHeight = 1, TileFactorY = 2, ClassName = "cdeEmptyButtoninner" });

            var ts = TheNMIEngine.AddStatusBlock(MyBaseThing, MyStatusForm, 100);
            ts["Group"].SetParent(1);

            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.CollapsibleGroup, 200, 2, 0xc0, "V-Sensor Settings...", null, ThePropertyBag.Create(new nmiCtrlCollapsibleGroup() { DoClose = true, MaxTileWidth = 6, IsSmall = true, ParentFld = 1 }));

            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.ComboBox, 210, 2, 0xc0, "Sensor Type", "StateSensorType", new nmiCtrlComboBox { ParentFld = 200, FldWidth = 4, TileWidth = 6, Options = "analog;binary;state" });

            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.CollapsibleGroup, 220, 2, 0xc0, "Settings (Analog Sensor Only)...", null, ThePropertyBag.Create(new nmiCtrlCollapsibleGroup() { DoClose = true, MaxTileWidth = 6, ParentFld = 1, IsSmall = true }));
            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.SingleEnded, 225, 2, 0xc0, "Sensor Units", "StateSensorUnit", new nmiCtrlSingleEnded { ParentFld = 220, TileWidth = 3 });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.SingleEnded, 221, 2, 0x0, "Sensor Value Name", "StateSensorValueName", new nmiCtrlSingleEnded { ParentFld = 220, TileWidth = 6 });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.Number, 228, 2, 0xc0, "Average Value", "StateSensorAverage", new nmiCtrlNumber { ParentFld = 220, TileWidth = 3 });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.Number, 230, 2, 0xc0, "Min Value", "StateSensorMinValue", new nmiCtrlNumber { ParentFld = 220, TileWidth = 3 });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.Number, 240, 2, 0xc0, "Max Value", "StateSensorMaxValue", new nmiCtrlNumber { ParentFld = 220, TileWidth = 3 });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.Number, 245, 2, 0xc0, "Value Divider", "StateSensorScaleFactor", new nmiCtrlNumber { TileWidth = 3, ParentFld = 220 });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.Number, 246, 2, 0xc0, "Digits", "StateSensorDigits", new nmiCtrlNumber { MinValue=0, TileWidth = 3, ParentFld = 220 });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.SingleCheck, 247, 2, 0xc0, "Force Absolute", "StateSensorIsAbs", new nmiCtrlSingleCheck { TileWidth = 3, ParentFld = 220 });

            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.ThingPicker, 212, 2, 0xc0, "Thing Picker", "RealSensorThing", new nmiCtrlThingPicker() { ParentFld = 200, IncludeEngines = true, IncludeRemotes = true });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.PropertyPicker, 213, 2, 0xc0, "Property Picker", "RealSensorProperty", new nmiCtrlPropertyPicker() { ParentFld = 200, ThingFld = 212 });
            GetProperty("RealSensorProperty", true).RegisterEvent(eThingEvents.PropertyChanged, (p) =>
            {
                EngageMapper();
            });
            var tEngage = TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.TileButton, 214, 2, 0xC0, "Restart Sensor", null, new nmiCtrlNumber { ParentFld = 200, TileWidth = 6, NoTE = true });
            tEngage.RegisterUXEvent(MyBaseThing, eUXEvents.OnClick, "reset", (sender, para) => { EngageMapper(); });

            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.CollapsibleGroup, 249, 2, 0xc0, "Advanced Settings...", null, ThePropertyBag.Create(new nmiCtrlCollapsibleGroup() { DoClose = true, MaxTileWidth = 6, ParentFld = 200, IsSmall = true }));
            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.SingleCheck, 250, 2, 0xC0, "Replicate", "IsGlobal", new nmiCtrlNumber { ParentFld = 249, TileWidth = 3 });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.Number, 255, 2, 0xC0, "NMI Interval", "Interval", new nmiCtrlNumber { ParentFld = 249, TileWidth = 3 });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.CollapsibleGroup, 260, 2, 0xc0, "Pre-Processing...", null, ThePropertyBag.Create(new nmiCtrlCollapsibleGroup() { DoClose = true, MaxTileWidth = 6, ParentFld = 220, IsSmall = true }));
            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.ComboBox, 261, 2, 0xC0, "Pre-Processing", "PrePro", new nmiCtrlComboBox { Options = "Raw Value:0;Min Value:1;Max Value:2;Average:3", ParentFld = 260, TileWidth = 6, DefaultValue = "0" });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.Number, 265, 2, 0xC0, "PrePro Window", "PreProTime", new nmiCtrlNumber { ParentFld = 260, TileWidth = 3 });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.SingleEnded, 216, 2, 0x0, "Match Pattern", nameof(ValueMatchPattern), new nmiCtrlSingleEnded { ParentFld = 200, TileWidth = 6 });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.SingleEnded, 217, 2, 0x0, "Filter Pattern", nameof(ValueFilterPattern), new nmiCtrlSingleEnded { ParentFld = 200, TileWidth = 6 });

            if (TheCommonUtils.CBool(TheBaseAssets.MySettings.GetSetting("RedPill")))
            {
                var t = TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.TileButton, 266, 2, 0xC0, "Test", null, new nmiCtrlTileButton { NoTE = true, Background = "transparent", TileWidth = 3, TileHeight = 1 });
                t.RegisterUXEvent(MyBaseThing, eUXEvents.OnClick, "hakko", (sender, para) =>
                {
                    cdeP tRW = GetProperty("RawValue", true);
                    cdeP.SetSafePropertyNumber(tRW, "StateSensorMinValue", TheCommonUtils.GetRandomUInt(0, 100));
                    TheThing.SetSafePropertyNumber(MyBaseThing, "[RawValue].[StateSensorAverage]", TheCommonUtils.GetRandomUInt(0, 100));
                });
                TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.Number, 267, 2, 0xc0, "Average Value", "[RawValue].[StateSensorAverage]", new nmiCtrlNumber { TileWidth = 3 });
                TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.Number, 268, 2, 0xc0, "Min Value", "[RawValue].[StateSensorMinValue]", new nmiCtrlNumber { TileWidth = 3 });
            }
            return true;
        }

    }
}
