// SPDX-FileCopyrightText: 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

﻿/*********************************************************************
*
* Project Name" 179-CDMyMeshSender
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
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;

using System.Threading.Tasks;
using System.Threading;

using nsCDEngine.Engines;
using nsCDEngine.BaseClasses;
using nsCDEngine.ViewModels;
using nsCDEngine.Engines.NMIService;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.Communication;

using nsTheSenderBase;
using nsTheEventConverters;
using System.IO;

#if CDEPUBSUB && !NET35
using MyProduct.Ipc.Cde;
using MyProduct.Ipc.PubSub.Cde;
#endif

namespace CDMyMeshSender.ViewModel
{
    [DeviceType(
        Capabilities = new [] { eThingCaps.SensorConsumer, eThingCaps.ConfigManagement },
        DeviceType = MeshDeviceTypes.MeshSender,
        Description="Mesh Sender")]
    public class TheMeshSender : TheSenderBase
    {

#region ThingProperties

        [ConfigProperty(DefaultValue = "")]
        public string MeshTargetEngine
        {
            get { return TheThing.MemberGetSafePropertyString(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyString(MyBaseThing, value); }
        }
        [ConfigProperty(DefaultValue = "")]
        public string MeshTargetTopic
        {
            get { return TheThing.MemberGetSafePropertyString(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyString(MyBaseThing, value); }
        }

        [ConfigProperty(DefaultValue = "")]
        public string MeshTargetNode
        {
            get { return TheThing.MemberGetSafePropertyString(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyString(MyBaseThing, value); }
        }

        // This property contains an TSM.ORG, not just a node id (although a single Guid is also a valid ORG)
        public string PingTargetNode
        {
            get { return TheThing.MemberGetSafePropertyString(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyString(MyBaseThing, value); }
        }
#if CDEPUBSUB
        [ConfigProperty(DefaultValue = "")]
        public string PubSubTopic
        {
            get { return TheThing.MemberGetSafePropertyString(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyString(MyBaseThing, value); }
        }
#endif

        public string LastPingAck
        {
            get { return TheThing.MemberGetSafePropertyString(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyString(MyBaseThing, value); }
        }

        public string LastPingAllAck
        {
            get { return TheThing.MemberGetSafePropertyString(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyString(MyBaseThing, value); }
        }

        public DateTimeOffset LastStartupTime
        {
            get { return TheThing.GetSafePropertyDate(MyBaseThing, nameof(LastStartupTime)); }
            set { TheThing.SetSafePropertyDate(MyBaseThing, nameof(LastStartupTime), value); }
        }

        public DateTimeOffset LastShutdownTime
        {
            get { return TheThing.GetSafePropertyDate(MyBaseThing, nameof(LastShutdownTime)); }
            set { TheThing.SetSafePropertyDate(MyBaseThing, nameof(LastShutdownTime), value); }
        }

        [ConfigProperty(DefaultValue = 0, Units = "min")]
        public int KPILogIntervalInMinutes
        {
            get { return (int)TheThing.MemberGetSafePropertyNumber(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyNumber(MyBaseThing, value); }
        }
        public TimeSpan KPILogInterval { get { return new TimeSpan(0, KPILogIntervalInMinutes, 0); } }

        [ConfigProperty(DefaultValue = 0, Units = "s", Description = "Indicates how often to log if no actual data has been sent")]
        public int HeartbeatIntervalInSeconds
        {
            get { return (int)TheThing.MemberGetSafePropertyNumber(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyNumber(MyBaseThing, value); }
        }
        public TimeSpan HearbeatInterval { get { return new TimeSpan(0, 0, HeartbeatIntervalInSeconds); } }

        [ConfigProperty(DefaultValue = 0, Units = "min")]
        public int WatchDogIdleRecycleIntervalInMinutes
        {
            get { return (int)TheThing.MemberGetSafePropertyNumber(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyNumber(MyBaseThing, value); }
        }

        [ConfigProperty(DefaultValue = 0, Units = "min")]
        public int SendToAckNodeIntervalInMinutes
        {
            get { return (int)TheThing.MemberGetSafePropertyNumber(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyNumber(MyBaseThing, value); }
        }

        [ConfigProperty(DefaultValue = "")]
        public string KPIPublishPropertyName
        {
            get { return TheThing.MemberGetSafePropertyString(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyString(MyBaseThing, value); }
        }


        [ConfigProperty(DefaultValue = false)]
        public bool EnableLogSentPayloads
        {
            get { return TheThing.MemberGetSafePropertyBool(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyBool(MyBaseThing, value); }
        }

        [ConfigProperty(DefaultValue = false)]
        public bool DontWaitForAcks
        {
            get { return TheThing.MemberGetSafePropertyBool(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyBool(MyBaseThing, value); }
        }

        [ConfigProperty(DefaultValue = false)]
        public bool DoNotBatchEvents
        {
            get { return TheThing.MemberGetSafePropertyBool(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyBool(MyBaseThing, value); }
        }

        [ConfigProperty(DefaultValue = false)]
        public bool SendToLocalNode
        {
            get { return TheThing.MemberGetSafePropertyBool(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyBool(MyBaseThing, value); }
        }

        public TimeSpan AckLatencyLatest
        {
            get { return new TimeSpan(0, 0, 0, 0, TheCommonUtils.CInt(TheThing.GetSafePropertyNumber(MyBaseThing, nameof(AckLatencyLatest)))); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, nameof(AckLatencyLatest), value.TotalMilliseconds, true); }
        }

        public TimeSpan AckLatencyAvg
        {
            get { return new TimeSpan(0, 0, 0, 0, TheCommonUtils.CInt(TheThing.GetSafePropertyNumber(MyBaseThing, nameof(AckLatencyAvg)))); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, nameof(AckLatencyAvg), value.TotalMilliseconds, true); }
        }
        public TimeSpan AckLatencyMin
        {
            get { return new TimeSpan(0, 0, 0, 0, TheCommonUtils.CInt(TheThing.GetSafePropertyNumber(MyBaseThing, nameof(AckLatencyMin)))); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, nameof(AckLatencyMin), value.TotalMilliseconds, true); }
        }
        public TimeSpan AckLatencyMax
        {
            get { return new TimeSpan(0, 0, 0, 0, TheCommonUtils.CInt(TheThing.GetSafePropertyNumber(MyBaseThing, nameof(AckLatencyMax)))); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, nameof(AckLatencyMax), value.TotalMilliseconds, true); }
        }
        public long SendTimeoutCount
        {
            get { return (long)TheThing.GetSafePropertyNumber(MyBaseThing, "SendTimeoutCount"); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, "SendTimeoutCount", value); }
        }
        public long SendRepublishSuccess
        {
            get { return (long)TheThing.GetSafePropertyNumber(MyBaseThing, nameof(SendRepublishSuccess)); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, nameof(SendRepublishSuccess), value); }
        }

#endregion

        public override void HandleMessage(ICDEThing sender, object pIncoming)
        {
            TheProcessMessage pMsg = pIncoming as TheProcessMessage;
            if (pMsg == null || pMsg.Message == null) return;
            var cmd = pMsg.Message.TXT.Split(':');
            switch (cmd[0])
            {
                case "MESHSENDER_DATA_ACK":
                    if (cmd.Length >= 3)
                    {
                        try
                        {
                            TheBaseAssets.MySYSLOG.WriteToLog(95315, TSM.L(eDEBUG_LEVELS.FULLVERBOSE) ? null : new TSM(MyBaseThing.EngineName, "Received ACK", eMsgLevel.l1_Error, pMsg.Message.TXT));

                            var correlationId = TheCommonUtils.CGuid(cmd[1]);
                            var bSuccess = TheCommonUtils.CBool(cmd[2]);
                            string error = "";
                            if (cmd.Length >= 4)
                            {
                                error = cmd[3];
                            }
                            TheAckInfo ackInfo = null;
                            if (!string.IsNullOrEmpty(pMsg.Message.PLS))
                            {
                                try
                                {
                                    ackInfo = TheCommonUtils.DeserializeJSONStringToObject<TheAckInfo>(pMsg.Message.PLS);
                                }
                                catch { }
                            }

                            ProcessAck(ackInfo, pMsg.Message.ORG, correlationId, bSuccess, error);
                        }
                        catch (Exception e)
                        {
                            TheBaseAssets.MySYSLOG.WriteToLog(95315, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, String.Format("Internal error processing ACK {0}", this.MyBaseThing.FriendlyName), eMsgLevel.l1_Error, e.ToString()));
                        }
                    }
                    break;
                case "CDE_PONG":
                    //if (cmd.Length >= 2)
                    {
                        var plscmd = pMsg.Message.PLS.Split(';');
                        var correlationId = TheCommonUtils.CGuid(plscmd[0]);
                        if (String.IsNullOrEmpty(LastPingAck) || !LastPingAck.StartsWith(TheCommonUtils.cdeGuidToString(correlationId)))
                        {
                            LastPingAck = TheCommonUtils.cdeGuidToString(correlationId);
                            LastPingAck += String.Format(":{0}:{1}", TheCommonUtils.cdeGuidToString(pMsg.Message.GetOriginator()), pMsg.Message.PLS);
                        }
                        else
                        {
                            if (String.IsNullOrEmpty(LastPingAllAck) || !LastPingAllAck.StartsWith(TheCommonUtils.cdeGuidToString(correlationId)))
                            {
                                LastPingAllAck = TheCommonUtils.cdeGuidToString(correlationId);
                                LastPingAllAck += String.Format(":{0}:{1}", TheCommonUtils.cdeGuidToString(pMsg.Message.GetOriginator()), pMsg.Message.PLS);
                            }
                        }
                    }
                    break;
                case "MESHSENDER_PING_ACK":
                    if (cmd.Length >= 2)
                    {
                        var correlationId = TheCommonUtils.CGuid(cmd[1]);
                        if (String.IsNullOrEmpty(LastPingAck) || !LastPingAck.StartsWith(TheCommonUtils.cdeGuidToString(correlationId)))
                        {
                            LastPingAck = TheCommonUtils.cdeGuidToString(correlationId);
                        }
                        LastPingAck += $":{TheCommonUtils.cdeGuidToString(pMsg.Message.GetOriginator())}:{pMsg.Message.PLS}";
                    }
                    break;
                case "MESHSENDER_PING_ALL_ACK":
                    if (cmd.Length >= 2)
                    {
                        var correlationId = TheCommonUtils.CGuid(cmd[1]);
                        if (String.IsNullOrEmpty(LastPingAllAck) || !LastPingAllAck.StartsWith(TheCommonUtils.cdeGuidToString(correlationId)))
                        {
                            LastPingAllAck = TheCommonUtils.cdeGuidToString(correlationId);
                        }
                        LastPingAllAck += $":{TheCommonUtils.cdeGuidToString(pMsg.Message.GetOriginator())}:{pMsg.Message.PLS}";
                    }
                    break;
                default:
                    base.HandleMessage(sender, pIncoming);
                    break;
            }
        }

        private void ProcessAck(TheAckInfo ackInfo, string originator, Guid correlationId, bool bSuccess, string error)
        {
            TaskCompletionSource<bool> meshAckCS;
            if (_pendingAcks.TryGetValue(correlationId, out meshAckCS))
            {
                bool bAckHandled = false;
                if (!string.IsNullOrEmpty(originator))
                {
                    PingTargetNode = originator;
                }
                if (ackInfo != null)
                {
                    if (ackInfo.RediscoverTarget)
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(95313, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, $"Ack indicated Rediscover Target: retrying for all nodes on next send. {this.MyBaseThing.FriendlyName} {correlationId}", eMsgLevel.l4_Message, $"{meshAckCS.Task.Status}"));
                        PingTargetNode = "";
                    }
                    if (!string.IsNullOrEmpty(ackInfo.NewORG))
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(95313, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, $"Ack indicated new target node. {this.MyBaseThing.FriendlyName} {correlationId}", eMsgLevel.l4_Message, $"{meshAckCS.Task.Status}"));
                        PingTargetNode = ackInfo.NewORG;
                    }
                }
                if (bSuccess)
                {
                    bAckHandled = meshAckCS.TrySetResult(bSuccess);

                }
                else
                {
                    MyBaseThing.LastMessage = DateTimeOffset.Now + ": Error from Mesh Data receiver: " + error;
                    bAckHandled = meshAckCS.TrySetException(new Exception("Failure from Mesh Data receiver: " + error));
                }
                if (bAckHandled)
                {
                    var latency = DateTimeOffset.Now - (DateTimeOffset)meshAckCS.Task.AsyncState;
                    _latencyAggregator?.UpdateAckLatency(latency);
                    if (latency > ackTimeout)
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(95301, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, String.Format("Long latency for ACK correlation token {0} - {1}", this.MyBaseThing.FriendlyName, correlationId), eMsgLevel.l2_Warning, $"{latency}"));
                    }
                }
                else
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(95313, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM(MyBaseThing.EngineName, String.Format("Ack token was previously set (likely canceled)", this.MyBaseThing.FriendlyName, correlationId), eMsgLevel.l6_Debug, $"{meshAckCS.Task.Status}"));
                }
            }
            else
            {
                TheBaseAssets.MySYSLOG.WriteToLog(95314, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM(MyBaseThing.EngineName, String.Format("Unexpected ACK correlation token {0} - {1}", this.MyBaseThing.Address, correlationId), eMsgLevel.l6_Debug, ""));
            }
        }

        class TheAckInfo
        {
            public bool RediscoverTarget = false;
            public string NewORG = null;
        }

#if CDEPUBSUB
        class ThePubSubMessage
        {
            public string MsgId;
            public string Payload;
            public string PayloadFormat;
            //public string PayloadPadding;
        }

        class ThePubSubAckMessage
        {
            public string MsgId;
            public bool Success;
            public string Error;
            public TheAckInfo AckInfo;
        }

        private void ProcessPubSubAck(DateTimeOffset time, string channel, string message)
        {
            var ack = TheCommonUtils.DeserializeJSONStringToObject<ThePubSubAckMessage>(message);
            if (ack != null)
            {
                ProcessAck(ack.AckInfo, null, TheCommonUtils.CGuid(ack.MsgId), ack.Success, ack.Error);
            }
        }
#endif


        public TheMeshSender(TheThing pThing, ICDEPlugin pPluginBase) : base(pThing, pPluginBase)
        {
        }

        public override bool Init()
        {
            base.Init();
            return InitAsync().Result;
        }

        TheLatencyAggregator _latencyAggregator;
        public Task<bool> InitAsync()
        {
            LastStartupTime = DateTimeOffset.Now;
            _latencyAggregator = new TheLatencyAggregator(MyBaseThing);
            if (string.IsNullOrEmpty(MeshTargetEngine))
            {
                MeshTargetEngine = "CDMyMeshReceiver.MeshReceiverService";
            }
            MyBaseThing.RegisterOnChange(nameof(MeshTargetEngine), OnUpdateTargetEngine);
            MyBaseThing.RegisterOnChange(nameof(LastSendAttemptTime), OnSendAttempt);

            TheCDEngines.MyContentEngine.RegisterEvent(eEngineEvents.PreShutdown, OnPreShutdown);

            var result = InitBase(MeshDeviceTypes.MeshSender);

            TheBaseEngine.WaitForEnginesStarted((t, o) =>
            {
                OnUpdateTargetEngine(null);
            });
            return TheCommonUtils.TaskFromResult(IsInit());
        }

        private void OnPreShutdown(ICDEThing arg1, object arg2)
        {
            LastShutdownTime = DateTimeOffset.Now;
            LogKPIs(false);
            var maxCooldown = GetSenderThingsWithKPIProp().Max(s => s.ChangeBufferLatency);
            TheCommonUtils.SleepOneEye(maxCooldown + 5000, 100);
            while (_pendingAcks.Count > 0 && TheBaseAssets.MasterSwitch)
            {
                TheCommonUtils.SleepOneEye(100, 100);
            }
        }

        DateTimeOffset lastKPILogTime = DateTimeOffset.MinValue;
        private void OnSendAttempt(cdeP notUsed)
        {
            if (KPILogIntervalInMinutes > 0 && DateTimeOffset.Now - lastKPILogTime > KPILogInterval)
            {
                lastKPILogTime = DateTimeOffset.Now - new TimeSpan(0, 0, 0, 5);  // Give a few seconds leeway so the next timer doesn't miss the window
                LogKPIs(false);
            }
        }

        private void LogKPIs(bool sendOnly)
        {
            var kpis = new Dictionary<string, object>();
            foreach (var kpiPropName in new List<string> {
                nameof(EventsSentSinceStart), nameof(PendingEvents), nameof(EventsSentErrorCountSinceStart), nameof(PropertiesSentSinceStart),
                nameof(LastSendTime), nameof(LastSendAttemptTime), nameof(AckLatencyLatest), nameof(AckLatencyAvg), nameof(AckLatencyMax), nameof(AckLatencyMin),
                nameof(SendTimeoutCount),nameof(SendRepublishSuccess),
                nameof(LastStartupTime), nameof(LastShutdownTime),
            })
            {
                kpis.Add(kpiPropName, TheThing.GetSafeProperty(MyBaseThing, kpiPropName));
            }
            var message = new TSM(MyBaseThing.EngineName, "Mesh Sender KPIs", eMsgLevel.l4_Message, TheCommonUtils.SerializeObjectToJSONString(kpis));
            //kpis.Aggregate("", (s, kv) =>
            //{
            //    object value = kv.Value;
            //    if (value is string)
            //    {
            //        var valueStr = value as string;
            //        if (valueStr.Length > 120)
            //        {
            //            valueStr = valueStr.Substring(0, 120) + "..."; ;
            //        }
            //        value = valueStr.Replace("\r\n", " ");
            //    }
            //    return $"{s}{value},";
            //}).TrimEnd(','));

            if (!sendOnly)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(95317, TSM.L(eDEBUG_LEVELS.OFF) ? null : message); // Mesh Sender KPIs
            }
            if (!String.IsNullOrEmpty(KPIPublishPropertyName))
            {
                foreach (var thingToSend in GetSenderThingsWithKPIProp().Select(s => s.GetThing()).Distinct())
                {
                    TheThing.SetSafeProperty(thingToSend, KPIPublishPropertyName, new TSMObjectPayload(message, kpis), ePropertyTypes.NOCHANGE); // CODE REVIEW: OK to put an entire message here? This will be serialized to JSON properly, but...
                }

            }
        }

        IEnumerable<TheSenderThing> GetSenderThingsWithKPIProp()
        {
            return MySenderThings.MyMirrorCache.TheValues.Where(s => ((s.PropertiesIncluded?.Count() ?? 0) == 0 && s.PropertiesExcluded?.Contains(KPIPublishPropertyName) != true) || s.PropertiesIncluded.Contains(KPIPublishPropertyName));
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
                if (WatchDogIdleRecycleIntervalInMinutes > 0 && timeSinceLastAttempt > new TimeSpan(0, 0, WatchDogIdleRecycleIntervalInMinutes, 0))
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(95318, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, $"WatchDog: No activity since {LastSendAttemptTime}. Disconnecting Mesh Sender and waiting 5 seconds to reconnect.", eMsgLevel.l4_Message));

                    //TheBaseAssets.MySYSLOG.WriteToLog(95318, eDEBUG_LEVELS.OFF, MyBaseThing.EngineName, () => new TSM(null, $"WatchDog: No activity since {LastSendAttemptTime}. Disconnecting Mesh Sender and waiting 5 seconds to reconnect.", eMsgLevel.l4_Message));

                    PingTargetNode = TheCommonUtils.cdeGuidToString(Guid.Empty);
                    Disconnect(true);
                    try
                    {
                        await TheCommonUtils.TaskDelayOneEye(5000, 100).ConfigureAwait(false);
                    }
                    catch (TaskCanceledException) { }

                    if (TheBaseAssets.MasterSwitch)
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(95319, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, $"WatchDog: Reinitializing Cloud Routes.", eMsgLevel.l4_Message));
                        TheQueuedSenderRegistry.ReinitCloudRoutes();

                        TheBaseAssets.MySYSLOG.WriteToLog(95320, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, $"WatchDog: Reconnecting Mesh Sender.", eMsgLevel.l4_Message));
                        Connect();
                    }
                }
                else if (HeartbeatIntervalInSeconds > 0 && timeSinceLastAttempt > HearbeatInterval)
                {
                    // Send a heartbeat: for now send the log
                    LogKPIs(true);
                }
            }
            catch (Exception ex)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(95302, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, $"WatchDog: Internal error.", eMsgLevel.l1_Error, ex.ToString()));
            }
        }

        private void OnUpdateTargetEngine(cdeP obj)
        {
            var engineName = MeshTargetEngine;
            // We are sending to an engine that is typically not installed on this node, so must make that engine known. Otherwise the cloud will not establish a subscription on behalf of the receiver
            TheCDEngines.RegisterNewMiniRelay(engineName);
        }

        private void ResetMeshSenderKPIs()
        {
            this.AckLatencyLatest = TimeSpan.Zero;
            this.AckLatencyAvg = TimeSpan.Zero;
            this.AckLatencyMin = TimeSpan.Zero;
            this.AckLatencyMax = TimeSpan.Zero;
            this.PingTargetNode = String.Empty;

            // We should have a separate button.
            // For now, piggyback on KPI reset.
            this.LastPingAck = String.Empty;
            this.LastPingAllAck = String.Empty;
        }

        public override bool CreateUX()
        {
            UXNoPartitionKey = true;
            UXNoMQTTTopicTemplate = true;
            UXNoSendAsFile = true;
            UXNoSendEntireTSM = true;

            CreateUXBase("Mesh Sender");
           
            if (MyForm != null)
            {
                // Group: Device Status
                MyForm.DeleteByOrder(24); //delete Address Field
                                          //NUI Definition for All clients

                // Group: KPIs (202)
                // Base Members Include:
                // -- Events Sent (203)
                // -- Events Pending (204)
                // -- Send Error Count (205)
                // -- Last Send (206)
                // -- Last Send Attempt (207)
                // -- [RESET] Button (250)
                // (Additional items)
                TheNMIEngine.AddSmartControl(MyBaseThing, MyForm, eFieldType.Number, 240, 0, 0, "Ack Latency Last", nameof(AckLatencyLatest), new nmiCtrlNumber() { ParentFld = 202, TileWidth = 3 });
                TheNMIEngine.AddSmartControl(MyBaseThing, MyForm, eFieldType.Number, 241, 0, 0, "Ack Latency Avg", nameof(AckLatencyAvg), new nmiCtrlNumber() { ParentFld = 202, TileWidth = 3 });
                TheNMIEngine.AddSmartControl(MyBaseThing, MyForm, eFieldType.Number, 242, 0, 0, "Ack Latency Min", nameof(AckLatencyMin), new nmiCtrlNumber() { ParentFld = 202, TileWidth = 3 });
                TheNMIEngine.AddSmartControl(MyBaseThing, MyForm, eFieldType.Number, 243, 0, 0, "Ack Latency Max", nameof(AckLatencyMax), new nmiCtrlNumber() { ParentFld = 202, TileWidth = 3 });
                TheNMIEngine.AddSmartControl(MyBaseThing, MyForm, eFieldType.SingleEnded, 244, 0, 0xC0, "Current Receiver Node", nameof(PingTargetNode), new nmiCtrlSingleEnded() { ParentFld = 202, TileWidth = 6 });
                TheFieldInfo tReset = TheNMIEngine.GetFieldByFldOrder(MyForm, 250);
                tReset.RegisterUXEvent(MyBaseThing, eUXEvents.OnClick, "RESET", (pThing, pObj) =>
                {
                    TheProcessMessage pMsg = pObj as TheProcessMessage;
                    if (pMsg == null || pMsg.Message == null) return;
                    ResetMeshSenderKPIs();
                });

                // Group: Connectivity (20)
                // Base Members from TheNMIEngine.AddConnectivityBlock(...)
                // Sender Base Members:
                // -- Force Disconnect Button (29)
                // (Additional items)
                TheNMIEngine.AddSmartControl(MyBaseThing, MyForm, eFieldType.SingleEnded, 24, 2, 0xC0, "Target Engine:", nameof(MeshTargetEngine), new nmiCtrlSingleEnded() { ParentFld = 20, DefaultValue = "CDMyMeshReceiver.MeshReceiverService" }); // TODO Add engine picker control?

                // Group: Ping Target Node (100)
                // Sub-Group of Connectivity
                TheNMIEngine.AddSmartControl(MyBaseThing, MyForm, eFieldType.CollapsibleGroup, 100, 2, 0xC0, "Ping Target Node", null, ThePropertyBag.Create(new nmiCtrlCollapsibleGroup() { ParentFld = 20, TileWidth = 6, IsSmall = true, DoClose = true }));//() { "TileWidth=6", "Format=Verification", "Style=font-size:26px;text-align: left" });
                TheNMIEngine.AddSmartControl(MyBaseThing, MyForm, eFieldType.SingleEnded, 110, 2, 0xC0, "Target Node", nameof(PingTargetNode), new nmiCtrlSingleEnded() { ParentFld = 100 });

                AddPingNodeButton(135, 100);
                TheNMIEngine.AddSmartControl(MyBaseThing, MyForm, eFieldType.TextArea, 140, 0, 0, "Ping Ack", nameof(LastPingAck), new ThePropertyBag() { "ParentFld=100", "TileHeight=2", "TileWidth=5", "NoTE=true" });

                AddPingAllButton(145, 100);
                TheNMIEngine.AddSmartControl(MyBaseThing, MyForm, eFieldType.TextArea, 150, 0, 0, "Ping All Ack", nameof(LastPingAllAck), new ThePropertyBag() { "ParentFld=100", "TileHeight=2", "TileWidth=5", "NoTE=true" });

                // Group: Advanced Configuration... (40)
                // Base Members Include:
                // -- Preserve Order for All Things (44)
                // -- Merge matching Sender Things (80)
                // -- Max Updates Per Batch (85)
                // -- Send Retry Period (ms) (87)
                TheNMIEngine.AddSmartControl(MyBaseThing, MyForm, eFieldType.SingleEnded, 41, 2, 0xC0, "Target Node (empty = entire Mesh)", nameof(MeshTargetNode), new nmiCtrlSingleEnded() { ParentFld = 40, LabelFontSize = 16 }); // TODO Node picker control?
                TheNMIEngine.AddSmartControl(MyBaseThing, MyForm, eFieldType.SingleEnded, 42, 2, 0xC0, "Target Topic", nameof(MeshTargetTopic), new nmiCtrlSingleEnded() { ParentFld = 40 });

#if CDEPUBSUB
                TheNMIEngine.AddSmartControl(MyBaseThing, MyForm, eFieldType.SingleEnded, 43, 2, 0xC0, "Pub Sub Topic", nameof(PubSubTopic), new nmiCtrlSingleEnded() { ParentFld = 40, LabelFontSize = 16 });
#endif
                TheNMIEngine.AddSmartControl(MyBaseThing, MyForm, eFieldType.Number, 45, 2, 0xC0, "KPI Log Interval (min)", nameof(KPILogIntervalInMinutes), new nmiCtrlNumber() { LabelFontSize = 16, ParentFld = 40, TileWidth = 3 });
                TheNMIEngine.AddSmartControl(MyBaseThing, MyForm, eFieldType.SingleEnded, 46, 2, 0xC0, "KPI Property Name", nameof(KPIPublishPropertyName), new nmiCtrlSingleEnded { ParentFld = 40, TileHeight = 1, TileWidth = 6 });
                TheNMIEngine.AddSmartControl(MyBaseThing, MyForm, eFieldType.SingleCheck, 47, 2, 0xC0, "Log Sent Payload Data", nameof(EnableLogSentPayloads), new nmiCtrlSingleCheck { ParentFld = 40, LabelFontSize = 16, TileHeight = 1, TileWidth = 3 });
                TheNMIEngine.AddSmartControl(MyBaseThing, MyForm, eFieldType.Number, 48, 2, 0xC0, "WatchDog Interval (min)", nameof(WatchDogIdleRecycleIntervalInMinutes), new nmiCtrlNumber { ParentFld = 40, LabelFontSize = 16, TileHeight = 1, TileWidth = 3 });
                TheNMIEngine.AddSmartControl(MyBaseThing, MyForm, eFieldType.Number, 49, 2, 0xC0, "Publish to All Nodes Interval (min)", nameof(SendToAckNodeIntervalInMinutes), new nmiCtrlNumber { ParentFld = 40, LabelFontSize = 16, TileHeight = 1, TileWidth = 3 });

                TheNMIEngine.AddSmartControl(MyBaseThing, MyForm, eFieldType.SingleCheck, 50, 2, 0xC0, "Do not wait for ACKs", nameof(DontWaitForAcks), new nmiCtrlSingleCheck { ParentFld = 40, TileHeight = 1, LabelFontSize = 16, TileWidth = 3 });
                TheNMIEngine.AddSmartControl(MyBaseThing, MyForm, eFieldType.SingleCheck, 51, 2, 0xC0, "Include Local Node", nameof(SendToLocalNode), new nmiCtrlSingleCheck() { ParentFld = 40, TileWidth = 3 });
                TheNMIEngine.AddSmartControl(MyBaseThing, MyForm, eFieldType.SingleCheck, 52, 2, 0xC0, "Do not batch events", nameof(DoNotBatchEvents), new nmiCtrlSingleCheck { ParentFld = 40, TileHeight = 1, LabelFontSize = 16, TileWidth = 3 });

                mIsUXInitialized = true;
                return true;
            }
            return false;
        }

        protected void AddPingNodeButton(int iFieldOrder, int iFieldParent)
        {
            TheFieldInfo tPingNode = TheNMIEngine.AddSmartControl(MyBaseThing, MyForm, eFieldType.TileButton, iFieldOrder, 2, 0xC0, "Ping Target Node", null, new nmiCtrlTileButton() { NoTE = true, ParentFld = iFieldParent, ClassName = "cdeGoodActionButton", TileWidth = 1, TileHeight = 2 });
            tPingNode.RegisterUXEvent(MyBaseThing, eUXEvents.OnClick, "PINGNODE", (pThing, pObj) =>
            {
                // Reset field we might be updating.
                LastPingAck = String.Empty;

                TheProcessMessage pMsg = pObj as TheProcessMessage;
                if (pMsg == null || pMsg.Message == null) return;
                if (String.IsNullOrEmpty(PingTargetNode) || TSM.GetOriginator(PingTargetNode) == Guid.Empty)
                {
                    TheCommCore.PublishToOriginator(pMsg.Message, new TSM(eEngineName.NMIService, "NMI_TOAST", "Specify a valid NodeId to send the ping message to"));
                }
                else
                {
                    Guid correlationId = Guid.NewGuid();
                    var pingMsg = new TSM(MeshTargetEngine, String.Format("MESHSENDER_PING:{0}", TheCommonUtils.cdeGuidToString(correlationId)),
                        String.Format("{0};{1};{2}", TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID, MyBaseThing.cdeMID, MyBaseThing.FriendlyName));

                    pingMsg.SetOriginatorThing(MyBaseThing.cdeMID);

                    TheCommCore.PublishToNode(TSM.GetOriginator(PingTargetNode), pingMsg);

                    TheCommCore.PublishToOriginator(pMsg.Message, new TSM(eEngineName.NMIService, "NMI_TOAST", $"Sent Mesh Sender Ping to Target Node {PingTargetNode}."));
                }
            });

        }

        protected void AddPingAllButton(int iFieldOrder, int iFieldParent)
        {
            TheFieldInfo tPingAll = TheNMIEngine.AddSmartControl(MyBaseThing, MyForm, eFieldType.TileButton, iFieldOrder, 2, 0xC0, "Ping All Nodes", null, new nmiCtrlTileButton() { NoTE = true, ParentFld = iFieldParent, ClassName = "cdeGoodActionButton", TileWidth = 1, TileHeight = 2 });
            tPingAll.RegisterUXEvent(MyBaseThing, eUXEvents.OnClick, "PINGALL", (pThing, pObj) =>
            {
                // Reset field we might be updating.
                LastPingAllAck = String.Empty;

                TheProcessMessage pMsg = pObj as TheProcessMessage;
                if (pMsg == null || pMsg.Message == null) return;
                Guid correlationId = Guid.NewGuid();
                var pingMsg = new TSM(MeshTargetEngine, String.Format("MESHSENDER_PING_ALL:{0}", TheCommonUtils.cdeGuidToString(correlationId)),
                    String.Format("{0};{1};{2}", TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID, MyBaseThing.cdeMID, MyBaseThing.FriendlyName));

                pingMsg.SetOriginatorThing(MyBaseThing.cdeMID);

                TheCommCore.PublishCentral(pingMsg, SendToLocalNode);

                TheCommCore.PublishToOriginator(pMsg.Message, new TSM(eEngineName.NMIService, "NMI_TOAST", "Sent Mesh Sender Ping to All Mesh Nodes."));
            });

        }

        bool bConnected = false;
        protected override bool DoConnect()
        {
            OnUpdateTargetEngine(null);
            TimeSpan timerInterval = TimeSpan.Zero;
            if (KPILogIntervalInMinutes > 0)
            {
                timerInterval = KPILogInterval;
            }
            if (HeartbeatIntervalInSeconds > 0)
            {
                if (HearbeatInterval < timerInterval)
                {
                    timerInterval = HearbeatInterval;
                }
            }
            if (timerInterval > TimeSpan.Zero)
            {
                myWatchDogTimer = new Timer(OnWatchDogTimer, null, timerInterval, timerInterval);
            }

#if CDEPUBSUB
            ConnectPubSub();
#endif
            bConnected = true;
            return true;
        }

#if CDEPUBSUB
#if !NET35
        private Subscriber _subscriber;
        private Publisher _publisher;
        private ComLine _comLine;
        private CancellationTokenSource _pubsubCancellationTokenSource;
#endif

        private void ConnectPubSub()
        {
#if !NET35
            if (string.IsNullOrEmpty(PubSubTopic))
            {
                return;
            }
            if (_pubsubCancellationTokenSource != null)
            {
                return; // already or still running
            }

            TheBaseEngine.WaitForEnginesStartedAsync().Wait();
            TheBaseEngine.WaitForStorageReadinessAsync(true).Wait();


            _pubsubCancellationTokenSource = new CancellationTokenSource();
            if (_comLine == null)
            {
                _comLine = Operator.GetLine(MyBaseThing);
            }

            _subscriber = new Subscriber(_comLine, _pubsubCancellationTokenSource.Token);
            //if (!string.IsNullOrEmpty(PubSubAckTopic))
            {
                //var catchAllSubscription = _subscriber.Subscribe("*");
                _subscriber.Subscribe($"{PubSubTopic}/ack")
                    .With((time, channel, message) =>
                    {
                        ProcessPubSubAck(time, channel, message);
                    });

            }
            _publisher = new Publisher(_comLine, _pubsubCancellationTokenSource.Token);
            _publisher.ConnectAsync().ContinueWith(t =>
            {
                var publisher = t.Result;
                publisher.Publish("public:debug", $"Publisher {MyBaseThing.FriendlyName} initialized");
                Thread.Sleep(30000);
#if RUNPUBSUBTESTS
                var tests = new List<Dictionary<string, object>>
                    {
                        new Dictionary<string, object> { { "size", 1024 }, { "wait", 100 } },
                        new Dictionary<string, object> { { "size", 2048 }, { "wait", 100 } },
                        new Dictionary<string, object> { { "size", 4096}, { "wait", 100 } },
                        new Dictionary<string, object> { { "size", 8192 }, { "wait", 100 } },

                        //new Dictionary<string, object> { { "size", 4096 }, { "wait", 100 } },
                        //new Dictionary<string, object> { { "size", 4096 }, { "wait", 100 } },
                        //new Dictionary<string, object> { { "size", 4096 }, { "wait", 100 } },
                        //new Dictionary<string, object> { { "size", 4096 }, { "wait", 100 } },
                        //new Dictionary<string, object> { { "size", 4096 }, { "wait", 100 } },
                        //new Dictionary<string, object> { { "size", 4096 }, { "wait", 100 } },
                        //new Dictionary<string, object> { { "size", 4096 }, { "wait", 100 } },
                        //new Dictionary<string, object> { { "size", 4096 }, { "wait", 100 } },
                        //new Dictionary<string, object> { { "size", 4096 }, { "wait", 100 } },

                        new Dictionary<string, object> { { "size", 16384 }, { "wait", 100 } },
                        new Dictionary<string, object> { { "size", 32768 }, { "wait", 100 } },
                        new Dictionary<string, object> { { "size", 65536 }, { "wait", 100 } },
                        new Dictionary<string, object> { { "size", 131072 }, { "wait", 100 } },
                        new Dictionary<string, object> { { "size", 262144 }, { "wait", 100 } },
                        new Dictionary<string, object> { { "size", 524288 }, { "wait", 100 } },
                        new Dictionary<string, object> { { "size", 1048576 }, { "wait", 100 } },
                        new Dictionary<string, object> { { "size", 2097152 }, { "wait", 100 } },
                        //new Dictionary<string, object> { { "size", 4194304 }, { "wait", 100 } },
                        //new Dictionary<string, object> { { "size", 8388608 }, { "wait", 100 } },
                        //new Dictionary<string, object> { { "size", 16777216 }, { "wait", 100 } },
                        //new Dictionary<string, object> { { "size", 33554432 }, { "wait", 100 } },
                        //new Dictionary<string, object> { { "size", 67108864 }, { "wait", 100 } },
                        //new Dictionary<string, object> { { "size", 134217728 }, { "wait", 100 } },
                        //new Dictionary<string, object> { { "size", 268435456 }, { "wait", 100 } },
                    };
                var msgObj = new Dictionary<string, object>
                {
                    { "pongId", "0" },
                    { "ackTo", "private/ping/ackj" },
                    { "replyTo", "public/pong/0/results" },
                    { "tests" , tests },
                };

                int TargetTestCount = 1000;
                bFinished = false;
                pendingTests = new cdeConcurrentDictionary<int, TestInfo>();
                var lastTestId = 0;
                foreach (var test in tests)
                {
                    pendingTests.TryAdd(lastTestId, new TestInfo { ResultCount = 0, });
                    lastTestId++;
                }
                var message = TheCommonUtils.SerializeObjectToJSONString(msgObj);

                _subscriber.Subscribe("public/pong/0/results").With((time, channel, resultMsg) =>
                {
                    lock (fileLock)
                    {
                        System.IO.File.AppendAllLines("pingresults.log", new List<string> { $"{DateTimeOffset.Now}: {resultMsg}," });
                    }
                    var result = TheCommonUtils.DeserializeJSONStringToObject<Dictionary<string, object>>(resultMsg);

                    int testId = 0;
                    var stackTrace = "";// Environment.StackTrace;

                    if (result.TryGetValue("id", out var testIdObj))
                    {
                        testId = TheCommonUtils.CInt(testIdObj);
                        if (testId >= 0)
                        {
                            lock (pendingTests)
                            {
                                try
                                {
                                    var testInfo = pendingTests[testId];
                                    if (!testInfo.Callstacks.Contains(stackTrace))
                                    {
                                        testInfo.Callstacks.Add(stackTrace);
                                    }
                                    testInfo.ResultCount += 1;
                                    if (testInfo.ResultCount > 1)
                                    {
                                        if (testInfo.Callstacks.Count > 1)
                                        {

                                        }
                                    }
                                }
                                catch
                                {
                                    // Test wasn't tracked
                                }
                            }
                        }
                    }
                    else
                    {
                        // Unexpected test id in result
                    }
                    if (result.TryGetValue("size", out var sizeObj))
                    {
                        int newTestId;
                        lock (pendingTests)
                        {
                            newTestId = lastTestId;
                            lastTestId++;
                        }
                        if (newTestId <= TargetTestCount && !_pubsubCancellationTokenSource.IsCancellationRequested)
                        {
                            var msgObj2 = new Dictionary<string, object>
                            {
                                { "pongId", "0" },
                                { "ackTo", "private/ping/ackj" },
                                { "replyTo", "public/pong/0/results" },
                                { "tests" , new List<Dictionary<string,object>>
                                    {
                                        new Dictionary<string, object> { { "id", TheCommonUtils.CInt(newTestId) }, { "size", TheCommonUtils.CInt(sizeObj) }, { "wait", 100 } },
                                    }
                                }
                            };
                            var message2 = TheCommonUtils.SerializeObjectToJSONString(msgObj2);
                            if (!pendingTests.TryAdd(newTestId, new TestInfo { Callstacks = new List<string> { stackTrace } } ))
                            {
                                // Test already initiated?
                            }
                            publisher.Publish("public/ping/0/request", message2);
                        }
                        else
                        {
                            if (!bFinished)
                            {
                                TheCommonUtils.cdeRunAsync("", true, async o =>
                                {
                                    try
                                    {
                                        do
                                        {
                                            var currentTests = pendingTests.GetDictionarySnapshot();
                                            var incomplete = currentTests.Where(kv => kv.Value.ResultCount == 0).ToList();
                                            var duplicates = currentTests.Where(kv => kv.Value.ResultCount > 1).ToList();
                                            if (!incomplete.Any())
                                            {

                                            }
                                            await TheCommonUtils.TaskDelayOneEye(30000, 100, TheBaseAssets.MasterSwitchCancelationToken /*_pubsubCancellationTokenSource.Token*/);
                                        }
                                        while (!_pubsubCancellationTokenSource.Token.IsCancellationRequested && TheBaseAssets.MasterSwitch);
                                    }
                                    catch { }
                                });
                            }
                            bFinished = true;
                        }
                    }
                    else
                    {
                        // unexpected test size in result
                    }
                });

                publisher.Publish("public/ping/0/request", message);
