// SPDX-FileCopyrightText: 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

﻿using System;

using nsCDEngine.Engines;
using nsCDEngine.BaseClasses;
using nsCDEngine.Communication;
using nsCDEngine.ViewModels;
using nsCDEngine.Engines.NMIService;
using nsCDEngine.Engines.ThingService;
using System.Collections.Generic;
using nsCDEngine.Engines.StorageService;
using nsCDEngine.Security;

namespace CDMyVisitorLog
{
    public class TheFootPrints: TheMetaDataBase
    {
        public string Footprint { get; set; }
        public int Counter { get; set; }

        public string NodeType { get; set; }

        public bool IsDirty { get; set; }

        public int ExactCount { get; set; }
    }

    class TheVisitorService : ThePluginBase
    {
        /// <summary>
        /// This constructor is called by The C-DEngine during initialization in order to register this service
        /// </summary>
        /// <param name="pBase">The C-DEngine is creating a Host for this engine and hands it to the Plugin Service</param>
        public override void InitEngineAssets(IBaseEngine pBase)
        {
            MyBaseEngine = pBase;
            MyBaseEngine.SetEngineName(GetType().FullName);      
            MyBaseEngine.SetEngineType(GetType());               
            MyBaseEngine.SetFriendlyName("My Visitor Log Service");   
            MyBaseEngine.SetEngineService(true);                      

            MyBaseEngine.SetEngineID(new Guid("{AAE144FE-364F-4FA0-9E67-ABC0A3A26A53}")); 
            MyBaseEngine.SetPluginInfo("This service allows you to track Visitors", 0, null, "toplogo-150.png", "C-Labs", "http://www.c-labs.com", null); 

            MyBaseEngine.SetVersion(0);
        }

        public override cdeP SetProperty(string pName, object pValue)
        {
            if (MyBaseThing != null)
            {
                if (pName.ToLower() == "logvisitor" && MyVisitorLog != null && pValue!=null && TheCommonUtils.CGuid(pValue) == Guid.Empty)  //Visitors Log - Requires StorageEngine
                    MyVisitorLog.LogVisitor(pValue.ToString(), "CMyVisitorLog SetP");
                return MyBaseThing.SetProperty(pName, pValue);
            }
            return null;
        }

        /// <summary>
        /// The Visitor Log of this node
        /// </summary>
        public TheVisitorLog MyVisitorLog;

        public int VisitorCount
        {
            get { return TheCommonUtils.CInt(TheThing.GetSafePropertyString(MyBaseThing, "VisitorCount")); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, "VisitorCount", value); }
        }

        private string MyNodePrintsName = null;
        TheStorageMirror<TheFootPrints> MyFootPrints = null;
        private void OnStorageReady(ICDEThing pThing, object para)
        {
            MyFootPrints = new TheStorageMirror<TheFootPrints>(TheCDEngines.MyIStorageService)
            {
                CacheTableName = MyNodePrintsName,
                IsRAMStore = true,
                IsCachePersistent=true
            };
            MyFootPrints.InitializeStore(false, false);
        }

        public override bool Init()
        {
            if (mIsInitCalled) return false;
            mIsInitCalled = true;

            MyBaseThing.RegisterEvent(eEngineEvents.IncomingMessage, HandleMessage);
            if (TheThing.GetSafePropertyString(MyBaseThing, "VisitorCount") == "")
            {
                VisitorCount = 0;
            }
            MyNodePrintsName = $"NodePrints{TheThing.GetSafeThingGuid(MyBaseThing, "NodePrints")}";
            TheBaseEngine.WaitForStorageReadiness(OnStorageReady, true);
            MyBaseThing.SetPublishThrottle(15000);
            TheCommCore.RegisterRelayEvents(null, null, sinkNewConnection);

            if (MyVisitorLog == null)
                MyVisitorLog = new TheVisitorLog(MyBaseThing);
            TheQueuedSenderRegistry.RegisterHealthTimer(GetSystemInfo);
            TheCDEngines.MyNMIService?.RegisterEvent("NMI_MY_LOCATION", (sender2, para) =>
            {
                var t = para as TheNMILocationInfo;
                if (t != null)
                {
                    var NewEntry = new TheVisitorLogData { cdeN=t.cdeN, latitude = t.Latitude, longitude = t.Longitude, ip = t.ClientInfo?.UserID.ToString(), LastVisit = DateTimeOffset.Now, Description = t.ClientInfo != null && t.ClientInfo.UserID!=Guid.Empty ? TheUserManager.GetUserFullName(t.ClientInfo.UserID) : "Unknown user" };
                    MyVisitorLog.LogVisitor(NewEntry);
                    myGoogleMap?.SetUXProperty(Guid.Empty, $"AddMarker={TheCommonUtils.SerializeObjectToJSONString(NewEntry)}");
                }
            });
            TheCommCore.MyHttpService.RegisterHttpInterceptorB4("/cdemeshinfo.aspx", sinkRequestMeshInfo);
            MyBaseEngine.ProcessInitialized();
            MyBaseEngine.SetEngineReadiness(true, null);
            MyBaseEngine.SetStatusLevel(1);
            mIsInitialized = true;
            return true;
        }

