// SPDX-FileCopyrightText: 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

﻿/*********************************************************************
*
* Project Name" 180-CDMyMeshReceiver
*
* Description:
*
* Date of creation:
*
* Author:
*
* NOTES:
        "FldOrder" for UX 10 to 45 and 100 to 110
*********************************************************************/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Threading.Tasks;
using System.Threading;
using System.Collections.Concurrent;

using nsCDEngine.Engines;
using nsCDEngine.BaseClasses;
using nsCDEngine.Communication;
using nsCDEngine.ViewModels;
using nsCDEngine.Engines.StorageService;
using nsCDEngine.Engines.NMIService;
using nsCDEngine.Engines.ThingService;
using System.Text.RegularExpressions;

using nsTheReceiverBase;
using nsTheConnectionBase;
using nsTheEventConverters;

#if CDEPUBSUB
using MyProduct.Ipc.Cde;
using MyProduct.Ipc.PubSub.Cde;
#endif

namespace CDMyMeshReceiver.ViewModel
{
    public class TheMeshReceiver : TheReceiverBase<TheConnectionThing<TheConnectionThingParam>, TheConnectionThingParam>
    {

#region ThingProperties

        public string ReceiverEventConverter
        {
            get { return TheThing.GetSafePropertyString(MyBaseThing, nameof(ReceiverEventConverter)); }
            set { TheThing.SetSafePropertyString(MyBaseThing, nameof(ReceiverEventConverter), value); }
        }
        public string TimeDriftPropertyName
        {
            get { return TheThing.GetSafePropertyString(MyBaseThing, nameof(TimeDriftPropertyName)); }
            set { TheThing.SetSafePropertyString(MyBaseThing, nameof(TimeDriftPropertyName), value); }
        }
        public string SenderTimePropertyName
        {
            get { return TheThing.GetSafePropertyString(MyBaseThing, nameof(SenderTimePropertyName)); }
            set { TheThing.SetSafePropertyString(MyBaseThing, nameof(SenderTimePropertyName), value); }
        }

        public long PingCounter
        {
            get { return (long)TheThing.GetSafePropertyNumber(MyBaseThing, "PingCounter"); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, "PingCounter", value); }
        }

        #if CDEPUBSUB
        public string PubSubTopic
        {
            get { return TheThing.GetSafePropertyString(MyBaseThing, nameof(PubSubTopic)); }
            set { TheThing.SetSafePropertyString(MyBaseThing, nameof(PubSubTopic), value); }
        }
#endif

        // KPIs in UI

        //public long PropertiesSent
        //{
        //    get { return (long) TheThing.GetSafePropertyNumber(MyBaseThing, "PropertiesSent"); }
        //    set { TheThing.SetSafePropertyNumber(MyBaseThing, "PropertiesSent", value); }
        //}
        //public long PropertiesSentSinceStart
        //{
        //    get { return (long)TheThing.GetSafePropertyNumber(MyBaseThing, "PropertiesSentSinceStart"); }
        //    set { TheThing.SetSafePropertyNumber(MyBaseThing, "PropertiesSentSinceStart", value); }
        //}

        //public double PropertiesPerSecond
        //{
        //    get { return TheThing.GetSafePropertyNumber(MyBaseThing, "PropertiesPerSecond"); }
        //    set { TheThing.SetSafePropertyNumber(MyBaseThing, "PropertiesPerSecond", value); }
        //}

