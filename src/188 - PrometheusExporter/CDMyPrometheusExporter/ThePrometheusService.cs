// SPDX-FileCopyrightText: 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

/*********************************************************************
*
* Project Name: 184- CDMyPrometheusExporter
*
* Description: 
*
* Date of creation: 
*
* Author: Markus Horstmann
*
*********************************************************************/
using CDMyPrometheusExporter.ViewModel;
using nsCDEngine.BaseClasses;
using nsCDEngine.Engines;
using nsCDEngine.Engines.NMIService;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.ViewModels;
using nsTheConnectionBase;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CDMyPrometheusExporter
{
    public class PrometheusDeviceTypes
    {
        public const string PrometheusExporter = "Prometheus Exporter";

        public static string GetValues()
        {
            var result = typeof(PrometheusDeviceTypes).GetFields()
                .Aggregate("", (agg, field) => 
                    agg += field.GetValue(null)+";")
                .TrimEnd(';');

            return result;
        }
    }

    
    class PrometheusExporterService : ThePluginBase
    {
        public override void InitEngineAssets(IBaseEngine pBase)
        {
            base.InitEngineAssets(pBase);
            MyBaseEngine.SetFriendlyName("Prometheus Exporter");      
            MyBaseEngine.SetEngineID(new Guid("{AF65D637-191B-40BA-8302-BDD254E11DFE}"));
            MyBaseEngine.SetPluginInfo("This service allows to export Thing properties as metrics to Prometheus.", 0, null, "images/prometheus_logo_grey.png", "C-Labs and its licensors", "http://www.c-labs.com", new List<string>() { "Service" });
            MyBaseEngine.AddManifestFiles(new List<string> {
                "Prometheus.NetStandard.dll", /*"ClientBin\\Scripts\\CreatePrometheusExporter.cdescript"*/
            });
        }

        public override bool Init()
        {
            if (mIsInitCalled) return false;
            mIsInitCalled = true;

            MyBaseThing.RegisterEvent(eEngineEvents.IncomingMessage, HandleMessage);
            MyBaseEngine.RegisterEvent(eEngineEvents.ThingDeleted, OnDeletedThing);
            MyBaseEngine.RegisterEvent(eEngineEvents.ThingRegistered, OnThingRegistered);
            MyBaseEngine.RegisterEvent(eEngineEvents.ShutdownEvent, OnShutdown);
            TheCommonUtils.cdeRunAsync("Init Prometheus Exporters", false, (state) =>
            {
                try
                {
                    InitServers();
                }
                catch (Exception e)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(181001, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, "Error initializing instances", eMsgLevel.l1_Error, e.ToString()));
                }

                mIsInitialized = true;
                FireEvent(eThingEvents.Initialized, this, true, true);
                MyBaseEngine.ProcessInitialized();
            }, null);

            return false;
        }

        void OnDeletedThing(ICDEThing pThing, object pPara)
        {
            TheThing tThing = pPara as TheThing;
            if (tThing != null && tThing.IsInit())
            {
                var tThingObj = (tThing.GetObject() as TheConnectionBase);
                if (tThingObj != null)
                {
                    tThingObj.Connect();
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
        public override bool CreateUX()
        {
            if (mIsUXInitCalled) return false;
            mIsUXInitCalled = true;
            TheNMIEngine.RegisterEngine(MyBaseEngine);

            mMyDashboard = TheNMIEngine.AddDashboard(MyBaseThing, new TheDashboardInfo(MyBaseEngine, "Prometheus Exporters") { PropertyBag = new ThePropertyBag() { "Category=Diagnostics", "Thumbnail=images/prometheus_logo_grey.png;0.5" } });

            TheFormInfo tAllCloudConnections = new TheFormInfo(MyBaseEngine) { cdeMID = TheThing.GetSafeThingGuid(MyBaseThing, "AZC"), defDataSource = string.Format("TheThing;:;0;:;True;:;EngineName={0}", MyBaseEngine.GetEngineName()), FormTitle = "Prometheus Exporters", AddButtonText = "Add a Sender" };
            TheNMIEngine.AddFormToThingUX(MyBaseThing, tAllCloudConnections, "CMyTable", "Prometheus Exporters", 1, 0x0D, 0xC0, TheNMIEngine.GetNodeForCategory(), null, new ThePropertyBag() { "Thumbnail=images/prometheus_logo_grey.png.png;0.5" });

            TheNMIEngine.AddCommonTableColumns(MyBaseThing, tAllCloudConnections, PrometheusDeviceTypes.GetValues(), PrometheusDeviceTypes.PrometheusExporter);

            TheNMIEngine.AddField(tAllCloudConnections, new TheFieldInfo() { FldOrder = 5, cdeA = 0xC0, Flags = 6, Type = eFieldType.SingleCheck, FldWidth = 1, Header = "Auto-Connect", DataItem = "MyPropertyBag.AutoConnect.Value" });
            TheNMIEngine.AddField(tAllCloudConnections, new TheFieldInfo() { FldOrder = 6, cdeA = 0xC0, Flags = 0, Type = eFieldType.SingleCheck, FldWidth = 1, Header = "Is Connected", DataItem = "MyPropertyBag.IsConnected.Value" });
            TheNMIEngine.AddField(tAllCloudConnections, new TheFieldInfo() { FldOrder = 7, cdeA = 0xC0, Flags = 0, Type = eFieldType.SingleCheck, FldWidth = 1, Header = "Connecting", DataItem = "MyPropertyBag.Connecting.Value" });
            TheNMIEngine.AddField(tAllCloudConnections, new TheFieldInfo() { FldOrder = 8, cdeA = 0xC0, Flags = 0, Type = eFieldType.SingleCheck, FldWidth = 1, Header = "Disconnecting", DataItem = "MyPropertyBag.Disconnecting.Value" });
            TheNMIEngine.AddField(tAllCloudConnections, new TheFieldInfo() { FldOrder = 50, cdeA = 0xFF, Type = eFieldType.DateTime, FldWidth = 2, Header = "Last Update", DataItem = "MyPropertyBag.LastUpdate.Value" });

            TheThingRegistry.UpdateEngineUX(MyBaseEngine.GetEngineName());

            TheNMIEngine.AddAboutButton(MyBaseThing, true, "REFRESH_DASH", 0xc0);

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
                    try
                    {
                        if (!tDev.HasLiveObject)
                        {
                            switch (tDev.DeviceType)
                            {
                                case PrometheusDeviceTypes.PrometheusExporter:
                                    var tPS = new ThePrometheusExporter(tDev, this);
                                    TheThingRegistry.RegisterThing(tPS);
                                    break;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(181001, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, $"Error creating exporter instance {tDev?.cdeMID} / {tDev?.FriendlyName}", eMsgLevel.l1_Error, e.ToString()));
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
        public override void HandleMessage(ICDEThing sender, object pIncoming)
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
                    TheThing tt = TheThingRegistry.GetThingByProperty(MyBaseEngine.GetEngineName(), Guid.Empty, "ID", pMsg.Message.PLS);
                    if (tt != null)
                        tt.HandleMessage(this, pMsg);
                    break;
            }
        }
#endregion



    }
}
