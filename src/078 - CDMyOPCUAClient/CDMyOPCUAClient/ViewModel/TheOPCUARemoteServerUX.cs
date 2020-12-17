// SPDX-FileCopyrightText: 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0
using nsCDEngine.BaseClasses;
using nsCDEngine.Communication;
using nsCDEngine.Engines.NMIService;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CDMyOPCUAClient.ViewModel
{
    public partial class TheOPCUARemoteServer
    {
        public bool UseTree=false;

        public override bool CreateUX()
        {
            if (mIsUXInitCalled) return false;
            mIsUXInitCalled = true;

            bool readOnly = TheThing.GetSafePropertyBool(MyBaseThing, TheThingRegistry.eOwnerProperties.cdeReadOnly);
            int writeEnableFlag;
            if (!readOnly)
            {
                writeEnableFlag = 2;
            }
            else
            {
                writeEnableFlag = 0;
            }

            //NUI Definition for All clients


            TheThing.SetSafePropertyString(MyBaseThing, "StateSensorUnit", "Live Tags");
            TheThing.SetSafePropertyString(MyBaseThing, "StateSensorIcon", "/Images/OPCLogo.png");
            var tFlds = TheNMIEngine.AddStandardForm(MyBaseThing, "FACEPLATE", null, 0, new nmiStandardForm { MaxTileWidth = 24, UseMargin = true });
            var tMyForm = tFlds["Form"] as TheFormInfo;

            var tSBlock = TheNMIEngine.AddStatusBlock(MyBaseThing, tMyForm, 10);
            tSBlock["Group"].SetParent(1);

            TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.Number, 20, 0, 0x0, "Live Tags:", "LiveTagCnt", new nmiCtrlNumber() { ParentFld = tSBlock["Group"].FldOrder });
            //TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.Number, 30, 0, 0x0, "Browsed Tags:", "BrowsedTagCnt", new nmiCtrlNumber() { ParentFld = tSBlock["Group"].FldOrder });

            var tCBlock = TheNMIEngine.AddConnectivityBlock(MyBaseThing, tMyForm, 100, (m, c) =>
            {
                if (c)
                {
                    Connect(false);
                }
                else
                {
                    Disconnect(false, false, "Disconnect due to user/NMI request");
                }
            });
            tCBlock["Group"].SetParent(1);

            CreateConfigurationSection(writeEnableFlag, tMyForm, 500,1);

            //TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.TextArea, 13, 0, 0, "Last-Msg", "LastMessage", new ThePropertyBag() { "TileHeight=2", "TileWidth=6" });

#if USE_CSVIMPORT
            TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.CollapsibleGroup, 400, 2, 0xc0, "Tag import...", null, ThePropertyBag.Create(new nmiCtrlCollapsibleGroup() { ParentFld = 1, DoClose = true, IsSmall = true, TileWidth = 6 }));
            TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.DropUploader, 410, 2, 0xC0, "Drop a CSV or JSON file with tags", null, new nmiCtrlDropUploader { NoTE = true, TileHeight = 4, TileWidth = 3, ParentFld = 400, EngineName = MyBaseEngine.GetEngineName(), MaxFileSize = 52428800 });
            TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.SingleCheck, 420, 2, 0xC0, "Replace existing tags", nameof(ReplaceAllTagsOnUpload), new nmiCtrlSingleCheck { TileHeight = 1, TileWidth = 3, ParentFld = 400 });
            TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.SingleCheck, 425, 2, 0xC0, "Bulk apply tags", nameof(BulkApplyOnUpload), new nmiCtrlSingleCheck { TileHeight = 1, TileWidth = 3, ParentFld = 400 });
            TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.TileButton, 430, writeEnableFlag, 0xF0, "Export Tags", null, new nmiCtrlTileButton() { ClassName = "cdeGoodActionButton", TileWidth = 3, TileHeight = 1, ParentFld = 400, NoTE = true })
                .RegisterUXEvent(MyBaseThing, eUXEvents.OnClick, "EXPORTTAGS", ExportTags);
            TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.SingleCheck, 435, 2, 0xC0, "Export As CSV", nameof(ExportAsCSV), new nmiCtrlSingleCheck { TileHeight = 1, TileWidth = 3, ParentFld = 400 });
