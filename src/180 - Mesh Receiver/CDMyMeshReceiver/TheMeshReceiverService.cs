// SPDX-FileCopyrightText: 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

﻿/*********************************************************************
*
* Project Name: 180-CDMyMeshReceiver
*
* Description: This service allows to send data to or receive data from Cloud Services like Azure ServiceBus and Eventhub.
*
* Date of creation: 
*
* Author: Markus Horstmann
*
*********************************************************************/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using nsCDEngine.Engines;
using nsCDEngine.BaseClasses;
using nsCDEngine.Communication;
using nsCDEngine.ViewModels;
using nsCDEngine.Engines.StorageService;
using nsCDEngine.Engines.NMIService;
using nsCDEngine.Engines.ThingService;

using CDMyMeshReceiver.ViewModel;
using nsCDEngine.Security;

using nsTheConnectionBase;

namespace CDMyMeshReceiver
{
    public class MeshDeviceTypes
    {
        public const string MeshReceiver = "Mesh Receiver";

        public static string GetValues()
        {
            var result = typeof(MeshDeviceTypes).GetFields()
                .Aggregate("", (agg, field) => 
                    agg += field.GetValue(null)+";")
                .TrimEnd(';');

            return result;
        }
    }

    
    class MeshReceiverService : ICDEPlugin, ICDEThing
    {
        #region ICDEPlugin
        private IBaseEngine MyBaseEngine;
        public IBaseEngine GetBaseEngine()
        {
            return MyBaseEngine;
        }
        /// <summary>
        /// This constructor is called by The C-DEngine during initialization in order to register this service
        /// </summary>
        /// <param name="pBase">The C-DEngine is creating a Host for this engine and hands it to the Plugin Service</param>
        public void InitEngineAssets(IBaseEngine pBase)
        {
            MyBaseEngine = pBase;
            MyBaseEngine.SetEngineName(this.GetType().FullName);            //Can be any arbitrary name - recommended is the class name
            MyBaseEngine.SetEngineType(this.GetType());                     //Has to be the type of this class
            MyBaseEngine.SetFriendlyName("C-Labs Mesh Receiver");      //TODO: Step 1: Give your plugin a friendly name
            MyBaseEngine.SetEngineService(true);                            //Keep True if this class is a service

            MyBaseEngine.SetEngineID(new Guid("{BF99690D-6114-427F-B17A-A606BE3145BD}")); //TODO: Step 2 - set Plugin GUID
            MyBaseEngine.SetPluginInfo("This service allows to receive data from a CDEngine mesh and place them into CDEngine Things.", 0, null, "", "C-Labs and its licensors", "http://www.c-labs.com", new List<string>() { });

            MyBaseEngine.SetCDEMinVersion(4.011);
            MyBaseEngine.AddManifestFiles(new List<string>
                {
                });
        }
        #endregion

        #region - Rare to Override
        public void SetBaseThing(TheThing pThing)
        {
            MyBaseThing = pThing;
        }
        public TheThing GetBaseThing()
        {
            return MyBaseThing;
        }
        public cdeP GetProperty(string pName, bool DoCreate)
        {
            if (MyBaseThing != null)
                return MyBaseThing.GetProperty(pName, DoCreate);
            return null;
        }
        public cdeP SetProperty(string pName, object pValue)
        {
            if (MyBaseThing != null)
                return MyBaseThing.SetProperty(pName, pValue);
            return null;
        }
        public void RegisterEvent(string pName, Action<ICDEThing, object> pCallBack)
        {
            if (MyBaseThing != null)
                MyBaseThing.RegisterEvent(pName, pCallBack);
        }
        public void UnregisterEvent(string pName, Action<ICDEThing, object> pCallBack)
        {
            if (MyBaseThing != null)
                MyBaseThing.UnregisterEvent(pName, pCallBack);
        }
        public void FireEvent(string pEventName, ICDEThing sender, object pPara, bool FireAsync)
        {
            if (MyBaseThing != null)
                MyBaseThing.FireEvent(pEventName, sender, pPara, FireAsync);
        }
        public bool HasRegisteredEvents(string pEventName)
        {
            if (MyBaseThing != null)
                return MyBaseThing.HasRegisteredEvents(pEventName);
            return false;
        }
        protected TheThing MyBaseThing = null;

