// SPDX-FileCopyrightText: 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using nsCDEngine.BaseClasses;
using nsCDEngine.Engines;
using nsCDEngine.Engines.NMIService;
using nsCDEngine.Engines.ThingService;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CDMyMSSQLStorage
{
    internal partial class TheStorageService 
    {

        public override bool CreateUX()
        {
            if (mIsUXInitialized) return true; //No processing if Storage is not active
            mIsUXInitialized = true;
            TheDashboardInfo tDash = TheNMIEngine.AddDashboard(MyBaseThing, new TheDashboardInfo(MyBaseEngine, "Storage Service: MS-SQL")
            {
                cdeA = 0xC0,
                FldOrder = 11,
                PropertyBag = new nmiDashboardTile() { Category = "Services", Caption= "<i class='fa faIcon fa-5x'>&#xf1c0;</i></br>Storage Service: MS-SQL"}
            });

            if (TheCDEngines.MyIStorageService != null)
            {
                var tF = TheNMIEngine.AddStandardForm(MyBaseThing, "SQL Storage Settings", 20, TheThing.GetSafeThingGuid(MyBaseThing, "SQLStorageSettings").ToString(), null, 0xF0);
                var tMyUserSettingsForm = tF["Form"] as TheFormInfo;
                TheNMIEngine.AddSmartControl(MyBaseThing, tMyUserSettingsForm, eFieldType.CollapsibleGroup, 30, 2, 0, "Storage Options", null, new nmiCtrlCollapsibleGroup() { ParentFld=1, DoClose = false, IsSmall = true, TileWidth = 6 });
                TheFieldInfo thingRegistryBtn = TheNMIEngine.AddSmartControl(MyBaseThing, tMyUserSettingsForm, eFieldType.SingleCheck, 40, 2, 0, "Store Thing Registry", "StoreThingRegistry", new nmiCtrlSingleCheck() { HelpText = "You have to restart the relay in order for this setting to get in effect", ParentFld = 30, DefaultValue = "False" });
                thingRegistryBtn.RegisterPropertyChanged(SinkUpdateStorage);
                TheNMIEngine.AddSmartControl(MyBaseThing, tMyUserSettingsForm, eFieldType.SingleCheck, 50, 2, 0, "Don't Log Queue Updates", "DontLogQueue", new nmiCtrlSingleCheck() { TileWidth = 3, ParentFld = 30});
            }

            //var tList=TheCDEngines.EnumerateStorageMirror().FirstOrDefault(s=>s.Value.Contains("TheFieldInfo"));
            ////if (tList != null)
            //{
            //    TheChartDefinition pChart = new TheChartDefinition(new Guid("{F7468BC6-03F7-4BF7-A0B7-A5A7B2A55645}"), tList.Value, 100, tList.Key, true, "", "", "");
            //    TheNMIEngine.AddChartScreen(MyBaseThing, pChart, 3, "Sensor Chart", 3, 0, "Charts", false, new ThePropertyBag() { ".NoTE=true", ".TileWidth=12", ".TileHeight=6", "Header=All Sensor Data" });
            //}
            TheNMIEngine.AddAboutButton(MyBaseThing, null, TheNMIEngine.GetNodeForCategory());

            return true;
        }

        private void SinkUpdateStorage(TheFieldInfo arg1, cdeP arg2)
        {
            TheBaseAssets.MySettings.SetSetting("UseStorageForThingRegistry", TheCommonUtils.CBool(arg2.Value).ToString());
        }
    }
}
