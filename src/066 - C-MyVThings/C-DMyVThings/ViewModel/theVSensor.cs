// SPDX-FileCopyrightText: 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

﻿using nsCDEngine.BaseClasses;
using nsCDEngine.Engines;
using nsCDEngine.Engines.NMIService;
using nsCDEngine.Engines.ThingService;
using System;
using TheSensorTemplate;

namespace CDMyVThings.ViewModel
{
    class TheVirtualSensor : TheDefaultSensor
    {
        public bool IsActive
        {
            get { return TheThing.GetSafePropertyBool(MyBaseThing, "IsActive"); }
            set
            {
                MyBaseThing.SetProperty("IsActive", value.ToString(), ePropertyTypes.TBoolean);
            }
        }

        public TheVirtualSensor(TheThing tBaseThing, ICDEPlugin pPluginBase) : base(tBaseThing, pPluginBase)
        {
            MyBaseThing.DeviceType = eVThings.eVirtualSensor;
        }

        public override void DoInit()
        {
            base.RegisterEvent("HistorianReady", sinkHistReady2);
            TheThing.SetSafePropertyString(MyBaseThing, "ReportCategory", "..Virtual Sensor");
            base.DoInit();
            MyBaseThing.LastMessage = "Sensor ready";
            if (!string.IsNullOrEmpty(TheThing.GetSafePropertyString(MyBaseThing, "RealSensorThing")) && !string.IsNullOrEmpty(TheThing.GetSafePropertyString(MyBaseThing, "RealSensorProperty")))
            {
                EngageMapper();
            }
        }

        void sinkHistReady2(ICDEThing sender, object para)
        {
            //MyHistorian.SetCacheStoreInterval(60);
        }


        public override void DoCreateUX(TheFormInfo tMyForm, ThePropertyBag pChartBag = null)
        {
            base.DoCreateUX(tMyForm);
            tMyForm.ModelID = string.IsNullOrEmpty(tMyForm.ModelID) ? "VSensorForm" : tMyForm.ModelID += ";VSensorForm";
            AddSpeedGauge(tMyForm);

            TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.ThingPicker, 25010, 2, 0xc0, "Thing Picker", "RealSensorThing", new nmiCtrlThingPicker() { ParentFld = TheDefaultSensor.SensorActionArea, IncludeEngines = true, IncludeRemotes = true });
            TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.PropertyPicker, 25011, 2, 0xc0, "Property Picker", "RealSensorProperty", new nmiCtrlPropertyPicker() { ParentFld = TheDefaultSensor.SensorActionArea, DefaultValue = "Value", ThingFld = 25010 });
            GetProperty("RealSensorProperty", true).RegisterEvent(eThingEvents.PropertyChanged, (p) =>
            {
                EngageMapper();
            });
            var tEngage = TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.TileButton, 25012, 2, 0xC0, "Restart Mapper", null, new nmiCtrlNumber { ParentFld = TheDefaultSensor.SensorActionArea, TileWidth = 6, NoTE = true });
            tEngage.RegisterUXEvent(MyBaseThing, eUXEvents.OnClick, "reset", (sender, para) => { EngageMapper(); });

        }

        protected override void SensorCyclicCalc(long timer)
        {
            base.SensorCyclicCalc(timer);
            if (MyBaseThing.StatusLevel != 0 && IsDisabled)
            {
                MyBaseThing.StatusLevel = 0;
                MyBaseThing.LastMessage = "V-Sensor disabled";
                TheThingRegistry.UnmapPropertyMapper(mRealThingGuid);
            }
            if (MyBaseThing.StatusLevel == 0 && !IsDisabled)
            {
                MyBaseThing.StatusLevel = 1;
                MyBaseThing.LastMessage = "V-Sensor Enabled";
                EngageMapper();
            }
        }

        public override bool StartSensor()
        {
            base.StartSensor();
            return true;
        }

        Guid mRealThingGuid = Guid.Empty;
        private void EngageMapper()
        {
            TheThingRegistry.UnmapPropertyMapper(mRealThingGuid);
            mRealThingGuid = TheThingRegistry.PropertyMapper(TheThing.GetSafePropertyGuid(MyBaseThing, "RealSensorThing"), TheThing.GetSafePropertyString(MyBaseThing, "RealSensorProperty"), MyBaseThing.cdeMID, "Value", false);
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
                CopyStateSensorInfo(MyBaseThing);
                IsActive = true;
            }
        }
    }
}
