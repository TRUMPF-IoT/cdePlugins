// SPDX-FileCopyrightText: 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

﻿using nsCDEngine.BaseClasses;
using nsCDEngine.Engines.NMIService;
using nsCDEngine.Engines.ThingService;
using System.Collections.Generic;

namespace CDMyVThings
{
    public partial class TheVThings 
    {

        public virtual bool CreateUX()
        {
            if (mIsUXInitCalled) return false;
            mIsUXInitCalled = true;

            MyDashboard = TheNMIEngine.AddDashboard(MyBaseThing, new TheDashboardInfo(MyBaseEngine, "Virtual Things") { FldOrder = 5000, PropertyBag = new ThePropertyBag { "Category=Services", "Caption=<i class='fa faIcon fa-5x'>&#xf61f;</i></br>Virtual Things" } });

            tVThingsForm = new TheFormInfo(TheThing.GetSafeThingGuid(MyBaseThing, "VTable"), eEngineName.NMIService, "Virtual Things", string.Format("TheThing;:;0;:;True;:;EngineName={0}", MyBaseThing.EngineName)) { IsNotAutoLoading = true, TileWidth = 12, AddButtonText = "Add V-Thing" };
            TheNMIEngine.AddFormToThingUX(MyBaseThing, tVThingsForm, "CMyTable", "<i class='fa faIcon fa-5x'>&#xf0ce;</i></br>V-Things List", 1, 3, 0xF0, TheNMIEngine.GetNodeForCategory(), null, new ThePropertyBag() { });
            TheNMIEngine.AddCommonTableColumns(MyBaseThing, tVThingsForm, new eVThings(), eVThings.eVCountdown,false);

            TheNMIEngine.AddFields(tVThingsForm, new List<TheFieldInfo> {
                {  new TheFieldInfo() { FldOrder=800,DataItem="MyPropertyBag.IsDisabled.Value",Flags=2,Type=eFieldType.SingleCheck,Header="Disable",FldWidth=1,TileWidth=1, TileHeight=1, PropertyBag = new ThePropertyBag() { } }},
                {  new TheFieldInfo(MyBaseThing,"Value",21,0x40,0) { Type=eFieldType.CircularGauge,Header="Current Value",FldWidth=2 }},
               });

            TheNMIEngine.AddAboutButton(MyBaseThing, null, null, true, "REFRESH_DASH", 0xc0);
            TheNMIEngine.RegisterEngine(MyBaseEngine);
            mIsUXInitialized = true;
            return true;
        }

        TheDashboardInfo MyDashboard;
        TheFormInfo tVThingsForm;

    }
}