        //public double DataSentKBytesPerSecond
        //{
        //    get { return TheThing.GetSafePropertyNumber(MyBaseThing, "DataSentKBytesPerSecond"); }
        //    set { TheThing.SetSafePropertyNumber(MyBaseThing, "DataSentKBytesPerSecond", value); }
        //}
        //public double DataSentKBytesSinceStart
        //{
        //    get { return TheThing.GetSafePropertyNumber(MyBaseThing, "DataSentKBytesSinceStart"); }
        //    set { TheThing.SetSafePropertyNumber(MyBaseThing, "DataSentKBytesSinceStart", value); }
        //}
        //public double EventsPerSecond
        //{
        //    get { return TheThing.GetSafePropertyNumber(MyBaseThing, "EventsPerSecond"); }
        //    set { TheThing.SetSafePropertyNumber(MyBaseThing, "EventsPerSecond", value); }
        //}
        //public long EventsSent
        //{
        //    get { return (long)TheThing.GetSafePropertyNumber(MyBaseThing, "EventsSent"); }
        //    set { TheThing.SetSafePropertyNumber(MyBaseThing, "EventsSent", value); }
        //}
        //public long EventsSentSinceStart
        //{
        //    get { return (long)TheThing.GetSafePropertyNumber(MyBaseThing, "EventsSentSinceStart"); }
        //    set { TheThing.SetSafePropertyNumber(MyBaseThing, "EventsSentSinceStart", value); }
        //}
        //public long PendingEvents
        //{
        //    get { return (long) TheThing.GetSafePropertyNumber(MyBaseThing, "PendingEvents"); }
        //    set { TheThing.SetSafePropertyNumber(MyBaseThing, "PendingEvents", value); }
        //}
        //public DateTimeOffset KPITime
        //{
        //    get { return TheThing.GetSafePropertyDate(MyBaseThing, "KPITime"); }
        //    set { TheThing.SetSafePropertyDate(MyBaseThing, "KPITime", value); }
        //}

        //public DateTimeOffset LastReceiveTime
        //{
        //    get { return TheThing.GetSafePropertyDate(MyBaseThing, nameof(LastReceiveTime)); }
        //    set { TheThing.SetSafePropertyDate(MyBaseThing, nameof(LastReceiveTime), value); }
        //}

        #endregion