        private void sinkNewConnection(TheSessionState arg1)
        {
            VisitorCount++;
            if (arg1?.SiteName == null || TheCommonUtils.CGuid(arg1.SiteName) != Guid.Empty || arg1.SiteName=="::1") return;
            MyVisitorLog?.LogVisitor(arg1.SiteName, arg1.InitReferer);
        }

        private void sinkRequestMeshInfo(TheRequestData request)
        {
            if (request != null)
            {
                request.ResponseMimeType = "application/json";
                string errorText = "";
                Dictionary<string, string> tQ = TheCommonUtils.ParseQueryString(request.RequestUri?.Query);
                if (tQ != null)
                {
                    tQ.TryGetValue("MESHINFOTOKEN", out string meshInfoToken);
                    if (tQ.TryGetValue("ORG", out string tOrg))
                    {
                        Guid requestorMID = TheCommonUtils.CGuid(tOrg);
                        if (requestorMID != Guid.Empty)
                        {
                            TheMeshInfoStatus meshInfoStatus = TheQueuedSenderRegistry.GetMeshInfoForNodeID(requestorMID, meshInfoToken);
                            if (string.IsNullOrEmpty(meshInfoStatus?.StatusMessage))
                            {
                                request.StatusCode = (int)eHttpStatusCode.OK;
                                request.ResponseBuffer = TheCommonUtils.CUTF8String2Array(TheCommonUtils.SerializeObjectToJSONString(meshInfoStatus.MeshInfo));
                            }
                            else
                            {
                                request.StatusCode = meshInfoStatus.StatusCode; //Better if meshInfoStatus also defines this as there are different messages (wrong MIT, too frequent tests etc)
                                errorText = meshInfoStatus.StatusMessage;
                            }
                        }
                        else
                        {
                            request.StatusCode = 400;
                            errorText = "Invalid or badly formed ORG parameter.";
                        }
                    }
                    else
                    {
                        request.StatusCode = 400;
                        errorText = "No ORG parameter found for mesh info request.";
                    }
                }
                else
                {
                    request.StatusCode = (int)eHttpStatusCode.AccessDenied;
                    errorText = "Access denied";
                }
                if (!string.IsNullOrEmpty(errorText))
                {
                    TheErrorResponse errorResponse = new TheErrorResponse { Error = errorText, CTIM = DateTimeOffset.Now };
                    request.ResponseBuffer = TheCommonUtils.CUTF8String2Array(TheCommonUtils.SerializeObjectToJSONString(errorResponse));
                }
            }
        }