        protected bool mIsUXInitCalled = false;
        protected bool mIsUXInitialized = false;
        protected bool mIsInitCalled = false;
        protected bool mIsInitialized = false;
        public bool IsUXInit()
        { return mIsUXInitialized; }
        public bool IsInit()
        { return mIsInitialized; }

        #endregion

        public bool Init()
        {
            if (mIsInitCalled) return false;
            mIsInitCalled = true;

            MyBaseThing.RegisterEvent(eEngineEvents.IncomingMessage, HandleMessage);
            MyBaseEngine.RegisterEvent(eEngineEvents.ThingDeleted, OnDeletedThing);
            MyBaseEngine.RegisterEvent(eEngineEvents.ThingRegistered, OnThingRegistered);
            MyBaseEngine.RegisterEvent(eEngineEvents.ShutdownEvent, OnShutdown);
            TheCommonUtils.cdeRunAsync("Init Mesh Receiver Connections", false, (state) =>
            {
                // Migrate instances from previous versions
                List<TheThing> tLegacyDeviceList = TheThingRegistry.GetThingsOfEngine("CDMyCloudServices.cdeMyCloudService");
                foreach (var tLegacyDevice in tLegacyDeviceList)
                {
                    if (!tLegacyDevice.HasLiveObject)
                    {
                        switch (tLegacyDevice.DeviceType)
                        {
                            case "Mesh Receiver":
                                tLegacyDevice.EngineName = MyBaseThing.EngineName;
                                tLegacyDevice.DeviceType = MeshDeviceTypes.MeshReceiver;
                                TheThingRegistry.UpdateThing(tLegacyDevice, false);
                                break;
                        }
                    }
                }

                //await System.Threading.Tasks.Task.Delay(30000);
                InitServers();
                mIsInitialized = true;
                FireEvent(eThingEvents.Initialized, this, true, true);
                MyBaseEngine.ProcessInitialized();
            }, null);

            return false;
        }
        public bool Delete()
        {
            mIsInitialized = false;
            // TODO Properly implement delete
            return true;
        }

        void OnDeletedThing(ICDEThing pThing, object pPara)
        {
            TheThing tThing = pPara as TheThing;
            if (tThing != null && tThing.IsInit())
            {
                var tThingObj = (tThing.GetObject() as TheConnectionBase);
                if (tThingObj != null)
                {
                    tThingObj.Disconnect(true);
                }
            }
        }

        private void OnThingRegistered(ICDEThing arg1, object arg2)
        {
            InitServers();
            mMyDashboard?.Reload(null, false);
        }

        void OnShutdown(ICDEThing pThing, object pPara)
        {
            ShutdownServers();
        }

