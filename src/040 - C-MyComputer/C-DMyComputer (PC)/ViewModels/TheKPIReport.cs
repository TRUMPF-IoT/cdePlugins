// SPDX-FileCopyrightText: 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

﻿using nsCDEngine.Engines;
using nsCDEngine.Engines.NMIService;
using nsCDEngine.Engines.ThingService;
using System;
using TheSensorTemplate;

namespace CDMyComputer.ViewModels
{
    public class TheKPIReport : TheDefaultSensor
    {
        public const string eDeviceType = "KPI Report";

        public bool IsActive
        {
            get { return TheThing.GetSafePropertyBool(MyBaseThing, "IsActive"); }
            set
            {
                MyBaseThing.SetProperty("IsActive", value.ToString(), ePropertyTypes.TBoolean);
            }
        }

        public TheKPIReport(TheThing tBaseThing, ICDEPlugin pPluginBase) : base(tBaseThing, pPluginBase)
        {
            MyBaseThing.DeviceType = eDeviceType;
        }

        public override void DoInit()
        {
            base.DoInit();
            IsActive = false;
            if (!string.IsNullOrEmpty(TheThing.GetSafePropertyString(MyBaseThing, "RealSensorThing")) && !string.IsNullOrEmpty(TheThing.GetSafePropertyString(MyBaseThing, "RealSensorProperty")))
            {
                EngageMapper();
            }
        }

        public override void DoCreateUX(TheFormInfo tMyForm, ThePropertyBag pChartsBag=null)
        {
            base.DoCreateUX(tMyForm);

            AddSpeedGauge(tMyForm);

            var tEngage = TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.TileButton, 25012, 2, 0xC0, "Restart KPI", null, new nmiCtrlNumber { ParentFld = TheDefaultSensor.SensorActionArea, TileWidth = 6, NoTE = true });
            tEngage.RegisterUXEvent(MyBaseThing, eUXEvents.OnClick, "reset", (sender, para) => { EngageMapper(); });
            var tRemove = TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.TileButton, 25013, 2, 0xC0, "Delete this KPI Report", null, new nmiCtrlNumber { ParentFld = TheDefaultSensor.SensorActionArea, TileWidth = 6, NoTE = true });
            tRemove.RegisterUXEvent(MyBaseThing, eUXEvents.OnClick, "remove", (sender, para) => 
            {
                TheThing tT = TheThingRegistry.GetThingByID(MyBaseEngine.GetEngineName(), MyBaseThing.ID);
                if (tT != null)
                {
                    TheThingRegistry.UnmapPropertyMapper(mRealThingGuid);
                    TheThingRegistry.DeleteThing(tT);
                }
            });

        }

        Guid mRealThingGuid = Guid.Empty;
        private void EngageMapper()
        {
            TheThingRegistry.UnmapPropertyMapper(mRealThingGuid);
            mRealThingGuid = TheThingRegistry.PropertyMapper(TheThing.GetSafePropertyGuid(MyBaseThing, "RealSensorThing"), TheThing.GetSafePropertyString(MyBaseThing, "RealSensorProperty"), MyBaseThing.cdeMID, "Value", false);
            if (mRealThingGuid == Guid.Empty)
            {
                MyBaseThing.StatusLevel = 0;
                MyBaseThing.LastMessage = "KPI Report not active";
                IsActive = false;
            }
            else
            {
                MyBaseThing.LastMessage = "KPI report engaged";
                MyBaseThing.StatusLevel = 1;
                CopyStateSensorInfo(MyBaseThing);
                IsActive = true;
            }
        }


    }
}