#endif
            });
#endif
            }
#endif

#if RUNPUBSUBTESTS
        static object fileLock = new object();
        bool bFinished = false;
        class TestInfo
        {
            public int ResultCount;
            public List<string> Callstacks;

            public TestInfo()
            {
                Callstacks = new List<string>();
            }
        }
        cdeConcurrentDictionary<int, TestInfo> pendingTests = new cdeConcurrentDictionary<int, TestInfo>();

        static string _messagePadding
        {
            get
            {
                if (__messagePadding == null)
                {
                    __messagePadding = "generating";
                    var size = 400 * 1024;
                    StringBuilder messagePadding = new StringBuilder(size);
                    Random r = new Random();
                    for (int i = 0; i < size; i++)
                    {
                        messagePadding.Append((char)(48 + r.Next(65)));
                    }
                    __messagePadding = messagePadding.ToString();
                }
                return __messagePadding;
            }
        }
        static string __messagePadding;

#endif

        protected override bool DoDisconnect(bool bDrain)
        {
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
#if CDEPUBSUB
#if !NET35
            _pubsubCancellationTokenSource?.Cancel();
            _pubsubCancellationTokenSource = null;
            try
            {
                _comLine?.Dispose();
                _comLine = null;
                _subscriber = null;
                _publisher = null;
            }
            catch { }
#endif
#endif
            bConnected = false;
            return true;
        }

        protected override object GetNextConnection()
        {
            if (!bConnected)
            {
                return null;
            }
            return this; // Connection multi-plexing not used in Mesh Sender
        }

        private bool bFirstPublishCentral = true;
        private bool bFirstDirectToNode = true;
        int maxEventDataSize = int.MaxValue;
        async override protected Task<SendEventResults> SendEventsAsync(object myClient, TheSenderThing senderThing, CancellationToken cancelToken, IEnumerable<TheThingStore> thingUpdatesToSend, IEventConverter eventConverter)
        {
            var results = new SendEventResults();
            results.SendTasks = new List<Task>();

            long batchLength = 0;
            var messagePayloads = eventConverter.GetEventData(thingUpdatesToSend, senderThing, maxEventDataSize, DoNotBatchEvents);
            foreach (var msgObj in messagePayloads)
            {
                if (msgObj is string)
                {
                    var correlationId = Guid.NewGuid();
                    string targetEng;
                    if (String.IsNullOrEmpty(MeshTargetEngine))
                    {
                        targetEng = MyBaseEngine.GetEngineName();
                    }
                    else
                    {
                        targetEng = MeshTargetEngine;
                    }

                    var msg = new TSM(targetEng, String.Format("MESHSENDER_DATA:{0}:{1}", TheCommonUtils.cdeGuidToString(correlationId), TheEventConverters.GetDisplayName(eventConverter)), msgObj as string /*+ "//"+ _messagePadding*/);

                    msg.SetOriginatorThing(MyBaseThing.cdeMID);

                    bool bPublished = false;

                    TaskCompletionSource<bool> meshAckCS;

                    bool bRepublish = false;
                    do
                    {
                        meshAckCS = _pendingAcks.AddOrUpdate(correlationId,
                            new TaskCompletionSource<bool>(DateTimeOffset.Now),
                            (g, cs) => {
                                if (!cs.Task.IsCanceled && !cs.Task.IsFaulted)
                                {
                                    return cs;
                                }
                                else
                                {
                                    return new TaskCompletionSource<bool>(DateTimeOffset.Now);
                                }
                            });
                        CheckAckTimeouts(); // Ensure we start a timer so timeouts actually make it back

                        if (meshAckCS.Task.Status == TaskStatus.RanToCompletion)
                        {
                            if (!bRepublish)
                            {
                                // This should never happen
                                TheBaseAssets.MySYSLOG.WriteToLog(95303, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, $"SendEventAsync {this.MyBaseThing.Address} - {thingUpdatesToSend.Count()} - internal error", eMsgLevel.l1_Error, ""));
                            }
                            // Completed while beginning the retry
                            break;
                        }
#if CDEPUBSUB
                        if (!string.IsNullOrEmpty(PubSubTopic))
                        {
                            var pubSubMessage = new ThePubSubMessage
                            {
                                MsgId = TheCommonUtils.cdeGuidToString(correlationId),
                                Payload = msgObj as string,
                                //PayloadPadding = _messagePadding,
                                PayloadFormat = TheEventConverters.GetDisplayName(eventConverter),
                            };
                            var pubSubMessageString = TheCommonUtils.SerializeObjectToJSONString(pubSubMessage);
#if !NET35
                            _publisher.Publish(PubSubTopic, pubSubMessageString);
#endif
                        }
                        else
#endif
                        if (!String.IsNullOrEmpty(MeshTargetNode))
                        {
                            var targetNode = TheCommonUtils.CGuid(MeshTargetNode);
                            if (targetNode != Guid.Empty)
                            {
                                TheCommCore.PublishToNode(targetNode, msg);
                                bPublished = true;
                                TheBaseAssets.MySYSLOG.WriteToLog(95303, TSM.L(eDEBUG_LEVELS.FULLVERBOSE) ? null : new TSM(MyBaseThing.EngineName, $"SendEventAsync {this.MyBaseThing.Address} - {thingUpdatesToSend.Count()}", eMsgLevel.l6_Debug, ""));
                            }
                            else
                            {
                                TheBaseAssets.MySYSLOG.WriteToLog(95304, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, $"Invalid target node in SendEventAsync {this.MyBaseThing.Address} - {thingUpdatesToSend.Count()}", eMsgLevel.l1_Error, ""));
                                _pendingAcks.TryRemove(correlationId, out var tcs);
                            }
                        }
                        else
                        {
                            if (SendToAckNodeIntervalInMinutes > 0 && TSM.GetOriginator(PingTargetNode) != Guid.Empty && LastSendAttemptTime - LastSendTime < new TimeSpan(0, 0, SendToAckNodeIntervalInMinutes, 0))
                            {
                                if (bFirstDirectToNode)
                                {
                                    TheBaseAssets.MySYSLOG.WriteToLog(95305, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, $"Sending message to target node {PingTargetNode} for {this.MyBaseThing.Address} - {thingUpdatesToSend.Count()}", eMsgLevel.l3_ImportantMessage, ""));
                                }
                                else
                                {
                                    TheBaseAssets.MySYSLOG.WriteToLog(95310, TSM.L(eDEBUG_LEVELS.FULLVERBOSE) ? null : new TSM(MyBaseThing.EngineName, $"SendEventAsync {this.MyBaseThing.Address} - {thingUpdatesToSend.Count()}", eMsgLevel.l6_Debug, $"'{PingTargetNode}' - {msg.TXT}"));
                                }
                                msg.GRO = PingTargetNode;
                                TheCommCore.PublishToNode(TSM.GetOriginator(PingTargetNode), msg);
                                bPublished = true;
                                bFirstDirectToNode = false;
                                bFirstPublishCentral = true;
                            }
                            else
                            {
                                if (SendToAckNodeIntervalInMinutes > 0)
                                {
                                    if (bFirstPublishCentral)
                                    {
                                        TheBaseAssets.MySYSLOG.WriteToLog(95306, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, $"Sending message to all Mesh Nodes. TargetNode: {PingTargetNode}. Last send {LastSendTime} - {this.MyBaseThing.Address} - {thingUpdatesToSend.Count()}", eMsgLevel.l2_Warning, ""));
                                    }
                                    else
                                    {
                                        TheBaseAssets.MySYSLOG.WriteToLog(95311, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM(MyBaseThing.EngineName, $"Sending message to all Mesh Nodes. TargetNode: {PingTargetNode}. Last send {LastSendTime} - {this.MyBaseThing.Address} - {thingUpdatesToSend.Count()}", eMsgLevel.l6_Debug, ""));
                                    }
                                }
                                else
                                {
                                    TheBaseAssets.MySYSLOG.WriteToLog(95312, TSM.L(eDEBUG_LEVELS.FULLVERBOSE) ? null : new TSM(MyBaseThing.EngineName, $"Sending message to all Mesh Nodes. {this.MyBaseThing.Address} - {thingUpdatesToSend.Count()}", eMsgLevel.l3_ImportantMessage, ""));
                                }

                                if (String.IsNullOrEmpty(MeshTargetTopic))
                                {
                                    msg.SetToServiceOnly(true); //REVIEW: This prevents sending to Devices (like JavaScript)
                                    TheCommCore.PublishCentral(msg, SendToLocalNode);
                                }
                                else
                                {
                                    TheCommCore.PublishCentral(MeshTargetTopic, msg);
                                }
                                bPublished = true;
                                bFirstPublishCentral = false;
                                bFirstDirectToNode = true;
                            }
                        }

                        Interlocked.Increment(ref _pendingKPIs.EventScheduledCount);

                        if (bPublished && EnableLogSentPayloads)
                        {
                            try
                            {
                                // Write log file to either
                                // (a) the log folder (if one is defined), or
                                // (b) the host's Clientbin folder.
                                var logFileName = (string.IsNullOrEmpty(TheBaseAssets.MySYSLOG.LogFilePath)) ?
                                    TheCommonUtils.cdeFixupFileName("meshsenderdata.log") :
                                    Path.Combine(TheBaseAssets.MySYSLOG.LogFilePath, "meshsenderdata.log");

                                File.AppendAllText(logFileName, $"{{\"TimePublished\":\"{DateTimeOffset.Now:O}\", \"PLS\": {msg.PLS}}},\r\n");
                            }
                            catch (Exception e)
                            {
                                TheBaseAssets.MySYSLOG.WriteToLog(95307, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, $"Unable to log data to file: {this.MyBaseThing.Address} - {thingUpdatesToSend.Count()}", eMsgLevel.l3_ImportantMessage, e.ToString()));
                            }
                        }

                        cancelToken.ThrowIfCancellationRequested();
                        if (DontWaitForAcks)
                        {
                            meshAckCS.TrySetResult(true);
                        }
                        if (GetPreserveOrderForSenderThing(senderThing))
                        {
                            try
                            {
                                await meshAckCS.Task.ConfigureAwait(false); // Stress: This does not always get canceled!
                                if (bRepublish)
                                {
                                    SendRepublishSuccess++;
                                    SendTimeoutCount--;
                                }
                                bRepublish = false;
                            }
                            catch
                            {
                                if (!bRepublish && !cancelToken.IsCancellationRequested)
                                {
                                    TheBaseAssets.MySYSLOG.WriteToLog(95312, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(MyBaseThing.EngineName, $"Message timed out: retrying once before giving up on the batch. {this.MyBaseThing.Address} - {thingUpdatesToSend.Count()}", eMsgLevel.l3_ImportantMessage, ""));
                                    bRepublish = true;
                                }
                                else
                                {
                                    bRepublish = false;
                                }
                            }
                        }
                    } while (bRepublish);
                    results.SendTasks.Add(meshAckCS.Task);
                    if (meshAckCS.Task.IsCanceled || meshAckCS.Task.IsFaulted)
                    {
                        // Send no further items to minimize out-of-order sending)
                        break;
                    }

                    batchLength += msg.PLS.Length;
                }
                else
                {
                    var taskCS = new TaskCompletionSource<bool>();
                    results.SendTasks.Add(taskCS.Task);
                    taskCS.TrySetException(new InvalidTransportEncodingException());
                }
            }
            CheckAckTimeouts();
            results.SizeSent = batchLength;
            return results;
        }

        cdeConcurrentDictionary<Guid, TaskCompletionSource<bool>> _pendingAcks = new cdeConcurrentDictionary<Guid, TaskCompletionSource<bool>>();

        Timer ackTimeoutTimer = null;
        object ackTimerLock = new object();

        DateTimeOffset lastAckTimeoutCheck = DateTimeOffset.MaxValue;
        TimeSpan AckIdlePeriod = new TimeSpan(0, 5, 0);
        TimeSpan ackTimeout = new TimeSpan(0, 0, 30);
        TimeSpan ackTimeoutCheck = new TimeSpan(0, 0, 35);
        private Timer myWatchDogTimer;

        void CheckAckTimeouts()
        {
            try
            {
                //lock (_pendingAcks)
                {
                    bool bPendingAcks = false;
                    foreach (var ackCS in _pendingAcks.GetDynamicEnumerable())
                    {
                        lastAckTimeoutCheck = DateTimeOffset.Now;
                        if (((DateTimeOffset)ackCS.Value.Task.AsyncState) + ackTimeout < DateTimeOffset.Now)
                        {
                            _pendingAcks.TryRemove(ackCS.Key, out var oldAckCS);
                            if (ackCS.Value.TrySetCanceled())
                            {
                                SendTimeoutCount++;
                            }
                        }
                        else
                        {
                            if (ackCS.Value.Task.IsCompleted || ackCS.Value.Task.IsFaulted || ackCS.Value.Task.IsCanceled)
                            {
                                _pendingAcks.TryRemove(ackCS.Key, out var oldAckCS);
                                if (ackCS.Value.TrySetCanceled())
                                {
                                    SendTimeoutCount++;
                                }
                            }
                            else
                            {
                                bPendingAcks = true;
                            }
                        }
                    }
                    lock (ackTimerLock)
                    {
                        if (bPendingAcks || (DateTimeOffset.Now - lastAckTimeoutCheck) < AckIdlePeriod)
                        {
                            if (ackTimeoutTimer == null)
                            {
                                ackTimeoutTimer = new System.Threading.Timer((o) => CheckAckTimeouts(), null, ackTimeoutCheck, ackTimeoutCheck);
                            }
                        }
                        else
                        {
                            if (ackTimeoutTimer != null)
                            {
                                try
                                {
                                    ackTimeoutTimer.Change(-1, -1);
                                    ackTimeoutTimer.Dispose();
                                }
                                catch { }
                                ackTimeoutTimer = null;
                            }
                        }
                    }
                }
            }
            catch
            {
            }
        }

    }

}