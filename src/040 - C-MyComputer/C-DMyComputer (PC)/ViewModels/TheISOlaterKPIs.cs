// SPDX-FileCopyrightText: 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

﻿// TODO: Add reference for (1) C-DEngine.dll and (2) CDMyNMIHtml5.dll
// TODO: Make sure plugin file name starts with either CDMy or C-DMy
using nsCDEngine.BaseClasses;
using nsCDEngine.Communication;
using nsCDEngine.Engines;
using nsCDEngine.Engines.NMIService;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.ViewModels;
using System;
using System.Diagnostics;
using System.Management;

namespace CDMyComputer.ViewModels
{
    class TheISOlaterKPIs : TheThingBase
    {
        public const string eDeviceType = "ISOLater KPIs";

        // Base object references 
        private readonly IBaseEngine MyBaseEngine;    // Base engine (service)
        private readonly ICDEPlugin MyPlugin;

        // Initialization flags
        protected bool mIsInitStarted = false;
        protected bool mIsInitCompleted = false;
        protected bool mIsUXInitStarted = false;
        protected bool mIsUXInitCompleted = false;

        // User-interface definition
        TheFormInfo mMyForm;
        Process MyIsolator;

        public TheISOlaterKPIs(TheThing tBaseThing, ICDEPlugin pPluginBase, string pEngineName)
        {
            MyBaseThing = tBaseThing ?? new TheThing();
            MyBaseEngine = pPluginBase.GetBaseEngine();
            MyPlugin = pPluginBase;
            MyBaseThing.EngineName = MyBaseEngine.GetEngineName();
            MyBaseThing.DeviceType = eDeviceType;
            TheThing.SetSafePropertyString(MyBaseThing, "ISOLaterName", pEngineName);
            if (string.IsNullOrEmpty(MyBaseThing.FriendlyName))
                MyBaseThing.FriendlyName = pEngineName;
            MyBaseThing.SetIThingObject(this);
        }

        public override bool Init()
        {
            if (!mIsInitStarted)
            {
                mIsInitStarted = true;
                MyBaseEngine.RegisterEvent(eEngineEvents.ShutdownEvent, OnShutdown);
                TheThing.SetSafePropertyBool(MyBaseThing, "IsConnected", false);

                IBaseEngine tBase = TheThingRegistry.GetBaseEngine(TheThing.GetSafePropertyString(MyBaseThing, "ISOLaterName"), true);
                if (tBase != null)
                {
                    MyIsolator = tBase.GetISOLater() as Process;
                    if (MyIsolator == null)
                    {
                        MyBaseThing.LastMessage = "Engine not isolated";
                        MyBaseThing.StatusLevel = 3;
                        mIsInitCompleted = true;
                        return true;
                    }
                    MyBaseThing.LastMessage = "KPIs monitor ready";
                    MyBaseThing.StatusLevel = 1;
                    if (TheThing.GetSafePropertyBool(MyBaseThing, "AutoConnect"))
                        Connect();
                }
                else
                {
                    MyBaseThing.LastMessage = "Base Engine cloud not be located";
                    MyBaseThing.StatusLevel = 3;
                }
                mIsInitCompleted = true;
            }
            return true;
        }

        void OnShutdown(ICDEThing pEngine, object notused)
        {

        }

        public override bool CreateUX()
        {
            if (!mIsUXInitStarted)
            {
                mIsUXInitStarted = true;

                var tSF = TheNMIEngine.AddStandardForm(MyBaseThing, MyBaseThing.FriendlyName,18,null,null,0,"ISOLated Plugin KPIs");
                mMyForm = tSF["Form"] as TheFormInfo;
                var sBlock = TheNMIEngine.AddStatusBlock(MyBaseThing, mMyForm, 100);
                sBlock["Group"].SetParent(1);
                var cBlock = TheNMIEngine.AddConnectivityBlock(MyBaseThing, mMyForm, 200, (msg, DoConnect) =>
                {
                    if (DoConnect)
                        Connect();
                    else
                        Disconnect();
                });
                TheNMIEngine.DeleteFieldById(cBlock["Address"].cdeMID);
                cBlock["Group"].SetParent(1);

                TheNMIEngine.AddSmartControl(MyBaseThing, mMyForm, eFieldType.CollapsibleGroup, 300, 2, 0, "KPIs", null, new nmiCtrlCollapsibleGroup { ParentFld = 1, IsSmall = true });

                ShowKPI(310,"Up Time","UpTime",2000,1000,"seconds");
                ShowKPI(320, "Working Set (KB)", "WorkingSet",100000,50000);
                ShowKPI(330, "Handles", "Handles",10000,5000,"handles");
                ShowKPI(340, "Threads", "Threads",100,40,"count");
                ShowKPI(350, "CPU Time", "CPU",100,25,"%");
                ShowKPI(360, "Total CPU Time", "CPUTime", 100,70,"second");
                ShowKPI(370, "IO Bytes", "IORead", 100000,30000);
                mIsUXInitCompleted = true;
            }
            return true;
        }

