// SPDX-FileCopyrightText: 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

﻿using nsCDEngine.BaseClasses;
using nsCDEngine.Engines.NMIService;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.ViewModels;
using System;
using System.Collections.Generic;

namespace CDMyVThings
{
    public partial class TheVThings
    {

        public override bool CreateUX()
        {
            if (mIsUXInitCalled) return false;
            mIsUXInitCalled = true;

            MyDashboard = TheNMIEngine.AddDashboard(MyBaseThing, new TheDashboardInfo(MyBaseEngine, "Virtual Things") { FldOrder = 5000, PropertyBag = new ThePropertyBag { "Category=Services", "Caption=<i class='fa faIcon fa-5x'>&#xf61f;</i></br>Virtual Things" } });

            tVThingsForm = new TheFormInfo(TheThing.GetSafeThingGuid(MyBaseThing, "VTable"), eEngineName.NMIService, "Virtual Things", string.Format("TheThing;:;0;:;True;:;EngineName={0}", MyBaseThing.EngineName)) { IsNotAutoLoading = true, TileWidth = 12, AddButtonText = "Add V-Thing" };
            TheNMIEngine.AddFormToThingUX(MyBaseThing, tVThingsForm, "CMyTable", "<i class='fa faIcon fa-5x'>&#xf0ce;</i></br>V-Things List", 1, 3, 0xF0, TheNMIEngine.GetNodeForCategory(), null, new ThePropertyBag() { });
            TheNMIEngine.AddCommonTableColumns(MyBaseThing, tVThingsForm, new eVThings(), eVThings.eVCountdown, false);

            TheNMIEngine.AddFields(tVThingsForm, new List<TheFieldInfo> {
                {  new TheFieldInfo() { FldOrder=800,DataItem="MyPropertyBag.IsDisabled.Value",Flags=2,Type=eFieldType.SingleCheck,Header="Disable",FldWidth=1,TileWidth=1, TileHeight=1, PropertyBag = new ThePropertyBag() { } }},
                {  new TheFieldInfo(MyBaseThing,"Value",21,0x40,0) { Type=eFieldType.CircularGauge,Header="Current Value",FldWidth=2 }},
               });

            AddVSensorWizard();
            TheNMIEngine.AddAboutButton(MyBaseThing, null, null, true, "REFRESH_DASH", 0xc0);
            TheNMIEngine.RegisterEngine(MyBaseEngine);
            mIsUXInitialized = true;
            return true;
        }

        TheDashboardInfo MyDashboard;
        TheFormInfo tVThingsForm;
        protected TheFieldInfo mInstanceButton;
        protected TheFieldInfo mResultInfo;

        public void AddVSensorWizard()
        {
            var flds = TheNMIEngine.AddNewWizard<TheVSensWiz>(new Guid("{4B536A76-8618-45A9-A6F5-B9AC1743EBA3}"), Guid.Empty, TheNMIEngine.GetEngineDashBoardByThing(MyBaseThing).cdeMID, "Welcome to the V-Things Wizard",
                new nmiCtrlWizard { PanelTitle = "<i class='fa faIcon fa-5x'>&#xf0d0;</i></br>Add a new Sensor Dashboard", SideBarTitle = "New Sensor Dashboard Wizard", SideBarIconFA = "&#xf545;", TileThumbnail = "FA5:f545" },
                (myClass, pClientInfo) =>
                {
                    myClass.cdeMID = Guid.Empty;
                    myClass.Error = "";

                    var tReq = new TheThingRegistry.MsgCreateThingRequestV1()
                    {
                        EngineName = MyBaseEngine.GetEngineName(),
                        DeviceType = eVThings.eVirtualSensor,
                        FriendlyName = myClass.ClientName,
                        InstanceId = Guid.NewGuid().ToString(),
                        Properties = new Dictionary<string, object> {
                    { "RealSensorThing", myClass.SelectedSensor },
                    { "RealSensorProperty", myClass.SelectedProp },
                },
                        CreateIfNotExist = true
                    };
                    var tMemTag = TheThingRegistry.CreateOwnedThingAsync(tReq).Result;
                    mResultInfo?.SetUXProperty(pClientInfo.NodeID, $"Text=Success!");
                    mInstanceButton?.SetUXProperty(pClientInfo.NodeID, $"OnClick=TTS:{tMemTag.GetBaseThing().cdeMID}");
                });
            var tMyForm2 = flds["Form"] as TheFormInfo;

            var tFlds = TheNMIEngine.AddNewWizardPage(MyBaseThing, tMyForm2, 0, 1, 0, "Sensor Selector");
            TheNMIEngine.AddWizardControl(MyBaseThing, tMyForm2, eFieldType.SingleEnded, 1, 1, 2, 0, "Dashboard Name", "ClientName", new TheNMIBaseControl { Explainer = "1. Enter name for the new Dashboard.", });
            var tf =TheNMIEngine.AddWizardControl(MyBaseThing, tMyForm2, eFieldType.ThingPicker, 1, 2, 2, 0, "Select Sensor Thing", "SelectedSensor", new nmiCtrlThingPicker() { IncludeEngines=true, Explainer = "2. Select a thing to show in Dashboard" });
            TheNMIEngine.AddWizardControl(MyBaseThing, tMyForm2, eFieldType.PropertyPicker, 1, 3, 2, 0, "Select Property", "SelectedProp", new nmiCtrlPropertyPicker() { ThingFld=tf.FldOrder, Explainer = "3. Select a property of the Thing" });

            TheNMIEngine.AddWizardFinishPage(MyBaseThing, tMyForm2, 2);
            mResultInfo = TheNMIEngine.AddWizardControl(MyBaseThing, tMyForm2, eFieldType.SmartLabel, 2, 1, 0, 0, null, "Error", new nmiCtrlSmartLabel { FontSize = 24, NoTE = true, TileWidth = 7 });
            TheNMIEngine.AddWizardControl(MyBaseThing, tMyForm2, eFieldType.SmartLabel, 2, 2, 0, 0, null, null, new nmiCtrlSmartLabel { NoTE = true, TileWidth = 7, Text = "What do you want to do next?", TileHeight = 2 });
            TheNMIEngine.AddWizardControl(MyBaseThing, tMyForm2, eFieldType.TileGroup, 2, 3, 0, 0, null, null, new nmiCtrlTileGroup { TileWidth = 1, TileHeight = 2, TileFactorX = 2 });
            TheNMIEngine.AddWizardControl(MyBaseThing, tMyForm2, eFieldType.TileButton, 2, 4, 2, 0, "Run another Wizard", null, new nmiCtrlTileButton { NoTE = true, TileHeight = 1, TileWidth = 3, OnClick = $"TTS:{MyDashboard.cdeMID}", ClassName = "cdeTransitButton" });
            TheNMIEngine.AddWizardControl(MyBaseThing, tMyForm2, eFieldType.TileGroup, 2, 5, 0, 0, null, null, new nmiCtrlTileGroup { TileWidth = 1, TileHeight = 2 });
            mInstanceButton = TheNMIEngine.AddWizardControl(MyBaseThing, tMyForm2, eFieldType.TileButton, 2, 6, 2, 0, "Go to Sensor Dashboard", null, new nmiCtrlTileButton { NoTE = true, TileHeight = 1, TileWidth = 3, ClassName = "cdeTransitButton" });
        }
    }
    public class TheVSensWiz : TheDataBase
    {
        public string ClientName { get; set; }
        public string SelectedSensor { get; set; }
        public string SelectedProp { get; set; }
        public bool AutoStart { get; set; }
        public string Error { get; set; }
    }
}
