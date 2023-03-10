// SPDX-FileCopyrightText: 2009-2023 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

﻿using nsCDEngine.BaseClasses;
using nsCDEngine.Communication;
using nsCDEngine.Engines.NMIService;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.ViewModels;
using System;
using System.Collections.Generic;

namespace CDMyVThings.ViewModel
{
    partial class TheNMIElement
    {
        void sinkUpdateControls(ICDEThing pThing, object para)
        {
            UpdateUx();
        }

        private void SetCtrlType()
        {
            if (CountBar == null) return;
            string tControl = ThePropertyBag.PropBagGetValue(CountBar.PropertyBag, "ControlType", "=");
            eFieldType tCtrlType = eFieldType.SingleEnded;
            if (!string.IsNullOrEmpty(tControl) && TheCommonUtils.CInt(tControl) == 0 && tControl.Length > 0)
            {
                TheControlType tType = TheNMIEngine.GetControlTypeByType(tControl);
                if (tType != null)
                    ThePropertyBag.PropBagUpdateValue(CountBar.PropertyBag, "EngineName", "=", tType.BaseEngineName);
                tCtrlType = eFieldType.UserControl;
            }
            else
                tCtrlType = (eFieldType)TheCommonUtils.CInt(tControl);
            CountBar.Type = tCtrlType;
            CountBar.Flags = TheCommonUtils.CInt(ThePropertyBag.PropBagGetValue(CountBar.PropertyBag, "Flags", "="));
            CountBar.PropertyBag = new TheNMIBaseControl { ParentFld = 1 };
            CountBar.UpdateUXProperties(Guid.Empty);
            CountBar.RegisterUXEvent(MyBaseThing, eUXEvents.OnClick, "click", (sener, para) => {
                TheThing.SetSafePropertyBool(MyBaseThing,"ClickState", !TheThing.GetSafePropertyBool(MyBaseThing,"ClickState"));
            });
        }