        public override void HandleMessage(ICDEThing sender, object pIncoming)
        {
            TheProcessMessage pMsg = pIncoming as TheProcessMessage;
            if (pMsg == null || pMsg.Message == null) return;

            var cmd = pMsg.Message.TXT.Split(':');
            switch (cmd[0])
            {
                case "MESHSENDER_DATA":
                    if (IsConnected || AutoConnect)
                    {
                        //TheBaseAssets.MySYSLOG.WriteToLog(180001, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("Mesh Receiver", $"Received TSM with TXT MESHSENDER_DATA {this.MyBaseThing.FriendlyName}: {pMsg.Message.ToString()}", eMsgLevel.l6_Debug));
                        bool bSuccess = false;
                        //bool bSendAck = true;
                        string error = "";

                        bool bIsTargeted = pMsg.Topic.StartsWith("CDE_SYSTEMWIDE");

                        string correlationToken = null;
                        if (cmd.Length >= 2)
                        {
                            correlationToken = cmd[1];
                        }
                        string eventConverterName = null;
                        if (cmd.Length >= 3)
                        {
                            eventConverterName = cmd[2];
                        }

                        //if (EnableDataLogging)
                        //{
                        //    try
                        //    {
                        //        lock (dataLoggerLock)
                        //        {
                        //            System.IO.File.AppendAllText("meshreceiverdata.log", $"{{\"TimeReceived\":\"{DateTimeOffset.Now:O}\", \"PLS\": {pMsg.Message.PLS},\"TXT\":{pMsg.Message.TXT}}},\r\n");
                        //        }
                        //    }
                        //    catch (Exception e)
                        //    {
                        //        TheBaseAssets.MySYSLOG.WriteToLog(180003, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("Mongo Writer", $"Unable to log data to file: {this.MyBaseThing.FriendlyName}", eMsgLevel.l3_ImportantMessage, e.ToString()));
                        //    }
                        //}

                        bSuccess = ProcessMessage(correlationToken, eventConverterName, pMsg.Message.TIM, pMsg.Message.PLS, pMsg.Message.ORG, bIsTargeted, out error, out var bSendAck);

                        if (bSendAck)
                        {
                            TSM response = new TSM(MyBaseEngine.GetEngineName(), $"MESHSENDER_DATA_ACK:{correlationToken}:{bSuccess}:{error}");
                            response.QDX = pMsg.Message.QDX;
                            TheBaseAssets.MySYSLOG.WriteToLog(180001, TSM.L(eDEBUG_LEVELS.FULLVERBOSE) ? null : new TSM("Mesh Receiver", $"Sending ACK for {this.MyBaseThing.FriendlyName} to ORG '{pMsg.Message.ORG}'", eMsgLevel.l1_Error, response.TXT));
                            TheCommCore.PublishToOriginator(pMsg.Message, response, true);

                            if (!bIsTargeted)
                            {
                                var notification = new TSM(MyBaseEngine.GetEngineName(), $"MESHRECEIVER_ACK_NOTIFY:{correlationToken}:{pMsg.Message.ORG}:{bIsTargeted}");
                                TheCommCore.PublishCentral(notification, false);
                            }
                        }
                    }

                    break;
                case "MESHRECEIVER_ACK_NOTIFY":
                    {
                        if (cmd.Length >= 4)
                        {
                            string correlationToken = cmd[1];
                            string sourceORG = cmd[2];
                            bool bIsTargeted = TheCommonUtils.CBool(cmd[3]);
                            _nodeOwnerManager.RegisterOwnerCandidate(correlationToken, sourceORG, pMsg.Message.ORG, bIsTargeted);
                        }
                        break;
                    }
                case "MESHSENDER_PING":
                case "MESHSENDER_PING_ALL":
                    {
                        string correlationToken = "nocorrelationtoken";
                        if (cmd.Length > 1)
                        {
                            correlationToken = cmd[1];
                        }

                        // MyBaseThing.LastMessage = DateTimeOffset.Now + String.Format(": {0} from {1}. Token {2}", cmd[0], TheCommonUtils.cdeGuidToString(pMsg.Message.GetOriginator()), correlationToken);
                        MyBaseThing.LastMessage = $"{DateTimeOffset.Now}: {cmd[0]} from {TheCommonUtils.cdeGuidToString(pMsg.Message.GetOriginator())}. Token {correlationToken}";

                        //TSM response = new TSM(MyBaseEngine.GetEngineName(), String.Format("{0}_ACK:{1}", cmd[0], correlationToken),
                        //    String.Format("{0}:{1}:{2}:{3}", TheCommonUtils.cdeGuidToString(MyBaseThing.cdeMID), ++PingCounter, MyBaseThing.FriendlyName, IsConnected));
                        TSM response = new TSM(MyBaseEngine.GetEngineName(), $"{cmd[0]}_ACK:{correlationToken}",
                             $"{TheCommonUtils.cdeGuidToString(MyBaseThing.cdeMID)}:{++PingCounter}:{MyBaseThing.FriendlyName}:{IsConnected}");
                        TheCommCore.PublishToOriginator(pMsg.Message, response, true);
                    }
                    break;
                default:
                    base.HandleMessage(sender, pIncoming);
                    break;
            }
        }