        private void ShowKPI(int pFldOrder, string pLabel, string pUpdName, int MaxVal = 100, int AveVal = 50, string pUnits = "bytes")
        {
            TheNMIEngine.AddSmartControl(MyBaseThing, mMyForm, eFieldType.TileGroup, pFldOrder, 0, 0, null, null, new nmiCtrlTileGroup { ParentFld=300, TileWidth = 6 });
            TheNMIEngine.AddSmartControl(MyBaseThing, mMyForm, eFieldType.SmartLabel, pFldOrder+1, 0, 0, null, null, new nmiCtrlSmartLabel { ParentFld = pFldOrder,Text=pLabel, NoTE=true, TileFactorY = 2, TileHeight = 1, TileWidth=5, ClassName= "cdeTileGroupHeaderSmall" });
            var tBut=TheNMIEngine.AddSmartControl(MyBaseThing, mMyForm, eFieldType.TileButton, pFldOrder + 2, 2, 0, "V-Tise", null, new nmiCtrlTileButton { ParentFld = pFldOrder, NoTE = true, TileFactorY = 2, TileHeight = 1, TileWidth = 1, ClassName="cdeGlassButton" });
            tBut.RegisterUXEvent(MyBaseThing, eUXEvents.OnClick, $"But{pUpdName}", (sender, para) => 
            {
                TheThing tT = TheThingRegistry.GetThingByID(MyBaseEngine.GetEngineName(), $"ISO{MyBaseThing.cdeMID}:{pUpdName}");
                if (tT == null)
                {
                    TheKPIReport tRep = new TheKPIReport(null, MyPlugin);
                    TheThing.SetSafePropertyGuid(tRep, "RealSensorThing", MyBaseThing.cdeMID);
                    tRep.GetBaseThing().ID = $"ISO{MyBaseThing.cdeMID}:{pUpdName}";
                    MyBaseThing.GetProperty(pUpdName, true);
                    TheThing.SetSafePropertyString(tRep, "RealSensorProperty", pUpdName);
                    TheThing.SetSafePropertyNumber(tRep, "StateSensorMaxValue", MaxVal);
                    TheThing.SetSafePropertyNumber(tRep, "StateSensorAverage", AveVal);
                    TheThing.SetSafePropertyNumber(tRep, "StateSensorSteps", MaxVal / 15);
                    TheThing.SetSafePropertyString(tRep, "StateSensorValueName", pLabel);
                    TheThing.SetSafePropertyString(tRep, "StateSensorUnit", pUnits);
                    TheThing.SetSafePropertyString(tRep, "FriendlyName", MyBaseThing.FriendlyName);
                    TheThing.SetSafePropertyString(tRep, "ReportName", $"ISO-KPI: {MyBaseThing.FriendlyName} - {pLabel}");
                    TheThing.SetSafePropertyString(tRep, "ReportCategory", "ISO KPI Reports");
                    ThePluginInfo tI = MyPlugin.GetBaseEngine().GetPluginInfo();
                    if (tI != null)
                    {
                        TheThing.SetSafePropertyString(tRep, "SerialNumber", TheCommonUtils.CStr(tI.CurrentVersion));
                        TheThing.SetSafePropertyString(tRep, "VendorName", TheCommonUtils.CStr(tI.Developer));
                        TheThing.SetSafePropertyString(tRep, "ProductName", TheCommonUtils.CStr(tI.ServiceDescription));
                        TheThing.SetSafePropertyString(tRep, "ProductText", TheCommonUtils.CStr(tI.LongDescription));
                        TheThing.SetSafePropertyString(tRep, "VendorText", TheCommonUtils.CStr(tI.DeveloperUrl));
                        TheThing.SetSafePropertyString(tRep, "ProductID", TheCommonUtils.CStr(tI.cdeMID));
                    }
                    TheThingRegistry.RegisterThing(tRep);
                    MyBaseEngine.ProcessMessage(new TheProcessMessage(new TSM(MyBaseEngine.GetEngineName(), "REFRESH_DASH")));
                }
                else
                    TheCommCore.PublishToOriginator((para as TheProcessMessage).Message, new TSM(eEngineName.NMIService, "NMI_TTS", tT.cdeMID.ToString()));
            });
            TheNMIEngine.AddSmartControl(MyBaseThing, mMyForm, eFieldType.SmartLabel, pFldOrder+3, 0, 0, null, pUpdName, new nmiCtrlSingleEnded { ParentFld = pFldOrder, TileWidth = 2 });
            TheNMIEngine.AddSmartControl(MyBaseThing, mMyForm, eFieldType.BarChart, pFldOrder+4, 0, 0, null, pUpdName, new nmiCtrlBarChart() { ParentFld = pFldOrder, MaxValue = MaxVal, TileWidth = 4, NoTE = true, Foreground = "blue" });
        }

