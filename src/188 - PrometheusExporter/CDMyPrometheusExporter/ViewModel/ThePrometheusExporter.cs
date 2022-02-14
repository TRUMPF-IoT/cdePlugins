// SPDX-FileCopyrightText: 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

/*********************************************************************
*
* Project Name" 095-CDMyCloudServices
*
* Description: 
*
* Date of creation: 
*
* Author: 
*
* NOTES:
*               "FldOrder" for UX 10 to 
*********************************************************************/
//#define TESTDIRECTUPDATES
using nsCDEngine.BaseClasses;
using nsCDEngine.Communication;
using nsCDEngine.Engines;
using nsCDEngine.Engines.NMIService;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.ViewModels;
using nsTheEventConverters;
using nsTheSenderBase;
using Prometheus;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CDMyPrometheusExporter.ViewModel
{
    public class ThePrometheusExporter: TheSenderBase
    {

        #region ThingProperties

        public string HttpTargetUrl
        {
            get { return TheThing.GetSafePropertyString(MyBaseThing, nameof(HttpTargetUrl)); }
            set { TheThing.SetSafePropertyString(MyBaseThing, nameof(HttpTargetUrl), value); }
        }


        public int KPILogIntervalInMinutes
        {
            get { return (int) TheThing.GetSafePropertyNumber(MyBaseThing, nameof(KPILogIntervalInMinutes)); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, nameof(KPILogIntervalInMinutes), value); }
        }

        public int WatchDogIdleRecycleIntervalInMinutes
        {
            get { return (int)TheThing.GetSafePropertyNumber(MyBaseThing, nameof(WatchDogIdleRecycleIntervalInMinutes)); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, nameof(WatchDogIdleRecycleIntervalInMinutes), value); }
        }

        public string KPIPublishPropertyName
        {
            get { return TheThing.GetSafePropertyString(MyBaseThing, "KPIPublishPropertyName"); }
            set { TheThing.SetSafePropertyString(MyBaseThing, "KPIPublishPropertyName", value); }
        }

        public bool EnableLogSentPayloads
        {
            get { return TheThing.GetSafePropertyBool(MyBaseThing, nameof(EnableLogSentPayloads)); }
            set { TheThing.SetSafePropertyBool(MyBaseThing, nameof(EnableLogSentPayloads), value); }
        }

        #endregion

        public override void HandleMessage(ICDEThing sender, object pIncoming)
        {
            TheProcessMessage pMsg = pIncoming as TheProcessMessage;
            if (pMsg == null || pMsg.Message == null) return;
            var cmd = pMsg.Message.TXT.Split(':');
            switch (cmd[0])
            {
                default:
                    base.HandleMessage(sender, pIncoming);
                    break;
            }
        }

        public ThePrometheusExporter(TheThing pThing, ICDEPlugin pPluginBase) : base(pThing, pPluginBase)
        {
        }

        const string strPrometheusExporter = "Prometheus Exporter";

        public override bool Init()
        {
            base.Init();
            if (_registry == null)
            {
                _registry = Metrics.DefaultRegistry;
            }
            if (string.IsNullOrEmpty(MyBaseThing.Address))
            {
                MyBaseThing.Address = "metrics";
            }
            MyBaseThing.RegisterOnChange("LastSendAttemptTime", OnSendAttempt);

            var result = InitBase(PrometheusDeviceTypes.PrometheusExporter);

            return IsInit();
        }

        DateTimeOffset lastKPILogTime = DateTimeOffset.MinValue;
        private void OnSendAttempt(cdeP notUsed)
        {
            if (KPILogIntervalInMinutes > 0 && DateTimeOffset.Now - lastKPILogTime > new TimeSpan(0, 0, KPILogIntervalInMinutes, 0))
            {
                lastKPILogTime = DateTimeOffset.Now - new TimeSpan(0, 0, 0, 5);  // Give a few seconds leeway so the next timer doesn't miss the window
                LogKPIs();
            }
        }

        private void LogKPIs()
        {
            var kpis = new Dictionary<string, object>();
            foreach (var kpiPropName in new List<string> {
                nameof(EventsSentSinceStart), nameof(PendingEvents), nameof(EventsSentErrorCountSinceStart),
                nameof(LastSendTime), nameof(LastSendAttemptTime),
            })
            {
                kpis.Add(kpiPropName, TheThing.GetSafeProperty(MyBaseThing, kpiPropName));
            }
            var message = new TSM(strPrometheusExporter, "Prometheus Exporter KPIs", eMsgLevel.l4_Message, TheCommonUtils.SerializeObjectToJSONString(kpis));

            TheBaseAssets.MySYSLOG.WriteToLog(95013, TSM.L(eDEBUG_LEVELS.OFF) ? null : message);
            if (!String.IsNullOrEmpty(KPIPublishPropertyName))
            {
                foreach (var senderThing in MySenderThings.MyMirrorCache.TheValues)
                {
                    TheThing.SetSafeProperty(senderThing.GetThing(), KPIPublishPropertyName, new TSMObjectPayload(message, kpis), ePropertyTypes.NOCHANGE); // CODE REVIEW: OK to put an entire message here? This will be serialized to JSON properly, but...
                }
            }
        }

        // Helper class to generate JSON with the KPI as JSON, not an embedded string (PLS). TODO v4: Move this capability into TSM? V3.2: Move to common utils?
        class TSMObjectPayload : TSM
        {
            public TSMObjectPayload(TSM tsm, object payloadObject)
            {
                this.PLO = payloadObject;
                this.PLS = null;

                this.TXT = tsm.TXT;
                this.TIM = tsm.TIM;
                this.FLG = tsm.FLG;
                this.ORG = tsm.ORG;//ORG-OK
                this.QDX = tsm.QDX;
                this.LVL = tsm.LVL;
                this.ENG = tsm.ENG;
                this.FID = tsm.FID;
                this.SID = tsm.SID;
                this.SEID = tsm.SEID;
                this.CST = tsm.CST;
                this.UID = tsm.UID;
                this.OWN = tsm.OWN;
                this.PLB = tsm.PLB;

            }
            public object PLO;
        }


        private async void OnWatchDogTimer(object state)
        {
            try
            {
                OnSendAttempt(null);
                var timeSinceLastAttempt = DateTimeOffset.Now - LastSendAttemptTime;
                if (timeSinceLastAttempt > new TimeSpan(0, 0, WatchDogIdleRecycleIntervalInMinutes, 0))
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(95014, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(strPrometheusExporter, $"WatchDog: No activity since {LastSendAttemptTime}. Disconnecting Sender and waiting 5 seconds to reconnect.", eMsgLevel.l4_Message));
                    Disconnect(true);
                    try
                    {
                        await TheCommonUtils.TaskDelayOneEye(5000, 100);
                    }
                    catch (TaskCanceledException) { }

                    if (TheBaseAssets.MasterSwitch)
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(95015, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(strPrometheusExporter, $"WatchDog: Reconnecting Sender.", eMsgLevel.l4_Message));
                        Connect();
                    }
                }
            }
            catch (Exception ex)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(95302, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(strPrometheusExporter, $"WatchDog: Internal error.", eMsgLevel.l1_Error, ex.ToString()));

            }
        }

        public override bool CreateUX()
        {
            if (mIsUXInitCalled) return false;
            mIsUXInitCalled = true;

            TheThing.SetSafePropertyString(MyBaseThing, "StateSensorValueName", "Number of Exports");
            TheThing.SetSafePropertyString(MyBaseThing, "StateSensorUnit", "");
            var t = TheNMIEngine.AddStandardForm(MyBaseThing, $"FACEPLATE", 18);
            MyForm = t["Form"] as TheFormInfo;
            var tStatBlock = TheNMIEngine.AddStatusBlock(MyBaseThing, MyForm, 10);
            tStatBlock["Group"].SetParent(1);
            var tConnBlock = TheNMIEngine.AddStartingBlock(MyBaseThing, MyForm, 100, (pMsg, DoConnect) =>
            {
                if (DoConnect)
                {
                    Connect();
                }
                else
                {
                    Disconnect(true);
                }
            }, 192,"IsConnected", "AutoConnect");
            TheNMIEngine.AddSmartControl(MyBaseThing, MyForm, eFieldType.SingleEnded, 110, 2, 0x80, "Host Path", "Address", new nmiCtrlSingleEnded() { ParentFld = 100, PlaceHolder = "###Address of service###" });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyForm, eFieldType.Number, 9991, 0, 0x80, "QV", "QValue", new nmiCtrlSingleEnded() { Visibility=false });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyForm, eFieldType.Number, 9992, 0, 0x80, "SSU", "StateSensorUnit", new nmiCtrlSingleEnded() { Visibility = false });
            tConnBlock["Group"].SetParent(1);
            TheNMIEngine.DeleteFieldById(tStatBlock["Value"].cdeMID);

            #region SenderThings
            // Form for the Things for which events are to be sent to the cloud
            {
                string tDataSource = "TheSenderThings";
                if (MySenderThings != null)
                    tDataSource = MySenderThings.StoreMID.ToString();
                tSenderThingsForm = new TheFormInfo(TheThing.GetSafeThingGuid(MyBaseThing, "SenderThings_ID"), eEngineName.NMIService, "Things to export to Prometheus", tDataSource) { AddButtonText = "New Export Definition" };
                TheNMIEngine.AddFormToThingUX(MyBaseThing, tSenderThingsForm, "CMyTable", "Export List", 1, 3, 0xF0, null, null, new ThePropertyBag() { "Visibility=false" });
                TheNMIEngine.AddFields(tSenderThingsForm, new List<TheFieldInfo> {
                    new TheFieldInfo() { FldOrder=11,DataItem="Disable",Flags=2,Type=eFieldType.SingleCheck,Header="Disable",FldWidth=1,  DefaultValue="true" },
                    new TheFieldInfo() { FldOrder=12,DataItem="ChangeNaNToNull",Flags=2,Type=eFieldType.SingleCheck,Header="Dont Send Zeros",FldWidth=1 },

                    new TheFieldInfo() { FldOrder=20,DataItem="ThingMID",Flags=2, cdeA = 0xC0, Type=eFieldType.ThingPicker,Header="Thing to Export",PropertyBag=new nmiCtrlThingPicker() { IncludeEngines=true, FldWidth=3 } },
                    new TheFieldInfo() { FldOrder=30,DataItem=nameof(TheSenderThing.EngineName), Flags=2, cdeA = 0xC0, Type=eFieldType.ThingPicker,Header="Engine Name",FldWidth=3,  PropertyBag=new nmiCtrlThingPicker() { ValueProperty="EngineName", IncludeEngines=true, Filter="DeviceType=IBaseEngine", FldWidth=3 } },
                    new TheFieldInfo() { FldOrder=35,DataItem=nameof(TheSenderThing.DeviceType), Flags=2, cdeA = 0xC0, Type=eFieldType.DeviceTypePicker,Header="DeviceType",FldWidth=3,  PropertyBag=new nmiCtrlDeviceTypePicker() { Filter="EngineName=%EngineName%", FldWidth=2 } },

                    new TheFieldInfo() { FldOrder=40,DataItem="TargetType",Flags=2, cdeA = 0xC0, Type=eFieldType.ComboBox,Header="Metric Type",  PropertyBag=new nmiCtrlComboBox() { DefaultValue="Gauge", Options="Gauge;Counter;Histogram;Summary", FldWidth=1 } },
                    new TheFieldInfo() { FldOrder=41,DataItem="PropertiesIncluded",Flags=2, cdeA = 0xC0, Type=eFieldType.PropertyPicker,Header="Properties to Export", PropertyBag=new nmiCtrlPropertyPicker() { DefaultValue="Value", Separator=",", AllowMultiSelect=true, ThingFld=20,FldWidth=4 } },
                    //new TheFieldInfo() { FldOrder=42,DataItem="PropertiesExcluded",Flags=2, cdeA = 0xC0, Type=eFieldType.PropertyPicker,Header="Properties to Exclude",  PropertyBag=new nmiCtrlPropertyPicker() { Separator=",", AllowMultiSelect=true, ThingFld=20, FldWidth=4 } },
                    new TheFieldInfo() { FldOrder=43,DataItem="PartitionKey",Flags=2, cdeA = 0xC0, Type=eFieldType.PropertyPicker,Header="Properties for labels", PropertyBag=new nmiCtrlPropertyPicker() { FldWidth = 4, AllowMultiSelect=true, ThingFld=20, DefaultValue="NodeId,FriendlyName", Separator=",", SystemProperties=true } },
                });
                TheNMIEngine.AddTableButtons(tSenderThingsForm, false, 100);
            }
            TheNMIEngine.AddSmartControl(MyBaseThing, MyForm, eFieldType.TileButton, 80, 2, 0xF0, "List of Exported Things", null, new nmiCtrlTileButton() { OnClick = $"TTS:{tSenderThingsForm.cdeMID}", ClassName = "cdeTransitButton", TileWidth = 6, NoTE = true, ParentFld = 10 });
            #endregion

            TheNMIEngine.AddSmartControl(MyBaseThing, MyForm, eFieldType.CollapsibleGroup, 400, 2, 0, "KPIs", false, null, null, new nmiCtrlCollapsibleGroup() { IsSmall = true, DoClose = true, TileWidth = 6, ParentFld = 1 });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyForm, eFieldType.DateTime, 414, 0, 0, "Last Export", nameof(LastSendTime), new ThePropertyBag() { "ParentFld=400", "TileWidth=6", "TileHeight=1" });
            //TheNMIEngine.AddSmartControl(MyBaseThing, MyForm, eFieldType.DateTime, 415, 0, 0, "Last Send Attempt", nameof(LastSendAttemptTime), new ThePropertyBag() { "ParentFld=400", "TileWidth=6", "TileHeight=1" });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyForm, eFieldType.Number, 416, 0, 0, "Exports Sent", nameof(EventsSentSinceStart), new ThePropertyBag() { "ParentFld=400", "TileWidth=6", "TileHeight=1" });
            //TheNMIEngine.AddSmartControl(MyBaseThing, MyForm, eFieldType.Number, 417, 0, 0, "Events Pending", nameof(PendingEvents), new ThePropertyBag() { "ParentFld=400", "TileWidth=6", "TileHeight=1" });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyForm, eFieldType.Number, 418, 0, 0, "Export Error Count", nameof(EventsSentErrorCountSinceStart), new ThePropertyBag() { "ParentFld=400", "TileWidth=6", "TileHeight=1" });

            var tKPIBut = TheNMIEngine.AddSmartControl(MyBaseThing, MyForm, eFieldType.TileButton, 425, 2, 0xC0, "Reset KPIs", null, new nmiCtrlTileButton() { ParentFld = 400, NoTE = true, ClassName = "cdeBadActionButton" });
            tKPIBut.RegisterUXEvent(MyBaseThing, eUXEvents.OnClick, "ResetKPI", (sender, para) => {
                PendingEvents = 0;
                EventsSentErrorCountSinceStart = 0;
                EventsSentSinceStart = 0;
            });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyForm, eFieldType.CollapsibleGroup, 430, 2, 0xC0, "KPI Logging", false, null, null, new nmiCtrlCollapsibleGroup() { IsSmall = true, DoClose = true, TileWidth = 6, ParentFld = 400 });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyForm, eFieldType.SingleEnded, 432, 2, 0xC0, "KPI Property Name", nameof(KPIPublishPropertyName), new nmiCtrlSingleEnded { ParentFld = 430, TileHeight = 1, TileWidth = 6 });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyForm, eFieldType.SingleCheck, 435, 2, 0xC0, "Log Sent Payload Data", nameof(EnableLogSentPayloads), new nmiCtrlSingleCheck { ParentFld = 430, TileHeight = 1, TileWidth = 3 });
            mIsUXInitialized = true;
            return true;
        }

        private TheREST _client;

        protected override bool DoConnect()
        {
            HoldOffOnSenderLoop = true; // Not doing push gateway yet

            TheCommCore.MyHttpService.RegisterHttpInterceptorB4(GetUrlTrimmed(), onPrometheusHttpRequest);

            if (KPILogIntervalInMinutes > 0)
            {
                myWatchDogTimer = new Timer(OnWatchDogTimer, null, KPILogIntervalInMinutes * 60 * 1000, KPILogIntervalInMinutes * 60 * 1000);
            }

            _client = new TheREST();
            return true;
        }

        private string GetUrlTrimmed()
        {
            if (string.IsNullOrEmpty(MyBaseThing.Address))
            {
                MyBaseThing.Address = "metrics";
            }
            return $"/{MyBaseThing.Address.Trim(new char[] { '/' })}";
        }

        CollectorRegistry _registry;

        DateTimeOffset _lastPollTime = DateTimeOffset.MinValue;
        TimeSpan maxMetricSampleRate = new TimeSpan(0, 0, 5);
        private void onPrometheusHttpRequest(TheRequestData tRequest)
        {
            try
            {
                tRequest.AllowStatePush = false; //No need to track state
                if (HoldOffOnSenderLoop && (DateTimeOffset.Now - _lastPollTime) > maxMetricSampleRate)
                {
                    // Using polling: generate all metrics now
                    GenerateMetrics();
                    _lastPollTime = DateTimeOffset.Now;
                }

                using (var outputStream = new MemoryStream())
                {

                    try
                    {
                        _registry.CollectAndExportAsTextAsync(outputStream, TheBaseAssets.MasterSwitchCancelationToken).Wait();
                    }
                    catch (ScrapeFailedException ex)
                    {
                        tRequest.StatusCode = 500;
                        EventsSentErrorCountSinceStart++;

                        if (!string.IsNullOrWhiteSpace(ex.Message))
                        {
                            tRequest.ResponseBuffer = Encoding.UTF8.GetBytes(ex.Message);
                        }

                        return;
                    }

                    if (tRequest.Header.TryGetValue("Accept", out var acceptHeader))
                    {
                        var acceptHeaders = acceptHeader?.Split(',');
                        var contentType = "text/plain";
                        tRequest.ResponseMimeType = contentType;

                        tRequest.StatusCode = 200;

                        tRequest.ResponseBuffer = outputStream.ToArray();
                        EventsSentSinceStart++;
                        TheThing.SetSafePropertyNumber(MyBaseThing, "QValue", EventsSentSinceStart);
                        LastSendTime = DateTimeOffset.Now;
                        TheThing.SetSafePropertyString(MyBaseThing, "StateSensorUnit", TheCommonUtils.GetDateTimeString(LastSendTime, -1));
                    }
                    else
                    {
                        tRequest.StatusCode = 406;
                        tRequest.ResponseBuffer = Encoding.UTF8.GetBytes("No accept header");
                        EventsSentErrorCountSinceStart++;
                        return;
                    }
                }
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                EventsSentErrorCountSinceStart++;
                TheBaseAssets.MySYSLOG.WriteToLog(95307, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(strPrometheusExporter, $"Error in metric server: {this.MyBaseThing?.Address}", eMsgLevel.l3_ImportantMessage, ex.ToString()));
                try
                {
                    tRequest.StatusCode = 500;
                }
                catch
                {
                    // Might be too late in request processing to set response code, so just ignore.
                }
            }
        }
        
        cdeConcurrentDictionary<string, Prometheus.Counter> myCountersByMetricName = new cdeConcurrentDictionary<string, Prometheus.Counter>();
        cdeConcurrentDictionary<string, Prometheus.Gauge> myGaugesByMetricName = new cdeConcurrentDictionary<string, Prometheus.Gauge>();
        cdeConcurrentDictionary<string, Prometheus.Histogram> myHistogramsByMetricName = new cdeConcurrentDictionary<string, Prometheus.Histogram>();
        cdeConcurrentDictionary<string, Prometheus.Summary> mySummariesByMetricName = new cdeConcurrentDictionary<string, Prometheus.Summary>();

        private void GenerateMetrics()
        {
            foreach (var senderThing in MySenderThings.TheValues)
            {
                if (senderThing.Disable)
                {
                    continue;
                }
                List<TheThing> thingsToSend = null;
                {
                    var tThing = senderThing.GetThing();
                    if (tThing == null)
                    {
                        if (!string.IsNullOrEmpty(senderThing.EngineName))
                        {
                            thingsToSend = TheThingRegistry.GetThingsOfEngine(senderThing.EngineName, true, true);
                            if (!string.IsNullOrEmpty(senderThing.DeviceType))
                            {
                                thingsToSend.RemoveAll(t => t.DeviceType != senderThing.DeviceType);
                            }
                        }
                        else
                        {
                            if (!string.IsNullOrEmpty(senderThing.DeviceType))
                            {
                                thingsToSend = TheThingRegistry.GetThingsByFunc("*", t => t.DeviceType.Equals(senderThing.DeviceType, StringComparison.Ordinal), true);
                            }
                        }
                    }
                    else
                    {
                        thingsToSend = new List<TheThing> { tThing };
                    }
                }
                if (thingsToSend == null || thingsToSend.Count == 0)
                {
                    continue;
                }
                //if (string.IsNullOrEmpty(senderThing.PropertiesIncluded)) senderThing.PropertiesIncluded = "Value"; //Make sure we have at least one included
                var propsIncludedConf = string.IsNullOrEmpty(senderThing.PropertiesIncluded) ? null : TheCommonUtils.cdeSplit(senderThing.PropertiesIncluded, ',', false, false);
                var propsExcluded = string.IsNullOrEmpty(senderThing.PropertiesExcluded) ? null : TheCommonUtils.cdeSplit(senderThing.PropertiesExcluded, ',', false, false);
                var propsIncludedSplit = propsIncludedConf?.Select(p => TheCommonUtils.cdeSplit(p, ';', false, false));
                var propsIncluded = propsIncludedSplit?.Select(p => p[0]);

                foreach (var tThing in thingsToSend)
                {

                    // Capture the metrics as specified in the sender thing
                    var tMetricThing = nsCDEngine.ViewModels.TheThingStore.CloneFromTheThing(tThing, false, true, true, propsIncluded, propsExcluded);

                    // Unpack any JSON objects (i.e. DeviceGateLogMesh) into separate properties

                    foreach (var tMetric in tMetricThing.PB.ToList())
                    {
                        if (tMetric.Value?.ToString().Contains("{") == true)
                        {
                            var metricValue = tMetric.Value.ToString().Trim();
                            if (metricValue.StartsWith("{") && metricValue.EndsWith("}"))
                            {
                                try
                                {
                                    var jsonBag = TheCommonUtils.DeserializeJSONStringToObject<Dictionary<string, object>>(metricValue);
                                    if (jsonBag?.Count > 0)
                                    {
                                        if (jsonBag.TryGetValue("PLO", out var payload))
                                        {
                                            // Unpack the PLO in case the payload is a KPI TSM
                                            try
                                            {
                                                var payloadBag = TheCommonUtils.DeserializeJSONStringToObject<Dictionary<string, object>>(payload.ToString());
                                                if (payloadBag != null && payloadBag.Count > 0)
                                                {
                                                    jsonBag = payloadBag as Dictionary<string, object>;
                                                }
                                            }
                                            catch { }
                                        }
                                        foreach (var prop in jsonBag)
                                        {
                                            var metricPrefix = $"[{tMetric.Key.Trim(new char[] { '[', ']' })}]";
                                            var metricName = $"{metricPrefix}.[{prop.Key}]";
                                            if (propsIncluded?.Contains(metricName) == true || propsIncluded?.Contains($"{metricPrefix}.*") == true)
                                            {
                                                tMetricThing.PB[metricName] = prop.Value;
                                            }
                                        }
                                        if (tMetricThing.PB.ContainsKey(tMetric.Key))
                                        {
                                            tMetricThing.PB.Remove(tMetric.Key);
                                        }
                                    }
                                }
                                catch { }
                            }
                        }
                    }

                    foreach (var tMetric in tMetricThing.PB)
                    {
                        string metricName = "";
                        try
                        {
                            var propertyName = tMetric.Key;
                            var metricInfo = propsIncludedSplit.FirstOrDefault(ps => ps[0] == propertyName);
                            if (metricInfo != null && metricInfo.Length > 1)
                            {
                                metricName = metricInfo[1];
                            }
                            else
                            {
                                metricName = GetValidLabelName(tMetric.Key);
                            }

                            var valueToReport = GetMetricValue(tMetric.Value);
                            if (senderThing.ChangeNaNToNull && valueToReport == 0) //CM: closest reuse of disable sending of null/0
                                continue;
                            var labels = GetMetricLabels(senderThing, propertyName, metricName);

                            switch (senderThing.TargetType?.ToLowerInvariant())
                            {
                                case "counter":
                                    {
                                        var counter = myCountersByMetricName.GetOrAdd(metricName,
                                        (s) =>
                                        {
                                            Prometheus.Counter cs;
                                            try
                                            {
                                                cs = Metrics.CreateCounter(s, "", labels);
                                                return cs;
                                            }
                                            catch (Exception e)
                                            {
                                                TheBaseAssets.MySYSLOG.WriteToLog(95307, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(strPrometheusExporter, $"Error creating metric {s} in server: {this.MyBaseThing?.Address}", eMsgLevel.l1_Error, e.ToString()));
                                            }
                                            return null;
                                        });
                                        if (counter != null)
                                        {
                                            if (counter.LabelNames.Length > 0)
                                            {
                                                var labelValues = GetLabelValues(tThing, counter.LabelNames);
                                                var labelCounter = counter.Labels(labelValues);
                                                var valueInc = valueToReport - labelCounter.Value;
                                                if (valueInc > 0)
                                                {
                                                    labelCounter.Inc(valueInc);
                                                }
                                                else if (valueInc < 0)
                                                {
                                                    TheBaseAssets.MySYSLOG.WriteToLog(95307, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(strPrometheusExporter, $"Error reporting metric {metricName} in '{this.MyBaseThing?.Address} {this.MyBaseThing}'. Counter value {valueToReport} smaller than reported {labelCounter.Value}", eMsgLevel.l1_Error));
                                                }
                                            }
                                            else
                                            {
                                                counter.Inc(valueToReport - counter.Value);
                                            }
                                        }
                                    }
                                    break;

                                case null:
                                case "":
                                case "gauge":
                                    {
                                        var gauge = myGaugesByMetricName.GetOrAdd(metricName, (s) => Metrics.CreateGauge(s, "", labels));
                                        if (gauge != null)
                                        {
                                            if (gauge.LabelNames.Length > 0)
                                            {
                                                var labelValues = GetLabelValues(tThing, gauge.LabelNames);
                                                gauge.Labels(labelValues).Set(valueToReport);
                                            }
                                            else
                                            {
                                                gauge.Set(valueToReport);
                                            }
                                        }
                                    }
                                    break;
                                case "histogram":
                                    {
                                        var histogram = myHistogramsByMetricName.GetOrAdd(metricName, (s) => Metrics.CreateHistogram(s, "", GetHistogramBuckets(senderThing, propertyName, metricName, labels)));
                                        if (histogram != null)
                                        {
                                            if (histogram.LabelNames.Length > 0)
                                            {
                                                var labelValues = GetLabelValues(tThing, histogram.LabelNames);
                                                histogram.Labels(labelValues).Observe(valueToReport);
                                            }
                                            else
                                            {
                                                histogram.Observe(valueToReport);
                                            }
                                        }
                                    }
                                    break;
                                case "summary":
                                    {
                                        var summary = mySummariesByMetricName.GetOrAdd(metricName, (s) => Metrics.CreateSummary(s, "", labels));
                                        if (summary != null)
                                        {
                                            if (summary.LabelNames.Length > 0)
                                            {
                                                var labelValues = GetLabelValues(tThing, summary.LabelNames);
                                                summary.Labels(labelValues).Observe(valueToReport);
                                            }
                                            else
                                            {
                                                summary.Observe(valueToReport);
                                            }
                                        }
                                    }
                                    break;
                                default:
                                    TheBaseAssets.MySYSLOG.WriteToLog(95307, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(strPrometheusExporter, $"Unexpected metric type in server: {this.MyBaseThing?.Address}", eMsgLevel.l1_Error, senderThing.TargetType));
                                    break;
                            }
                        }
                        catch (Exception e)
                        {
                            TheBaseAssets.MySYSLOG.WriteToLog(95307, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(strPrometheusExporter, $"Error reporting metric {metricName} in server: {this.MyBaseThing?.Address}", eMsgLevel.l1_Error, e.ToString()));
                        }
                    }
                }
            }
        }

        private double GetMetricValue(object value)
        {
            if (value is DateTimeOffset)
            {
                return ((DateTimeOffset)value).ToUnixTimeSeconds();
            }
            else if (value is DateTime)
            {
                return (new DateTimeOffset((DateTime)value)).ToUnixTimeSeconds();
            }
            else
            {
                return TheCommonUtils.CDbl(value);
            }
        }

        private HistogramConfiguration GetHistogramBuckets(TheSenderThing senderThing, string propertyName, string metricName, string[] labelNames)
        {
            // TODO Make these configurable (if we need them)
            return new HistogramConfiguration { Buckets = new double[] { 0, 50, 100, 200, 400, 800, 1600, 3200, 6400, 12800, 30000 } };
        }

        private string[] GetMetricLabels(TheSenderThing senderThing, string propertyName, string metricName)
        {
            if (string.IsNullOrEmpty(senderThing.PartitionKey))
            {
                return new string[] { "NodeId", "Address" };
            }
            var partitionKeyParts = TheCommonUtils.cdeSplit(senderThing.PartitionKey, ",", true, true);
            return partitionKeyParts;
        }

        string[] GetLabelValues(TheThing tThing, string[] labelNames)
        {
            var labelValues = new string[labelNames.Length];
            int i = 0;
            foreach (var labelName in labelNames)
            {
                if (labelName == "cdeN" || labelName == "NodeId")
                {
                    labelValues[i] = GetValidLabelValue(tThing.cdeN.ToString());
                }
                else if (labelName == "cdeMID")
                {
                    labelValues[i] = GetValidLabelValue(tThing.cdeMID.ToString());
                }
                else
                {
                    labelValues[i] = GetValidLabelValue(TheThing.GetSafePropertyString(tThing, labelName));
                }
                i++;
            }
            return labelValues;
        }
        string GetValidLabelName(string labelName)
        {
            var label = labelName.Replace("[", "").Replace("]", "");
            label = Regex.Replace(label, "[^A-Za-z0-9_]", "_"); // Replace any other illegal character with _
            if (label[0]>='0'&& label[0]<='9')
            {
                label = "n" + label;
            }
            if (string.IsNullOrEmpty(label))
            {
                label = "null";
            }
            return label;
        }
        string GetValidLabelValue(string value)
        {
            return value;
        }

        protected override bool DoDisconnect(bool bDrain)
        {
            TheCommCore.MyHttpService.UnregisterHttpInterceptorB4(GetUrlTrimmed());

            if (myWatchDogTimer != null)
            {
                var wd = myWatchDogTimer;
                myWatchDogTimer = null;
                if (wd != null)
                {
                    try
                    {
                        wd.Change(Timeout.Infinite, Timeout.Infinite);
                        wd.Dispose();
                    }
                    catch { }
                }
            }
            var client = _client;
            if (client != null)
            {
                _client = null;
            }
            return true;
        }

        protected override object GetNextConnection()
        {
            return _client; // Connection multi-plexing not used in Http Sender
        }

        int maxEventDataSize = int.MaxValue;
        async override protected Task<SendEventResults> SendEventsAsync(object myClient, TheSenderThing azureThing, CancellationToken cancelToken, IEnumerable<nsCDEngine.ViewModels.TheThingStore> thingUpdatesToSend, IEventConverter eventConverter)
        {
            var client = myClient as TheREST;
            if (client == null)
            {
                throw new Exception("Internal error: Invalid or null client");
            }
            var results = new SendEventResults(); 
            results.SendTasks = new List<Task>();
            var messagePayloads = eventConverter.GetEventData(thingUpdatesToSend, azureThing, maxEventDataSize, false);
            long batchLength = 0;
            foreach (var msgObj in messagePayloads)
            {
                var msgString = msgObj as string;
                var correlationId = Guid.NewGuid();

                try
                {
                    var postCS = new TaskCompletionSource<bool>();
                    Task sendTask = postCS.Task;
                    if (msgString != null)
                    {
                        var payload = Encoding.UTF8.GetBytes(msgString);

                        client.PostRESTAsync(new Uri($"{HttpTargetUrl}"),
                            rd => {
                                postCS.TrySetResult(true);
                            }, payload, "application/json", Guid.Empty, null, rd => {
                                if (EnableLogSentPayloads)
                                {
                                    try
                                    {
                                        string strFilePath = TheCommonUtils.cdeFixupFileName("httpsenderdata.log");
                                        System.IO.File.AppendAllText(strFilePath, $"{{\"TimePublished\":\"{DateTimeOffset.Now:O}\", \"PLS\": {rd?.ErrorDescription}}},\r\n");
                                    }
                                    catch (Exception e)
                                    {
                                        TheBaseAssets.MySYSLOG.WriteToLog(95307, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(strPrometheusExporter, $"Unable to log data to file: {this.MyBaseThing?.Address} - {thingUpdatesToSend.Count()}", eMsgLevel.l3_ImportantMessage, e.ToString()));
                                    }

                                }
                                postCS.TrySetException(new Exception($"PostRESTAsync Failed: {rd?.ErrorDescription}"));
                            }, null);

                        batchLength += msgString.Length;
                        if (EnableLogSentPayloads)
                        {
                            try
                            {
                                string strFilePath = TheCommonUtils.cdeFixupFileName("httpsenderdata.log");
                                System.IO.File.AppendAllText(strFilePath, $"{{\"TimePublished\":\"{DateTimeOffset.Now:O}\", \"Body\": {msgString}}},\r\n");
                            }
                            catch (Exception e)
                            {
                                TheBaseAssets.MySYSLOG.WriteToLog(95307, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(strPrometheusExporter, $"Unable to log data to file: {this.MyBaseThing?.Address} - {thingUpdatesToSend.Count()}", eMsgLevel.l3_ImportantMessage, e.ToString()));
                            }

                        }

                        Interlocked.Increment(ref _pendingKPIs.EventScheduledCount);
                    }
                    else
                    {
                        postCS.TrySetException(new InvalidTransportEncodingException());
                    }
                    results.SendTasks.Add(sendTask);
                    if (GetPreserveOrderForSenderThing(azureThing))
                    {
                        await sendTask;
                    }

                }
                catch (Exception e)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(95307, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(strPrometheusExporter, $"Internal error: {this.MyBaseThing?.Address} - {thingUpdatesToSend.Count()}", eMsgLevel.l3_ImportantMessage, e.ToString()));
                }

                cancelToken.ThrowIfCancellationRequested();
            }
            results.SizeSent = batchLength;
            return results;
        }

        private Timer myWatchDogTimer;
    }

}