        private bool ProcessMessage(string correlationToken, string eventConverterName, DateTimeOffset messageTime, string messagePayload, string originator, bool bIsTargetedMessage, out string error, out bool bSendAck)
        {
            bool bSuccess = false;
            bSendAck = true;

            if (bIsTargetedMessage)
            {
                if (_nodeOwnerManager.RegisterOwnerCandidate(correlationToken, originator, TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID.ToString(), true))
                {
                    var notification = new TSM(MyBaseEngine.GetEngineName(), $"MESHRECEIVER_ACK_NOTIFY:{correlationToken}:{bIsTargetedMessage}");
                    TheCommCore.PublishCentral(notification, false);
                }
            }
            else
            {
                if (_nodeOwnerManager.CheckOwner(originator, TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID.ToString()) != true)
                {
                    _nodeOwnerManager.RequestOwnership(correlationToken, originator, TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID.ToString());
                }
            }

            IEventConverter eventConverter = null;
            if (!string.IsNullOrEmpty(eventConverterName))
            {
                eventConverter = TheEventConverters.GetEventConverter(eventConverterName);
                if (eventConverter == null)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(180001, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("Mesh Receiver", $"Unknown event converter in incoming MESHSENDER_DATA {this.MyBaseThing.FriendlyName}: {eventConverterName}", eMsgLevel.l1_Error, TSM.L(eDEBUG_LEVELS.OFF) ? null : messagePayload));
                }
            }
            else
            {
                eventConverter = TheEventConverters.GetEventConverter(ReceiverEventConverter);
                if (eventConverter == null)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(180002, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("Mesh Receiver", $"Unknown default event converter {this.MyBaseThing.FriendlyName}: {ReceiverEventConverter}", eMsgLevel.l1_Error));
                }
            }

            //if (EnableDataLogging)
            //{
            //    try
            //    {
            //        lock (dataLoggerLock)
            //        {
            //            System.IO.File.AppendAllText("meshreceiverdata.log", $"{{\"TimeReceived\":\"{DateTimeOffset.Now:O}\", \"PLS\": {pMsg.Message.PLS},\"TXT\":{pMsg.Message.TXT}}},\r\n");
            //        }
            //    }
            //    catch (Exception e)
            //    {
            //        TheBaseAssets.MySYSLOG.WriteToLog(180003, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("Mongo Writer", $"Unable to log data to file: {this.MyBaseThing.FriendlyName}", eMsgLevel.l3_ImportantMessage, e.ToString()));
            //    }
            //}

            if (eventConverter == null)
            {
                error = "Unknown event converter";
            }
            else
            {
                if (IsConnected)
                {
                    try
                    {
                        var now = DateTimeOffset.Now;
                        if (!string.IsNullOrEmpty(TimeDriftPropertyName))
                        {
                            // Calculate time difference (in minutes) between the sender and this node
                            var timeDrift = now - messageTime;
                            timeDrift = TimeSpan.FromMinutes(timeDrift.TotalMinutes > 0 ? Math.Floor(timeDrift.TotalMinutes) : Math.Ceiling(timeDrift.TotalMinutes));
                            if (eventConverter.StaticPropsToAdd == null)
                            {
                                eventConverter.StaticPropsToAdd = new Dictionary<string, object>();
                            }
                            eventConverter.StaticPropsToAdd[TimeDriftPropertyName] = System.Xml.XmlConvert.ToString(timeDrift);
                        }
                        if (!string.IsNullOrEmpty(SenderTimePropertyName))
                        {
                            if (eventConverter.StaticPropsToAdd == null)
                            {
                                eventConverter.StaticPropsToAdd = new Dictionary<string, object>();
                            }
                            eventConverter.StaticPropsToAdd[SenderTimePropertyName] = messageTime;
                        }
                        bool bDetectedOtherOwner = false;
                        // TODO Support picking a thing for formats that don't carry a thing id
                        eventConverter.NewThingCallback = async (t) =>
                        {
                            if (await _nodeOwnerManager.CheckOwnerAsync(correlationToken, originator, TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID.ToString()).ConfigureAwait(false) == false)
                            {
                                bDetectedOtherOwner = true;
                                return false;
                            }
                            return true;
                        };
                        error = eventConverter.ProcessEventData(null, messagePayload, now);
                        if (bDetectedOtherOwner)
                        {
                            error = "Detected other receiver";
                            bSendAck = false;
                            bSuccess = false;
                        }
                        else
                        {
                            if (String.IsNullOrEmpty(error))
                            {
                                ReceiveCount++;
                                bSuccess = true;
                            }
                            else
                            {
                                TheBaseAssets.MySYSLOG.WriteToLog(180004, new TSM("Mesh Receiver", "Error processing data", eMsgLevel.l1_Error, error));
                                ReceiveErrorCount++;
                            }
                            LastReceiveTime = now;
                        }
                    }
                    catch (Exception e)
                    {
                        error = "Error parsing data: " + e.Message;
                        TheBaseAssets.MySYSLOG.WriteToLog(180005, new TSM("Mesh Receiver", "Error processing data", eMsgLevel.l1_Error, e.ToString()));
                        bSuccess = false;
                        ReceiveErrorCount++;
                    }

                    if (bSuccess)
                    {
                        MyBaseThing.StatusLevel = 1;
                    }
                }
                else
                {
                    error = "Not connected";
                }
            }

            return bSuccess;
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
        }

        class ThePubSubAckMessage
        {
            public string MsgId;
            public bool Success;
            public string Error;
            public TheAckInfo AckInfo;
        }

        private void ProcessPubSubMessage(DateTimeOffset time, string channel, string message)
        {
            try
            {
                var pubSubMessage = TheCommonUtils.DeserializeJSONStringToObject<ThePubSubMessage>(message);

                var bSuccess = ProcessMessage(pubSubMessage.MsgId, pubSubMessage.PayloadFormat, time, pubSubMessage.Payload, null, true, out var ReceiveErrorCount, out var bSendAck);
                if (bSendAck)
                {
                    var ackMessage = new ThePubSubAckMessage
                    {
                        MsgId = pubSubMessage.MsgId,
                        Error = ReceiveErrorCount,
                        Success = bSuccess,
                    };
                    var ackMessageString = TheCommonUtils.SerializeObjectToJSONString(ackMessage);
                    _publisher.Publish($"{channel}/ack", ackMessageString);
                }
            }
            catch { }
        }