#endif

            TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.CollapsibleGroup, 615, 2, 0xC0, "Security...", null, ThePropertyBag.Create(new nmiCtrlCollapsibleGroup() { ParentFld = 100, UseMargin=false, DoClose = true, IsSmall = true, TileWidth = 6 }));
            TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.SingleEnded, 620, writeEnableFlag, 0xC0, "User Name", "UserName", new nmiCtrlSingleEnded { ParentFld = 615, TileWidth = 6, TileHeight = 1 });
            TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.Password, 630, writeEnableFlag | 1, 0xC0, "Password", "Password", new nmiCtrlPassword() { ParentFld = 615, HideMTL = true });
            TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.SingleCheck, 640, writeEnableFlag, 0xC0, "Disable Security", "DisableSecurity", new nmiCtrlSingleCheck() { ParentFld = 615, TileWidth = 3 });
            TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.SingleCheck, 650, writeEnableFlag, 0xC0, "Accept Untrusted Cert", "AcceptUntrustedCertificate", new nmiCtrlSingleCheck { ParentFld = 615, TileWidth = 3, TileHeight = 1 });
            TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.SingleCheck, 660, writeEnableFlag, 0xC0, "Disable Domain Check", "DisableDomainCheck", new ThePropertyBag() { "ParentFld=615", "TileWidth=3", "TileHeight=1" });
            TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.SingleCheck, 670, writeEnableFlag, 0xC0, "Accept Invalid Cert", "AcceptInvalidCertificate", new ThePropertyBag() { "ParentFld=615", "TileWidth=3", "TileHeight=1" });
            TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.SingleCheck, 680, writeEnableFlag, 0xC0, "Anonymous", "Anonymous", new ThePropertyBag() { "ParentFld=615", "TileWidth=3", "TileHeight=1" });
            TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.SingleEnded, 690, writeEnableFlag, 0xC0, "Client Cert Subject Name", "AppCertSubjectName", new ThePropertyBag() { "ParentFld=615", "TileWidth=6", "TileHeight=1" });


#if USE_ADVANCED
            TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.CollapsibleGroup, 600, 2, 0xC0, "Advanced Configurations...", null, ThePropertyBag.Create(new nmiCtrlCollapsibleGroup() { ParentFld = 1, DoClose = true, IsSmall = true/*, MaxTileWidth=18*/ }));

            TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.CollapsibleGroup, 695, 2, 0xC0, "Reconnects and Timeouts...", null, ThePropertyBag.Create(new nmiCtrlCollapsibleGroup() { ParentFld = 600, DoClose = true, IsSmall = true, TileWidth = 6 }));
            TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.Number, 700, writeEnableFlag, 0xC0, "Reconnect Delay", "ReconnectPeriod", new ThePropertyBag() { "ParentFld=695", "TileWidth=6", "TileHeight=1" });
            TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.Number, 710, writeEnableFlag, 0xC0, "Keep-Alive Interval", nameof(KeepAliveInterval), new ThePropertyBag() { "ParentFld=695", "TileWidth=6", "TileHeight=1" });
            TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.Number, 715, writeEnableFlag, 0xC0, "Keep-Alive Timeout", nameof(KeepAliveTimeout), new ThePropertyBag() { "ParentFld=695", "TileWidth=6", "TileHeight=1" });
            TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.Number, 720, writeEnableFlag, 0xC0, "Publishing Interval", nameof(PublishingInterval), new ThePropertyBag() { "ParentFld=695", "TileWidth=6", "TileHeight=1" });
            TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.Number, 730, writeEnableFlag, 0xC0, "Operation Timeout", "OperationTimeout", new ThePropertyBag() { "ParentFld=695", "TileWidth=6", "TileHeight=1" });
            TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.Number, 740, writeEnableFlag, 0xC0, "Session Timeout", "SessionTimeout", new ThePropertyBag() { "ParentFld=695", "TileWidth=6", "TileHeight=1" });

            TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.CollapsibleGroup, 745, 2, 0xC0, "Status info and data formats...", null, ThePropertyBag.Create(new nmiCtrlCollapsibleGroup() { ParentFld = 600, DoClose = true, IsSmall = true, TileWidth = 6 }));
            TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.SingleCheck, 750, writeEnableFlag, 0xC0, "Send Status Code", nameof(SendStatusCode), new ThePropertyBag() { "ParentFld=745", "TileWidth=3", "TileHeight=1" });
            TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.SingleCheck, 755, writeEnableFlag, 0xC0, "Use Local Timestamp", nameof(UseLocalTimestamp), new ThePropertyBag() { "ParentFld=745", "TileWidth=3", "TileHeight=1" });
            TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.SingleCheck, 760, writeEnableFlag, 0xC0, "Send Server Timestamp", nameof(SendServerTimestamp), new ThePropertyBag() { "ParentFld=745", "TileWidth=3", "TileHeight=1" });
            TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.SingleCheck, 770, writeEnableFlag, 0xC0, "Send Pico Seconds", nameof(SendPicoSeconds), new ThePropertyBag() { "ParentFld=745", "TileWidth=3", "TileHeight=1" });
            TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.SingleCheck, 772, writeEnableFlag, 0xC0, "Send OPC Data Type", nameof(SendOpcDataType), new ThePropertyBag() { "ParentFld=745", "TileWidth=3", "TileHeight=1" });
            TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.SingleCheck, 775, writeEnableFlag, 0xC0, "Send Sequence Number", nameof(SendSequenceNumber), new ThePropertyBag() { "ParentFld=745", "TileWidth=3", "TileHeight=1" });
            TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.SingleCheck, 778, writeEnableFlag, 0xC0, "Use OPC Sequence Number for Thing sequence", nameof(UseSequenceNumberForThingSequence), new ThePropertyBag() { "ParentFld=745", "TileWidth=3", "TileHeight=1" });
            TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.SingleCheck, 780, writeEnableFlag, 0xC0, "Do not use sub-properties ", nameof(DoNotUsePropsOfProps), new ThePropertyBag() { "ParentFld=745", "TileWidth=3", "TileHeight=1" });
            TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.SingleCheck, 790, writeEnableFlag, 0xC0, "Do not write array properties", nameof(DoNotWriteArrayElementsAsProperties), new ThePropertyBag() { "ParentFld=745", "TileWidth=3", "TileHeight=1" });

            TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.CollapsibleGroup, 795, 2, 0xC0, "Browsing and logging...", null, ThePropertyBag.Create(new nmiCtrlCollapsibleGroup() { ParentFld = 600, DoClose = true, IsSmall = true, TileWidth = 6 }));
            TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.SingleCheck, 800, writeEnableFlag, 0xC0, "Do Not Use Parent Path", nameof(DoNotUseParentPath), new ThePropertyBag() { "ParentFld=795", "TileWidth=3", "TileHeight=1" });
            TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.SingleCheck, 810, writeEnableFlag, 0xC0, "Show Tags in Properties", nameof(ShowTagsInProperties), new ThePropertyBag() { "ParentFld=795", "TileWidth=3", "TileHeight=1" });
            TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.SingleCheck, 820, writeEnableFlag, 0xC0, "Enable Data Logging", nameof(EnableOPCDataLogging), new ThePropertyBag() { "ParentFld=795", "TileWidth=3", "TileHeight=1" });