        protected TheFieldInfo CountBar;
        protected TheFieldInfo TimestampField;
        protected TheFieldInfo ChangeTimestampField;
        protected TheFieldInfo LiveScreenField;
        public override bool DoCreateUX()
        {
            MyBaseThing.RegisterProperty("ClickState");
            var tFlds = TheNMIEngine.AddStandardForm(MyBaseThing, null, 24, null, "Value");
            MyStatusForm = tFlds["Form"] as TheFormInfo;
            //MyStatusForm.PropertyBag = new ThePropertyBag { "TileWidth=6" };
            MyStatusForm.RegisterEvent2(eUXEvents.OnShow, (pmse,sen) => 
            { 
                UpdateUx();
                LiveScreenField?.SetUXProperty(Guid.Empty, TheCommonUtils.GenerateFinalStr("Options=%GetLiveScreens%"));
            });
            SummaryForm = tFlds["DashIcon"] as TheDashPanelInfo;
            SummaryForm.PropertyBag = new nmiDashboardTile() { Format = $"{MyBaseThing.FriendlyName}<br>{{0}}", Caption = MyBaseThing.FriendlyName };

            CountBar = TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.SingleEnded, 5, 2, MyBaseThing.cdeA, "CurrentValue", "Value", null);
            ChangeTimestampField = TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.DateTime, 6, 0, MyBaseThing.cdeA, "Last Change", nameof(ValueChangeTimestamp), new nmiCtrlDateTime { ParentFld=1, Visibility = ShowChangeTimestamp });
            TimestampField = TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.DateTime, 7, 0, MyBaseThing.cdeA, "Timestamp", nameof(ValueTimestamp), new nmiCtrlDateTime { ParentFld=1, Visibility = ShowChangeTimestamp });

            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.CollapsibleGroup, 800, 2, 0x80, "Configuration...", null, ThePropertyBag.Create(new nmiCtrlCollapsibleGroup() { ParentFld = 1, DoClose = true, IsSmall = true }));

            var ts = TheNMIEngine.AddStatusBlock(MyBaseThing, MyStatusForm, 810);
            ts["Group"].SetParent(800);
            ts["Group"].PropertyBag = new nmiCtrlCollapsibleGroup { DoClose = true };



            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.CollapsibleGroup, 1000, 2, 0x80, "NMI Settings...", null, ThePropertyBag.Create(new nmiCtrlCollapsibleGroup() { ParentFld=800, DoClose = true, IsSmall = true, TileWidth = 6 }));
            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.CollapsibleGroup, 3000, 2, 0x80, "Advanced NMI Settings...", null, ThePropertyBag.Create(new nmiCtrlCollapsibleGroup() { ParentFld = 1000, DoClose = true, IsSmall = true, TileWidth = 6 }));
            LiveScreenField= TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.ComboBox, 3010, 2, 0x80, "NMI Screen", "FormName", new nmiCtrlComboBox() { Options="%GetLiveScreens%", ParentFld=3000 });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.CheckField, 3020, 2, 0x80, "Flags", "Flags", new ThePropertyBag() { "Bits=6", "TileHeight=6","TileFactorY=2", 
                      "ImageList=<span class='fa-stack'><i class='fa fa-stack-1x'>&#xf21b;</i><i class='fa fa-stack-2x'>&#x2003;</i></span>,"+
                                "<span class='fa-stack'><i class='fa fa-stack-1x'>&#xf044;</i><i class='fa fa-stack-2x'>&#x2003;</i></span>,"+
                                "<span class='fa-stack'><i class='fa fa-stack-1x'>&#xf10b;</i><i class='fa fa-stack-2x text-danger' style='opacity:0.5'>&#xf05e;</i></span>,"+
                                "<span class='fa-stack'><i class='fa fa-stack-1x'>&#xf0ce;</i><i class='fa fa-stack-2x text-danger' style='opacity:0.5'>&#xf05e;</i></span>,"+
                                "<span class='fa-stack'><i class='fa fa-stack-1x'>&#xf0f6;</i><i class='fa fa-stack-2x text-danger' style='opacity:0.5'>&#xf05e;</i></span>,"+
                                "<span class='fa-stack'><i class='fa fa-stack-1x'>&#xf15c;</i><i class='fa fa-stack-2x text-danger' style='opacity:0.5'>&#xf05e;</i></span>","ParentFld=3000" }).FldWidth = 1;

            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.ComboBox, 1500, 2, 0x80, "Control Type", "ControlType", new ThePropertyBag() { "Options=%RegisteredControlTypes%", "ParentFld=1000" });

            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.CollapsibleGroup, 2000, 2, 0x80, "All Properties...", null, ThePropertyBag.Create(new nmiCtrlCollapsibleGroup() { NoTE=true, ParentFld = 1000, DoClose = true, IsSmall = true, TileWidth = 6 }));

            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.Table, 2010, 0xA2, 0x80, "All Properties", "mypropertybag;1", new ThePropertyBag() { "NoTE=true", "TileHeight=4", "TileLeft=9", "TileTop=3", "TileWidth=6", "FldWidth=6", "ParentFld=2000" });
            TheNMIEngine.AddFields(MyStatusForm, new List<TheFieldInfo> {
                    {  new TheFieldInfo() { FldOrder=2020,DataItem="MyPropertyBag.ScratchName.Value",Flags=0x0A,Type=eFieldType.SingleEnded,Header="New Property Name",TileWidth=6, TileHeight=1  , PropertyBag = new ThePropertyBag() { "ParentFld=2000"}}},
               });

            TheFieldInfo tBut = TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.TileButton, 2040, 0x0A, 0, "Add Property", false, null, null, new nmiCtrlTileButton() { ParentFld=2000, NoTE=true, ClassName = "cdeGoodActionButton" });
            tBut.RegisterUXEvent(MyBaseThing, eUXEvents.OnClick, "AddProp", (pThing, pObj) =>
            {
                TheProcessMessage pMsg = pObj as TheProcessMessage;
                if (pMsg?.Message == null) return;
                string[] parts = pMsg.Message.PLS.Split(':');
                TheThing tOrg = TheThingRegistry.GetThingByMID(MyBaseEngine.GetEngineName(), TheCommonUtils.CGuid(parts[2]));
                if (tOrg == null) return;

                string tNewPropName = TheThing.GetSafePropertyString(tOrg, "ScratchName");
                if (string.IsNullOrEmpty(tNewPropName))
                    TheCommCore.PublishToOriginator(pMsg.Message, new TSM(eEngineName.NMIService, "NMI_TOAST", "Please specify a new property name"));
                else
                {
                    if (tOrg.GetProperty(tNewPropName) != null)
                    {
                        TheCommCore.PublishToOriginator(pMsg.Message, new TSM(eEngineName.NMIService, "NMI_TOAST", "Property already exists"));
                        tOrg.SetProperty("ScratchName", "");
                    }
                    else
                    {
                        tOrg.DeclareNMIProperty(tNewPropName, ePropertyTypes.TString);
                        TheCommCore.PublishToOriginator(pMsg.Message, new TSM(eEngineName.NMIService, "NMI_TOAST", "Property Added"));
                        tOrg.SetProperty("ScratchName", "");
                        MyStatusForm.Reload(pMsg, false);
                    }
                }
            });
            TheFieldInfo mSendbutton = TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.TileButton, 1550, 2, 0x80, "Reload", false, "", null, new nmiCtrlTileButton() { TileWidth = 6,NoTE=true, ClassName = "cdeGoodActionButton", ParentFld = 1000 });
            mSendbutton.RegisterUXEvent(MyBaseThing, eUXEvents.OnClick, "", (pThing, pPara) =>
            {
                TheProcessMessage pMsg = pPara as TheProcessMessage;
                if (pMsg?.Message == null) return;
                UpdateUx();
                MyStatusForm.Reload(pMsg, true);
                //TheNMIEngine.GetEngineDashBoardByThing(MyBaseEngine.GetBaseThing()).Reload(pMsg, true);
            });


            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.CollapsibleGroup, 5000, 2, 0x80, "Source...", null, ThePropertyBag.Create(new nmiCtrlCollapsibleGroup() { DoClose = true, IsSmall = true, ParentFld = 800, TileWidth = 6 }));
            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.ThingPicker, 5010, 2, 0x80, "Source Thing", "Address", new nmiCtrlThingPicker() { ParentFld=5000, IncludeEngines=true, IncludeRemotes=true });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.PropertyPicker, 5020, 2, 0x80, "Source Property", "SourceProp", new nmiCtrlPropertyPicker() {  ParentFld=5000, ThingFld=5010 });


            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.SingleCheck, 5030, 2, 0x80, "Show Timestamp", nameof(ShowTimestamp), new nmiCtrlSingleCheck { ParentFld = 5000, TileWidth = 3 });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.SingleCheck, 5040, 2, 0x80, "Show Change Time", nameof(ShowChangeTimestamp), new nmiCtrlSingleCheck { ParentFld = 5000, TileWidth = 3 });
            TheFieldInfo mSendbutton2 = TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.TileButton, 5050, 2, 0x80, "Engage", false, "", null, new nmiCtrlTileButton() { NoTE=true, TileWidth = 6, ClassName = "cdeGoodActionButton", ParentFld = 5000 });
            mSendbutton2.RegisterUXEvent(MyBaseThing, eUXEvents.OnClick, "", (pThing, pPara) =>
            {
                TheProcessMessage pMsg = pPara as TheProcessMessage;
                if (pMsg?.Message == null) return;
                UpdateUx();
            });

            UpdateUx();
            return true;
        }

        private void UpdateUx()
        {
            ThePropertyBag.MergeUXBagFromProperties(CountBar?.PropertyBag, MyBaseThing);
            SetCtrlType();

            if (TimestampField != null)
            {
                TimestampField?.SetUXProperty(Guid.Empty, $"Visibility={ShowTimestamp}");
                ChangeTimestampField?.SetUXProperty(Guid.Empty,$"Visibility={ShowChangeTimestamp}");
            }

            TheThingRegistry.UnmapPropertyMapper(MapperGuid);
            if (!string.IsNullOrEmpty(MyBaseThing.Address))
            {
                MapperGuid=TheThingRegistry.PropertyMapper(TheCommonUtils.CGuid(MyBaseThing.Address), TheThing.GetSafePropertyString(MyBaseThing, "SourceProp"), MyBaseThing.cdeMID, "Value", true);
                if (MapperGuid!=Guid.Empty)
                {
                    MyBaseThing.StatusLevel = 1;
                    MyBaseThing.LastMessage = "Mapper engaged";
                }
                else
                {
                    MyBaseThing.StatusLevel = 2;
                    MyBaseThing.LastMessage = "Mapper failed to engaged";
                }
            }
            else
            {
                MyBaseThing.StatusLevel = 0;
                MyBaseThing.LastMessage = "Mapper not engaged";
            }
        }
        Guid MapperGuid;
    }
}