        TheDashboardInfo mMyDashboard;
        public bool CreateUX()
        {
            if (mIsUXInitCalled) return false;
            mIsUXInitCalled = true;

            //NUI Definition for All clients
            mMyDashboard = TheNMIEngine.AddDashboard(MyBaseThing, new TheDashboardInfo(MyBaseEngine, "<i class='cl-font cl-Logo fa-5x'></i><br>Mesh Receiver") { PropertyBag = new ThePropertyBag() { "Category=Connectors" } });

            TheFormInfo tAllCloudConnections = new TheFormInfo(MyBaseEngine) { cdeMID = TheThing.GetSafeThingGuid(MyBaseThing, "MeshR"), defDataSource = string.Format("TheThing;:;0;:;True;:;EngineName={0}", MyBaseEngine.GetEngineName()), FormTitle = "Mesh Receiver Connections", AddButtonText = "Add a Connection" };
            TheNMIEngine.AddFormToThingUX(MyBaseThing, tAllCloudConnections, "CMyTable", "Mesh Receivers", 1, 0x0D, 0xC0, TheNMIEngine.GetNodeForCategory(), null, new ThePropertyBag() { "Thumbnail=MicrosoftAzure.png;0.5" });
            TheNMIEngine.AddCommonTableColumns(MyBaseThing, tAllCloudConnections, MeshDeviceTypes.GetValues(), MeshDeviceTypes.MeshReceiver, false, true);

            TheNMIEngine.AddField(tAllCloudConnections, new TheFieldInfo() { FldOrder = 5, cdeA = 0xC0, Flags = 6, Type = eFieldType.SingleCheck, FldWidth=1, Header = "Auto-Connect", DataItem = "MyPropertyBag.AutoConnect.Value" });
            TheNMIEngine.AddField(tAllCloudConnections, new TheFieldInfo() { FldOrder = 6, cdeA = 0xC0, Flags = 0, Type = eFieldType.SingleCheck, FldWidth = 1, Header = "Is Connected", DataItem = "MyPropertyBag.IsConnected.Value", PropertyBag = new nmiCtrlSingleCheck { AreYouSure = "Are you sure you want to connect/disconnect?" } });
            TheNMIEngine.AddField(tAllCloudConnections, new TheFieldInfo() { FldOrder = 7, cdeA = 0xC0, Flags = 0, Type = eFieldType.SingleCheck, FldWidth = 1, Header = "Connecting", DataItem = "MyPropertyBag.Connecting.Value" });
            TheNMIEngine.AddField(tAllCloudConnections, new TheFieldInfo() { FldOrder = 8, cdeA = 0xC0, Flags = 0, Type = eFieldType.SingleCheck, FldWidth = 1, Header = "Disconnecting", DataItem = "MyPropertyBag.Disconnecting.Value" });
            TheNMIEngine.AddField(tAllCloudConnections, new TheFieldInfo() { FldOrder = 50, cdeA = 0xFF, Type = eFieldType.DateTime, FldWidth = 2, Header = "Last Update", DataItem = "MyPropertyBag.LastUpdate.Value" });

            TheThingRegistry.UpdateEngineUX(MyBaseEngine.GetEngineName());

            TheNMIEngine.AddAboutButton(MyBaseThing, true, "REFRESH_DASH", 0xc0);
            TheNMIEngine.RegisterEngine(MyBaseEngine);

            mIsUXInitialized = true;
            return true;
        }

        void InitServers()
        {
            List<TheThing> tDevList = TheThingRegistry.GetThingsOfEngine(this.MyBaseEngine.GetEngineName()); 
            if (tDevList.Count > 0)
            {
                foreach (TheThing tDev in tDevList)
                {
                    if (!tDev.HasLiveObject)
                    {
                        switch (tDev.DeviceType)
                        {
                            case MeshDeviceTypes.MeshReceiver:
                                TheMeshReceiver tMR = new TheMeshReceiver(tDev, this);
                                TheThingRegistry.RegisterThing(tMR);
                                break;
                        }
                    }
                }
            }

            MyBaseEngine.SetStatusLevel(-1);
        }

        void ShutdownServers()
        {
            List<TheThing> tDevList = TheThingRegistry.GetThingsOfEngine(this.MyBaseEngine.GetEngineName());
            if (tDevList.Count > 0)
            {
                foreach (TheThing tDev in tDevList)
                {
                    if (tDev.HasLiveObject)
                    {
                        var t = tDev.GetObject();

                        if (t is TheConnectionBase)
                        {
                            (t as TheConnectionBase).Disconnect(true);
                        }
                    }
                }
            }
        }


#region Message Handling
        /// <summary>
        /// Handles Messages sent from a host sub-engine to its clients
        /// </summary>
        /// <param name="Command"></param>
        /// <param name="pMessage"></param>
        public void HandleMessage(ICDEThing sender, object pIncoming)
        {
            TheProcessMessage pMsg = pIncoming as TheProcessMessage;
            if (pMsg == null) return;

            string[] cmd = pMsg.Message.TXT.Split(':');
            switch (cmd[0])
            {
                case "CDE_INITIALIZED":
                    MyBaseEngine.SetInitialized(pMsg.Message);
                    break;
                case "CDE_INITIALIZE":
                    if (MyBaseEngine.GetEngineState().IsService)
                    {
                        if (!MyBaseEngine.GetEngineState().IsEngineReady)
                            MyBaseEngine.SetEngineReadiness(true, null);
                        MyBaseEngine.ReplyInitialized(pMsg.Message);
                    }
                    break;
                case "REFRESH_DASH":
                    InitServers();
                    mMyDashboard.Reload(pMsg, false);
                    break;
                default:
                    //TheThing tt = TheThingRegistry.GetThingByProperty(MyBaseEngine.GetEngineName(), Guid.Empty, "ID", pMsg.Message.PLS);
                    //if (tt != null)
                    //    tt.HandleMessage(this, pMsg);
                    break;
            }
        }
        #endregion

    }
}