        TheFieldInfo myGoogleMap = null;
        public override bool CreateUX()
        {
            if (mIsUXInitCalled) return false;
            mIsUXInitCalled = true;

            //NUI Definition for All clients
            TheNMIEngine.AddDashboard(MyBaseThing, new TheDashboardInfo(MyBaseEngine, "Visitor Log") { PropertyBag = new nmiDashboard { Category="Diagnostics", Caption = "<i class='fa faIcon fa-5x'>&#xf0c0;</i></br>Visitor Log" } });

            TheFormInfo tMyForm = TheNMIEngine.AddForm(new TheFormInfo(MyBaseThing) { FormTitle = "Visitor Log of " + TheBaseAssets.MyServiceHostInfo.MyStationName, DefaultView = eDefaultView.Form, TileWidth=10 });
            TheNMIEngine.AddFormToThingUX(MyBaseThing, tMyForm, "CMyForm", "My Visitor Log</br><span style='font-size:xx-small'>" + TheBaseAssets.MyServiceHostInfo.MyStationName + "</span>", 3, 3, 0, TheNMIEngine.GetNodeForCategory(), null, null);
            TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.SingleEnded, 2, 0, 0, "Visitor Count Since Startup", "VisitorCount", new ThePropertyBag() { "TileWidth=3" });
            TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.SingleEnded, 3, 0, 0, "Connected Browser", "BrowserCount", new ThePropertyBag() { "TileWidth=3" });

            #region GoogleMaps
            string gKey = null;
            if (TheBaseAssets.MyCmdArgs.ContainsKey("GoogleMapsKey"))
                gKey=TheBaseAssets.MyCmdArgs["GoogleMapsKey"];
            if (TheNMIEngine.IsControlTypeRegistered("cdeNMI.ctrlGoogleMaps") && !string.IsNullOrEmpty(gKey))
            {
                TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.CollapsibleGroup, 420, 2, 0, "Google Maps", null, new nmiCtrlCollapsibleGroup() { TileWidth = 12, IsSmall=true });
                var myGoogleMap = TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.UserControl, 421, 0, 0, null, "MapCoords", new ThePropertyBag()
                {
                    "NoTE=true", "ParentFld=420", "Zoom=4", "Tilt=45", "Lat=48", "Lng=-122", $"GoogleKey={gKey}", //TODO: Make configuration setting
                    "TileWidth=10", "TileHeight=10", "ControlType=cdeNMI.ctrlGoogleMaps", "Markers=[{ \"Description\": \"Home\",\"latitude\": 48,\"longitude\": -122 }]"
                });
                TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.SingleEnded, 422, 2, 0, "Coordinates", "Lat", new nmiCtrlSingleEnded() { TileWidth = 3, ParentFld = 420 });
                TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.SingleEnded, 423, 2, 0, "", "Lng", new nmiCtrlSingleEnded() { NoTE=true, TileWidth = 1, ParentFld = 420 });
                GetProperty("Lat", true).RegisterEvent(eThingEvents.PropertyChangedByUX, (p) => {
                    myGoogleMap?.SetUXProperty(Guid.Empty, $"Lat={p.ToString()}");
                });
                GetProperty("Lng", true).RegisterEvent(eThingEvents.PropertyChangedByUX, (p) => {
                    myGoogleMap?.SetUXProperty(Guid.Empty, $"Lng={p.ToString()}");
                });

                TheFieldInfo tBarZOOM = TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.BarChart, 427, 2, 0, "Zoom", "ZoomVal", new ThePropertyBag() { "NoTE=true", "TileHeight=1", "TileWidth=6", "IsVertical=false", "ParentFld=420", "MaxValue=20" });
                tBarZOOM.RegisterUXEvent(MyBaseThing, eUXEvents.OnPropertyChanged, "Value", (sender, para) =>
                {
                    TheProcessMessage tP = para as TheProcessMessage;
                    if (tP != null && tP.Message != null)
                        myGoogleMap?.SetUXProperty(tP.Message.GetOriginator(), $"Zoom={tP.Message.PLS}");
                });

                tMyForm.RegisterEvent2(eUXEvents.OnShow, (pMsg, arg) => {
                    var tVisits=MyVisitorLog.GetAllVisitors();
                    if (tVisits?.Count>0)
                    {
                        myGoogleMap?.SetUXProperty(pMsg.Message.GetOriginator(), $"Markers={TheCommonUtils.SerializeObjectToJSONString(tVisits)}");
                    }
                });
            }
            #endregion


            TheFormInfo tConns = new TheFormInfo(TheThing.GetSafeThingGuid(MyBaseThing, "FootPrints"), eEngineName.NMIService, "Node Footprints", MyNodePrintsName, "Add Footprint",null) { IsNotAutoLoading = true };
            TheNMIEngine.AddFormToThingUX(MyBaseThing, tConns, "CMyTable", "Node Footprints", 1, 9, 0x80, "Node Type Definitions", null, new ThePropertyBag() { "Visibility=true" });
            TheNMIEngine.AddSmartControl(MyBaseThing, tConns, eFieldType.Number, 10, 0, 0x0, "Counter", "Counter", new nmiCtrlNumber() { TileWidth = 1, FldWidth = 1 });
            TheNMIEngine.AddSmartControl(MyBaseThing, tConns, eFieldType.SingleEnded, 20, 2, 0x0, "Node Type", "NodeType", new nmiCtrlNumber() { TileWidth = 3, FldWidth = 3 });
            TheNMIEngine.AddSmartControl(MyBaseThing, tConns, eFieldType.Number, 25, 2, 0x0, "Plugin Count", "ExactCount", new nmiCtrlNumber() { TileWidth = 1, FldWidth = 1 });
            TheNMIEngine.AddSmartControl(MyBaseThing, tConns, eFieldType.TextArea, 30, 2, 0xC0, "Plugin Footprint", "Footprint", new nmiCtrlTextArea() { TileWidth = 10, TileHeight=2, FldWidth = 10 });
            TheNMIEngine.AddTableButtons(tConns, false, 100);

            TheNMIEngine.AddAboutButton(MyBaseThing);
            mIsUXInitialized = true;
            return true;
        }

        //public string Foot1Print = "CDMyBHyve.TheHyve;CDMyDoorBell.TheDoorService";  //Later in Configuration SM

        void GetSystemInfo(long ticks)
        {
            if ((ticks % 15) != 0 || MyFootPrints==null || MyFootPrints.Count==0)
                return;

            TheCDEngines.MyContentEngine.HandleMessage(this, new TheProcessMessage() { Message = new TSM(eEngineName.ContentService, "CDE_GET_NODETOPICS"), LocalCallback = sinkServiceInfo });
        }

        void sinkServiceInfo(TSM pMsgMessage)
        {
            if (string.IsNullOrEmpty(pMsgMessage?.PLS))
                return;
            var MyInfo = TheCommonUtils.DeserializeJSONStringToObject<List<TheNodeTopics>>(pMsgMessage.PLS);

            int bk = 0;
            foreach (TheFootPrints tf in MyFootPrints.MyMirrorCache.TheValues)
            {
                tf.Counter = 0;
                tf.IsDirty = false;
            }
            if (MyInfo?.Count > 0)
            {
                foreach (var tIn in MyInfo)
                {
                    if (tIn.NodeType == cdeSenderType.CDE_JAVAJASON)
                        bk++;
                    else
                    {
                        foreach (TheFootPrints tf in MyFootPrints.MyMirrorCache.TheValues)
                        {
                            List<string> tFs = TheCommonUtils.CStringToList(tf.Footprint, ';');
                            if (IsListInList(tIn.Topics,tFs) && (tf.ExactCount == 0 || tf.ExactCount == tIn.Topics.Count))
                            {
                                tf.Counter++;
                                tf.IsDirty = true;
                            }
                        }
                    }
                }
                TheThing.SetSafePropertyNumber(MyBaseThing, "BrowserCount", bk);
            }
            foreach (TheFootPrints tf in MyFootPrints.MyMirrorCache.TheValues)
            {
                if (tf.IsDirty || tf.Counter == 0)
                {
                    MyFootPrints.UpdateItem(tf);
                    TheThing.SetSafePropertyNumber(MyBaseThing, $"{tf.NodeType}_Count", tf.Counter);
                }
            }
        }

        public override void HandleMessage(ICDEThing sender, object pIncoming)
        {
            TheProcessMessage pMsg = pIncoming as TheProcessMessage;
            if (pMsg == null) return;

            var tCmd = pMsg.Message.TXT.Split(':');
            switch (tCmd[0])
            {
                case "CDE_NODETOPICS":
                    sinkServiceInfo(pMsg?.Message);
                    break;
                case "ADD_FOOTPRINT":
                    var Tf = TheCommonUtils.DeserializeJSONStringToObject<NodeTypeScript>(pMsg.Message.PLS);
                    if (Tf?.NodeTypes != null && Tf.NodeTypes.Count > 0)
                        MyFootPrints.AddItems(Tf.NodeTypes, null);
                    TheCommCore.PublishToOriginator(pMsg.Message, new TSM(pMsg.Message.ENG, $"{tCmd[0]}_RESPONSE{(tCmd.Length>1?$":{tCmd[1]}":"")}", "{\"RESPONSE\":\"SUCCESS\"}"), true);
                    break;
            }
        }

        bool IsListInList(List<string> tMain, List<string> inStrings)
        {
            if (tMain == null || inStrings == null || tMain.Count == 0 || inStrings.Count == 0)
                return false;

            int count = 0;
            foreach (string t in inStrings)
            {
                foreach (string s in tMain)
                {
                    if (!string.IsNullOrEmpty(t) && !string.IsNullOrEmpty(s) && s.StartsWith(t))
                    {
                        count++;
                        if (count == inStrings.Count)
                            return true;
                    }
                }
            }
            return false;
        }
    }

    public class NodeTypeScript
    {
        public List<TheFootPrints> NodeTypes;
    }
}