#endif

        NodeOwnerManager _nodeOwnerManager = new NodeOwnerManager();

        class NodeOwnerManager
        {
            // TODO Use Paxos consensus protocol to determine owner node reliably. Failure mode right now is that more nodes than necessary end up with a thing, but this failure mode occurs also in reliable mode (in rare cases like prolongued node failure)
            // TODO consider load of the nodes in determining the owner (i.e. # of things being processed)

            cdeConcurrentDictionary<Guid, NodeOwnerInfo> _nodeOwners = new cdeConcurrentDictionary<Guid, NodeOwnerInfo>();
            class NodeOwnerInfo
            {
                public string SourceORG;
                public string OwnerORG;
                public bool WasTarget;
                public DateTimeOffset TimeObserved;
            }

            cdeConcurrentDictionary<string, TaskCompletionSource<bool>> _pendingAcks = new cdeConcurrentDictionary<string, TaskCompletionSource<bool>>();
            Task _ackCleanupTask = null;
            internal void RequestOwnership(string correlationToken, string sourceORG, string ownerORG)
            {
                _pendingAcks.TryAdd(correlationToken, new TaskCompletionSource<bool>(DateTimeOffset.Now + new TimeSpan(0,1,0)));

                if (_ackCleanupTask == null)
                {
                    _ackCleanupTask = TheCommonUtils.cdeRunTaskAsync("ackCleanup", async (o) =>
                    {
                        try
                        {
                            while (!_pendingAcks.IsEmpty && TheBaseAssets.MasterSwitch)
                            {
                                try
                                {
                                    await TheCommonUtils.TaskDelayOneEye(61000, 100).ConfigureAwait(false);
                                }
                                catch { }
                                var now = DateTimeOffset.Now;
                                foreach (var ack in _pendingAcks.GetDynamicEnumerable())
                                {
                                    try
                                    {
                                        var expiration = (DateTimeOffset?)ack.Value?.Task?.AsyncState;
                                        if (expiration < now)
                                        {
                                            _pendingAcks.RemoveNoCare(ack.Key);
                                        }
                                    }
                                    catch { }
                                }
                            }
                        }
                        catch { }
                        finally
                        {
                            _ackCleanupTask = null;
                        }
                    }).ContinueWith(c => { var ignored = c.Exception; }, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
                }
            }

            /// <summary>
            /// Synchronously checks if a node is the current owner for a source.
            /// </summary>
            /// <param name="sourceORG"></param>
            /// <param name="ownerORG"></param>
            /// <returns></returns>
            internal bool? CheckOwner(string sourceORG, string ownerORG)
            {
                if (_nodeOwners.TryGetValue(TSM.GetOriginator(sourceORG), out var nodeInfo))
                {
                    if (TSM.GetOriginator(nodeInfo.OwnerORG) == TSM.GetOriginator(ownerORG))
                    {
                        return true;
                    }
                    return false;
                }
                return null;
            }

            /// <summary>
            /// Checks if a node is the current owner for the given source. Waits for a random time, up to 5 seconds, for an owner ship ack from other nodes before doing the actual check.
            /// </summary>
            /// <param name="correlationToken"></param>
            /// <param name="sourceORG"></param>
            /// <param name="ownerORG"></param>
            /// <returns></returns>
            internal async Task<bool?> CheckOwnerAsync(string correlationToken, string sourceORG, string ownerORG)
            {
                if (_pendingAcks.TryGetValue(correlationToken, out var taskCS))
                {
                    try
                    {
                        await TheCommonUtils.TaskWaitTimeout(taskCS.Task, new TimeSpan(0, 0, 5) + new TimeSpan(0, 0, 0, 0, (int)TheCommonUtils.GetRandomUInt(0, 5000))).ConfigureAwait(false);
                    }
                    catch { }
                }
                return CheckOwner(sourceORG, ownerORG);
            }

            /// <summary>
            /// Registers an owner for a given source
            /// </summary>
            /// <param name="correlationToken"></param>
            /// <param name="sourceORG"></param>
            /// <param name="ownerORG"></param>
            /// <param name="bIsTargeted"></param>
            /// <returns>true if the owner has changed or was newly registered</returns>
            internal bool RegisterOwnerCandidate(string correlationToken, string sourceORG, string ownerORG, bool bIsTargeted)
            {
                string previousOwnerORG = null;
                var newOwnerInfo = _nodeOwners.AddOrUpdate(TSM.GetOriginator(sourceORG), new NodeOwnerInfo { OwnerORG = ownerORG, SourceORG = sourceORG, WasTarget = bIsTargeted, TimeObserved = DateTimeOffset.Now }, (s, nodeInfo) =>
                {
                    if (bIsTargeted || !nodeInfo.WasTarget || nodeInfo.TimeObserved < DateTimeOffset.Now - new TimeSpan(0,5,0))
                    {
                        previousOwnerORG = nodeInfo.OwnerORG;
                        nodeInfo.OwnerORG = ownerORG;
                        nodeInfo.WasTarget = bIsTargeted;
                        nodeInfo.TimeObserved = DateTimeOffset.Now;
                    }
                    return nodeInfo;
                });
                _pendingAcks.RemoveNoCare(correlationToken);
                return TSM.GetOriginator(newOwnerInfo.OwnerORG) != TSM.GetOriginator(previousOwnerORG);
            }

        }

        public TheMeshReceiver(TheThing pThing, ICDEPlugin pPluginBase) : base (pThing, pPluginBase)
        {
            //TheAzureConnectionHelper.InitializeThing(pThing);
        }

        public override bool Init()
        {
            if (mIsInitCalled) return false;
            mIsInitCalled = true;

            MyBaseEngine.RegisterEvent(eEngineEvents.IncomingMessage, HandleMessage);
            base.Init();
            mIsInitialized = InitBase("Mesh Receiver", MeshDeviceTypes.MeshReceiver);

            MyBaseThing.StatusLevel = IsConnected ? 1 : 0;

            return mIsInitialized;
        }

        public override bool CreateUX()
        {
            if (mIsUXInitCalled) return false;
            mIsUXInitCalled = true;

            base.CreateUXBase("Mesh Receiver");
            if (MyForm != null)
            {
                MyForm.DeleteByOrder(11);
                MyForm.DeleteByOrder(24); //Removes the ADDRES Field from the Form
                //TheNMIEngine.AddSmartControl(MyBaseThing, MyForm, eFieldType.CollapsibleGroup, 40, 2, 0xC0, "Advanced Configurations...", null, ThePropertyBag.Create(new nmiCtrlCollapsibleGroup() { DoClose = true }));//() { "TileWidth=6", "Format=Advanced Configurations", "Style=font-size:26px;text-align: left" });
                //TheNMIEngine.AddSmartControl(MyBaseThing, MyForm, eFieldType.Password, 41, 7, 0xC0, "Connection String", TheAzureConnectionHelper.strConnectionString, new ThePropertyBag() { "ParentFld=40", "TileWidth=6", "TileHeight=1", "HideMTL=true" });
                //TheNMIEngine.AddSmartControl(MyBaseThing, MyForm, eFieldType.SingleEnded, 42, 7, 0xC0, "Event Hub Name", TheAzureConnectionHelper.strEventHubName, new ThePropertyBag() { "ParentFld=40", "TileWidth=6", "TileHeight=1" });
                //TheNMIEngine.AddSmartControl(MyBaseThing, MyForm, eFieldType.SingleEnded, 43, 0, 0xC0, "Event Hub Address:", "Address", new ThePropertyBag() { "ParentFld=40", "TileHeight=1", "TileWidth=6" });
                //TheNMIEngine.AddSmartControl(MyBaseThing, MyForm, eFieldType.SingleEnded, 44, 7, 0xC0, "Consumer Group", strConsumerGroup, new ThePropertyBag() { "ParentFld=40", "TileWidth=6", "TileHeight=1" });
                //TheNMIEngine.AddSmartControl(MyBaseThing, MyForm, eFieldType.Password, 45, 7, 0xC0, "Storage Connection String (Checkpoints)", strStorageConnectionString, new ThePropertyBag() { "ParentFld=40", "TileWidth=6", "TileHeight=1", "HideMTL=true" });
                //TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.SingleCheck, 46, 3, 0xC0, "Preserve Order for all Things", "PreserveSendOrderAllThings", new ThePropertyBag() { "TileWidth=6", "TileHeight=1" });

                TheNMIEngine.AddSmartControl(MyBaseThing, MyForm, eFieldType.ComboBox, 24, 2, 0xC0, "Receiver Event Converter", nameof(ReceiverEventConverter), new nmiCtrlComboBox { ParentFld=20, DefaultValue = TheEventConverters.GetDisplayName(typeof(JSonObjectEventConverter)), Options = TheEventConverters.GetDisplayNamesAsSemicolonSeperatedList() });
                TheNMIEngine.AddSmartControl(MyBaseThing, MyForm, eFieldType.SingleEnded, 30, 2, 0xC0, "Property for Sender Time:", nameof(SenderTimePropertyName), new ThePropertyBag() { "ParentFld=20", "TileHeight=1", "TileWidth=6" });
                TheNMIEngine.AddSmartControl(MyBaseThing, MyForm, eFieldType.SingleEnded, 40, 2, 0xC0, "Property for Time Drift:", nameof(TimeDriftPropertyName), new ThePropertyBag() { "ParentFld=20", "TileHeight=1", "TileWidth=6" });
#if CDEPUBSUB
                TheNMIEngine.AddSmartControl(MyBaseThing, MyForm, eFieldType.SingleEnded, 45, 2, 0xC0, "PubSub Topic", nameof(PubSubTopic), new ThePropertyBag() { "ParentFld=20", "TileHeight=1", "TileWidth=6" });
#endif
                //TheNMIEngine.AddSmartControl(MyBaseThing, MyForm, eFieldType.Number, 310, 0, 0, "Last Received", nameof(LastReceiveTime), new ThePropertyBag() { "ParentFld=300", "TileHeight=1", "TileWidth=6", "FldWidth=4" });

                mIsUXInitialized = true;
                return true;
            }
            return false;
        }


        public override void Connect()
        {
            if (this.Connecting)
            {
                return;
            }
            try
            {
#if CDEPUBSUB
                ConnectPubSub();
#endif
                IsConnected = true;
                MyBaseThing.StatusLevel = 1;
                TheBaseAssets.MySYSLOG.WriteToLog(180006, new TSM(MyBaseThing.EngineName, "Connect", eMsgLevel.l3_ImportantMessage, String.Format("Started Mesh Receiver {0}", this.GetBaseThing().FriendlyName)));
                MyBaseThing.LastMessage = DateTimeOffset.Now + ": Running";
            }
            catch (Exception e)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(180007, new TSM(MyBaseThing.EngineName, "Connect", eMsgLevel.l1_Error, String.Format("Unable to start Mesh Receiver {0}. Exception: {1}", this.GetBaseThing().FriendlyName, TheCommonUtils.GetAggregateExceptionMessage(e))));
                MyBaseThing.LastMessage = DateTimeOffset.Now + ": Unable to connect - " + TheCommonUtils.GetAggregateExceptionMessage(e);
            }
            finally
            {
                this.Connecting = false;
            }

        }

