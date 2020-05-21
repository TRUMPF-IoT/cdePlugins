// SPDX-FileCopyrightText: 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

﻿using nsCDEngine.BaseClasses;
using nsCDEngine.Engines.NMIService;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.ViewModels;
using System;
using System.Collections.Generic;

namespace CDMyComputer
{
    public partial class TheCDMyComputerEngine 
    {
        public TheDashboardInfo MyPCVitalsDashboard;
        public bool CreateUX()
        {
            if (mIsUXInitCalled) return false;
            mIsUXInitCalled = true;

            MyPCVitalsDashboard = TheNMIEngine.AddDashboard(MyBaseThing, new TheDashboardInfo(MyBaseEngine, "Node Vitals") { PropertyBag = new ThePropertyBag() { "Category=Diagnostics", "Caption=Node Vitals", "Thumbnail=FA5:f108", } });

            TheFormInfo tMyConfForm = new TheFormInfo(TheThing.GetSafeThingGuid(MyBaseThing, "Config"), eEngineName.NMIService, "Setting", string.Format("TheThing;:;0;:;True;:;cdeMID={0}", MyBaseThing.cdeMID)) { DefaultView = eDefaultView.Form, PropertyBag = new ThePropertyBag { "TileWidth=6" } };
            TheNMIEngine.AddFormToThingUX(MyBaseThing, tMyConfForm, "CMyForm", "Settings", 1, 9, 0xC0, TheNMIEngine.GetNodeForCategory(), null, null);

            TheNMIEngine.AddSmartControl(MyBaseThing, tMyConfForm, eFieldType.SingleCheck, 1, 2, 0xC0, "Disable Collection", "IsHealthCollectionOff");
            //TheNMIEngine.AddSmartControl(MyBaseThing, tMyConfForm, eFieldType.SingleCheck, 2, 2, 0xC0, "Enable OHM", "EnableOHM");
            TheNMIEngine.AddSmartControl(MyBaseThing, tMyConfForm, eFieldType.Number, 10, 2, 0xC0, "Health Collection Cycle", "HealthCollectionCycle");
            TheNMIEngine.AddSmartControl(MyBaseThing, tMyConfForm, eFieldType.Number, 11, 2, 0xC0, "Sensor Delay", "SensorDelay");
            TheNMIEngine.AddSmartControl(MyBaseThing, tMyConfForm, eFieldType.Number, 12, 2, 0xC0, "Sensor Deadband", "SensorAccelDeadband");
            TheNMIEngine.AddSmartControl(MyBaseThing, tMyConfForm, eFieldType.Number, 13, 2, 0xC0, "Chart Values", "ChartValues", new nmiCtrlNumber { MinValue = 10 });
            TheNMIEngine.AddSmartControl(MyBaseThing, tMyConfForm, eFieldType.Number, 14, 2, 0xC0, "Default Chart TileWidth", "ChartSize", new nmiCtrlNumber { MinValue = 6, MaxValue = 30 });


            TheFormInfo tMyForm = new TheFormInfo() { cdeMID = new Guid("{A3765D29-8EFF-4F09-B5BC-E5CE4C7DEA6F}"), FormTitle = "CPU Details", defDataSource = "TheThing;:;0;:;True;:;DeviceType=CPUInfo" };
            TheNMIEngine.AddFormToThingUX(MyBaseThing, tMyForm, "CMyTable", "CPUs", 2, 3, 0, "Live Tables", null, null);

            TheNMIEngine.AddField(tMyForm, new TheFieldInfo() { FldOrder = 5, FldWidth=4, Flags = 2, cdeA = 0x0, Type = eFieldType.SingleEnded, Header = "Friendly Name", DataItem = "MyPropertyBag.FriendlyName.Value" });
            TheNMIEngine.AddField(tMyForm, new TheFieldInfo() { FldOrder = 6, FldWidth=2, Flags = 0, cdeA = 0x0, Type = eFieldType.SingleEnded, Header = "HostURL", DataItem = "MyPropertyBag.HostUrl.Value" });

            TheNMIEngine.AddField(tMyForm, new TheFieldInfo() { FldOrder = 11, Flags = 0, cdeA = 0x0, Type = eFieldType.SingleEnded, Header = "Speed", DataItem = "MyPropertyBag.MaxClockSpeed.Value" });
            TheNMIEngine.AddField(tMyForm, new TheFieldInfo() { FldOrder = 12, Flags = 0, cdeA = 0x0, Type = eFieldType.SingleEnded, Header = "Cores", DataItem = "MyPropertyBag.NumberOfCores.Value" });
            TheNMIEngine.AddField(tMyForm, new TheFieldInfo() { FldOrder = 13,FldWidth=2, Flags = 0, cdeA = 0x0, Type = eFieldType.SingleEnded, Header = "Manufacturer", DataItem = "MyPropertyBag.Manufacturer.Value" });
            TheNMIEngine.AddField(tMyForm, new TheFieldInfo() { FldOrder = 14, Flags = 0, cdeA = 0xC0, Type = eFieldType.SingleEnded, Header = "Architecture", DataItem = "MyPropertyBag.AddressWidth.Value" });
            TheNMIEngine.AddField(tMyForm, new TheFieldInfo() { FldOrder = 15, Flags = 0, cdeA = 0xC0, Type = eFieldType.SingleEnded, Header = "Rev", DataItem = "MyPropertyBag.Revision.Value" });
            TheNMIEngine.AddField(tMyForm, new TheFieldInfo() { FldOrder = 16, Flags = 0, cdeA = 0xC0, Type = eFieldType.SingleEnded, Header = "L2 Cache", DataItem = "MyPropertyBag.L2CacheSize.Value" });
            TheNMIEngine.AddField(tMyForm, new TheFieldInfo() { FldOrder = 17, Flags = 0, cdeA = 0xC0, Type = eFieldType.SingleEnded, Header = "Version", DataItem = "MyPropertyBag.Version.Value" });

            TheNMIEngine.AddField(tMyForm, new TheFieldInfo() { FldOrder = 80, FldWidth=2, cdeA = 0xFF, Type = eFieldType.DateTime, Header = "Last Update", DataItem = "MyPropertyBag.LastUpdate.Value" });


            TheFormInfo tMyHForm = new TheFormInfo() { cdeMID = new Guid("{33170B1F-CA19-4DC6-A18F-15B5F7669E0A}"), FormTitle = "PC Health Details", defDataSource = "TheThing;:;0;:;True;:;DeviceType=PC-Health" };
            TheNMIEngine.AddFormToThingUX(MyBaseThing, tMyHForm, "CMyTable", "PC Health", 3, 3, 0, "Live Tables", null, null);

            TheNMIEngine.AddField(tMyHForm, new TheFieldInfo() { FldOrder = 5,FldWidth=3, Flags = 2, cdeA = 0x0, Type = eFieldType.SingleEnded, Header = "Friendly Name", DataItem = "MyPropertyBag.FriendlyName.Value" });
            TheNMIEngine.AddField(tMyHForm, new TheFieldInfo() { FldOrder = 6,FldWidth=2, Flags = 0, cdeA = 0x0, Type = eFieldType.SingleEnded, Header = "HostAddress", DataItem = "MyPropertyBag.HostAddress.Value" });
            TheNMIEngine.AddField(tMyHForm, new TheFieldInfo() { FldOrder = 7,FldWidth=3, Flags = 0, cdeA = 0x0, Type = eFieldType.SingleEnded, Header = "Host Version", DataItem = "MyPropertyBag.HostVersion.Value" });
            //TheNMIEngine.AddField(tMyHForm, new TheFieldInfo() { FldOrder = 8, Flags = 0, cdeA = 0xC0, Type = eFieldType.SingleEnded, Header = "Station Roles", DataItem = "MyPropertyBag.StationRoles.Value" });

            TheNMIEngine.AddField(tMyHForm, new TheFieldInfo() { FldOrder = 11, Flags = 0, cdeA = 0x0, Type = eFieldType.SingleEnded, Header = "CPU Load", DataItem = "MyPropertyBag.CPULoad.Value" });
            TheNMIEngine.AddField(tMyHForm, new TheFieldInfo() { FldOrder = 13, Flags = 0, cdeA = 0x0, Type = eFieldType.SingleEnded, Header = "CPU Temp", DataItem = "MyPropertyBag.CPUTemp.Value" });
            //TheNMIEngine.AddField(tMyHForm, new TheFieldInfo() { FldOrder = 14, Flags = 0, cdeA = 0x0, Type = eFieldType.SingleEnded, Header = "Core Temps", DataItem = "MyPropertyBag.CoreTemps.Value" });

            TheNMIEngine.AddField(tMyHForm, new TheFieldInfo() { FldOrder = 21,FldWidth=2, Flags = 0, cdeA = 0x0, Type = eFieldType.SingleEnded, Header = "RAM Available", DataItem = "MyPropertyBag.RAMAvailable.Value" });
            TheNMIEngine.AddField(tMyHForm, new TheFieldInfo() { FldOrder = 22,FldWidth=2, Flags = 0, cdeA = 0x0, Type = eFieldType.SingleEnded, Header = "PC Uptime", DataItem = "MyPropertyBag.PCUptime.Value" });
            TheNMIEngine.AddField(tMyHForm, new TheFieldInfo() { FldOrder = 23, Flags = 0, cdeA = 0x0, Type = eFieldType.SingleEnded, Header = "Watts", DataItem = "MyPropertyBag.StationWatts.Value" });

            TheNMIEngine.AddField(tMyHForm, new TheFieldInfo() { FldOrder = 31, Flags = 0, cdeA = 0x0, Type = eFieldType.SingleEnded, Header = "cdeUptime", DataItem = "MyPropertyBag.cdeUptime.Value" });
            TheNMIEngine.AddField(tMyHForm, new TheFieldInfo() { FldOrder = 32, Flags = 0, cdeA = 0x0, Type = eFieldType.SingleEnded, Header = "cdeHandles", DataItem = "MyPropertyBag.cdeHandles.Value" });
            TheNMIEngine.AddField(tMyHForm, new TheFieldInfo() { FldOrder = 33, Flags = 0, cdeA = 0x0, Type = eFieldType.SingleEnded, Header = "cdeWorkingSetSize", DataItem = "MyPropertyBag.cdeWorkingSetSize.Value" });
            TheNMIEngine.AddField(tMyHForm, new TheFieldInfo() { FldOrder = 34, Flags = 0, cdeA = 0x0, Type = eFieldType.SingleEnded, Header = "cdeThreadCount", DataItem = "MyPropertyBag.cdeThreadCount.Value" });
            TheNMIEngine.AddField(tMyHForm, new TheFieldInfo() { FldOrder = 35, Flags = 0, cdeA = 0x0, Type = eFieldType.SingleEnded, Header = "EventTimeOuts", DataItem = "MyPropertyBag.TotalEventTimeouts.Value" });
            TheNMIEngine.AddField(tMyHForm, new TheFieldInfo() { FldOrder = 36, Flags = 0, cdeA = 0x0, Type = eFieldType.SingleEnded, Header = "QSenders", DataItem = "MyPropertyBag.QSenders.Value" });

            TheNMIEngine.AddField(tMyHForm, new TheFieldInfo() { FldOrder = 80,FldWidth=2, cdeA = 0xFF, Type = eFieldType.DateTime, Header = "Last Update", DataItem = "MyPropertyBag.LastUpdate.Value" });

            int tBS = (int)TheThing.GetSafePropertyNumber(MyBaseThing, "ChartValues");
            if (tBS < 10)
                tBS = 10;
            if (tBS > 10000)
                tBS = 10000;


            int tCS = (int)TheThing.GetSafePropertyNumber(MyBaseThing, "ChartSize");
            if (tCS < 6)
                tCS = 6;

            TheNMIEngine.AddChartScreen(MyBaseThing, new TheChartDefinition(TheThing.GetSafeThingGuid(MyBaseThing, "CPUState"), "Computer CPU State", tBS, "TheHealthHistory", true, "", "PB.HostAddress", "PB.CPULoad,PB.CPUTemp,PB.RAMAvailable") { GroupMode = 0, IntervalInMS = 0 }, 2, 3, 0, TheNMIEngine.GetNodeForCategory(), false, new ThePropertyBag() { ".TileHeight=8", ".NoTE=true", $".TileWidth={tCS}", $"Header={TheNMIEngine.GetNodeForCategory()}" });
            TheNMIEngine.AddChartScreen(MyBaseThing, new TheChartDefinition(TheThing.GetSafeThingGuid(MyBaseThing, "CDERes"), "CDE Resources", tBS, "TheHealthHistory", true, "", "PB.HostAddress", "PB.cdeHandles,PB.cdeWorkingSetSize") { GroupMode = 0, IntervalInMS = 0 }, 5, 3, 0, TheNMIEngine.GetNodeForCategory(), false, new ThePropertyBag() { ".TileHeight=8", ".NoTE=true", $".TileWidth={tCS}", $"Header={TheNMIEngine.GetNodeForCategory()}" });
            TheNMIEngine.AddChartScreen(MyBaseThing, new TheChartDefinition(TheThing.GetSafeThingGuid(MyBaseThing, "CDEKPIs"), "CDE KPIs", tBS, "TheHealthHistory", true, "", "PB.HostAddress", "PB.QSenders,PB.QSLocalProcessed,PB.QSSent,PB.QKBSent,PB.QKBReceived,PB.QSInserted,PB.EventTimeouts,PB.TotalEventTimeouts,PB.CCTSMsRelayed,PB.CCTSMsReceived,PB.CCTSMsEvaluated,PB.HTCallbacks,PB.KPI1,PB.KPI2,PB.KPI3,PB.KPI4,PB.KPI5,PB.KPI10") { GroupMode = 0, IntervalInMS = 0 }, 5, 3, 0, TheNMIEngine.GetNodeForCategory(), false, new ThePropertyBag() { ".TileHeight=8", ".NoTE=true", $".TileWidth={tCS}", $"Header={TheNMIEngine.GetNodeForCategory()}" });


            var tMyLiveForm = TheNMIEngine.AddStandardForm(MyBaseThing, "Live CPU Chart", 18, "CPULoad",null,0,TheNMIEngine.GetNodeForCategory());
            (tMyLiveForm["Header"] as TheFieldInfo).Header = $"{TheNMIEngine.GetNodeForCategory()} CPU Chart";
            var tc = TheNMIEngine.AddSmartControl(MyBaseThing, tMyLiveForm["Form"] as TheFormInfo, eFieldType.UserControl, 22, 0, 0, "CPU Load", "LoadBucket", new ThePropertyBag()
                {   "ParentFld=1",
                    "ControlType=Line Chart", "Title=CPU Load", "SubTitle="+ GetProperty("HostAddress",true),
                    "SeriesNames=[{ \"name\":\"CPU-Load\", \"lineColor\":\"rgba(0,255,0,0.39)\"}, { \"name\":\"CDE-Load\", \"lineColor\":\"rgba(0,0,255,0.39)\"}]",
                    "TileHeight=4", "Speed=800", "Delay=0", "Background=rgba(0,0,0,0.01)", "MaxValue=100", "NoTE=true"
                });
            tc.AddOrUpdatePlatformBag(eWebPlatform.Any, new nmiPlatBag { TileWidth = 18 });
            tc.AddOrUpdatePlatformBag(eWebPlatform.Mobile, new nmiPlatBag { TileWidth = 6 });
            tc.AddOrUpdatePlatformBag(eWebPlatform.HoloLens, new nmiPlatBag { TileWidth = 12 });

            if (TheCommonUtils.CBool(TheBaseAssets.MySettings.GetSetting("PCVitalsMaster")))
            {
                TheFormInfo tMyFormUp = TheNMIEngine.AddForm(new TheFormInfo(MyBaseThing) { cdeMID = TheThing.GetSafeThingGuid(MyBaseThing, "THHUPLOAD"), FormTitle = "PC Vitals Uploader", DefaultView = eDefaultView.Form });
                TheNMIEngine.AddFormToThingUX(MyBaseThing, tMyFormUp, "CMyForm", "PC Vitals Uploader", 3, 13, 0x80, TheNMIEngine.GetNodeForCategory(), null, new nmiDashboardTile { TileThumbnail = "FA3:f093", });
                TheNMIEngine.AddSmartControl(MyBaseThing, tMyFormUp, eFieldType.DropUploader, 3, 2, 128, "Drop a TheHealthHistory-file here", null,
                    new nmiCtrlDropUploader { TileHeight = 6, NoTE = true, TileWidth = 6, EngineName = MyBaseEngine.GetEngineName(), MaxFileSize = 10000000 });
                FindTHHFiles();
            }


            TheNMIEngine.AddPageDefinitions(new List<ThePageDefinition>
                        {
                            {  new ThePageDefinition(new Guid("{7FED3369-AF7C-451F-9ED1-71131BB993F4}"), "/PCHEALTH", "Health Info","", Guid.Empty)
                            { WPID=10,IncludeCDE=true,RequireLogin=false,PortalGuid=MyBaseEngine.GetDashboardGuid(),StartScreen=MyBaseEngine.GetDashboardGuid() }
                            },
                        });
            TheNMIEngine.AddAboutButton(MyBaseThing);
            TheNMIEngine.RegisterEngine(MyBaseEngine);

            mIsUXInitialized = true;
            return true;
        }
    }
}