#endif
            mIsUXInitialized = true;
            return true;
        }

        TheFieldInfo OpcTagTree = null;
        private void CreateConfigurationSection(int writeEnableFlag, TheFormInfo tMyForm, int pStartFld, int pParentFld)
        {
            UseTree = TheCommonUtils.CBool(TheBaseAssets.MySettings.GetSetting("UseTreeView"));

            TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.CollapsibleGroup, pStartFld, 2, 0xc0, "Tag Management...", null, ThePropertyBag.Create(new nmiCtrlCollapsibleGroup() { DoClose = !UseTree, IsSmall = true, ParentFld = pParentFld, TileWidth = UseTree ? 18 : 6 }));

            if (!UseTree)
                TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.SingleEnded, pStartFld + 1, writeEnableFlag, 0xC0, "Browse Branch", "BrowseBranch", new nmiCtrlSingleEnded() { ParentFld = pStartFld });
#if USE_WEBIX
            else
            {
                var stringCols = TheCommonUtils.SerializeObjectToJSONString(new List<TheWXColumn> {
                        { new TheWXColumn() { TileWidth = 6, Header = "Node Name", ID = "DisplayName", FilterType = "textFilter", Template="{common.treetable()}{common.treecheckbox()}&nbsp;<strong>#DisplayName#</strong>" } },
                        //{ new TheWXColumn() { TileWidth = 2, Header = "Host Property", ID = "HostPropertyNameOverride", SortType = "string", Flags=2 } },
                        { new TheWXColumn() { TileWidth = 1, Header = "Sample-Rate", ID = "SampleRate", SortType = "int", Flags=2 } },
                        { new TheWXColumn() { TileWidth = 1, Header = "Deadband-Filter", ID = "DeadbandFilterValue", SortType = "int", Flags=2 } },
                        { new TheWXColumn() { TileWidth = 1, Header = "Trigger", ID = "ChangeTrigger", SortType = "int", Flags=2 } },
                        { new TheWXColumn() { TileWidth = 6, Header = "Node ID", ID = "NodeIdName",FilterType = "textFilter",  SortType = "int" } }
                });

                OpcTagTree = TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.UserControl, pStartFld + 6, 2, 0, "Sample Tree", "SelectedIs", new nmiCtrlWXTreeTable()
                {
                    ParentFld = pStartFld,
                    TileWidth = 12,
                    TileHeight = 12,
                    NoTE = true,
                    RootNode = "Objects",
                    TreeNode = "Parent",
                    NameNode = "DisplayName",
                    SelectNode="HasActiveHostThing",
                    LeftSplit = 1,
                    OpenAllBranches = false,
                    SplitCharacter = ".",
                    Columns = stringCols
                });
                OpcTagTree.RegisterEvent2("NMI_FIELD_EVENT", (pMSG, para) => {
                    var tUpdate = TheCommonUtils.DeserializeJSONStringToObject<TheWXFieldEvent>(pMSG.Message.PLS);
                    if (tUpdate!=null)
                    {
                        var MyTag = MyTags.MyMirrorCache.GetEntryByID(tUpdate.cdeMID);
                        if (MyTag!=null)
                        {
                            switch (tUpdate.Name)
                            {
                                case "SampleRate":
                                    MyTag.SampleRate = TheCommonUtils.CInt(tUpdate.Value);
                                    break;
                                case "DeadbandFilterValue":
                                    MyTag.DeadbandFilterValue = TheCommonUtils.CDbl(tUpdate.Value);
                                    break;
                                case "ChangeTrigger":
                                    MyTag.ChangeTrigger = TheCommonUtils.CInt(tUpdate.Value);
                                    break;
                            }
                        }
                    }
                });
            }
