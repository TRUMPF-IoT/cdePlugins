// SPDX-FileCopyrightText: 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

﻿using CDMyComputer.ViewModels;
using nsCDEngine.BaseClasses;
using nsCDEngine.Communication;
using nsCDEngine.Engines;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.ViewModels;
using System;
using System.Collections.Generic;

namespace CDMyComputer
{
    public partial class TheCDMyComputerEngine
    {
        private void sinkEngineShutdown(ICDEThing sender, object NOP)
        {
            if (MyServiceHealth != null)
                MyServiceHealth.Shutdown();
            MyBaseThing.UnregisterEvent(eEngineEvents.ShutdownEvent, sinkEngineShutdown);
            MyBaseThing.UnregisterEvent(eEngineEvents.IncomingMessage, HandleMessage);        //Event when C-DEngine has new Telegram for this service as a subscriber (Client Side)
        }

        /// <summary>
        /// Engine Started is fired when a new Engine has been Started by the C-DEngine
        /// </summary>
        private void StartEngineServices()
        {
            if (MyServiceHealth == null) 
            {
                TheCommonUtils.cdeRunAsync("InitHealthService", true, o => 
                { 
                    MyServiceHealth = new TheHealthMonitor(HealthCollectionCycle, this, IsHealthCollectionOff);
                    MyServiceHealth.eventNewHealthData += sinkNewData;
                });
                if (TheCommCore.MyHttpService != null)
                {
                    TheCommCore.MyHttpService.RegisterStatusRequest(sinkGetStatus);
                }
            }
            TheCDEngines.eventAllEnginesStarted += sinkRegisterIsos;

            MyBaseEngine.ProcessInitialized();
            MyBaseEngine.SetStatusLevel(1);
        }

        void sinkRegisterIsos()
        {
            var tEngs = TheThingRegistry.GetBaseEnginesAsThing(false, true);
            foreach (var t in tEngs)
            {
                if (TheThing.GetSafePropertyBool(t, "IsIsolated"))
                {
                    IBaseEngine tBase = TheThingRegistry.GetBaseEngine(t, true);
                    if (tBase != null && tBase.GetISOLater() != null)
                    {
                        TheThing tT = TheThingRegistry.GetThingByFunc(MyBaseEngine.GetEngineName(), s => s.DeviceType == TheISOlaterKPIs.eDeviceType && TheThing.GetSafePropertyString(s, "ISOLaterName") == t.EngineName);
                        var tKPI = new TheISOlaterKPIs(tT, this, t.EngineName);
                        TheThingRegistry.RegisterThing(tKPI);
                    }
                }
            }
            List<TheThing> tDevList = TheThingRegistry.GetThingsByProperty(MyBaseEngine.GetEngineName(),Guid.Empty,"DeviceType",TheKPIReport.eDeviceType);
            if (tDevList.Count > 0)
            {
                foreach (TheThing tDev in tDevList)
                {
                    if (tDev.GetObject() == null)
                    {
                        TheKPIReport cs = new TheKPIReport(tDev, this);
                        TheThingRegistry.RegisterThing(cs);
                    }
                }
            }
        }

        private void sinkNewData(TheServiceHealthData pHealth)
        {
            List<TheChartPoint> tb = new List<TheChartPoint>{
                new TheChartPoint { value = pHealth.CPULoad.cdeTruncate(2), name = "CPU Load" },
                new TheChartPoint { value = pHealth.cdeLoad.cdeTruncate(2), name = "CDE Load" }
            };
            //TheThing.SetSafePropertyNumber(MyBaseThing, "LoadBucket", pHealth.CPULoad.cdeTruncate(2));
            TheThing.SetSafePropertyString(MyBaseThing, "LoadBucket", TheCommonUtils.SerializeObjectToJSONString(tb));// pHealth.cdeLoad.cdeTruncate(2) +";1");
            MyServiceHealth.HealthUpdateCycle = HealthCollectionCycle;
        }

        private void sinkGetStatus(TheRequestData pReq)
        {
            if (MyServiceHealth == null) return;
            if (string.IsNullOrEmpty(pReq.ResponseBufferStr)) pReq.ResponseBufferStr = "";
        }


        private TheHealthMonitor MyServiceHealth;

        /// <summary>
        /// Handles Messages sent from a host sub-engine to its clients
        /// </summary>
        /// <param name="Command"></param>
        /// <param name="pMessage"></param>
        public void HandleMessage(ICDEThing sender, object pIncoming)
        {
            if (!(pIncoming is TheProcessMessage pMsg)) return;
            try
            {
                string[] cmd = pMsg.Message.TXT.Split(':');

                switch (cmd[0]) //string 2 cases
                {
                    case "CDE_INITIALIZED":
                        MyBaseEngine.SetInitialized(pMsg.Message);
                        return;
                    case "GET_NODECPUINFO":
                        if (MyServiceHealth != null)
                            //MyServiceHealth.SendCPUInfo(pMsg.Message.GetOriginator());
                            MyServiceHealth.SendCPUInfo(pMsg.Message);
                        break;
                    case "GET_NODEISMHEALTH":
                        if (MyServiceHealth != null)
                            //MyServiceHealth.SendHealthInfo(pMsg.Message.GetOriginator());
                            MyServiceHealth.SendHealthInfo(pMsg.Message);
                        break;
                    case "REFRESH_DASH":
                        MyPCVitalsDashboard.Reload(pMsg, true);
                        break;
                    default:
                        break;
                }
            }
            catch (Exception e)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(8032, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(MyBaseEngine.GetEngineName(), "ProcessServiceMessage Error", eMsgLevel.l1_Error, e.ToString()));
            }
        }
    }
}