        /// <summary>
        /// Connect to the Thing
        /// </summary>
        public void Connect()
        {
            if (MyIsolator == null) return;
            readBytesSec = new PerformanceCounter("Process", "IO Other Bytes/sec", MyIsolator.ProcessName);
            TheThing.SetSafePropertyBool(MyBaseThing, "IsConnected", true);
            TheQueuedSenderRegistry.RegisterHealthTimer(sinkTimer);
        }
        PerformanceCounter readBytesSec;

        public void Disconnect()
        {
            TheQueuedSenderRegistry.UnregisterHealthTimer(sinkTimer);
            TheThing.SetSafePropertyBool(MyBaseThing, "IsConnected", false);
        }

        void sinkTimer(long t)
        {
            if (MyIsolator == null) return;
            if (MyIsolator.HasExited)
            {
                TheQueuedSenderRegistry.UnregisterHealthTimer(sinkTimer);
                MyBaseThing.StatusLevel = 0;
                MyBaseThing.LastMessage = "Plugin no longer running";
                return;
            }
            try
            {
                TheThing.SetSafePropertyString(MyBaseThing, "IORead", string.Format("{0:0.00}", readBytesSec?.NextValue()));
                //if ((t%15)!=0) return;
                TheThing.SetSafePropertyString(MyBaseThing, "CPUTime", string.Format("{0:0.00}", MyIsolator.TotalProcessorTime.TotalSeconds));
                TheThing.SetSafePropertyNumber(MyBaseThing, "WorkingSet", MyIsolator.WorkingSet64 / 1024);
                TheThing.SetSafePropertyNumber(MyBaseThing, "Handles", MyIsolator.HandleCount);
                TheThing.SetSafePropertyNumber(MyBaseThing, "Threads", MyIsolator.Threads.Count);
                TimeSpan ts = DateTime.Now.Subtract(MyIsolator.StartTime);
                TheThing.SetSafePropertyNumber(MyBaseThing, "UpTime", ts.TotalMinutes);
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("root\\CIMV2", $"SELECT * FROM Win32_PerfFormattedData_PerfProc_Process where IDProcess='{MyIsolator.Id}'"))
                {

                    foreach (ManagementObject queryObj in searcher.Get())
                    {
                        TheThing.SetSafePropertyString(MyBaseThing, "CPU", queryObj["PercentProcessorTime"].ToString());
                        MyBaseThing.Value = queryObj["PercentProcessorTime"].ToString();
                        //foreach (var ttt in queryObj.Properties)
                        //{
                        //    Console.WriteLine($"{ttt.Name}: {ttt.Value}");
                        //}
                    }
                }
            }
            catch (Exception)
            {
            }
        }
    }
}