#endif

            // BROWSE Button
            TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.TileButton, pStartFld + 2, writeEnableFlag, 0xC0, "Browse", null, new nmiCtrlTileButton() { ParentFld = pStartFld, ClassName = "cdeGoodActionButton", TileWidth = 3, NoTE = true })
                .RegisterUXEvent(MyBaseThing, eUXEvents.OnClick, "BROWSE", (pThing, pObj) =>
                {
                    TheProcessMessage pMsg = pObj as TheProcessMessage;
                    if (pMsg == null || pMsg.Message == null) return;
                    if (ConnectionState != ConnectionStateEnum.Connected)
                    {
                        TheCommCore.PublishToOriginator(pMsg.Message, new TSM(eEngineName.NMIService, "NMI_TOAST", "Server not connected - please connect first"));
                    }
                    else
                    {
                        TheCommCore.PublishToOriginator(pMsg.Message, new TSM(eEngineName.NMIService, "NMI_TOAST", "Browsing..."));
                        LastMessage = "Browsing Started at " + DateTimeOffset.Now.ToString();
                        BrowsedTagCnt = 0;
                        Browser(currentRoot, currentRoot.ToString(), true, false, null);


                        var dataModelJson = TheCommonUtils.SerializeObjectToJSONString(
                            MyTags.MyMirrorCache.TheValues
                            .Where(t => t.HostPropertyNameOverride?.Contains("].[") != true) // Filter out any DataValue sub-properties (i.e. engineering units), as the tree view doesn't seem to handle nodes under leaf-nodes (or inner nodes with data) yet
                            .ToList());
                        OpcTagTree?.SetUXProperty(pMsg.Message.GetOriginator(), $"DataModel={dataModelJson}", true);
                        LastMessage += " - Browsing done at " + DateTimeOffset.Now.ToString();
                        TheCommCore.PublishToOriginator(pMsg.Message, new TSM(eEngineName.NMIService, "NMI_TOAST", LastMessage));
                    }
                });
            TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.Number, pStartFld + 3, 0, 0x0, "Browsed Tags:", "BrowsedTagCnt", new nmiCtrlNumber() { ParentFld = pStartFld, TileWidth=3 });


            ///Browsed TAGS Form
            {
                var tDataSource = "TheOPCTags";
                if (MyTags != null)
                    tDataSource = MyTags.StoreMID.ToString();
                var tOPCTagForm = new TheFormInfo(TheThing.GetSafeThingGuid(MyBaseThing, "TAGLIST_ID"), eEngineName.NMIService, "OPC-UA Server Tags", $"{tDataSource};:;1000") { IsNotAutoLoading = true, AddButtonText = "Add new Tag", PropertyBag= new nmiCtrlTableView { ShowFilterField = true } };
                TheNMIEngine.AddFormToThingUX(MyBaseThing, tOPCTagForm, "CMyTable", "Tag List", 1, 3, 0xF0, null, null, new ThePropertyBag() { "Visibility=false" });
                TheNMIEngine.AddFields(tOPCTagForm, new List<TheFieldInfo> {
                    // 1: Thing subscription
                    {  new TheFieldInfo() { FldOrder=3,DataItem="IsSubscribedAsThing",Flags=0,Type=eFieldType.SingleCheck,Header="Has Tag Thing",FldWidth=1 }},
                    {  new TheFieldInfo() { FldOrder=11,DataItem="Parent",Flags=0,Type=eFieldType.SingleEnded,Header="Parent",FldWidth=4 }},
                    {  new TheFieldInfo() { FldOrder=12,DataItem="DisplayName",Flags=0,Type=eFieldType.SingleEnded,Header="Name",FldWidth=3 }},
                    {  new TheFieldInfo() { FldOrder=13,DataItem=nameof(TheOPCTag.HostPropertyNameOverride),Flags=writeEnableFlag,Type=eFieldType.SingleEnded,Header="Host Property Name",FldWidth=3 }},
                    {  new TheFieldInfo() { FldOrder=14,DataItem="HostThingMID",Flags=writeEnableFlag,Type=eFieldType.ThingPicker,Header="Host Thing",FldWidth=3 }},
                    {  new TheFieldInfo() { FldOrder=15,DataItem=nameof(TheOPCTag.ChangeTrigger),Flags=writeEnableFlag,Type=eFieldType.ComboBox,Header="Change Trigger",FldWidth=1, PropertyBag=new nmiCtrlComboBox{ Options="Status:0;Value:1;Value & Timestamp:2" } }},
                    {  new TheFieldInfo() { FldOrder=16,DataItem=nameof(TheOPCTag.SampleRate),Flags=writeEnableFlag,Type=eFieldType.Number,Header="Sample Rate (ms)",FldWidth=1 }},
                    {  new TheFieldInfo() { FldOrder=17,DataItem=nameof(TheOPCTag.DeadbandFilterValue),Flags=writeEnableFlag,Type=eFieldType.Number,Header="Deadband",FldWidth=1 }},
                    {  new TheFieldInfo() { FldOrder=18,DataItem="NodeIdName",Flags=2,Type=eFieldType.SingleEnded,Header="NodeId",FldWidth=6 }},
                    // 30: Property subscription
                    {  new TheFieldInfo() { FldOrder=19,DataItem=nameof(TheOPCTag.HistoryStartTime),Flags=writeEnableFlag,Type=eFieldType.SingleEnded,Header="History Start",FldWidth=3 }},
                    //{  new TheFieldInfo() { FldOrder=13,DataItem="PropAttr.7.Value.Value",Flags=0,Type=eFieldType.SingleEnded,Header="Value",FldWidth=5 }},
                    //{  new TheFieldInfo() { FldOrder=14,DataItem="PropAttr.7.DataType",Flags=0,Type=eFieldType.SingleEnded,Header="Type",FldWidth=5 }},
                });
                TheNMIEngine.AddTableButtons(tOPCTagForm, false, 100, 0);

                // Button Subscribe as Thing
                TheNMIEngine.AddSmartControl(MyBaseThing, tOPCTagForm, eFieldType.TileButton, 1, writeEnableFlag, 0xC0, "Create Tag Thing", null, new nmiCtrlTileButton() { ClassName = "cdeGoodActionButton", TileHeight = 1 })
                    .RegisterUXEvent(MyBaseThing, eUXEvents.OnClick, "SUBSCRIBE", (tThing, pMsg) => SubscribeAsThing(tThing, pMsg, MyTags));

                // Button: Subscribe property into host thing
                TheNMIEngine.AddSmartControl(MyBaseThing, tOPCTagForm, eFieldType.TileButton, 5, writeEnableFlag, 0xC0, "Monitor as Property", null, new nmiCtrlTileButton() { ClassName = "cdeGoodActionButton", TileHeight = 1 })
                    .RegisterUXEvent(MyBaseThing, eUXEvents.OnClick, "SUBSCRIBEPROP", (tThing, pMsg) => SubscribeAsProperty(tThing, pMsg, MyTags));

                TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.TileButton, pStartFld + 4, writeEnableFlag, 0xF0, "Show Tag List", null, new nmiCtrlTileButton() { OnClick = $"TTS:{tOPCTagForm.cdeMID}", ParentFld = pStartFld, ClassName = "cdeTransitButton", TileWidth = 3, NoTE = true });
            }

            TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.Number, pStartFld + 5, writeEnableFlag, 0xC0, "Default Sample Rate", nameof(DefSampleRate), new nmiCtrlNumber() { ParentFld = pStartFld, TileWidth = 3 });


            TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.ThingPicker, pStartFld + 6, writeEnableFlag, 0xC0, "Thing for Property Subs", "TagHostThingForSubscribeAll", new nmiCtrlThingPicker() { NoTE=true, ParentFld = pStartFld, TileWidth=4 });

            // SUBSCRIBE all tags as Properties
            TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.TileButton, pStartFld + 7, writeEnableFlag, 0xC0, $"Subscribe {(UseTree ? "selected" : "all")} in properties", null, new nmiCtrlTileButton() { ParentFld = pStartFld, ClassName = "cdeGoodActionButton", TileWidth = 2, NoTE = true })
                .RegisterUXEvent(MyBaseThing, eUXEvents.OnClick, "ALLTAGS", (pThing, pObj) =>
                {
                TheProcessMessage pMsg = pObj as TheProcessMessage;
                if (pMsg == null || pMsg.Message == null) return;
                if (ConnectionState != ConnectionStateEnum.Connected)
                {
                    TheCommCore.PublishToOriginator(pMsg.Message, new TSM(eEngineName.NMIService, "NMI_TOAST", "Server not connected - please connect first"));
                }
                else
                {
                    var tHostThing = TheThingRegistry.GetThingByMID("*", TheCommonUtils.CGuid(this.TagHostThingForSubscribeAll));
                    if (tHostThing == null)
                    {
                        TheCommCore.PublishToOriginator(pMsg.Message, new TSM(eEngineName.NMIService, "NMI_TOAST", "Host Thing not specified or invalid"));
                        return;
                    }
                    TheCommCore.PublishToOriginator(pMsg.Message, new TSM(eEngineName.NMIService, "NMI_TOAST", "Subscribing..."));

                    if (MyTags.MyMirrorCache.Count == 0 && !UseTree)
                    {
                        // Clear all previous subscription in either MyTags (tag as thing) oder HostThing (tag as property)
                        MyTags.MyMirrorCache.Reset();

                        // TODO Figure out how to clean up subscribed things in properties. 
                        // - Can't just delete as a customer may have hand picked certain properties via browse paths etc.
                        // - Allow customer to use multiple host things?
                        //var tThing = TheThingRegistry.GetThingByMID("*", TheCommonUtils.CGuid(TagHostThing));
                        //if (tThing != null)
                        //{
                        //    foreach (var prop in tThing.GetPropertiesMetaStartingWith("OPCUA:"))
                        //    {
                        //        tThing.RemoveProperty(prop.Name);
                        //    }
                        //}

                        LastMessage = "Subscribing started at " + DateTimeOffset.Now.ToString();

                        BrowsedTagCnt = 0;

                        Browser(currentRoot, currentRoot.ToString(), true, true, tHostThing, CreateIDFilter());

                        LastMessage += " - Subscribing done at " + DateTimeOffset.Now.ToString();
                        TheCommCore.PublishToOriginator(pMsg.Message, new TSM(eEngineName.NMIService, "NMI_TOAST", LastMessage));
                    }
                    else
                    {
                        // Previously browsed: use those tags to do the subscriptions
                        LastMessage = "Subscribing started at " + DateTimeOffset.Now.ToString();

                        var subscription = GetOrCreateSubscription(0);
                        var pFilterIDs = CreateIDFilter();
                        int count = 0;
                            //var results = new List<Opc.Ua.Client.MonitoredItem>();//CM: removed as Nothing is done with "results"

                            foreach (var tag in MyTags.TheValues)
                            {
                                string tagNameForFilter;
                                if (tag.DisplayName == "EngineeringUnits" || tag.DisplayName=="EURange")
                                {
                                    tagNameForFilter = tag.Parent;
                                }
                                else
                                {
                                    tagNameForFilter = $"{tag.Parent}.{tag.DisplayName}";
                                }
                            
                                if (pFilterIDs != null && pFilterIDs.Contains(tagNameForFilter))
                                {
                                    tag.HostThingMID = TheCommonUtils.cdeGuidToString(tHostThing.cdeMID);
                                    var childTags = tag.GetChildTags();
                                    if (childTags?.Any() == true)
                                    {
                                        foreach (var childTag in childTags)
                                        {
                                            childTag.HostThingMID = tag.HostThingMID;
                                        }
                                    }
                                    if (!RegisterAndMonitorTagInHostThing(subscription, tag, out string error, false, true, false) || !string.IsNullOrEmpty(error))
                                    {
                                        // error
                                    }
                                    else
                                    {
                                        count++;
                                        if (count % 5000 == 0)
                                        {
                                            //results.AddRange( //CM: removed as Nothing is done with "results"
                                            subscription.ApplyChanges();
                                        }
                                    }
                                }
                            }
                            //CM: see above results.AddRange(
                            subscription.ApplyChanges();
                            LastMessage += $" - Subscribing of {count} tags done at {TheCommonUtils.GetDateTimeString(DateTimeOffset.Now, pMsg.CurrentUserID)}";
                            TheCommCore.PublishToOriginator(pMsg.Message, new TSM(eEngineName.NMIService, "NMI_TOAST", LastMessage));
                        }
                    }
                });

            // 250: Show Browsed Tags
            // 255: Show Subscribed tags
            // 260: Show Methods
            // 270: Show Events

            // SUBSCRIBE all tags as Things
            TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.TileButton, pStartFld + 8, writeEnableFlag, 0xC0, $"Subscribe {(UseTree ? "selected " : "all")} in Things", null,
                new nmiCtrlTileButton() { AreYouSure = "Are you sure you want to subscribe all OPC Tags as Things? This will create a new OPCTag Thing for each tag and can put a strain on this node", ParentFld = pStartFld, ClassName = "cdeBadActionButton", TileWidth = 6, NoTE = true })
                .RegisterUXEvent(MyBaseThing, eUXEvents.OnClick, "ALLTAGS", (pThing, pObj) =>
                {
                    TheProcessMessage pMsg = pObj as TheProcessMessage;
                    if (pMsg == null || pMsg.Message == null) return;
                    if (ConnectionState != ConnectionStateEnum.Connected)
                    {
                        TheCommCore.PublishToOriginator(pMsg.Message, new TSM(eEngineName.NMIService, "NMI_TOAST", "Server not connected - please connect first"));
                    }
                    else
                    {
                        TheCommCore.PublishToOriginator(pMsg.Message, new TSM(eEngineName.NMIService, "NMI_TOAST", "Subscribing..."));
                        LastMessage = "Subscribing Started at " + DateTimeOffset.Now.ToString();
                        BrowsedTagCnt = 0;
                        Browser(currentRoot, currentRoot.ToString(), true, true, null, CreateIDFilter());
                        LastMessage += " - Subscribing done at " + DateTimeOffset.Now.ToString();
                        TheCommCore.PublishToOriginator(pMsg.Message, new TSM(eEngineName.NMIService, "NMI_TOAST", LastMessage));
                    }
                });


            //Subscribed TAGS Form
            {
                var tDataSource = "TheOPCTagSubs";
                if (MyTagSubscriptions != null)
                    tDataSource = MyTagSubscriptions.StoreMID.ToString();
                var tOPCTagSubscriptionForm = new TheFormInfo(TheThing.GetSafeThingGuid(MyBaseThing, "TAGSUBLIST_ID"), eEngineName.NMIService, "OPC-UA Server Tag Subscriptions", tDataSource) { IsNotAutoLoading = true };
                TheNMIEngine.AddFormToThingUX(MyBaseThing, tOPCTagSubscriptionForm, "CMyTable", "Tag Subscription List", 1, 3, 0xF0, null, null, new ThePropertyBag() { "Visibility=false" });
                TheNMIEngine.AddFields(tOPCTagSubscriptionForm, new List<TheFieldInfo> {
                    {  new TheFieldInfo() { FldOrder=3,DataItem="IsSubscribedAsThing",Flags=0,Type=eFieldType.SingleCheck,Header="Has Tag Thing",FldWidth=1 }},
                    {  new TheFieldInfo() { FldOrder=11,DataItem="Parent",Flags=0,Type=eFieldType.SingleEnded,Header="Parent",FldWidth=4 }},
                    {  new TheFieldInfo() { FldOrder=12,DataItem="DisplayName",Flags=0,Type=eFieldType.SingleEnded,Header="Name",FldWidth=3 }},
                    {  new TheFieldInfo() { FldOrder=13,DataItem=nameof(TheOPCTag.SampleRate),Flags=writeEnableFlag,Type=eFieldType.Number,Header="Sample Rate (ms)",FldWidth=1 }},
                    {  new TheFieldInfo() { FldOrder=14,DataItem=nameof(TheOPCTag.ChangeTrigger),Flags=writeEnableFlag,Type=eFieldType.ComboBox,Header="Change Trigger",FldWidth=1, PropertyBag=new nmiCtrlComboBox{ Options="Status:0;Value:1;Value & Timestamp:2" }  }},
                    {  new TheFieldInfo() { FldOrder=15,DataItem=nameof(TheOPCTag.DeadbandFilterValue),Flags=writeEnableFlag,Type=eFieldType.Number,Header="Deadband",FldWidth=1 }},
                    //  new TheFieldInfo() { FldOrder=17,DataItem=nameof(TheOPCTag.HistoryStartTime),Flags=0,Type=eFieldType.SingleEnded,Header="History Start",FldWidth=10 }},
                    {  new TheFieldInfo() { FldOrder=20,DataItem=nameof(TheOPCTag.HostPropertyNameOverride),Flags=0,Type=eFieldType.SingleEnded,Header="Custom Property Name",FldWidth=3 }},
                    {  new TheFieldInfo() { FldOrder=21,DataItem="HostThingMID",Flags=0,Type=eFieldType.ThingPicker,Header="Host Thing",FldWidth=3 }},
                    {  new TheFieldInfo() { FldOrder=30,DataItem="NodeIdName",Flags=0,Type=eFieldType.SingleEnded,Header="NodeId",FldWidth=6 }},
                    //{  new TheFieldInfo() { FldOrder=13,DataItem="PropAttr.7.Value.Value",Flags=0,Type=eFieldType.SingleEnded,Header="Value",FldWidth=5 }},
                    //{  new TheFieldInfo() { FldOrder=14,DataItem="PropAttr.7.DataType",Flags=0,Type=eFieldType.SingleEnded,Header="Type",FldWidth=5 }},
                });
                var tTransButton = TheNMIEngine.AddSmartControl(MyBaseThing, tOPCTagSubscriptionForm, eFieldType.TileButton, 22, 2, 0, "View Thing", null, new nmiCtrlTileButton { OnClick = "TTS:<%HostThingMID%>", ClassName = "cdeTransitButton", TileWidth = 1, TileHeight = 1, FldWidth = 1 });

                TheNMIEngine.AddTableButtons(tOPCTagSubscriptionForm, false, 100, 2, 0x80);

                TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.TileButton, pStartFld + 9, writeEnableFlag, 0xF0, "Show Subscription List", null, new nmiCtrlTileButton() { OnClick = $"TTS:{tOPCTagSubscriptionForm.cdeMID}", ParentFld = pStartFld, TileWidth=2, ClassName = "cdeTransitButton", NoTE = true });
            }


            ////EVENTS form
            {
                string tEventDataSource = "TheOPCEvents";
                if (MyEvents != null)
                    tEventDataSource = MyEvents.StoreMID.ToString();
                var tOPCEventForm = new TheFormInfo(TheThing.GetSafeThingGuid(MyBaseThing, "EVENTLIST_ID"), eEngineName.NMIService, "OPC-UA Server Events", tEventDataSource) { IsNotAutoLoading = true };
                TheNMIEngine.AddFormToThingUX(MyBaseThing, tOPCEventForm, "CMyTable", "Event List", 1, 3, 0xF0, null, null, new ThePropertyBag() { "Visibility=false" });
                TheNMIEngine.AddFields(tOPCEventForm, new List<TheFieldInfo>
                {
                    {  new TheFieldInfo() { FldOrder=11,DataItem="DisplayName",Flags=0,Type=eFieldType.SingleEnded,Header="Event-Name",FldWidth=3 }},
                    {  new TheFieldInfo() { FldOrder=3,DataItem="IsSubscribedAsProperty",Flags=0,Type=eFieldType.SingleCheck,Header="Subscribed",FldWidth=1 }},
                    {  new TheFieldInfo() { FldOrder=12,DataItem="HostThingMID",Flags=writeEnableFlag, cdeA = 0xC0, Type=eFieldType.ThingPicker,Header="Thing to Update",FldWidth=3 } },
                });
                TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.TileButton, pStartFld + 10, writeEnableFlag, 0xF0, "Show Events", null, new nmiCtrlTileButton() { OnClick = $"TTS:{tOPCEventForm.cdeMID}", ParentFld = pStartFld, ClassName = "cdeTransitButton", TileWidth = 2, NoTE = true });
            }
            ////METHODS Form
            {
                string tDataSource = "TheOPCMethods";
                if (MyMethods != null)
                    tDataSource = MyMethods.StoreMID.ToString();
                var tOPCMethodForm = new TheFormInfo(TheThing.GetSafeThingGuid(MyBaseThing, "METHODLIST_ID"), eEngineName.NMIService, "OPC-UA Server Methods", tDataSource) { IsNotAutoLoading = true };
                TheNMIEngine.AddFormToThingUX(MyBaseThing, tOPCMethodForm, "CMyTable", "Method List", 1, 3, 0xF0, null, null, new ThePropertyBag() { "Visibility=false" });
                TheNMIEngine.AddFields(tOPCMethodForm, new List<TheFieldInfo> {
                    {  new TheFieldInfo() { FldOrder=11,DataItem="ParentIdName",Flags=0,Type=eFieldType.SingleEnded,Header="Parent Id",FldWidth=4 }},
                    {  new TheFieldInfo() { FldOrder=12,DataItem="DisplayName",Flags=0,Type=eFieldType.SingleEnded,Header="Method-Name",FldWidth=3 }},
                    {  new TheFieldInfo() { FldOrder=13,DataItem="NodeIdName",Flags=0,Type=eFieldType.SingleEnded,Header="NodeId",FldWidth=6 }},

                    {  new TheFieldInfo() { FldOrder=3,DataItem="IsSubscribed",Flags=0,Type=eFieldType.SingleCheck,Header="Is Thing",FldWidth=1 }},
                });

                // Button: Convert method to thing
                TheNMIEngine.AddSmartControl(MyBaseThing, tOPCMethodForm, eFieldType.TileButton, 1, writeEnableFlag, 0xC0, "Convert to Thing", null, new nmiCtrlTileButton() { ClassName = "cdeGoodActionButton", TileWidth = 1, TileHeight = 1, NoTE = true })
                    .RegisterUXEvent(MyBaseThing, eUXEvents.OnClick, "SUBSCRIBEMETHOD", (pThing, pObj) =>
                    {
                        TheProcessMessage pMsg = pObj as TheProcessMessage;
                        if (ConnectionState != ConnectionStateEnum.Connected)
                        {
                            TheCommCore.PublishToOriginator(pMsg.Message, new TSM(eEngineName.NMIService, "NMI_TOAST", "Server not connected - please connect first"));
                            return;
                        }
                        try
                        {
                            TSM tTSM = pMsg.Message;
                            if (tTSM == null || string.IsNullOrEmpty(tTSM.PLS)) return;
                            string[] tPara = tTSM.PLS.Split(':');
                            if (tPara.Length < 3) return;
                            TheOPCMethod tMeth = MyMethods.MyMirrorCache.GetEntryByID(TheCommonUtils.CGuid(tPara[2]).ToString());
                            if (tMeth == null || tMeth.IsSubscribed) return;
                            tMeth.IsSubscribed = true;
                            TheOPCUAMethodThing tNewTag = new TheOPCUAMethodThing(null, MyBaseThing.EngineName);
                            tNewTag.Setup(tMeth);
                            TheThingRegistry.RegisterThing(tNewTag);
                            MyMethods.UpdateItem(tMeth, null);
                            TheCommCore.PublishToOriginator(tTSM, new TSM(eEngineName.NMIService, "NMI_TOAST", string.Format("Method ({0}) converted to Thing", tMeth.DisplayName)));
                        }
                        catch (Exception eee)
                        {
                            LastMessage = "Failed to Subscribe";
                            TheCommCore.PublishToOriginator(pMsg.Message, new TSM(eEngineName.NMIService, "NMI_INFO", string.Format("Error during Subscribe: {0}", eee.ToString())));
                        }
                    });
                TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.TileButton, pStartFld + 11, writeEnableFlag, 0xF0, "Show Methods", null, new nmiCtrlTileButton() { OnClick = $"TTS:{tOPCMethodForm.cdeMID}", ParentFld = pStartFld, ClassName = "cdeTransitButton", TileWidth = 2, NoTE = true });
            }

        }

        private List<string> CreateIDFilter()
        {
            string tFilter = TheThing.GetSafePropertyString(MyBaseThing, "SelectedIs");
            List<string> idlist = null;
            if (!string.IsNullOrEmpty(tFilter))
                idlist = TheCommonUtils.DeserializeJSONStringToObject<List<string>>(tFilter);
            return idlist;
        }
    }
}
