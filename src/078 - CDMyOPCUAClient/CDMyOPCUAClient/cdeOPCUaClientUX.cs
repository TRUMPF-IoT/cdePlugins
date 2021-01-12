// SPDX-FileCopyrightText: 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0
using CDMyOPCUAClient.Contracts;
using nsCDEngine.BaseClasses;
using nsCDEngine.Communication;
using nsCDEngine.Engines.NMIService;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.ViewModels;
using System;
using System.Collections.Generic;

namespace CDMyOPCUAClient
{
    partial class cdeOPCUaClient
    {
        TheDashboardInfo mMyDashboard;
        public override bool CreateUX()
        {
            if (mIsUXInitCalled) return false;
            mIsUXInitCalled = true;

            //NUI Definition for All clients
            mMyDashboard = TheNMIEngine.AddDashboard(MyBaseThing, new TheDashboardInfo(MyBaseEngine, "OPC UA Client") { PropertyBag = new nmiDashboardTile() { Category= " Connectivity", Thumbnail = "opcLarge.png;1;cdeLargeIcon", TileWidth = 3, TileHeight = 4, ClassName = "cdeLiveTile cdeLargeTile" } });

            var tFlds = TheNMIEngine.AddStandardForm(MyBaseThing, "OPC General Settings", "OPCD", 0, new nmiStandardForm { MaxTileWidth = 12, UseMargin = true, Category=TheNMIEngine.GetNodeForCategory() });
            TheFormInfo tMyForm = tFlds["Form"] as TheFormInfo;
            TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.CollapsibleGroup, 5, 2, 0xc0, "GLS Discovery Service", null, ThePropertyBag.Create(new nmiCtrlCollapsibleGroup() { DoClose = false, IsSmall = true, TileWidth = 6, ParentFld = 1 }));
            TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.SingleEnded, 10, 2, 0, "GLS Address", "Address", new nmiCtrlSingleEnded() { ParentFld = 5 });
            TheFieldInfo tBut = TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.TileButton, 11, 2, 0, "Scan for Servers", null, new nmiCtrlTileButton() { ParentFld = 5, ClassName = "cdeGoodActionButton" });
            tBut.RegisterUXEvent(MyBaseThing, eUXEvents.OnClick, "SCAN", (pThing, pObj) =>
            {
                TheProcessMessage pMsg = pObj as TheProcessMessage;
                if (pMsg != null)
                {
                    TheCommCore.PublishToOriginator(pMsg.Message, new TSM(eEngineName.NMIService, "NMI_TOAST", "Scanning..."));
                    GetEndpoints();
                    mMyDashboard.Reload(pMsg, false);
                }
            });

            TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.CollapsibleGroup, 15, 2, 0xc0, "Tracing (all clients)...", null, ThePropertyBag.Create(new nmiCtrlCollapsibleGroup() { DoClose = false, TileWidth = 6, IsSmall = true, ParentFld = 1 }));

            TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.SingleCheck, 16, 2, 0, "OPC Client File Tracing", nameof(EnableTracing), new nmiCtrlSingleCheck { TileWidth = 3, ParentFld = 15 });
            TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.SingleCheck, 17, 2, 0, "OPC Client Trace to Log", nameof(EnableTracingToLog), new nmiCtrlSingleCheck { TileWidth = 3, ParentFld = 15 });
            TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.Number, 18, 2, 0, "OPC Client Trace Mask", nameof(OPCTraceMask), new nmiCtrlNumber { TileWidth = 6, ParentFld = 15, HelpText = "1=Err,2=Info,4=Stk,8=Svc,16=SvcDtl,32=Op,64=OpDtl,128=Start,256=Ext,512=Sec" });





            TheFormInfo tAllOPCUASrvs = new TheFormInfo(MyBaseEngine) { cdeMID = TheThing.GetSafeThingGuid(MyBaseThing, "OPCS"), defDataSource = string.Format("TheThing;:;0;:;True;:;DeviceType={1};EngineName={0}", MyBaseEngine.GetEngineName(), eOPCDeviceTypes.OPCRemoteServer), FormTitle = "Discovered OPC-UA Servers", AddButtonText = "Add a Server" };
            TheNMIEngine.AddFormToThingUX(MyBaseThing, tAllOPCUASrvs, "CMyTable", "All OPC-UA Remote Servers", 1, 0x0F, 0xC0, TheNMIEngine.GetNodeForCategory(), null, new ThePropertyBag() { "Thumbnail=OPCLogo.png;1.0" });
            var tOpcs = TheNMIEngine.AddCommonTableColumns(MyBaseThing, tAllOPCUASrvs);
            tOpcs["Address"].Header = "Server URL";
            tOpcs["Address"].PropertyBag = new nmiCtrlSingleEnded { TileWidth = 4, FldWidth = 4 };

            TheNMIEngine.AddField(tAllOPCUASrvs, new TheFieldInfo() { FldOrder = 5, cdeA = 0xC0, Flags = 2, Type = eFieldType.SingleCheck, Header = "Auto-Connect", DataItem = "MyPropertyBag.AutoConnect.Value" });
            TheNMIEngine.AddField(tAllOPCUASrvs, new TheFieldInfo() { FldOrder = 6, cdeA = 0xC0, Flags = 2, Type = eFieldType.SingleCheck, Header = "Connected", DataItem = "MyPropertyBag.IsConnected.Value", PropertyBag = new nmiCtrlSingleCheck { AreYouSure = "Are you sure you want to connect/disconnect?" } });
            //            TheNMIEngine.AddField(tAllOPCUASrvs, new TheFieldInfo() { FldOrder = 13, Flags = 2, cdeA = 0xFF, Type = eFieldType.SingleEnded, Header = "Friendly Name", DataItem = "MyPropertyBag.FriendlyName.Value", PropertyBag = new ThePropertyBag() { "FldWidth=3" } });
            //TheNMIEngine.AddField(tAllOPCUASrvs, new TheFieldInfo() { FldOrder = 14, Flags = 2, cdeA = 0xC0, Type = eFieldType.SingleEnded, Header = "Server URL", DataItem = "MyPropertyBag.Address.Value", PropertyBag = new ThePropertyBag() { "FldWidth=5" } });
            TheNMIEngine.AddField(tAllOPCUASrvs, new TheFieldInfo() { FldOrder = 50, cdeA = 0xFF, Type = eFieldType.DateTime, Header = "Last Update", DataItem = "MyPropertyBag.LastUpdate.Value", PropertyBag = new ThePropertyBag() { "FldWidth=3" } });
            TheNMIEngine.AddField(tAllOPCUASrvs, new TheFieldInfo() { FldOrder = 55, cdeA = 0xFF, Type = eFieldType.DateTime, Header = "Last Receive", DataItem = "MyPropertyBag.LastDataReceivedTime.Value", PropertyBag = new ThePropertyBag() { "FldWidth=3" } });
            //TheNMIEngine.AddTableButtons(tAllOPCUASrvs);

            TheThingRegistry.UpdateEngineUX(MyBaseEngine.GetEngineName());

            TheNMIEngine.AddLiveTagTable(MyBaseThing, eOPCDeviceTypes.OPCLiveTag, "UA Live Tags", TheNMIEngine.GetNodeForCategory());
            TheNMIEngine.AddLiveTagTable(MyBaseThing, eOPCDeviceTypes.OPCMethod, "UA Methods", TheNMIEngine.GetNodeForCategory());

            TheNMIEngine.AddTileBreak(MyBaseThing, mMyDashboard, "..A");

            TheNMIEngine.AddAboutButton(MyBaseThing, true, "REFRESH_DASH", 0xc0);
            TheNMIEngine.RegisterEngine(MyBaseEngine);

            CreateOPCWizard();

            mIsUXInitialized = true;
            return true;
        }

        public void CreateOPCWizard()
        {
            TheFieldInfo tTargetButton = null;
            var flds = TheNMIEngine.AddNewWizard<TheOPCSetClass>(new Guid("{56565656-6AD1-45AE-BE61-96AF02329614}"), Guid.Empty, TheNMIEngine.GetEngineDashBoardByThing(MyBaseThing).cdeMID, "Welcome to the OPC Wizard",
                new nmiCtrlWizard { PanelTitle = "<i class='fa faIcon fa-3x'>&#xf0d0;</i></br>New OPC Client", SideBarTitle = "New OPC Client Wizard", SideBarIconFA = "&#xf545;", TileThumbnail="FA5:f545" },
                (myClass, pClientInfo) =>
                {
                    myClass.cdeMID = Guid.Empty;
                    TheThing tMemTag = null;
                    if (myClass.CreateMemoryTag)
                    {
                        var tReq = new TheThingRegistry.MsgCreateThingRequestV1()
                        {
                            EngineName = "CDMyVThings.TheVThings",
                            DeviceType = "Memory Tag",
                            FriendlyName = myClass.ClientName,
                            OwnerAddress = MyBaseThing,
                            InstanceId = Guid.NewGuid().ToString(),
                            CreateIfNotExist = true
                        };
                        tMemTag = TheThingRegistry.CreateOwnedThingAsync(tReq).Result;
                    }

                    var tOPCReq = new TheThingRegistry.MsgCreateThingRequestV1()
                    {
                        EngineName = "CDMyOPCUAClient.cdeOPCUaClient",
                        DeviceType = "OPC-UA Remote Server",
                        FriendlyName = myClass.ClientName,
                        Address = myClass.OPCAddress,
                        OwnerAddress = MyBaseThing,
                        InstanceId = Guid.NewGuid().ToString(),
                        CreateIfNotExist = true
                    };
                    tOPCReq.Properties = new Dictionary<string, object>();
                    //tOPCReq.Properties["ID"] = Guid.NewGuid().ToString();
                    tOPCReq.Properties["AutoConnect"] = myClass.AutoConnect;
                    tOPCReq.Properties["SendOpcDataType"] = true;
                    if (!myClass.DisableSecurity)
                    {
                        tOPCReq.Properties["DisableSecurity"] = true;
                        tOPCReq.Properties["AcceptUntrustedCertificate"] = true;
                        tOPCReq.Properties["DisableDomainCheck"] = true;
                        tOPCReq.Properties["AcceptInvalidCertificate"] = true;
                        tOPCReq.Properties["Anonymous"] = true;
                    }
                    if (tMemTag != null)
                        tOPCReq.Properties["TagHostThingForSubscribeAll"] = tMemTag.cdeMID;
                    var tOPCServer = TheThingRegistry.CreateOwnedThingAsync(tOPCReq).Result;
                    try
                    {
                        if (tOPCServer != null && myClass.Prop2Tag && myClass.AutoConnect)
                        {
                            var response = TheCommRequestResponse.PublishRequestJSonAsync<MsgOPCUAConnect, MsgOPCUAConnectResponse>(MyBaseThing, tOPCServer, new MsgOPCUAConnect { LogEssentialOnly = true, WaitUntilConnected = true }).Result;
                            if (response != null && string.IsNullOrEmpty(response.Error))
                            {
                                var tBrowseResponse = TheCommRequestResponse.PublishRequestJSonAsync<MsgOPCUABrowse, MsgOPCUABrowseResponse>(MyBaseThing, tOPCServer, new MsgOPCUABrowse()).Result;
                                if (string.IsNullOrEmpty(tBrowseResponse.Error))
                                {
                                    TheCommCore.PublishToNode(pClientInfo.NodeID, new TSM(eEngineName.NMIService, "NMI_TOAST", $"OPC UA Client browse error: {tBrowseResponse.Error}"));
                                    return;
                                }
                                else
                                {
                                    List<MsgOPCUACreateTags.TagInfo> tTagList = tBrowseResponse.Tags;
                                    if (tTagList != null && tTagList.Count > 0)
                                    {
                                        var tres = TheCommRequestResponse.PublishRequestJSonAsync<MsgOPCUACreateTags, MsgOPCUACreateTagsResponse>(MyBaseThing, tOPCServer, new MsgOPCUACreateTags { Tags = tTagList, BulkApply = true }).Result;
                                        if (tres != null && string.IsNullOrEmpty(tres.Error))
                                            TheCommCore.PublishToNode(pClientInfo.NodeID, new TSM(eEngineName.NMIService, "NMI_TOAST", "OPC UA Client Created and memory tag ready"));
                                    }
                                }
                            }
                        }
                        TheCommCore.PublishToNode(pClientInfo.NodeID, new TSM(eEngineName.NMIService, "NMI_TOAST", "OPC UA Client Created and ready"));
                    }
                    catch (Exception)
                    {
                        TheCommCore.PublishToNode(pClientInfo.NodeID, new TSM(eEngineName.NMIService, "NMI_TOAST", "Something went wrong! Check the OPC and Memory Tag settings"));
                    }
                    tTargetButton.SetUXProperty(pClientInfo.NodeID, $"OnClick=TTS:{tOPCServer.GetBaseThing().cdeMID}");
                    TheCommonUtils.SleepOneEye(2000, 100);
                });

            var tMyForm2 = flds["Form"] as TheFormInfo;

            var tFlds = TheNMIEngine.AddNewWizardPage(MyBaseThing, tMyForm2, 0, 1, 2, null /*"Name and Address"*/);

            TheNMIEngine.AddWizardControl(MyBaseThing, tMyForm2, eFieldType.SingleEnded, 1, 1, 2, 0, "Connection Name", "ClientName", new TheNMIBaseControl { Explainer = "1. Enter name for the new OPC connection.", });
            TheNMIEngine.AddWizardControl(MyBaseThing, tMyForm2, eFieldType.SingleEnded, 1, 2, 2, 0, "OPC Server Address", "OPCAddress", new TheNMIBaseControl { Explainer = "1. Enter address of the OPC server.", });

            tFlds = TheNMIEngine.AddNewWizardPage(MyBaseThing, tMyForm2, 1, 2, 3, null /* "Settings"*/);
            TheNMIEngine.AddWizardControl(MyBaseThing, tMyForm2, eFieldType.SingleCheck, 2, 1, 2, 0, "OPC Server requires Security", "DisableSecurity", new nmiCtrlSingleCheck { Explainer = "Check if the OPC Server requires security" });
            TheNMIEngine.AddWizardControl(MyBaseThing, tMyForm2, eFieldType.SingleCheck, 2, 2, 2, 0, "Create a Memory Tag", "CreateMemoryTag", new nmiCtrlSingleCheck { TileWidth = 3, Explainer = "Check to create a Memory Tag and check to subscribes all Tags into it" });
            TheNMIEngine.AddWizardControl(MyBaseThing, tMyForm2, eFieldType.SingleCheck, 2, 3, 2, 0, "All Tags in Memory Tag", "Prop2Tag", new nmiCtrlSingleCheck { TileWidth = 3 });
            TheNMIEngine.AddWizardControl(MyBaseThing, tMyForm2, eFieldType.SingleCheck, 2, 4, 2, 0, "Auto Connect to Server", "AutoConnect", new nmiCtrlSingleCheck { TileWidth = 3, Explainer = "Don't select this if your server requires security settings" });

            tFlds = TheNMIEngine.AddNewWizardPage(MyBaseThing, tMyForm2, 2, 3, 0, null  /*"Final Settings"*/);
            TheNMIEngine.AddWizardControl(MyBaseThing, tMyForm2, eFieldType.SmartLabel, 3, 2, 0, 0, null, null, new nmiCtrlSmartLabel { Text = "Once you click finish, the Wizard will create the items you requested. It will notify you with a toast when its done", TileHeight = 5, TileWidth=7, NoTE = true });
            //HELP SECTION final step help section

            TheNMIEngine.AddWizardProcessPage(MyBaseThing, tMyForm2, 4);
            TheNMIEngine.AddWizardControl(MyBaseThing, tMyForm2, eFieldType.SmartLabel, 4, 1, 0, 0, null, null, new nmiCtrlSmartLabel { NoTE = true, TileWidth = 7, Text = "Creating the new instance..please wait", TileHeight = 2 });

            TheNMIEngine.AddWizardFinishPage(MyBaseThing, tMyForm2, 5);
            TheNMIEngine.AddWizardControl(MyBaseThing, tMyForm2, eFieldType.SmartLabel, 5, 1, 0, 0, null, null, new nmiCtrlSmartLabel { NoTE = true, TileWidth = 7, Text = "Done...what do you want to do next?", TileHeight = 2 });

            TheNMIEngine.AddWizardControl(MyBaseThing, tMyForm2, eFieldType.TileGroup, 5, 2, 0, 0, null, null, new nmiCtrlTileGroup { TileWidth = 1, TileHeight = 2, TileFactorX = 2 });
            TheNMIEngine.AddWizardControl(MyBaseThing, tMyForm2, eFieldType.TileButton, 5, 3, 2, 0, "Go to Dashboard", null, new nmiCtrlTileButton { NoTE = true, TileHeight = 2, TileWidth = 3, OnClick = $"TTS:{mMyDashboard.cdeMID}", ClassName = "cdeTransitButton" });
            TheNMIEngine.AddWizardControl(MyBaseThing, tMyForm2, eFieldType.TileGroup, 5, 4, 0, 0, null, null, new nmiCtrlTileGroup { TileWidth = 1, TileHeight = 2 });
            tTargetButton = TheNMIEngine.AddWizardControl(MyBaseThing, tMyForm2, eFieldType.TileButton, 5, 5, 2, 0, "Go to New OPC Client", null, new nmiCtrlTileButton { NoTE = true, TileHeight = 2, TileWidth = 3, ClassName = "cdeTransitButton" });
        }

        public class TheOPCSetClass : TheDataBase
        {
            public string ClientName { get; set; }
            public string OPCAddress { get; set; }
            public bool CreateMemoryTag { get; set; }
            public bool DisableSecurity { get; set; }
            public bool AutoConnect { get; set; }
            public bool Prop2Tag { get; set; }
        }
    }
}