#if CDEPUBSUB
#if !NET35
        private Subscriber _subscriber;
        private Publisher _publisher;
#endif
        private CancellationTokenSource _pubsubCancellationTokenSource;

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
            _pubsubCancellationTokenSource = new CancellationTokenSource();

            TheBaseEngine.WaitForEnginesStartedAsync().Wait();
            TheBaseEngine.WaitForStorageReadinessAsync(true).Wait();

            var comLine = Operator.GetLine(MyBaseThing);

            _subscriber = new Subscriber(comLine, _pubsubCancellationTokenSource.Token);
            //if (!string.IsNullOrEmpty(PubSubAckTopic))
            {
                //var catchAllSubscription = _subscriber.Subscribe("*");
                _subscriber.Subscribe(PubSubTopic)
                    .With((time, channel, message) =>
                    {
                        ProcessPubSubMessage(time, channel, message);
                    });

            }
            _publisher = new Publisher(comLine, _pubsubCancellationTokenSource.Token);
            _publisher.ConnectAsync().ContinueWith(t => t.Result.Publish("public:debug", $"Publisher {MyBaseThing.FriendlyName} initialized"));
#endif
        }
#endif

        public override void UpdateConnectionThing(TheConnectionThing<TheConnectionThingParam> currentThing, TheConnectionThing<TheConnectionThingParam> newThing)
        {
            // Nothing to do: changes get picked up dynamically
        }



        public override void Disconnect(bool bDrain)
        {
            try
            {
                this.Disconnecting = true;
                TheBaseAssets.MySYSLOG.WriteToLog(180008, new TSM(MyBaseThing.EngineName, "Disconnect", eMsgLevel.l6_Debug, String.Format("Closing Mesh Receiver for connection {0}", this.GetBaseThing().FriendlyName)));

#if CDEPUBSUB
                _pubsubCancellationTokenSource?.Cancel();
                _pubsubCancellationTokenSource = null;
#endif
                TheBaseAssets.MySYSLOG.WriteToLog(180009, new TSM(MyBaseThing.EngineName, "Disconnect", eMsgLevel.l3_ImportantMessage, String.Format("Stopped Mesh Receiver {0}", this.GetBaseThing().FriendlyName)));
                MyBaseThing.LastMessage = DateTimeOffset.Now + ": Stopped.";
                IsConnected = false;
                MyBaseThing.StatusLevel = 0;
            }
            catch (Exception e)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(180010, new TSM(MyBaseThing.EngineName, "Disconnect", eMsgLevel.l1_Error, String.Format("Unable to stop Mesh Receiver {0}. Exception: {1}", this.GetBaseThing().FriendlyName, TheCommonUtils.GetAggregateExceptionMessage(e))));
                MyBaseThing.LastMessage = DateTimeOffset.Now + ": Unable to stop - " + e.Message;
            }
            finally
            {
                this.Disconnecting = false;
            }
        }

    }

}
