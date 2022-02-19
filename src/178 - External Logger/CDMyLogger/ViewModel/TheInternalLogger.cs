// SPDX-FileCopyrightText: 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0
using nsCDEngine.BaseClasses;
using nsCDEngine.Engines;
using nsCDEngine.Engines.NMIService;
using nsCDEngine.Engines.StorageService;
using nsCDEngine.Engines.ThingService;
using System;
using System.Collections.Generic;
using System.Text;

namespace CDMyLogger.ViewModel
{
    internal class TheInternalLogger : TheLoggerBase
    {
        public TheInternalLogger(TheThing tBaseThing, ICDEPlugin pPluginBase) : base(tBaseThing, pPluginBase)
        {
            MyBaseThing.DeviceType = eTheLoggerServiceTypes.InternalLogger;
        }
        protected override void DoInit()
        {
            TheBaseEngine.WaitForStorageReadiness(sinkStorageStationIsReadyFired, true);
        }
        public override bool CreateUX()
        {
            if (!mIsUXInitCalled)
            {
                mIsUXInitCalled = true;

                var tFormGuid = new TheFormInfo(MyBaseThing) { FormTitle = "Event Log", defDataSource = "EventLog", IsReadOnly = true, IsNotAutoLoading = true, PropertyBag = new nmiCtrlTableView { ShowFilterField = true } };
                TheNMIEngine.AddFormToThingUX(MyBaseThing, tFormGuid, "CMyTable", "Event Log", 6, 3, 128, $"..Event Logs on {TheCommonUtils.GetMyNodeName()}", null, new ThePropertyBag { "Thumbnail=FA5:f073" }); //;:;50;:;True
                                                                                                                                                                                                                   //TheNMIEngine.AddForm(tFormGuid);
                TheNMIEngine.AddFields(tFormGuid, new List<TheFieldInfo> {
                {  new TheFieldInfo() { FldOrder=5,DataItem="EventCategory",Flags=0,Type=eFieldType.DateTime,Header="Category",FldWidth=2 }},
                {  new TheFieldInfo() { FldOrder=10,DataItem="EventTime",Flags=0,Type=eFieldType.DateTime,Header="Event Time",FldWidth=2 }},
                {  new TheFieldInfo() { FldOrder=20,DataItem="StationName",Flags=0,Type=eFieldType.SingleEnded,Header="Node Name",FldWidth=2 }},
                {  new TheFieldInfo() { FldOrder=30,DataItem="EventName",Flags=0,Type=eFieldType.SingleEnded,Header="Event Name",FldWidth=2 }},
                {  new TheFieldInfo() { FldOrder=40,DataItem="EventString",Flags=0,Type=eFieldType.SingleEnded,Header="Event",FldWidth=4 }},
                {  new TheFieldInfo() { FldOrder=50,DataItem="EventTrigger",Flags=0,Type=eFieldType.SingleEnded,Header="Trigger Object",FldWidth=2 }},
                {  new TheFieldInfo() { FldOrder=60,DataItem="ActionObject",Flags=0,Type=eFieldType.SingleEnded,Header="Action Object",FldWidth=2 }},
                });
                mIsUXInitialized = true;
            }
            return true;
        }

        private void sinkStorageStationIsReadyFired(ICDEThing sender, object pReady)
        {
            if (pReady != null)
            {
                if (MyRuleEventLog == null)
                {
                    MyRuleEventLog = new TheStorageMirror<TheEventLogData>(TheCDEngines.MyIStorageService);
                    MyRuleEventLog.CacheTableName = "EventLog";
                    MyRuleEventLog.UseSafeSave = true;
                    MyRuleEventLog.SetRecordExpiration(604800, null);
                    MyRuleEventLog.CacheStoreInterval = 15;
                    MyRuleEventLog.IsStoreIntervalInSeconds = true;
                    MyRuleEventLog.AppendOnly = true;

                    if (!MyRuleEventLog.IsRAMStore)
                        MyRuleEventLog.CreateStore("The Event Log", "History of events fired by the RulesEngine", null, true, TheBaseAssets.MyServiceHostInfo.IsNewDevice);
                    else
                        MyRuleEventLog.InitializeStore(true, TheBaseAssets.MyServiceHostInfo.IsNewDevice);
                    LogEvent(new TheEventLogData
                    {
                        EventCategory = eLoggerCategory.EngineEvent,
                        EventTime = DateTimeOffset.Now,
                        StationName = TheBaseAssets.MyServiceHostInfo?.GetPrimaryStationURL(false),
                        EventName = "Event Log Started"
                    });
                }
            }
        }
        internal TheStorageMirror<TheEventLogData> MyRuleEventLog;   //The Storage Container for data to store

        public override bool LogEvent(TheEventLogData pData)
        {
            if (!MyRuleEventLog.IsReady) return false;
            MyRuleEventLog.AddAnItem(pData);
            return true;
        }
    }
}
