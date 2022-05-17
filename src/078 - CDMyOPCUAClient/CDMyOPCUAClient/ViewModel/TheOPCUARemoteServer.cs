// SPDX-FileCopyrightText: 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0
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
using System.Security.Cryptography.X509Certificates;
using System.IO;
using CDMyOPCUAClient.Contracts;
using nsTheSenderBase;
using System.Diagnostics;
using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Client.Controls;
using Opc.Ua.Configuration;
#if ENABLE_CSVIMPORT
using nsTheCSVParser;
#endif
using System.Threading.Tasks;

namespace CDMyOPCUAClient.ViewModel
{
    [DeviceType(
        DeviceType = eOPCDeviceTypes.OPCRemoteServer,
        Capabilities = new eThingCaps[] { eThingCaps.SensorProvider, eThingCaps.ConfigManagement }, 
        Description="OPC UA Client")]
    public partial class TheOPCUARemoteServer : TheThingBase, IDisposable
    {
        #region Message Handlers
        public override void HandleMessage(ICDEThing sender, object pIncoming)
        {
            TheProcessMessage pMsg = pIncoming as TheProcessMessage;
            if (pMsg == null || pMsg.Message == null) return;

            string[] cmd = pMsg.Message.TXT.Split(':');
            switch (cmd[0])
            {
                case "RUREADY":
                    if (cmd.Length > 1 && cmd[1] == TheCommonUtils.cdeGuidToString(MyBaseThing.cdeMID))
                    {
                        TheCommCore.PublishToOriginator(pMsg.Message, new TSM(pMsg.Message.ENG, "IS_READY:" + TheCommonUtils.cdeGuidToString(MyBaseThing.cdeMID), mIsInitialized.ToString()) { FLG = 8 }, true);
                    }
                    break;
                case "CONNECT_SERVER":
                    {
                        Connect(false);
                    }
                    break;
                case "DISCONNECT_SERVER":
                    {
                        Disconnect(false, false, "Disconnect requested via TSM"); // Was: IsConnected = false;
                    }
                    break;
                case nameof(MsgOPCUAConnect):
                    {
                        var request = TheCommRequestResponse.ParseRequestMessageJSON<MsgOPCUAConnect>(pMsg.Message);
                        var response = HandleConnectMessage(request);
                        TheCommRequestResponse.PublishResponseMessageJson(pMsg.Message, response);
                    }
                    break;
                case nameof(MsgOPCUADisconnect):
                    {
                        var request = TheCommRequestResponse.ParseRequestMessageJSON<MsgOPCUADisconnect>(pMsg.Message);
                        var response = HandleDisconnectMessage(pMsg, request);
                        TheCommRequestResponse.PublishResponseMessageJson(pMsg.Message, response);
                    }
                    break;
                case "CREATE_TAGS":
                    // Format: JSon List of :;: separated values:
                    // <UATAG|UAMETHOD> : Tag or Method
                    //  <DisplayName> : OPC DisplayName
                    //  <parent> : Display name of the parent (TODO: [Markus] Do we really need this? Check with Chris)
                    //  <nodeid> : OPC NodeId (NodeId.ToString(), consume via NodeId.Parse()
                    //  <samplerate> : Sample rate in ms, for use in OPC Monitored Item
                    //  <host thing MID> : MID of thing into which to place the property
                    TSM tNewTSM2 = new TSM(MyBaseThing.EngineName, "CREATE_TAGS_RESULT");
                    try
                    {
                        if (ConnectionState != ConnectionStateEnum.Connected)
                            tNewTSM2.PLS = "ERROR: Not connected";
                        else
                        {
                            //MyTags.MyMirrorCache.Clear(false);
                            List<string> pSubs = TheCommonUtils.DeserializeJSONStringToObject<List<string>>(pMsg.Message.PLS);
                            List<string> tIDs = CreateTagSubscriptions(pSubs);
                            tNewTSM2.PLS = TheCommonUtils.SerializeObjectToJSONString<List<string>>(tIDs);
                        }
                    }
                    catch (Exception e)
                    {
                        tNewTSM2.PLS = "ERROR: " + e.Message;
                    }
                    if (pMsg.LocalCallback != null)
                        pMsg.LocalCallback(tNewTSM2);
                    TheCommCore.PublishToOriginator(pMsg.Message, tNewTSM2);
                    break;
                case nameof(MsgOPCUACreateTags):
                    {
                        var request = TheCommRequestResponse.ParseRequestMessageJSON<MsgOPCUACreateTags>(pMsg.Message);
                        var response = HandleCreateTagsMessage(request);
                        TheCommRequestResponse.PublishResponseMessageJson(pMsg.Message, response);
                    }
                    break;
                case nameof(MsgOPCUAReadTags):
                    {
                        var request = TheCommRequestResponse.ParseRequestMessageJSON<MsgOPCUAReadTags>(pMsg.Message);
                        var response = HandleReadTagsMessage(request);
                        TheCommRequestResponse.PublishResponseMessageJson(pMsg.Message, response);
                    }
                    break;

                #region Sensor Provider messages
                case nameof(TheThing.MsgBrowseSensors):
                    {
                        var request = TheCommRequestResponse.ParseRequestMessageJSON<TheThing.MsgBrowseSensors>(pMsg.Message);
                        var response = HandleBrowseMessage();
                        TheCommRequestResponse.PublishResponseMessageJson(pMsg.Message, response);
                    }
                    break;

                case nameof(TheThing.MsgSubscribeSensors):
                    {
                        var request = TheCommRequestResponse.ParseRequestMessageJSON<TheThing.MsgSubscribeSensors<TheOPCSensorSubscription>>(pMsg.Message);
                        var response = HandleSubscribeSensorsMessage(request);
                        TheCommRequestResponse.PublishResponseMessageJson(pMsg.Message, response);
                    }
                    break;

                case nameof(TheThing.MsgUnsubscribeSensors):
                    {
                        var request = TheCommRequestResponse.ParseRequestMessageJSON<TheThing.MsgUnsubscribeSensors>(pMsg.Message);
                        var response = HandleUnsubscribeSensorsMessage(request);
                        TheCommRequestResponse.PublishResponseMessageJson(pMsg.Message, response);
                    }
                    break;


                case nameof(TheThing.MsgGetSensorSubscriptions):
                    {
                        var request = TheCommRequestResponse.ParseRequestMessageJSON<TheThing.MsgGetSensorSubscriptions<TheOPCSensorSubscription>>(pMsg.Message);
                        var response = HandleGetSensorSubscriptionsMessage(request);
                        TheCommRequestResponse.PublishResponseMessageJson(pMsg.Message, response);
                    }
                    break;


                #endregion

                case nameof(MsgOPCUABrowse):
                    {
                        var request = TheCommRequestResponse.ParseRequestMessageJSON<MsgOPCUABrowse>(pMsg.Message);

                        MsgOPCUABrowseResponse response = HandleOPCUABrowse();
                        TheCommRequestResponse.PublishResponseMessageJson(pMsg.Message, response);
                    }
                    break;
                case "BROWSE":
                    bool SendResults = true;
                    if (String.IsNullOrEmpty(pMsg.Message.PLS))
                    {
                        if (MyTags.MyMirrorCache.Count == 0)
                            SendResults = Browser(currentRoot, currentRoot.ToString(), true, false, null);
                        if (SendResults)
                        {
                            TSM tNewTSM = new TSM(MyBaseThing.EngineName, "BROWSE_RESULT", TheCommonUtils.SerializeObjectToJSONString<List<TheOPCTag>>(MyTags.MyMirrorCache.TheValues));
                            if (pMsg.LocalCallback != null)
                                pMsg.LocalCallback(tNewTSM);
                            TheCommCore.PublishToOriginator(pMsg.Message, tNewTSM);
                        }
                    }
                    else
                    {
                        // TODO Return browse result, report errors. For now this just browses the nodes for servers that don't allow direct access by NodeId without prior browsing (like some TruLaser versions)
                        var nodeId = new NodeId(pMsg.Message.PLS);
                        var bSuccess = Browser(new ReferenceDescription { NodeId = nodeId, }, nodeId.ToString(), true, false, null);
                        TSM tNewTSM = new TSM(MyBaseThing.EngineName, "BROWSE_RESULT", bSuccess ? "" : "ERROR: Failure browsing");
                        if (pMsg.LocalCallback != null)
                            pMsg.LocalCallback(tNewTSM);
                        TheCommCore.PublishToOriginator(pMsg.Message, tNewTSM);
                    }
                    break;

                #region Config Management messages
                case nameof(TheThing.MsgExportConfig):
                    {
                        var request = TheCommRequestResponse.ParseRequestMessageJSON<TheThing.MsgExportConfig>(pMsg.Message);
                        var response = new TheThing.MsgExportConfigResponse { Error = "Internal error" };

                        // No custom config beyond config properties and subscriptions
                        response.Error = null;

                        TheCommRequestResponse.PublishResponseMessageJson(pMsg.Message, response);
                    }
                    break;
                case nameof(TheThing.MsgApplyConfig):
                    {
                        var request = TheCommRequestResponse.ParseRequestMessageJSON<TheThing.MsgApplyConfig>(pMsg.Message);
                        var response = new TheThing.MsgApplyConfigResponse { Error = "Internal error" };

                        // No custom config beyond config properties and subscriptions, but must reconnect

                        try
                        {
                            if (!request.ConfigurationPending)
                            {

                                if (IsTargetStateConnected)
                                {
                                    var sw = new Stopwatch();
                                    sw.Start();
                                    while (ConnectionState == ConnectionStateEnum.Connecting && sw.ElapsedMilliseconds < 45000)
                                    {
                                        TheCommonUtils.SleepOneEye(100, 100);
                                    }
                                    Disconnect(false, true, "Configuration applied.");
                                    response.Error = Connect(false);
                                }
                                else
                                {
                                    response.Error = null;
                                }
                            }
                            else
                            {
                                response.Error = null;
                            }
                        }

                        catch (Exception e)
                        {
                            response.Error = $"Internal error: {e.Message}";
                        }
                        TheCommRequestResponse.PublishResponseMessageJson(pMsg.Message, response);
                    }
                    break;
                    #endregion
            }
        }

        private MsgOPCUABrowseResponse HandleOPCUABrowse()
        {
            var response = new MsgOPCUABrowseResponse { Error = "Internal error" };
            try
            {

                bool SendResults2 = true;
                if (MyTags.MyMirrorCache.Count == 0)
                    SendResults2 = Browser(currentRoot, currentRoot.ToString(), true, false, null);
                if (SendResults2)
                {
                    var tEcht = MyTags.MyMirrorCache.TheValues.Select(t => new MsgOPCUACreateTags.TagInfo { NodeId = t.NodeIdName, DisplayName = t.DisplayName }).ToList();
                    response.Tags = tEcht;
                }
                else
                {
                    response.Error = "Error during browse";
                }
            }
            catch (Exception e)
            {
                response.Error = $"Internal error: {e.Message}";
            }

            return response;
        }

        private TheThing.MsgBrowseSensorsResponse HandleBrowseMessage()
        {
            var response = new TheThing.MsgBrowseSensorsResponse { Error = "Internal error", Sensors = null };

            try
            {
                var isConnected = ConnectWithTimeout();

                if (isConnected)
                {

                    bool SendResults2 = true;
                    if (MyTags.MyMirrorCache.Count == 0)
                        SendResults2 = Browser(currentRoot, currentRoot.ToString(), true, false, null);
                    if (SendResults2)
                    {
                        response.Sensors = MyTags.MyMirrorCache.TheValues.Select(t =>
                            new TheThing.TheSensorSourceInfo
                            {
                                SensorId = t.NodeIdName,
                                DisplayNamePath = t.GetDisplayNamePathArray(),
                                // TODO Get/store type info from the OPC Server on browse. After a browse, TargetProperty is null, so there is currently no type info returned here
                                SourceType = t.TargetProperty?.GetSensorMeta().SourceType, 
                                cdeType = (ePropertyTypes?) t.TargetProperty?.cdeT,
                            }).ToList();
                        response.Error = null;
                    }
                    else
                    {
                        response.Error = "Failure while browsing";
                    }
                }
                else
                {
                    response.Error = "Failed to connect";
                }
            }
            catch (Exception e)
            {
                response.Error = $"Internal Error: {e.Message}";
            }
            return response;
        }

        private bool ConnectWithTimeout(int timeoutMS = 30000)
        {
            var sw = new Stopwatch();
            sw.Start();
            while (!IsConnected && sw.ElapsedMilliseconds < timeoutMS && TheBaseAssets.MasterSwitch)
            {
                Connect(true);
                TheCommonUtils.SleepOneEye(1000, 100);
            }
            sw.Stop();
            return IsConnected;
        }

        private MsgOPCUAReadTagsResponse HandleReadTagsMessage(MsgOPCUAReadTags request)
        {
            var response = new MsgOPCUAReadTagsResponse { };
            try
            {
                if (request != null)
                {
                    if (request.Tags != null)
                    {
                        response.Results = new List<MsgOPCUAReadTagsResponse.TagResult>();
                        var tempThing = new TheThing();

                        var results = TheOPCTag.ReadTags(this.m_session, request.Tags.Select((ti) => ti.NodeId));

                        MsgOPCUAReadTagsResponse.TagResult tagResult;
                        int index = 0;
                        foreach (var result in results)
                        {
                            if (!StatusCode.IsBad(result.StatusCode))
                            {
                                var prop = tempThing.GetProperty($"Prop_{index}", true);
                                var structTypeInfo = TheOPCTag.UpdateValueProperty(this, prop, MyBaseThing.cdeMID, result, null);
                                tagResult = new MsgOPCUAReadTagsResponse.TagResult { TagValue = prop.Value, StructTypeInfo = structTypeInfo };
                            }
                            else
                            {
                                tagResult = new MsgOPCUAReadTagsResponse.TagResult
                                {
                                    Error = result.StatusCode.ToString(),
                                };
                            }
                            response.Results.Add(tagResult);
                            index++;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                response.Results = null;
                response.Error = e.Message;
            }

            return response;
        }

        private TheThing.MsgGetSensorSubscriptionsResponse<TheOPCSensorSubscription> HandleGetSensorSubscriptionsMessage(TheThing.MsgGetSensorSubscriptions<TheOPCSensorSubscription> request)
        {
            var response = new TheThing.MsgGetSensorSubscriptionsResponse<TheOPCSensorSubscription> { Error = "Internal error" };
            try
            {
                MonitorAllTagsInServer(true);
                response.Subscriptions = new List<TheOPCSensorSubscription>();
                foreach (var tag in MyTagSubscriptions.TheValues)
                {
                    var subscription = new TheOPCSensorSubscription
                    {
                        // TODO properly return tags in things, events and methods
                        SubscriptionId = GetSubscriptionId(tag),
                        SensorId = tag.NodeIdName,
                        TargetProperty = tag.GetTargetPropertyName(),
                        TargetThing = tag.GetHostThing(),
                        SampleRate = tag.SampleRate,

                        ChangeTrigger = tag.ChangeTrigger,
                        DeadbandValue = tag.DeadbandFilterValue,
                    };

                    response.Subscriptions.Add(subscription);
                }
                response.Error = null;
            }
            catch (Exception e)
            {
                response.Error = $"Internal Error: {e.Message}";
            }
            return response;
        }

        //public class OPCCustomSubscriptionInfo
        //{
        //    public int? SampleRate { get { return GetInt(nameof(SampleRate)); } set { SetValue(nameof(SampleRate), value); } }
        //    public int? QueueSize { get { return GetInt(nameof(QueueSize)); } set { SetValue(nameof(QueueSize), value); } }
        //    public int? ChangeTrigger { get { return GetInt(nameof(ChangeTrigger)); } set { SetValue(nameof(ChangeTrigger), value); } }
        //    public double? DeadbandValue { get { return GetDouble(nameof(DeadbandValue)); } set { SetValue(nameof(DeadbandValue), value); } }

        //    private void SetValue(string propertyName, object value)
        //    {
        //        if (_subInfo.CustomSubscriptionInfo == null)
        //        {
        //            _subInfo.CustomSubscriptionInfo = new Dictionary<string, object>();
        //        }
        //        _subInfo.CustomSubscriptionInfo[propertyName] = value;
        //    }

        //    private int? GetInt(string propertyName)
        //    {
        //        object valueObj = null; ;
        //        if (_subInfo?.CustomSubscriptionInfo?.TryGetValue(propertyName, out valueObj) == true)
        //        {
        //            return TheCommonUtils.CInt(valueObj);
        //        }
        //        return null;
        //    }
        //    private double? GetDouble(string propertyName)
        //    {
        //        object valueObj = null; ;
        //        if (_subInfo?.CustomSubscriptionInfo?.TryGetValue(propertyName, out valueObj) == true)
        //        {
        //            return TheCommonUtils.CDbl(valueObj);
        //        }
        //        return null;
        //    }

        //    TheThing.TheSensorSubscription _subInfo;

        //    public OPCCustomSubscriptionInfo(TheThing.TheSensorSubscription subInfo)
        //    {
        //        _subInfo = subInfo;
        //    }
        //}

        private TheThing.MsgSubscribeSensorsResponse<TheOPCSensorSubscription> HandleSubscribeSensorsMessage(TheThing.MsgSubscribeSensors<TheOPCSensorSubscription> request)
        {
            var response = new TheThing.MsgSubscribeSensorsResponse<TheOPCSensorSubscription> { Error = "Internal error" };
            try
            {
                var opcTags = new List<MsgOPCUACreateTags.TagInfo>();
                var opcEvents = new List<MsgOPCUACreateTags.EventInfo>();
                var opcMethods = new List<MsgOPCUACreateTags.MethodInfo>();

                foreach (var sr in request.SubscriptionRequests)
                {
                    if (sr.TargetThing == null)
                    {
                        sr.TargetThing = request.DefaultTargetThing;
                    }
                    if (sr.MethodInfo != null)
                    {
                        var methodInfo = new MsgOPCUACreateTags.MethodInfo
                        {
                            NodeId = sr.SensorId,
                            ObjectId = sr.MethodInfo.ObjectId,
                            CallTimeout = sr.MethodInfo.CallTimeout,
                        };
                        opcMethods.Add(methodInfo);
                    }
                    else if (sr.EventInfo != null)
                    {
                        var eventInfo = new MsgOPCUACreateTags.EventInfo
                        {
                            HostThing = sr.TargetThing,
                            DisplayName = sr.TargetProperty,
                            NodeId = sr.SensorId,
                            Subscription = sr.EventInfo,
                        };

                        opcEvents.Add(eventInfo);
                    }
                    else
                    {
                        var tagInfo = new MsgOPCUACreateTags.TagInfo
                        {
                            HostThing = sr.TargetThing,
                            DisplayName = sr.TargetProperty,
                            NodeId = sr.SensorId,
                            SamplingInterval = sr.SampleRate ?? 0,
                            DeadbandValue = sr.DeadbandValue ?? 0,
                            ChangeTrigger = (ChangeTrigger)(sr.ChangeTrigger ?? 1),
                        };
                        opcTags.Add(tagInfo);
                    }
                };

                var opcRequest = new MsgOPCUACreateTags
                {
                    BulkApply = true,
                    Tags = opcTags.Count > 0 ? opcTags : null,
                    Events = opcEvents.Count > 0 ? opcEvents : null,
                    Methods = opcMethods.Count > 0 ? opcMethods : null,
                };

                var opcResponse = HandleCreateTagsMessage(opcRequest);
                response.Error = opcResponse.Error;
                if (string.IsNullOrEmpty(response.Error))
                {
                    int index = 0;
                    response.SubscriptionStatus = request.SubscriptionRequests.Select(sr =>
                    {
                        var subscriptionStatus = new TheThing.TheSensorSubscriptionStatus<TheOPCSensorSubscription>
                        {
                            Subscription = sr,
                            Error = opcResponse.Results[index].Error,
                        };
                        subscriptionStatus.Subscription.SubscriptionId = GetSubscriptionId(sr.SensorId, sr.TargetThing?.ThingMID ?? Guid.Empty, sr.TargetProperty, sr.TargetProperty);

                        index++;
                        return subscriptionStatus;
                    }).ToList();

                    response.Error = null;
                }
            }
            catch (Exception e)
            {
                response.Error = $"Internal Error: {e.Message}";
            }
            return response;
        }

        private static Guid GetSubscriptionId(TheOPCTag tTag)
        {
            if (tTag.TargetProperty != null)
            {
                return tTag.TargetProperty.cdeMID;
            }
            return GetSubscriptionId(tTag.NodeIdName, TheCommonUtils.CGuid(tTag.HostThingMID), tTag.DisplayName, tTag.TargetProperty?.Name);
        }
        private static Guid GetSubscriptionId(string sensorId, Guid thingMID, string displayName, string targetPropertyName)
        {
            if (targetPropertyName == null)
            {
                targetPropertyName = displayName;
            }
            var prop = TheThingRegistry.GetThingByMID(thingMID, true)?.GetProperty(targetPropertyName);
            //return $"{sensorId}:;:{thingMID.ToString()}:;:{targetPropertyName}";
            return prop?.cdeMID ?? Guid.Empty;
        }

        //private static string ParseSubscriptionId(string subscriptionId, out string nodeId, out Guid? targetThingMid, out string targetPropertyName)
        //{
        //    nodeId = null;
        //    targetThingMid = null;
        //    targetPropertyName = null;

        //    string error = null;
        //    var parts = TheCommonUtils.cdeSplit(subscriptionId, ":;:", false, false);
        //    if (parts.Length != 3)
        //    {
        //        error = $"Invalid subscription id: {subscriptionId}. Expected 3 parts, found {parts.Length}.";
        //        return error;
        //    }
        //    else
        //    {
        //        nodeId = parts[0];
        //        targetThingMid = TheCommonUtils.CGuid(parts[1]);
        //        targetPropertyName = parts[2];
        //        if (targetThingMid == Guid.Empty)
        //        {
        //            error = $"Invalid subscription Id - bad thing mid: {subscriptionId}";
        //            return error;
        //        }
        //    }
        //    return error;
        //}

        private TheThing.MsgUnsubscribeSensorsResponse HandleUnsubscribeSensorsMessage(TheThing.MsgUnsubscribeSensors request)
        {
            var response = new TheThing.MsgUnsubscribeSensorsResponse { Error = "Internal error" };
            try
            {
                var tagsToUnsubscribe = new List<TheOPCTag>();
                foreach( var subscriptionId in request.SubscriptionIds)
                {
                    //var error = ParseSubscriptionId(subscriptionId, out var nodeId, out var targetThingMid, out var targetPropertyName);
                    //if (!string.IsNullOrEmpty(error))
                    //{ 
                    //    response.Error = error;
                    //    return response;
                    //}

                    //var matchingTags = MyTagSubscriptions.MyMirrorCache.GetEntriesByFunc(t => 
                    //       t.TagRef == nodeId 
                    //    && (t.TargetProperty != null ? t.TargetProperty.Name == targetPropertyName : t.DisplayName == targetPropertyName) 
                    //    && TheCommonUtils.CGuid(t.HostThingMID) == targetThingMid);
                    //if (matchingTags.Count > 1)
                    //{
                    //    response.Error = $"Internal error: more than one subscription for subscription Id: {subscriptionId}";
                    //    return response;
                    //}

                    var matchingTag = MyTagSubscriptions.MyMirrorCache.GetEntryByFunc(t => 
                        GetSubscriptionId(t) == subscriptionId
                    );
                    if (matchingTag == null)
                    {
                        response.Error = $"Invalid subscription Id: {subscriptionId}";
                        return response;
                    }
                    else
                    {
                        tagsToUnsubscribe.Add(matchingTag);
                    }
                }
                var subscription = GetOrCreateSubscription(0, false); // OK if no live OPC connection subscription: then only the registrations will be removed
                if (subscription.ChangesPending)
                {
                    response.Error = $"OPC operation in progress. Please retry late."; // TODO make these changes fully offline?
                }

                foreach (var tag in tagsToUnsubscribe)
                {
                    tag.UnregisterInHostThing(subscription, false);
                }

                response.Error = null;

                if (subscription != null)
                {
#if OLD_UA
                    var items = subscription.ApplyChanges();
                    foreach (var item in items)
#else                    
                    subscription.ApplyChanges();
                    foreach (var item in subscription.MonitoredItems)
#endif
                    {
                        if (item.Status.Error != null)
                        {
                            if (response.Failed == null)
                            {
                                response.Failed = new List<TheThing.TheSensorSubscriptionStatus>();
                            }
                            var tag = ((item.Handle) as TheOPCTag);
                            var subscriptionId = GetSubscriptionId(tag);
                            if (request.SubscriptionIds.Contains(subscriptionId))
                            {
                                response.Failed.Add(new TheThing.TheSensorSubscriptionStatus
                                {
                                    Subscription = new TheThing.TheSensorSubscription { SubscriptionId = subscriptionId },
                                    Error = item.Status.Error.ToString(),
                                });
                            }
                            else
                            {
                                if (response.Error == null)
                                {
                                    response.Error = "";
                                }
                                response.Error += $"[Unexpected subscription id '{subscriptionId}' with error: {item.Status.Error.ToString()}] ";
                            }
                        }
                    }
                }

            }
            catch (Exception e)
            {
                response.Error = $"Internal Error: {e.Message}";
            }
            return response;
        }


        private MsgOPCUADisconnectResponse HandleDisconnectMessage(TheProcessMessage pMsg, MsgOPCUADisconnect request)
        {
            var response = new MsgOPCUADisconnectResponse { };

            if (request != null)
            {
                LastMessage = DateTimeOffset.Now + $": Disconnecting due to disconnect message from {pMsg.Message.ENG} - {pMsg.Message.GetOriginatorThing()}";
                response.Error = Disconnect(ConnectionState == ConnectionStateEnum.Disconnected, false, "Disconnect requested via TSM");
            }
            else
            {
                response.Error = "Invalid request message";
            }
            return response;
        }

        private MsgOPCUAConnectResponse HandleConnectMessage(MsgOPCUAConnect request)
        {
            var response = new MsgOPCUAConnectResponse { };

            if (request != null)
            {
                response.Error = Connect(request.LogEssentialOnly);
                int cnt = 0;
                while (!IsConnected && request.WaitUntilConnected && TheBaseAssets.MasterSwitch)
                {
                    TheCommonUtils.SleepOneEye(1000, 100); //TODO: Make dependend on IsConnected Event!
                    response.Error = Connect(request.LogEssentialOnly);
                    cnt++; if (cnt > 60) break;
                }
            }
            else
            {
                response.Error = "Invalid request message";
            }
            return response;
        }

        internal MsgOPCUACreateTagsResponse HandleCreateTagsMessage(MsgOPCUACreateTags request)
        {
            var response = new MsgOPCUACreateTagsResponse
            {
                Results = new List<MsgOPCUACreateTagsResponse.TagResult>(),
            };
            try
            {
                if (request != null)
                {
                    Subscription subscription = null;
                    var sw = new Stopwatch();
                    sw.Start();
                    while (subscription == null && IsTargetStateConnected && sw.ElapsedMilliseconds < 45000)
                    {
                        subscription = GetOrCreateSubscription(DefSampleRate);
                        TheCommonUtils.SleepOneEye(100, 100);
                    }
                    if (subscription == null && IsTargetStateConnected)
                    {
                        response.Error = "Unable to obtain session";
                        return response;
                    }
                    if (request.ReplaceAllTags)
                    {
                        // TODO Remove monitored items only rather than recreating the entire subscription? We may want to use separate subscriptions for tags in properties vs. things?
                        m_session.RemoveSubscription(subscription);
                        UnsubscribeTagsInAllHostThings();
                        LiveTagCnt = 0;
                        subscription = GetOrCreateSubscription(DefSampleRate);
                        if (subscription == null && IsTargetStateConnected)
                        {
                            response.Error = "Unable to obtain session";
                            return response;
                        }
                    }
                    if (request.Tags != null)
                    {
                        bool bDidBrowse = false;
                        var previousThingMid = Guid.Empty;
                        foreach (var tag in request.Tags)
                        {
                            string error = null;
                            if (String.IsNullOrEmpty(tag.NodeId) && !String.IsNullOrEmpty(tag.BrowsePath))
                            {
                                TheOPCTag browsedTag;
                                browsedTag = MyTags.MyMirrorCache.GetEntryByFunc(t => String.Equals(tag.BrowsePath, t.GetDisplayNamePath(), StringComparison.Ordinal));
                                if (browsedTag != null)
                                {
                                    tag.NodeId = browsedTag.NodeIdName;
                                }
                                else
                                {
                                    if (!bDidBrowse)
                                    {
                                        if (subscription == null)
                                        {
                                            error = "Must be connected to subscribe via browsepath";
                                        }
                                        else
                                        {
                                            // TODO Make this side effect free by passing the browsebranch as a parameter to Browser() etc.
                                            string oldBranch = this.BrowseBranch;
                                            this.BrowseBranch = request.Tags.Aggregate(tag.BrowsePath, (s, t) => CommonBrowsePathPrefix(s, t.BrowsePath));
                                            MyTags.MyMirrorCache.Reset();
                                            BrowsedTagCnt = 0;
                                            bDidBrowse = Browser(currentRoot, currentRoot.ToString(), true, false, null);
                                            BrowseBranch = oldBranch;
                                            browsedTag = MyTags.MyMirrorCache.GetEntryByFunc(t => String.Equals(tag.BrowsePath, t.GetDisplayNamePath(), StringComparison.Ordinal));
                                            if (browsedTag != null)
                                            {
                                                tag.NodeId = browsedTag.NodeIdName;
                                            }
                                        }
                                    }
                                    if (string.IsNullOrEmpty(error) && browsedTag == null)
                                    {
                                        error = "ERROR:BrowsePath not found";
                                    }
                                }
                            }

                            if (error == null)
                            {
                                tag.NodeId = NormalizeNodeIdName(tag.NodeId);
                                var thingMid = tag.HostThingAddress?.ThingMID ?? Guid.Empty;
                                if (thingMid == Guid.Empty)
                                {
                                    TheThingMatcher matcher = null;
                                    if (tag.HostThing != null)
                                    {
                                        matcher = new TheThingMatcher(tag.HostThing);
                                    }
                                    else if (tag.HostThingAddress != null)
                                    {
                                        matcher = new TheThingMatcher(tag.HostThingAddress, null);
                                    }
                                    if (matcher != null)
                                    {
                                        var matchingThings = matcher.GetMatchingThings();
                                        if (matchingThings.Count() == 1)
                                        {
                                            thingMid = matchingThings.FirstOrDefault()?.cdeMID ?? Guid.Empty;
                                        }
                                    }
                                }
                                if (thingMid == Guid.Empty)
                                {
                                    thingMid = previousThingMid;
                                }
                                error = CreateTagSubscription(subscription, "UATAG", tag.DisplayName, "", tag.NodeId, tag.SamplingInterval, TheCommonUtils.cdeGuidToString(thingMid),
                                    tag.HistoryStartTime, (int)tag.ChangeTrigger, tag.DeadbandValue, !request.BulkApply, null);
                                previousThingMid = thingMid;
                            }

                            if (!string.IsNullOrEmpty(error) && error.ToUpperInvariant().StartsWith("ERROR"))
                            {
                                TheBaseAssets.MySYSLOG.WriteToLog(78102, TSM.L(eDEBUG_LEVELS.FULLVERBOSE) ? null : new TSM(MyBaseThing.EngineName, $"[{GetLogAddress()}] Error adding tag {tag.ToString()}: {error}", eMsgLevel.l1_Error, ""));
                                response.Results.Add(new MsgOPCUACreateTagsResponse.TagResult { Error = error, });
                            }
                            else
                            {
                                TheBaseAssets.MySYSLOG.WriteToLog(78102, TSM.L(eDEBUG_LEVELS.FULLVERBOSE) ? null : new TSM(MyBaseThing.EngineName, $"[{GetLogAddress()}] Adding tag {tag.ToString()}: {error}", eMsgLevel.l3_ImportantMessage, ""));
                                response.Results.Add(new MsgOPCUACreateTagsResponse.TagResult { });
                            }
                        }
                        if (subscription != null && request.BulkApply)
                        {
#if OLD_UA
                            var items = subscription.ApplyChanges();
                            foreach (var item in items)
#else
                            subscription.ApplyChanges();
                            foreach (var item in subscription.MonitoredItems)
#endif
                            {
                                if (item.Status.Error != null)
                                {
                                    var itemIndex = request.Tags.FindIndex(t => item.StartNodeId.Equals((NodeId)t.NodeId));
                                    if (itemIndex >= 0 && itemIndex < response.Results.Count)
                                    {
                                        response.Results[itemIndex].Error = item.Status.Error.ToLongString();
                                    }
                                    subscription.RemoveItem(item);
                                }
                            }
                        }

                        if (bDidBrowse)
                        {
                            MyTags.MyMirrorCache.Reset();
                            BrowsedTagCnt = 0;
                            // Update the tag list if so configured, make sure monitored item list is properly updated (update not working yet)
                            Disconnect(false, false, "Disconnect due to Tag Creation via TSM"); 
                            Connect(false);
                        }
                    }
                    if (request.Methods != null)
                    {
                        foreach (var method in request.Methods)
                        {
                            if (subscription == null)
                            {

                            }
                            method.NodeId = NormalizeNodeIdName(method.NodeId);

                            var result = CreateTagSubscription(subscription, "UAMETHOD", method.NodeId, method.ObjectId, method.NodeId, method.CallTimeout, "", null, 0, 0, true, null);
                            if (!string.IsNullOrEmpty(result) && result.ToUpperInvariant().StartsWith("ERROR"))
                            {
                                response.Results.Add(new MsgOPCUACreateTagsResponse.TagResult { Error = result, });
                            }
                            else
                            {
                                var tThing = TheThingRegistry.GetThingByMID(TheOPCUAClientEngines.EngineName, TheCommonUtils.CGuid(result));
                                if (tThing != null)
                                {
                                    response.Results.Add(new MsgOPCUACreateTagsResponse.TagResult { MethodThingAddress = tThing });
                                }
                                else
                                {
                                    response.Results.Add(new MsgOPCUACreateTagsResponse.TagResult { Error = "Internal error (Invalid thing mid)", });
                                }
                            }
                        }
                    }
                    if (request.Events != null)
                    {
                        var previousThingMid = Guid.Empty;
                        foreach (var eventRequest in request.Events)
                        {
                            var thingMid = Guid.Empty;
                            TheThingMatcher matcher = null;
                            if (eventRequest.HostThing != null)
                            {
                                matcher = new TheThingMatcher(eventRequest.HostThing);
                            }
                            if (matcher != null)
                            {
                                var matchingThings = matcher.GetMatchingThings();
                                if (matchingThings.Count() == 1)
                                {
                                    thingMid = matchingThings.FirstOrDefault()?.cdeMID ?? Guid.Empty;
                                }
                            }
                            if (thingMid == Guid.Empty)
                            {
                                thingMid = previousThingMid;
                            }
                            eventRequest.NodeId = NormalizeNodeIdName(eventRequest.NodeId);
                            var error = CreateTagSubscription(subscription, "UAEVENT", eventRequest.DisplayName, null, eventRequest.NodeId, 0, TheCommonUtils.cdeGuidToString(thingMid), null, 0, 0, !request.BulkApply, eventRequest.Subscription);
                            previousThingMid = thingMid;

                            if (!string.IsNullOrEmpty(error) && error.ToUpperInvariant().StartsWith("ERROR"))
                            {
                                TheBaseAssets.MySYSLOG.WriteToLog(78102, TSM.L(eDEBUG_LEVELS.FULLVERBOSE) ? null : new TSM(MyBaseThing.EngineName, $"[{GetLogAddress()}] Error adding event subscription {eventRequest.ToString()}: {error}", eMsgLevel.l1_Error, ""));
                                response.Results.Add(new MsgOPCUACreateTagsResponse.TagResult { Error = error, });
                            }
                            else
                            {
                                TheBaseAssets.MySYSLOG.WriteToLog(78102, TSM.L(eDEBUG_LEVELS.FULLVERBOSE) ? null : new TSM(MyBaseThing.EngineName, $"[{GetLogAddress()}] Adding event subscription {eventRequest.ToString()}: {error}", eMsgLevel.l3_ImportantMessage, ""));
                                response.Results.Add(new MsgOPCUACreateTagsResponse.TagResult { });
                            }
                        }
                        if (subscription != null && request.BulkApply)
                        {
#if OLD_UA
                            var items = subscription.ApplyChanges();
                            foreach (var item in items)
#else
                            subscription.ApplyChanges();
                            foreach (var item in subscription.MonitoredItems)
#endif
                            {
                                IEnumerable<StatusCode> eventErrors = null; //TODO: CM: FilterResult no longer exists. Is this critical? (item.Status.FilterResult as EventFilterResult)?.SelectClauseResults;
                                bool bEventErrors = eventErrors?.Any(cl => StatusCode.IsBad(cl.Code)) == true;
                                if (item.Status.Error != null || bEventErrors)
                                {
                                    int itemIndex;
                                    if (!bEventErrors)
                                    {
                                        itemIndex = request.Tags.FindIndex(t => item.StartNodeId.Equals((NodeId)t.NodeId));
                                    }
                                    else
                                    {
                                        itemIndex = request.Events.FindIndex(t => item.StartNodeId.Equals((NodeId)t.NodeId) && ((item.Handle as TheOPCEvent).EventInfo.AggregateRetainedConditions) == t.Subscription.AggregateRetainedConditions);
                                        if (itemIndex >= 0)
                                        {
                                            itemIndex += request.Tags.Count;
                                        }
                                    }
                                    if (itemIndex >= 0 && itemIndex < response.Results.Count)
                                    {
                                        var error = item.Status.Error?.ToLongString() ?? "";
                                        if (bEventErrors)
                                        {
                                            error += "Event Property Errors: ";
                                            int eventPropertyIndex = 0;
                                            foreach (var eventError in eventErrors)
                                            {
                                                if (StatusCode.IsBad(eventError))
                                                {
                                                    var clause = (item.Filter as EventFilter).SelectClauses[eventPropertyIndex];
                                                    error += $"'{clause.ToString()}':{eventError.ToString()},";
                                                }
                                                eventPropertyIndex++;
                                            }
                                        }
                                        response.Results[itemIndex].Error = error;
                                    }
                                }
                            }
                            try
                            {
                                TheOPCEvent.ConditionRefresh(subscription, this);
                            }
                            catch (Exception e)
                            {
                                TheBaseAssets.MySYSLOG.WriteToLog(78102, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, $"Error requesting condition request for {this.GetLogAddress()}", eMsgLevel.l1_Error, TSM.L(eDEBUG_LEVELS.VERBOSE) ? e.Message : e.ToString()));
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                response.Results = null;
                response.Error = e.Message;
            }
            return response;
        }
#endregion

        private static string NormalizeNodeIdName(string nodeIdName)
        {
            try
            {
                var exNodeId = ExpandedNodeId.Parse(nodeIdName);
                return exNodeId.ToString();
            }
            catch
            {
                try
                {
                    var nodeId = new NodeId(nodeIdName); // Fall back to previous behavior for compatibility
                    return nodeId.ToString();
                }
                catch
                {
                    // Preserve input string
                }
            }
            return nodeIdName;
        }

        private string CommonBrowsePathPrefix(string path1, string path2)
        {
            var prefix = new StringBuilder();
            var path1Parts = path1.Split('.');
            var path2Parts = path2.Split('.');

            for (int i = 0; i < path1Parts.Length && i < path2Parts.Length; i++)
            {
                if (String.Equals(path1Parts[i], path2Parts[i], StringComparison.Ordinal))
                {
                    if (prefix.Length > 0)
                    {
                        prefix.Append(".");
                    }
                    prefix.Append(path1Parts[i]);
                }
            }
            return prefix.ToString();
        }

        [ConfigProperty(Required = true, Generalize = true)]
        public string Address
        {
            get { return TheThing.MemberGetSafePropertyString(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyString(MyBaseThing, value); }
        }
        [ConfigProperty(Generalize = true)]
        public string FriendlyName
        {
            get { return TheThing.MemberGetSafePropertyString(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyString(MyBaseThing, value); }
        }
        [ConfigProperty(DefaultValue = false, Description = "Allow connections to unsecured servers")]
        public bool DisableSecurity
        {
            get { return TheThing.GetSafePropertyBool(MyBaseThing, "DisableSecurity"); }
            set { TheThing.SetSafePropertyBool(MyBaseThing, "DisableSecurity", value); }
        }
        [ConfigProperty(DefaultValue = false)]
        public bool Anonymous
        {
            get { return TheThing.GetSafePropertyBool(MyBaseThing, "Anonymous"); }
            set { TheThing.SetSafePropertyBool(MyBaseThing, "Anonymous", value); }
        }
        [ConfigProperty(DefaultValue = false)]
        public bool DisableDomainCheck
        {
            get { return TheThing.GetSafePropertyBool(MyBaseThing, "DisableDomainCheck"); }
            set { TheThing.SetSafePropertyBool(MyBaseThing, "DisableDomainCheck", value); }
        }
        [ConfigProperty(DefaultValue = false)]
        public bool AutoConnect
        {
            get { return TheThing.GetSafePropertyBool(MyBaseThing, "AutoConnect"); }
            set { TheThing.SetSafePropertyBool(MyBaseThing, "AutoConnect", value); }
        }

        [ConfigProperty()]
        public string PreferredLocales
        {
            get { return TheThing.GetSafePropertyString(MyBaseThing, "PreferredLocales"); }
            set { TheThing.SetSafePropertyString(MyBaseThing, "PreferredLocales", value); }
        }

        public bool IsConnected
        {
            get { return TheThing.GetSafePropertyBool(MyBaseThing, "IsConnected"); }
            set { TheThing.SetSafePropertyBool(MyBaseThing, "IsConnected", value); }
        }

        public bool IsReconnecting
        {
            get { return TheThing.GetSafePropertyBool(MyBaseThing, "IsReconnecting"); }
            set { TheThing.SetSafePropertyBool(MyBaseThing, "IsReconnecting", value); }
        }

        [ConfigProperty(DefaultValue = false)]
        public bool AcceptUntrustedCertificate
        {
            get { return TheThing.GetSafePropertyBool(MyBaseThing, "AcceptUntrustedCertificate"); }
            set { TheThing.SetSafePropertyBool(MyBaseThing, "AcceptUntrustedCertificate", value); }
        }

        [ConfigProperty(DefaultValue = false)]
        public bool AcceptInvalidCertificate
        {
            get { return TheThing.GetSafePropertyBool(MyBaseThing, "AcceptInvalidCertificate"); }
            set { TheThing.SetSafePropertyBool(MyBaseThing, "AcceptInvalidCertificate", value); }
        }

        [ConfigProperty()]
        public string AppCertSubjectName
        {
            get { return TheThing.GetSafePropertyString(MyBaseThing, "AppCertSubjectName"); }
            set { TheThing.SetSafePropertyString(MyBaseThing, "AppCertSubjectName", value); }
        }


        [ConfigProperty(DefaultValue = 0)]
        public int PublishingInterval
        {
            get { return TheCommonUtils.CInt(TheThing.GetSafePropertyString(MyBaseThing, nameof(PublishingInterval))); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, nameof(PublishingInterval), value); }
        }

        [ConfigProperty(DefaultValue = 1000)]
        public int DefSampleRate
        {
            get { return TheCommonUtils.CInt(TheThing.GetSafePropertyString(MyBaseThing, nameof(DefSampleRate))); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, nameof(DefSampleRate), value); }
        }

        [ConfigProperty()]
        public DateTimeOffset DefHistoryStartTime
        {
            get { return TheThing.GetSafePropertyDate(MyBaseThing, nameof(DefHistoryStartTime)); }
            set { TheThing.SetSafePropertyDate(MyBaseThing, nameof(DefHistoryStartTime), value); }
        }

        public int LiveTagCnt
        {
            get { return TheCommonUtils.CInt(TheThing.GetSafePropertyString(MyBaseThing, "LiveTagCnt")); }
            private set {
                TheThing.SetSafePropertyNumber(MyBaseThing, "LiveTagCnt", value);
                TheThing.SetSafePropertyNumber(MyBaseThing, "QValue", value);
            }
        }
        public int BrowsedTagCnt
        {
            get { return TheCommonUtils.CInt(TheThing.GetSafePropertyString(MyBaseThing, "BrowsedTagCnt")); }
            private set { TheThing.SetSafePropertyNumber(MyBaseThing, "BrowsedTagCnt", value); }
        }
        [ConfigProperty(DefaultValue = 1000)]
        public int ReconnectPeriod
        {
            get { return TheCommonUtils.CInt(TheThing.GetSafePropertyString(MyBaseThing, "ReconnectPeriod")); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, "ReconnectPeriod", value); }
        }
        [ConfigProperty(DefaultValue = 0)]
        public int ReconnectCount
        {
            get { return TheCommonUtils.CInt(TheThing.GetSafePropertyString(MyBaseThing, nameof(ReconnectCount))); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, nameof(ReconnectCount), value); }
        }
        [ConfigProperty(DefaultValue = 5000)]
        public int KeepAliveInterval
        {
            get { return TheCommonUtils.CInt(TheThing.GetSafePropertyString(MyBaseThing, "KeepAliveInterval")); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, "KeepAliveInterval", value); }
        }
        [ConfigProperty(DefaultValue = 0)]
        public int KeepAliveTimeout
        {
            get { return TheCommonUtils.CInt(TheThing.GetSafePropertyString(MyBaseThing, "KeepAliveTimeout")); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, "KeepAliveTimeout", value); }
        }
        [ConfigProperty(DefaultValue = 60000)]
        public int OperationTimeout
        {
            get { return TheCommonUtils.CInt(TheThing.GetSafePropertyString(MyBaseThing, "OperationTimeout")); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, "OperationTimeout", value); }
        }
        [ConfigProperty(DefaultValue = 60000)]
        public int SessionTimeout
        {
            get { return TheCommonUtils.CInt(TheThing.GetSafePropertyString(MyBaseThing, "SessionTimeout")); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, "SessionTimeout", value); }
        }
        [ConfigProperty()]
        public string SessionName
        {
            get { return TheThing.GetSafePropertyString(MyBaseThing, "SessionName"); }
            set { TheThing.SetSafePropertyString(MyBaseThing, "SessionName", value); }
        }

        public string LastMessage
        {
            get { return TheThing.GetSafePropertyString(MyBaseThing, "LastMessage"); }
            set { TheThing.SetSafePropertyString(MyBaseThing, "LastMessage", value); }
        }
        [ConfigProperty(Generalize = true)]
        public string UserName
        {
            get { return TheThing.GetSafePropertyString(MyBaseThing, "UserName"); }
            set { TheThing.SetSafePropertyString(MyBaseThing, "UserName", value); }
        }
        [ConfigProperty(Secure = true, Generalize = true)]
        public string Password
        {
            get { return TheThing.GetSafePropertyString(MyBaseThing, "Password"); }
            set { TheThing.SetSafePropertyString(MyBaseThing, "Password", value); }
        }

        [ConfigProperty(Generalize = true)]
        public string TagHostThingForSubscribeAll
        {
            get { return TheThing.GetSafePropertyString(MyBaseThing, "TagHostThingForSubscribeAll"); }
            set { TheThing.SetSafePropertyString(MyBaseThing, "TagHostThingForSubscribeAll", value); }
        }
        public string TagHostThings
        {
            get { return TheThing.GetSafePropertyString(MyBaseThing, "TagHostThings"); }
            set { TheThing.SetSafePropertyString(MyBaseThing, "TagHostThings", value); }
        }
        [ConfigProperty()]
        public string BrowseBranch
        {
            get { return TheThing.GetSafePropertyString(MyBaseThing, "BrowseBranch"); }
            set { TheThing.SetSafePropertyString(MyBaseThing, "BrowseBranch", value); }
        }

        [ConfigProperty(DefaultValue = false)]
        public bool SendStatusCode
        {
            get { return TheThing.GetSafePropertyBool(MyBaseThing, nameof(SendStatusCode)); }
            set { TheThing.SetSafePropertyBool(MyBaseThing, nameof(SendStatusCode), value); }
        }

        [ConfigProperty(DefaultValue = false)]
        public bool SendServerTimestamp
        {
            get { return TheThing.GetSafePropertyBool(MyBaseThing, nameof(SendServerTimestamp)); }
            set { TheThing.SetSafePropertyBool(MyBaseThing, nameof(SendServerTimestamp), value); }
        }

        [ConfigProperty(DefaultValue = false)]
        public bool SendPicoSeconds
        {
            get { return TheThing.GetSafePropertyBool(MyBaseThing, nameof(SendPicoSeconds)); }
            set { TheThing.SetSafePropertyBool(MyBaseThing, nameof(SendPicoSeconds), value); }
        }

        [ConfigProperty(DefaultValue = false)]
        public bool SendSequenceNumber
        {
            get { return TheThing.GetSafePropertyBool(MyBaseThing, nameof(SendSequenceNumber)); }
            set { TheThing.SetSafePropertyBool(MyBaseThing, nameof(SendSequenceNumber), value); }
        }

        [ConfigProperty(DefaultValue = false)]
        public bool SendOpcDataType
        {
            get { return TheThing.GetSafePropertyBool(MyBaseThing, nameof(SendOpcDataType)); }
            set { TheThing.SetSafePropertyBool(MyBaseThing, nameof(SendOpcDataType), value); }
        }
        [ConfigProperty(DefaultValue = false)]
        public bool UseSequenceNumberForThingSequence
        {
            get { return TheThing.GetSafePropertyBool(MyBaseThing, nameof(UseSequenceNumberForThingSequence)); }
            set { TheThing.SetSafePropertyBool(MyBaseThing, nameof(UseSequenceNumberForThingSequence), value); }
        }
        [ConfigProperty(DefaultValue = false)]
        public bool DoNotUsePropsOfProps
        {
            get { return TheThing.GetSafePropertyBool(MyBaseThing, nameof(DoNotUsePropsOfProps)); }
            set { TheThing.SetSafePropertyBool(MyBaseThing, nameof(DoNotUsePropsOfProps), value); }
        }
        [ConfigProperty(DefaultValue = false)]
        public bool UseLocalTimestamp
        {
            get { return TheThing.GetSafePropertyBool(MyBaseThing, nameof(UseLocalTimestamp)); }
            set { TheThing.SetSafePropertyBool(MyBaseThing, nameof(UseLocalTimestamp), value); }
        }

        [ConfigProperty(DefaultValue = false)]
        public bool DoNotWriteArrayElementsAsProperties
        {
            get { return TheThing.GetSafePropertyBool(MyBaseThing, nameof(DoNotWriteArrayElementsAsProperties)); }
            set { TheThing.SetSafePropertyBool(MyBaseThing, nameof(DoNotWriteArrayElementsAsProperties), value); }
        }

        [ConfigProperty(DefaultValue = false)]
        public bool DoNotUseParentPath
        {
            get { return TheThing.GetSafePropertyBool(MyBaseThing, nameof(DoNotUseParentPath)); }
            set { TheThing.SetSafePropertyBool(MyBaseThing, nameof(DoNotUseParentPath), value); }
        }
        [ConfigProperty(DefaultValue = false)]
        public bool ShowTagsInProperties
        {
            get { return TheThing.GetSafePropertyBool(MyBaseThing, nameof(ShowTagsInProperties)); }
            set { TheThing.SetSafePropertyBool(MyBaseThing, nameof(ShowTagsInProperties), value); }
        }
        [ConfigProperty(DefaultValue = false)]
        public bool ReplaceAllTagsOnUpload
        {
            get { return TheThing.GetSafePropertyBool(MyBaseThing, nameof(ReplaceAllTagsOnUpload)); }
            set { TheThing.SetSafePropertyBool(MyBaseThing, nameof(ReplaceAllTagsOnUpload), value); }
        }
        [ConfigProperty(DefaultValue = false)]
        public bool BulkApplyOnUpload
        {
            get { return TheThing.GetSafePropertyBool(MyBaseThing, nameof(BulkApplyOnUpload)); }
            set { TheThing.SetSafePropertyBool(MyBaseThing, nameof(BulkApplyOnUpload), value); }
        }
        [ConfigProperty(DefaultValue = false)]
        public bool ExportAsCSV
        {
            get { return TheThing.GetSafePropertyBool(MyBaseThing, nameof(ExportAsCSV)); }
            set { TheThing.SetSafePropertyBool(MyBaseThing, nameof(ExportAsCSV), value); }
        }

        [ConfigProperty(DefaultValue = false)]
        public bool EnableOPCDataLogging
        {
            get { return TheThing.GetSafePropertyBool(MyBaseThing, nameof(EnableOPCDataLogging)); }
            set { TheThing.SetSafePropertyBool(MyBaseThing, nameof(EnableOPCDataLogging), value); }
        }

        public DateTimeOffset LastDataReceivedTime
        {
            get { return TheThing.GetSafePropertyDate(MyBaseThing, nameof(LastDataReceivedTime)); }
            internal set { TheThing.SetSafePropertyDate(MyBaseThing, nameof(LastDataReceivedTime), value); }
        }
        public long DataReceivedCount
        {
            get {
                return (long) TheThing.GetSafePropertyNumber(MyBaseThing, nameof(DataReceivedCount));
            }
            internal set {
                _dataReceivedCount = value;
                TheThing.SetSafePropertyNumber(MyBaseThing, nameof(DataReceivedCount), value);
            }
        }
        long _dataReceivedCount;
        public void IncrementDataReceivedCount()
        {
            var newCount = _dataReceivedCount++; // Not using Interlocked.* here to minimize impact on performance. In race conditions, a count could be missed
            TheThing.SetSafePropertyNumber(MyBaseThing, nameof(DataReceivedCount), newCount);
        }

        string DiscoUrl = null;
        IBaseEngine MyBaseEngine;
        public TheOPCUARemoteServer(TheThing pThing, ICDEPlugin pPluginBase, string pUrl)
        {
            if (pThing != null)
                MyBaseThing = pThing;
            else
                MyBaseThing = new TheThing();
            DiscoUrl = pUrl;
            MyBaseEngine = pPluginBase.GetBaseEngine();
            MyBaseThing.SetIThingObject(this);
            MyBaseThing.cdeRequiresCustomConfig = true;

            m_CertificateValidation = new CertificateValidationEventHandler(CertificateValidator_CertificateValidation);
        }

        public override bool Init()
        {
            if (mIsInitCalled) return false;
            mIsInitCalled = true;

            TheBaseAssets.MySYSLOG.WriteToLog(78102, TSM.L(eDEBUG_LEVELS.FULLVERBOSE) ? null : new TSM(MyBaseThing.EngineName, $"[{GetLogAddress()}] Initializing.", eMsgLevel.l3_ImportantMessage, ""));

            MyBaseThing.StatusLevel = 4;

            ConnectionState = ConnectionStateEnum.Disconnected;
            BrowsedTagCnt = 0;

            // Migration from V3.1
            if (GetProperty("UseSecurity", false) != null)
            {
                DisableSecurity = !TheThing.GetSafePropertyBool(MyBaseThing, "UseSecurity");
                MyBaseThing.RemoveProperty("UseSecurity");
            }

            //MyBaseThing.DeclareConfigProperty(new TheThing.TheConfigurationProperty { Name = nameof(MyBaseThing.Address), cdeT = ePropertyTypes.TString, Required = true, Description = "", Generalize = true, });
            //MyBaseThing.DeclareConfigProperty(new TheThing.TheConfigurationProperty { Name = nameof(MyBaseThing.FriendlyName), cdeT = ePropertyTypes.TString, Description = "", Generalize = true });

            //MyBaseThing.DeclareConfigProperty(new TheThing.TheConfigurationProperty { Name = nameof(DisableSecurity), cdeT = ePropertyTypes.TBoolean, DefaultValue = false, Description = "Allow connections to unsecured servers", });
            //MyBaseThing.DeclareConfigProperty(new TheThing.TheConfigurationProperty { Name = nameof(Anonymous), cdeT = ePropertyTypes.TBoolean, DefaultValue = false, Description = "", });
            //MyBaseThing.DeclareConfigProperty(new TheThing.TheConfigurationProperty { Name = nameof(DisableDomainCheck), cdeT = ePropertyTypes.TBoolean, DefaultValue = false, Description = "", });
            //MyBaseThing.DeclareConfigProperty(new TheThing.TheConfigurationProperty { Name = nameof(AcceptUntrustedCertificate), cdeT = ePropertyTypes.TBoolean, DefaultValue = false, Description = "", });
            //MyBaseThing.DeclareConfigProperty(new TheThing.TheConfigurationProperty { Name = nameof(AcceptInvalidCertificate), cdeT = ePropertyTypes.TBoolean, DefaultValue = false, Description = "", });
            //MyBaseThing.DeclareConfigProperty(new TheThing.TheConfigurationProperty { Name = nameof(AppCertSubjectName), cdeT = ePropertyTypes.TString, Description = "", });

            //MyBaseThing.DeclareConfigProperty(new TheThing.TheConfigurationProperty { Name = nameof(AutoConnect), cdeT = ePropertyTypes.TBoolean, DefaultValue = false, Description = "", });

            //MyBaseThing.DeclareConfigProperty(new TheThing.TheConfigurationProperty { Name = nameof(PreferredLocales), cdeT = ePropertyTypes.TString, Description = "", });

            //MyBaseThing.DeclareConfigProperty(new TheThing.TheConfigurationProperty { Name = nameof(UserName), cdeT = ePropertyTypes.TString, Description = "", Generalize = true, });
            //MyBaseThing.DeclareConfigProperty(new TheThing.TheConfigurationProperty { Name = nameof(Password), cdeT = ePropertyTypes.TString, Secure = true, Description = "", Generalize = true, });

            //MyBaseThing.DeclareConfigProperty(new TheThing.TheConfigurationProperty { Name = nameof(ReconnectCount), cdeT = ePropertyTypes.TNumber, DefaultValue = 0, Description = "", });
            //MyBaseThing.DeclareConfigProperty(new TheThing.TheConfigurationProperty { Name = nameof(ReconnectPeriod), cdeT = ePropertyTypes.TNumber, DefaultValue = 1000, Description = "", });
            //MyBaseThing.DeclareConfigProperty(new TheThing.TheConfigurationProperty { Name = nameof(KeepAliveInterval), cdeT = ePropertyTypes.TNumber, DefaultValue = 5000, Description = "", });
            //MyBaseThing.DeclareConfigProperty(new TheThing.TheConfigurationProperty { Name = nameof(KeepAliveTimeout), cdeT = ePropertyTypes.TNumber, DefaultValue = 0, Description = "", });
            //MyBaseThing.DeclareConfigProperty(new TheThing.TheConfigurationProperty { Name = nameof(DefSampleRate), cdeT = ePropertyTypes.TNumber, DefaultValue = 1000, Description = "", });
            //MyBaseThing.DeclareConfigProperty(new TheThing.TheConfigurationProperty { Name = nameof(SessionName), cdeT = ePropertyTypes.TString, Description = "", });
//            MyBaseThing.DeclareConfigProperty(new TheThing.TheConfigurationProperty { Name = nameof(SessionTimeout), cdeT = ePropertyTypes.TNumber, DefaultValue = 60000, Description = "", });
            //MyBaseThing.DeclareConfigProperty(new TheThing.TheConfigurationProperty { Name = nameof(OperationTimeout), cdeT = ePropertyTypes.TNumber, DefaultValue = 60000, Description = "", });
            //MyBaseThing.DeclareConfigProperty(new TheThing.TheConfigurationProperty { Name = nameof(PublishingInterval), cdeT = ePropertyTypes.TNumber, DefaultValue = 0, Description = "", });
            //MyBaseThing.DeclareConfigProperty(new TheThing.TheConfigurationProperty { Name = nameof(DefHistoryStartTime), cdeT = ePropertyTypes.TDate, Description = "", });

            //MyBaseThing.DeclareConfigProperty(new TheThing.TheConfigurationProperty { Name = nameof(BrowseBranch), cdeT = ePropertyTypes.TString, Description = "", });
            //MyBaseThing.DeclareConfigProperty(new TheThing.TheConfigurationProperty { Name = nameof(TagHostThingForSubscribeAll), cdeT = ePropertyTypes.TString, Description = "", }); // TODO This is a thing reference: How do we model properties like this?

            //MyBaseThing.DeclareConfigProperty(new TheThing.TheConfigurationProperty { Name = nameof(ShowTagsInProperties), cdeT = ePropertyTypes.TBoolean, DefaultValue = false, Description = "", });
            //MyBaseThing.DeclareConfigProperty(new TheThing.TheConfigurationProperty { Name = nameof(SendStatusCode), cdeT = ePropertyTypes.TBoolean, DefaultValue = false, Description = "", });
            //MyBaseThing.DeclareConfigProperty(new TheThing.TheConfigurationProperty { Name = nameof(SendServerTimestamp), cdeT = ePropertyTypes.TBoolean, DefaultValue = false, Description = "", });
            //MyBaseThing.DeclareConfigProperty(new TheThing.TheConfigurationProperty { Name = nameof(SendPicoSeconds), cdeT = ePropertyTypes.TBoolean, DefaultValue = false, Description = "", });
            //MyBaseThing.DeclareConfigProperty(new TheThing.TheConfigurationProperty { Name = nameof(SendSequenceNumber), cdeT = ePropertyTypes.TBoolean, DefaultValue = false, Description = "", });
            //MyBaseThing.DeclareConfigProperty(new TheThing.TheConfigurationProperty { Name = nameof(SendOpcDataType), cdeT = ePropertyTypes.TBoolean, DefaultValue = false, Description = "", });
            //MyBaseThing.DeclareConfigProperty(new TheThing.TheConfigurationProperty { Name = nameof(UseSequenceNumberForThingSequence), cdeT = ePropertyTypes.TBoolean, DefaultValue = false, Description = "", });
            //MyBaseThing.DeclareConfigProperty(new TheThing.TheConfigurationProperty { Name = nameof(DoNotUsePropsOfProps), cdeT = ePropertyTypes.TBoolean, DefaultValue = false, Description = "", });
            //MyBaseThing.DeclareConfigProperty(new TheThing.TheConfigurationProperty { Name = nameof(DoNotWriteArrayElementsAsProperties), cdeT = ePropertyTypes.TBoolean, DefaultValue = false, Description = "", });
            //MyBaseThing.DeclareConfigProperty(new TheThing.TheConfigurationProperty { Name = nameof(DoNotUseParentPath), cdeT = ePropertyTypes.TBoolean, DefaultValue = false, Description = "", });
            //MyBaseThing.DeclareConfigProperty(new TheThing.TheConfigurationProperty { Name = nameof(ReplaceAllTagsOnUpload), cdeT = ePropertyTypes.TBoolean, DefaultValue = false, Description = "", });
            //MyBaseThing.DeclareConfigProperty(new TheThing.TheConfigurationProperty { Name = nameof(BulkApplyOnUpload), cdeT = ePropertyTypes.TBoolean, DefaultValue = false, Description = "", });
            //MyBaseThing.DeclareConfigProperty(new TheThing.TheConfigurationProperty { Name = nameof(ExportAsCSV), cdeT = ePropertyTypes.TBoolean, DefaultValue = false, Description = "", });

            //MyBaseThing.DeclareConfigProperty(new TheThing.TheConfigurationProperty { Name = nameof(EnableOPCDataLogging), cdeT = ePropertyTypes.TBoolean, DefaultValue = false, Description = "", });

            if (string.IsNullOrEmpty(MyBaseThing.ID))
            {
                MyBaseThing.ID = Guid.NewGuid().ToString();
                if (MyBaseThing.GetProperty("ReconnectPeriod") == null)
                {
                    ReconnectPeriod = 1000; // Default to 1 second, if not specified
                }
                else if (ReconnectPeriod > 0 && ReconnectPeriod < 1000) // Don't allow faster Reconnect than 1 second
                {
                    ReconnectPeriod = 1000;
                }
                AcceptUntrustedCertificate = true;
                if (DefSampleRate == 0)
                {
                    DefSampleRate = 1000;
                }
                if (KeepAliveInterval == 0)
                {
                    KeepAliveInterval = 5000;
                }
                //if (KeepAliveTimeout == 0)
                //{
                //    KeepAliveTimeout = KeepAliveInterval * 2;
                //}
                if (SessionTimeout == 0)
                {
                    SessionTimeout = 60000;
                }
                if (OperationTimeout == 0)
                {
                    OperationTimeout = 60000;
                }

                if (string.IsNullOrEmpty(TagHostThingForSubscribeAll))
                    TagHostThingForSubscribeAll = TheCommonUtils.cdeGuidToString(MyBaseThing.cdeMID);
            }
            if (string.IsNullOrEmpty(MyBaseThing.FriendlyName))
            {
                MyBaseThing.FriendlyName = "OPC UA Srv:" + MyBaseThing.ID;
                AppCertSubjectName = ""; // Falls back to CN=<friendlyname> dc=<hostname> inside the OPC libraries. Otherwise need to specify full set of CN= ... properties
            }
            MyBaseThing.LastUpdate = DateTimeOffset.Now;

            if (MyBaseThing.GetProperty(nameof(ShowTagsInProperties), false) == null)
            {
                ShowTagsInProperties = true;
            }

            MyBaseThing.EngineName = MyBaseEngine.GetEngineName();
            MyBaseThing.DeviceType = eOPCDeviceTypes.OPCRemoteServer;
            if (!string.IsNullOrEmpty(DiscoUrl) && string.IsNullOrEmpty(MyBaseThing.Address))
                MyBaseThing.Address = DiscoUrl;

            string pwTemp = null;
            if (MyBaseThing.GetProperty("Password") != null && (MyBaseThing.GetProperty("Password").cdeE & 0x01) == 0) // TODO Need to use an TheThing.IsSecureProperty function or similar
            {
                pwTemp = Password;
            }
            MyBaseThing.DeclareSecureProperty("Password", ePropertyTypes.TString);
            if (pwTemp != null)
            {
                Password = pwTemp;
            }

            MyBaseThing.RegisterEvent("FileReceived", OnFileReceived);

            //MyTags = new TheStorageMirror<TheOPCTag>(null);
            //MyTags.IsRAMStore = true;
            //MyTags.InitializeStore(true);
            //MyTags.AllowFireUpdates = true;
            MyTags.GetMirrorAsync().ContinueWith(t =>
            {
                MyTags.Mirror.RegisterEvent(eStoreEvents.HasUpdates, OnTagThingUpdate);
                MyTags.Mirror.RegisterEvent(eStoreEvents.DeleteRequested, OnTagThingDelete);
            });
            // CODEREVIEW: Do we also want a delete for host things? Would this be done by deleting items from MyTags? Setting the host thing to "not selected"?

            //MyTagSubscriptions = new TheStorageMirror<TheOPCTag>(null);
            //MyTagSubscriptions.IsRAMStore = true;
            //MyTagSubscriptions.InitializeStore(true);
            //MyTagSubscriptions.AllowFireUpdates = true;
            MyTagSubscriptions.GetMirrorAsync().ContinueWith(t =>
            {
                MyTagSubscriptions.Mirror.RegisterEvent(eStoreEvents.HasUpdates, OnTagThingUpdate);
                MyTagSubscriptions.Mirror.RegisterEvent(eStoreEvents.DeleteRequested, OnTagSubscriptionThingDelete);
            });

            //MyMethods = new TheStorageMirror<TheOPCMethod>(null);
            //MyMethods.IsRAMStore = true;
            //MyMethods.InitializeStore(true);

            //MyEvents = new TheStorageMirror<TheOPCEvent>(TheCDEngines.MyIStorageService);
            //MyEvents.IsRAMStore = true;
            //MyEvents.InitializeStore(false, false);
            //MyEvents.AllowFireUpdates = true;
            MyEvents.GetMirrorAsync().ContinueWith(t =>
            {
                MyEvents.Mirror.RegisterEvent(eStoreEvents.HasUpdates, OnEventThingUpdate);
            });

            //TheBaseEngine.WaitForStorageReadiness(OnStorageReady, true);

            TheBaseEngine.WaitForEnginesStarted(sinkEnginesStarted);
            //TheCDEngines.eventAllEnginesStarted += sinkAutoConnect;
            //if (TheBaseAssets.MyServiceHostInfo.AreAllEnginesStarted)
            //    sinkAutoConnect();

            TheCDEngines.eventEngineShutdown += sinkShutdown;

            // More efficient to just register for the property that we care about, as we might use this thing as the thing host, with a potentially high rate of property changes
            this.GetProperty("IsConnected", false).RegisterEvent(eThingEvents.PropertyChanged, sinkPropChanged);
            //RegisterEvent(eThingEvents.PropertyChanged, sinkPropChanged);
            return false;
        }

        SafeStorageMirror<TheOPCTag> MyTags = new SafeStorageMirror<TheOPCTag>();
        SafeStorageMirror<TheOPCTag> MyTagSubscriptions = new SafeStorageMirror<TheOPCTag>();
        SafeStorageMirror<TheOPCMethod> MyMethods = new SafeStorageMirror<TheOPCMethod>();
        SafeStorageMirror<TheOPCEvent> MyEvents = new SafeStorageMirror<TheOPCEvent>();

        class SafeStorageMirror<T> where T : TheDataBase, new()
        {
            TheStorageMirror<T> _mirror;
            Action<TheStorageMirror<T>> _initStore;
            Action<TheStorageMirror<T>> _registerEvents { get; set; }
            IStorageService _storageService;

            public SafeStorageMirror()
            {
            }

            public SafeStorageMirror(Action<TheStorageMirror<T>> initStore = null, IStorageService storageService = null)
            {
                _initStore = initStore;
                _storageService = storageService;
            }

            public TheStorageMirror<T> Mirror { get { return GetMirrorAsync().Result; } }
            public static implicit operator TheStorageMirror<T>(SafeStorageMirror<T> st)
            {
                return st.Mirror;
            }

            public Task<TheStorageMirror<T>> GetMirrorAsync()
            {
                if (_mirror != null)
                {
                    return TheCommonUtils.TaskFromResult(_mirror);
                }

                var storeCS = new TaskCompletionSource<TheStorageMirror<T>>();
                TheBaseEngine.WaitForStorageReadiness((t, ready) =>
                {
                    var mirror = new TheStorageMirror<T>(_storageService);
                    mirror.RegisterEvent(eStoreEvents.StoreReady, args =>
                    {
                        if (_mirror == null)
                        {
                            _mirror = mirror;
                        }
                        storeCS.TrySetResult(_mirror);
                    });

                    if (_initStore == null)
                    {
                        mirror.IsRAMStore = true;
                        if (_registerEvents != null)
                        {
                            _registerEvents(mirror);
                        }
                        mirror.InitializeStore(true);
                        mirror.AllowFireUpdates = true;
                    }
                    else
                    {
                        _initStore(mirror);
                    }
                }, true);
                return storeCS.Task;
            }
            // Helper methods to make SafeStorageMirror usable in most places where TheStorageMirror would be used
            public TheMirrorCache<T> MyMirrorCache { get { return Mirror.MyMirrorCache; } }
            public bool FlushCache(bool bBackupFirst) { return Mirror.FlushCache(bBackupFirst); }
            public T GetEntryByID(Guid pMagic) { return Mirror.GetEntryByID(pMagic); }
            public Guid StoreMID { get { return Mirror.StoreMID; } }
            public void AddAnItem(T pDetails) { Mirror.AddAnItem(pDetails); }
            public void UpdateItem(T pDetails) { Mirror.UpdateItem(pDetails); }
            public void UpdateItem(T pDetails, Action<TheStorageMirror<T>.StoreResponse> Callback) { Mirror.UpdateItem(pDetails, Callback); }
            public void RemoveItems(List<T> pList, Action<TheStorageMirror<T>.StoreResponse> pCallback) { Mirror.RemoveItems(pList, pCallback); }
            public Action<StoreEventArgs> RegisterEvent(string pEventName, Action<StoreEventArgs> pCallback) { return Mirror.RegisterEvent(pEventName, pCallback); }
            public void UnregisterEvent(string pEventName, Action<StoreEventArgs> pCallback) { Mirror.UnregisterEvent(pEventName, pCallback); }
            public List<T> TheValues { get { return Mirror.TheValues; } }
            public IReaderWriterLock MyRecordsRWLock {  get { return Mirror.MyRecordsRWLock; } }
        }

        void OnFileReceived(ICDEThing pThing, object pFileName)
        {
            TheProcessMessage pMsg = pFileName as TheProcessMessage;
            if (pMsg == null || pMsg.Message == null) return;

            string remoteFileName = "";
            try
            {
                // Get the file name part of the file path for UX purposes
                remoteFileName = System.IO.Path.GetFileName(pMsg.Message.TXT);
                var localFileName = TheCommonUtils.cdeFixupFileName(pMsg.Message.TXT);

                var response = ImportFile(remoteFileName, localFileName).Result;
                if (response.StartsWith("ERROR:"))
                {
                    var responseText = response.Substring("ERROR:".Length);
                    LastMessage = $"{DateTimeOffset.Now}: Error importing tags: {responseText}";
                    TheCommCore.PublishToOriginator(pMsg.Message, new TSM(eEngineName.NMIService, "NMI_ERROR", responseText));
                }
                else
                {
                    LastMessage = $"{DateTimeOffset.Now}: Imported tags: {response}";
                    TheCommCore.PublishToOriginator(pMsg.Message, new TSM(eEngineName.NMIService, "NMI_INFO", response));
                }
            }
            catch (Exception ex)
            {
                var errorText = string.Format("Error importing tags from file '{0}' - {1}", remoteFileName, ex.Message);
                LastMessage = $"{DateTimeOffset.Now}: {errorText}";
                TheCommCore.PublishToOriginator(pMsg.Message, new TSM(eEngineName.NMIService, "NMI_ERROR", errorText));
            }

        }

        private Task<string> ImportFile(string remoteFileName, string localFileName)
        {
            string response = "";
            switch (Path.GetExtension(remoteFileName).ToLowerInvariant())
            {
#if ENABLE_CSVIMPORT
                case ".csv":
                    {
                        // Format:
                        // DisplayName,(NodeId|BrowsePath)[,SamplingInterval][,ChangeTrigger][,DeadbandValue],ThingMID
                        // ChangeTrigger: 0 = Status, 1 = Value, 2 = Timestamp
                        // Sampling Interval: in ms
                        // DeadbandValue: 0 = any change, >0 = absolute deadband, <0 = % deadband
                        // ThingMID: cdeMID of the thing to receive the tag values
                        var csvData = File.ReadAllText(localFileName);
                        var createTagMsg = new MsgOPCUACreateTags
                        {
                            ReplaceAllTags = ReplaceAllTagsOnUpload,
                            BulkApply = BulkApplyOnUpload,
                            Tags = new List<MsgOPCUACreateTags.TagInfo>(),
                        };
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
                        var bSuccess = TheCSVParser.ParseCSVData(csvData, new CSVParserOptions { }, async (dict, sourceTimestamp) =>
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
                        {
                            var tag = new MsgOPCUACreateTags.TagInfo();
                            if (dict.TryGetValue(nameof(tag.DisplayName), out object displayName))
                            {
                                tag.DisplayName = TheCommonUtils.CStr(displayName);
                            }
                            if (dict.TryGetValue(nameof(tag.NodeId), out object nodeId))
                            {
                                tag.NodeId = TheCommonUtils.CStr(nodeId);
                            }
                            if (dict.TryGetValue(nameof(tag.BrowsePath), out object browsePath))
                            {
                                tag.BrowsePath = TheCommonUtils.CStr(browsePath);
                            }
                            if (dict.TryGetValue(nameof(tag.SamplingInterval), out object samplingInterval))
                            {
                                tag.SamplingInterval = TheCommonUtils.CInt(samplingInterval);
                            }
                            if (dict.TryGetValue(nameof(tag.ChangeTrigger), out object changeTrigger))
                            {
                                tag.ChangeTrigger = (ChangeTrigger)TheCommonUtils.CInt(changeTrigger);
                            }
                            if (dict.TryGetValue(nameof(tag.DeadbandValue), out object deadbandValue))
                            {
                                tag.DeadbandValue = TheCommonUtils.CDbl(deadbandValue);
                            }
                            if (dict.TryGetValue("ThingMID", out object thingMid))
                            {
                                tag.HostThingAddress = new TheMessageAddress { ThingMID = TheCommonUtils.CGuid(thingMid) };
                            }
                            if (dict.TryGetValue("HistoryStartTime", out object historyStartTime))
                            {
                                tag.HistoryStartTime = TheCommonUtils.CDate(historyStartTime);
                            }
                            createTagMsg.Tags.Add(tag);
                        }).Result;
                        if (bSuccess)
                        {
                            response = HandleImportedTagCreation(createTagMsg).Result;
                        }
                        else
                        {
                            response = $"ERROR:Error parsing file {remoteFileName}";
                        }
                    }
                    break;
#endif
                case ".json":
                    {
                        var jsonData = File.ReadAllText(localFileName);

                        var createTagMsg = TheCommonUtils.DeserializeJSONStringToObject<MsgOPCUACreateTags>(jsonData);
                        if (createTagMsg != null)
                        {
#if !NET35 && !NET4
                            response = HandleImportedTagCreation(createTagMsg).Result;
#else
                            response = HandleImportedTagCreation(createTagMsg).Result;
#endif
                        }
                        else
                        {
                            response = $"ERROR:Error parsing {remoteFileName} JSON file";
                        }
                    }
                    break;
                case ".uap":
                    {
                        var uapFileLines = File.ReadAllLines(localFileName);
                        var iniFile = ParseIniFile(uapFileLines);
                        var tags = ParseUAPProject(iniFile);

                        var hostThing = TheThingRegistry.GetThingByMID(new Guid(TagHostThingForSubscribeAll));
                        if (hostThing != null)
                        {
                            var createTagMsg = new MsgOPCUACreateTags
                            {
                                ReplaceAllTags = ReplaceAllTagsOnUpload,
                                BulkApply = BulkApplyOnUpload,
                                Tags = new List<MsgOPCUACreateTags.TagInfo>(),
                            };

                            foreach (var tagKV in tags)
                            {
                                var tag = tagKV.Value;
                                if (!string.IsNullOrEmpty(tag.DisplayName))
                                {
                                    if (tag.HostThingAddress == null)
                                    {
                                        tag.HostThingAddress = new TheMessageAddress(hostThing);
                                    }
                                    createTagMsg.Tags.Add(tag);
                                }
                            }
                            if (createTagMsg.Tags.Count > 0)
                            {
                                response = HandleImportedTagCreation(createTagMsg).Result;
                            }
                            else
                            {
                                response = $"ERROR:Error parsing file {remoteFileName} - no tags found";
                            }
                        }
                        else
                        {
                            response = $"ERROR:No host thing specified";
                        }
                    }
                    break;
                default:
                    response = $"ERROR:Unsupported file extension ({remoteFileName})!";
                    break;
            }
            return TheCommonUtils.TaskFromResult(response);
        }

        Dictionary<string, Dictionary<string, string>> ParseIniFile(string[] iniFileLines)
        {
            var iniFile = new Dictionary<string, Dictionary<string, string>>();
            string section = null;
            foreach (var line in iniFileLines)
            {
                if (line.StartsWith("["))
                {
                    section = line.Trim(new char[] { '[', ']' });
                    if (!iniFile.ContainsKey(section))
                    {
                        iniFile.Add(section, new Dictionary<string, string>());
                    }
                }
                else
                {
                    var lineParts = line.Split(new char[] { '=' }, 2);
                    if (lineParts.Length == 2)
                    {
                        iniFile[section][lineParts[0]] = lineParts[1];
                    }
                }
            }
            return iniFile;
        }

        Dictionary<string, MsgOPCUACreateTags.TagInfo> ParseUAPProject(Dictionary<string,Dictionary<string,string>> uapProjectFile)
        {
            var uapItemPrefix = @"Data%20Access%20View\DocumentSettings\Items\";
            var tags = new Dictionary<string, MsgOPCUACreateTags.TagInfo>();

            string server = null;
            foreach (var line in uapProjectFile["Servers"])
            {
                var serverName = line.Key.Split(new char[] { '\\' }, 2)[0];
                if (server == null)
                {
                    server = serverName;
                }
                else
                {
                    if (serverName != server)
                    {
                        throw new Exception("More then one server in UAP file: not supported.");
                    }
                }
            }
            if (server == null)
            {
                throw new Exception("No server found in UAP file: not supported");
            }
            foreach (var line in uapProjectFile["Documents"])
            {
                if (line.Key.StartsWith(uapItemPrefix))
                {
                    var itemParts = line.Key.Substring(uapItemPrefix.Length).Split(new char[] { '\\' }, 3);
                    if (itemParts.Length >= 3)
                    {
                        var itemId = itemParts[0] + @"\" + itemParts[1]; // TODO handle server identifier (itemParts[0])
                        var itemName = itemParts[2];
                        var itemValue = line.Value.Trim('\"'); ;
                        if (!tags.ContainsKey(itemId))
                        {
                            tags[itemId] = new MsgOPCUACreateTags.TagInfo();
                        }
                        var tag = tags[itemId];
                        switch (itemName)
                        {
                            case "DisplayName":
                                tag.DisplayName = itemValue;
                                break;
                            case "NodeId":
                                tag.NodeId = itemValue;
                                break;
                            case "MonitoringMode":
                                break;
                            case @"Filter\Trigger":
                                tag.ChangeTrigger = (ChangeTrigger)TheCommonUtils.CInt(itemValue);
                                break;
                            case @"MonitoringParameters\SamplingInterval":
                                tag.SamplingInterval = TheCommonUtils.CInt(itemValue);
                                break;
                        }

                    }
                }
            }
            return tags;
        }

        private Task<string> HandleImportedTagCreation(MsgOPCUACreateTags createTagMsg)
        {
            string response;
            MsgOPCUACreateTagsResponse createResponse = TheCommRequestResponse.PublishRequestJSonAsync<MsgOPCUACreateTags, MsgOPCUACreateTagsResponse>(MyBaseThing, MyBaseThing, createTagMsg).Result;

            if (String.IsNullOrEmpty(createResponse.Error))
            {
                var successCount = createResponse.Results.Count(r => String.IsNullOrEmpty(r.Error));
                if (successCount >= createResponse.Results.Count)
                {
                    response = $"Created {successCount} tag subscriptions";
                    TheBaseAssets.MySYSLOG.WriteToLog(78102, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM(MyBaseThing.EngineName, $"[{GetLogAddress()}] {response}", eMsgLevel.l4_Message, ""));
                }
                else
                {
                    string badTags = "";
                    for (int i = 0; i < createTagMsg.Tags.Count; i++)
                    {
                        if (!String.IsNullOrEmpty(createResponse.Results[i].Error))
                        {
                            badTags += $"{createTagMsg.Tags[i].DisplayName}/{createTagMsg.Tags[i].NodeId ?? createTagMsg.Tags[i].BrowsePath}:{createResponse.Results[i].Error},";
                        }
                    }
                    response = $"ERROR:Created {successCount} tag subscriptions. Failed {createResponse.Results.Count - successCount} subscriptions.";
                    TheBaseAssets.MySYSLOG.WriteToLog(78102, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, $"[{GetLogAddress()}] {response}", eMsgLevel.l1_Error, badTags));

                    response += " See log for details.";
                }
            }
            else
            {
                response = $"ERROR:General error creating {createTagMsg.Tags.Count} tag subscriptions: {createResponse.Error}";
                TheBaseAssets.MySYSLOG.WriteToLog(78102, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, $"[{GetLogAddress()}] {response}", eMsgLevel.l1_Error, ""));
            }
            return TheCommonUtils.TaskFromResult(response);
        }

        public override bool Delete()
        {
            if (!mIsInitialized)
            {
                return false;
            }
            mIsInitialized = false;

            UnsubscribeTagsInAllHostThings();

            var thingsOfEngine = TheThingRegistry.GetThingsOfEngine(MyBaseEngine.GetEngineName());
            foreach (var thing in thingsOfEngine)
            {
                if (TheThing.GetSafePropertyString(thing, "ServerID") == GetBaseThing().ID) // && thing != MyBaseThing)
                {
                    TheThingRegistry.DeleteThing(thing);
                }
            }
            Dispose(true);
            GC.Collect();
            return true;
        }

        public virtual void Dispose()
        {
            Dispose(true);
        }
        void Dispose(bool disposing)
        {
            if (disposing)
            {
                try
                {
                    Disconnect(false, false, "Disconnect due to Delete/Dispose");
                }
                catch { }
                UnregisterEvent(eEngineEvents.FileReceived, OnFileReceived);
                MyTags.Mirror.UnregisterEvent(eStoreEvents.HasUpdates, OnTagThingUpdate);
                MyTags.Mirror.UnregisterEvent(eStoreEvents.DeleteRequested, OnTagThingDelete);
                MyTagSubscriptions.Mirror.UnregisterEvent(eStoreEvents.HasUpdates, OnTagThingUpdate);
                MyTagSubscriptions.Mirror.UnregisterEvent(eStoreEvents.DeleteRequested, OnTagSubscriptionThingDelete);
                MyEvents.Mirror.UnregisterEvent(eStoreEvents.HasUpdates, OnEventThingUpdate);
                TheCDEngines.eventEngineShutdown -= sinkShutdown;
                this.GetProperty("IsConnected", false).UnregisterEvent(eThingEvents.PropertyChanged, sinkPropChanged);
            }
        }

        void UnsubscribeTagsInAllHostThings()
        {
            if (!string.IsNullOrEmpty(TagHostThings))
            {
                foreach (string hostThingMID in TheCommonUtils.CStringToList(TagHostThings, ';'))
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(78102, TSM.L(eDEBUG_LEVELS.FULLVERBOSE) ? null : new TSM(MyBaseThing.EngineName, $"[{GetLogAddress()}] MonitoredAllTagsInServer for {hostThingMID}", eMsgLevel.l3_ImportantMessage, ""));
                    UnsubscribeTagsInHostThing(hostThingMID);
                }
                TagHostThings = "";
            }
        }

        //private void OnStorageReady(ICDEThing pThing, object para)
        //{
        //    MyEvents = new TheStorageMirror<TheOPCEvent>(TheCDEngines.MyStorageService);
        //    MyEvents.CacheTableName = "OPCEventThings" + TheThing.GetSafeThingGuid(MyBaseThing, "OEvntThngs");
        //    MyEvents.IsRAMStore = true;
        //    MyEvents.CacheStoreInterval = 1;
        //    MyEvents.IsStoreIntervalInSeconds = true;
        //    MyEvents.IsCachePersistent = true;
        //    MyEvents.InitializeStore(false, false);

        //    if (tOPCEventForm != null)
        //    {
        //        tOPCEventForm.defDataSource = MyEvents.StoreMID.ToString();
        //    }
        //    MyEvents.AllowFireUpdates = true;
        //    MyEvents.RegisterEvent(eStoreEvents.HasUpdates, OnEventThingUpdate);
        //}

        void OnTagThingUpdate(object param)
        {

            try
            {
                string errors = "";
                string tags = "";
                // Note: this is used for both browsed tags and subscribed tags storage mirrors
                Subscription subscription = null;

                foreach (var tTag in ((IEnumerable<TheOPCTag>)((TheStorageMirror<TheOPCTag>.StoreResponse)((StoreEventArgs)param).Para).MyRecords))
                {
                    tTag.NodeIdName = NormalizeNodeIdName(tTag.NodeIdName);
                    if (tTag.IsSubscribedAsProperty && tTag.MyOPCServer != null && tTag.TargetProperty != null)
                    {
                        if (subscription == null)
                        {
                            subscription = GetOrCreateSubscription(tTag.SampleRate);
                            if (subscription == null)
                            {
                                errors = "Not connected or no subscription";
                                break;
                            }
                        }
                        lock (subscription.MonitoredItems)
                        {
                            var existingItem = subscription.MonitoredItems.FirstOrDefault(item => item.Handle == tTag);
                            if (existingItem != null && !existingItem.StartNodeId.Equals(tTag.GetResolvedNodeIdName()))
                            {
                                try
                                {
                                    subscription.RemoveItem(existingItem);
                                    subscription.ApplyChanges();
                                }
                                catch { }
                            }

                            string error;
                            RegisterAndMonitorTagInHostThing(subscription, tTag, out error, false, false, false);
                            if (!string.IsNullOrEmpty(error))
                            {
                                errors += $"{tTag}:{error}. ";
                            }
                            else
                            {
                                tags += $"{tTag},";
                            }
                        }
                    }
                }
                if (subscription != null)
                {
#if OLD_UA
                            var items = subscription.ApplyChanges();
                            foreach (var item in items)
#else
                    subscription.ApplyChanges();
                    foreach (var item in subscription.MonitoredItems)
#endif
                    {
                        if (ServiceResult.IsBad(item.Status.Error))
                        {
                            errors += $"{item.StartNodeId}:{item.Status.Error.ToLongString()}. ";
                        }
                    }
                }
                if (!string.IsNullOrEmpty(errors))
                {
                    LastMessage = $"{DateTimeOffset.Now}: Error updating monitored tags: {errors}. Success: {tags}";
                    TheBaseAssets.MySYSLOG.WriteToLog(78102, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, $"[{GetLogAddress()}] Error updating monitored tag", eMsgLevel.l1_Error, errors));
                }
                else
                {
                    LastMessage = $"{DateTimeOffset.Now}: Updated monitored tags: {tags}.";
                }
                TheBaseAssets.MySYSLOG.WriteToLog(78102, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(MyBaseThing.EngineName, $"[{GetLogAddress()}] Updated monitored tags", eMsgLevel.l1_Error, tags));
            }
            catch (Exception e)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(78102, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, $"[{GetLogAddress()}] Internal error updating monitored tags", eMsgLevel.l1_Error, e.ToString()));
            }
        }

        void OnTagThingDelete(object param)
        {
            // Delete of browsed tags merely for the user: unsubscribe must happen in subscribed tags
            //var itemToDelete = (Guid) ((StoreEventArgs)param).Para;
            //var tTag = MyTags.GetEntryByID(itemToDelete);
            //tTag?.UnregisterInHostThing();
        }

        void OnTagSubscriptionThingDelete(object param)
        {
            var itemToDelete = (Guid)((StoreEventArgs)param).Para;
            var tTag = MyTagSubscriptions.GetEntryByID(itemToDelete);
            if (tTag != null)
            {
                tTag.UnregisterInHostThing(GetSubscription(), true);
            }
        }

        void OnEventThingUpdate(object param)
        {
            bool bChanges = false;
            Subscription subscription = null;
            foreach (var tEvent in ((IEnumerable<TheOPCEvent>)((TheStorageMirror<TheOPCEvent>.StoreResponse)((StoreEventArgs)param).Para).MyRecords))
            {
                if (tEvent.IsSubscribedAsProperty)
                {
                    if (subscription == null)
                    {
                        subscription = GetOrCreateSubscription(tEvent.SampleRate);
                        if (subscription == null)
                        {
                            continue;
                        }
                    }
                    string error;
                    if (RegisterAndMonitorTagInHostThing(subscription, tEvent, out error, false, false, false))
                    {
                        bChanges = true;
                    }
                    // TODO log/handle/show error
                }
            }
            if (bChanges && subscription != null)
            {
                subscription.ApplyChanges(); // TODO Report any errors
            }
        }

        void sinkPropChanged(/*ICDEThing pThing,*/ object pPara)
        {
            cdeP tProp = pPara as cdeP;
            var bConnected = ConnectionState == ConnectionStateEnum.Connected;
            if (tProp != null && tProp.Name == nameof(IsConnected) && TheCommonUtils.CBool(tProp) != bConnected)
            {
                // This property is read-only: fix it to reflect the actual state
                IsConnected = bConnected;
                //IsConnected = TheCommonUtils.CBool(tProp);
            }
        }

        void sinkShutdown()
        {
            Disconnect(false, false, "Disconnect due to shutdown");
        }

        void sinkEnginesStarted(ICDEThing pThing, object para)
        {
            mIsInitialized = true;
            FireEvent(eThingEvents.Initialized, this, true, true);
            FireEvent("ServerInit", this, ConnectionState == ConnectionStateEnum.Connected, true);

            TheBaseAssets.MySYSLOG.WriteToLog(78102, TSM.L(eDEBUG_LEVELS.FULLVERBOSE) ? null : new TSM(MyBaseThing.EngineName, $"[{GetLogAddress()}] Initialized.", eMsgLevel.l3_ImportantMessage, ""));

            if (MyBaseThing.StatusLevel == 4)
                MyBaseThing.StatusLevel = 0;

            if (AutoConnect)
            {
                if (ConnectionState != ConnectionStateEnum.Connected)
                {
                    //CODE-REVIEW: There might be a racing condition between AutoConnect and the HandleMessage coming in from other plugins => Should be handled now with the lock-protected ConnectionState
                    TheCommonUtils.cdeRunAsync("OPCClientAutoConnect", true,
                    (p) =>
                    {
                        do
                        {
                            try
                            {
                                if (!MyBaseThing.cdePendingConfig)
                                {
                                    Connect(false);
                                }
                            }
                            catch (Exception)
                            {
                            }
                            TheCommonUtils.SleepOneEye(TheCommonUtils.GetRandomUInt((uint)(this.ReconnectPeriod * .75), (uint)(this.ReconnectPeriod * 1.25)), 100);        ///Random Band of Reconnect time ensure that not all try at the same time
                        } while (ConnectionState != ConnectionStateEnum.Connected && this.AutoConnect && ReconnectPeriod >0 && TheBaseAssets.MasterSwitch);
                    });
                }
                // Browser(currentRoot, currentRoot.ToString(), true, false, null);
            }
            else
            {
                MonitorAllTagsInHostThing(true);
            }
        }



        private void ExportTags(ICDEThing arg1, object pObj)
        {
            TheProcessMessage pMsg = pObj as TheProcessMessage;

            string exportText;
            string fileExt;
            if (ExportAsCSV)
            {
                var exportTextSB = new StringBuilder();
                exportTextSB.Append("DisplayName,BrowsePath,NodeId,ThingMID,ChangeTrigger,DeadbandValue,SamplingInterval");

                var theValues = MyTagSubscriptions.TheValues;
                if (theValues.FirstOrDefault(t => t.HistoryStartTime.HasValue && t.HistoryStartTime != DateTimeOffset.MinValue) != null)
                {
                    exportTextSB.AppendLine(",HistoryStartTime");
                }
                else
                {
                    exportTextSB.AppendLine();
                }

                foreach (var tag in theValues)
                {
                    exportTextSB.Append($"{tag.DisplayName},{tag.GetDisplayNamePath()},{tag.NodeIdName},{tag.HostThingMID},{(int) tag.ChangeTrigger},{tag.DeadbandFilterValue},{tag.SampleRate}");
                    if (tag.HistoryStartTime.HasValue && tag.HistoryStartTime.Value != DateTimeOffset.MinValue)
                    {
                        exportTextSB.AppendLine(TheCommonUtils.CStr(tag.HistoryStartTime.Value));
                    }
                    else
                    {
                        exportTextSB.AppendLine();
                    }
                }
                exportText = exportTextSB.ToString();
                fileExt = "CSV";
            }
            else
            {
                var createTagMsg = new MsgOPCUACreateTags { BulkApply = true, ReplaceAllTags = false, Tags = new List<MsgOPCUACreateTags.TagInfo>() };
                foreach(var tag in MyTagSubscriptions.TheValues)
                {
                    var tagRequest = new MsgOPCUACreateTags.TagInfo
                    {
                        DisplayName = tag.DisplayName,
                        BrowsePath = tag.GetDisplayNamePath(),
                        NodeId = tag.NodeIdName,
                        HostThingAddress = new TheMessageAddress { ThingMID = TheCommonUtils.CGuid(tag.HostThingMID), },
                        ChangeTrigger = (ChangeTrigger) tag.ChangeTrigger,
                        DeadbandValue = tag.DeadbandFilterValue,
                        SamplingInterval = tag.SampleRate,
                        HistoryStartTime = tag.HistoryStartTime,
                    };
                    createTagMsg.Tags.Add(tagRequest);
                }

                exportText = TheCommonUtils.SerializeObjectToJSONString(createTagMsg);
                fileExt = "JSON";
            }

            var fileName = $"Tags{MyBaseThing.FriendlyName}_{TheCommonUtils.GetTimeStamp()}.{fileExt}";
            foreach(var invalidChar in Path.GetInvalidFileNameChars())
            {
                fileName = fileName.Replace(invalidChar, '_');
            }
            // TODO Switch to the following when it's OK to take dependency on C-DEngine >= 4.1012
            //var fileName = TheCommonUtils.GetSafeFileName($"Tags{MyBaseThing.FriendlyName}", fileExt, true);
            TSM tFilePush = new TSM(eEngineName.ContentService, $"CDE_FILE:{fileName}:{(ExportAsCSV? "text/csv" : "application/json")}")
            {
                SID = pMsg.Message.SID,
                PLS = exportText, // "bin",
                //PLB = TheCommonUtils.CUTF8String2Array(exportText)
            };
            TheCommCore.PublishToOriginator(pMsg.Message, tFilePush);
        }

        private void SubscribeAsProperty(ICDEThing arg1, object pObj, TheStorageMirror<TheOPCTag> tagStore)
        {
            TheProcessMessage pMsg = pObj as TheProcessMessage;
            if (currentRoot == null)
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

                if (MyBaseThing.GetProperty(nameof(ShowTagsInProperties), false) == null)
                {
                    ShowTagsInProperties = true;
                }

                TheOPCTag newTag;
                {
                    TheOPCTag tTag = tagStore.MyMirrorCache.GetEntryByID(TheCommonUtils.CGuid(tPara[2]).ToString());
                    if (tTag == null) return;

                    newTag = new TheOPCTag(tTag);
                    if (TheCommonUtils.CGuid(newTag.HostThingMID) == Guid.Empty)
                    {
                        newTag.HostThingMID = TagHostThingForSubscribeAll;
                    }
                    newTag.MyOPCServer = this;
                }

                if (String.IsNullOrEmpty(newTag.DisplayName))
                {
                    newTag.DisplayName = newTag.NodeIdName;
                }

                if (TheCommonUtils.CGuid(newTag.HostThingMID) == Guid.Empty)
                {
                    TheCommCore.PublishToOriginator(pMsg.Message, new TSM(eEngineName.NMIService, "NMI_TOAST", "No Host Thing specified - please specify a Thing to receive the OPC Tags"));
                    return;
                }

                var cdeHostThing = TheThingRegistry.GetThingByMID("*", TheCommonUtils.CGuid(newTag.HostThingMID));
                if (cdeHostThing == null)
                {
                    TheCommCore.PublishToOriginator(pMsg.Message, new TSM(eEngineName.NMIService, "NMI_TOAST", "Host Thing not found - please verify the Thing to receive the OPC Tags"));
                    return;
                }

                if (newTag.IsSubscribedAsPropertyInThing(cdeHostThing))
                {
                    // TODO offer to unsubscribe or just do it?
                    TheCommCore.PublishToOriginator(pMsg.Message, new TSM(eEngineName.NMIService, "NMI_TOAST", $"Tag ({newTag.DisplayName}) already subscribed as Property in Thing"));
                    return;
                }

                try
                {
                    var subscription = GetOrCreateSubscription(newTag.SampleRate);
                    if (subscription == null)
                    {
                        TheCommCore.PublishToOriginator(tTSM, new TSM(eEngineName.NMIService, "NMI_TOAST", $"Error subscribing Tag ({newTag.DisplayName}) to Property {newTag?.TargetProperty?.Name}: Not connected or no subscription"));
                    }
                    else
                    {
                        //tTag.SampleRate = DefSampleRate;
                        string error;
                        if (!RegisterAndMonitorTagInHostThing(subscription, newTag, out error, true, true, false))
                        {
                            newTag.UnregisterInHostThing(subscription, true);
                            TheCommCore.PublishToOriginator(tTSM, new TSM(eEngineName.NMIService, "NMI_TOAST", $"Error subscribing Tag ({newTag.DisplayName}) to Property {newTag?.TargetProperty?.Name}: {error}"));
                        }
                        else
                        {
                            //LiveTagCnt++;
                            //MyTags.AddAnItem(newTag, null);
                            TheCommCore.PublishToOriginator(tTSM, new TSM(eEngineName.NMIService, "NMI_TOAST", string.Format("Tag ({0}) converted to Property", newTag.DisplayName)));
                        }
                    }
                }
                catch (Exception e)
                {
                    TheCommCore.PublishToOriginator(tTSM, new TSM(eEngineName.NMIService, "NMI_TOAST", $"Error subscribing Tag ({newTag.DisplayName}) to Property {newTag?.TargetProperty?.Name}: {e.ToString()}"));
                }
            }
            catch (Exception eee)
            {
                LastMessage = "Failed to Subscribe";
                TheCommCore.PublishToOriginator(pMsg.Message, new TSM(eEngineName.NMIService, "NMI_INFO", string.Format("Error during Subscribe: {0}", eee.ToString())));
            }
        }

        private void SubscribeAsThing(ICDEThing arg1, object pObj, TheStorageMirror<TheOPCTag> tagStore)
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
                TheOPCTag tTag = tagStore.MyMirrorCache.GetEntryByID(TheCommonUtils.CGuid(tPara[2]).ToString());
                if (tTag == null) return;
                if (tTag.IsSubscribedAsThing)
                {
                    TheCommCore.PublishToOriginator(pMsg.Message, new TSM(eEngineName.NMIService, "NMI_TOAST", $"Tag ({tTag.DisplayName}) already subscribed as Thing"));
                    return;
                }
                //tTag.SampleRate = DefSampleRate;

                if (String.IsNullOrEmpty(tTag.DisplayName))
                {
                    tTag.DisplayName = tTag.NodeIdName;
                }

                tTag.MyOPCServer = this;

                tTag.IsSubscribedAsThing = true;

                var result = RegisterAndMonitorTagThing(null, tTag, null, null, false);
                if (result?.StartsWith("ERROR") == true)
                {
                    tTag.IsSubscribedAsThing = false;
                    TheCommCore.PublishToOriginator(pMsg.Message, new TSM(eEngineName.NMIService, "NMI_INFO", string.Format("Error during Subscribe: {0}", result)));
                }
                else
                {
                    LiveTagCnt++;
                }
                tagStore.UpdateItem(tTag, null);
                TheCommCore.PublishToOriginator(tTSM, new TSM(eEngineName.NMIService, "NMI_TOAST", string.Format("Thing created for Tag '{0}'", tTag.DisplayName)));
            }
            catch (Exception eee)
            {
                LastMessage = "Failed to Subscribe";
                TheCommCore.PublishToOriginator(pMsg.Message, new TSM(eEngineName.NMIService, "NMI_INFO", string.Format("Error during Subscribe: {0}", eee.ToString())));
            }
        }

        public string GetLogAddress()
        {
            return String.Format("{0} {1}", MyBaseThing.Address, MyBaseThing.FriendlyName);
        }

        private void FlushStores()
        {
            MyTags.MyMirrorCache.Reset();
            MyMethods.MyMirrorCache.Reset();
        }

        //protected TheFormInfo tOPCTagForm = null;
        //protected TheFormInfo tOPCMethodForm = null;
        //protected TheFormInfo tOPCEventForm = null;
        // Create a monitored item for the tag and track the fact that the tag was subscribed to either an TheOPCUATagThing (pTag.IsSubcribedAsThing) or and external host thing (this.TagHostThings)
        string RegisterAndMonitorTagThing(Subscription subscription, TheOPCTag pTag, TheThing pBaseThingForUaTag, TheOPCUATagThing tNewTag, bool bPopulateSubscribedTagsOnly)
        {
            TheThing tThing = null;
            if (!bPopulateSubscribedTagsOnly)
            {
                if (subscription == null)
                {
                    subscription = GetOrCreateSubscription(pTag.SampleRate);
                    if (subscription == null)
                    {
                        return "ERROR: Not connected or failed to subscribe.";
                    }
                }
                if (pTag == null)
                {
                    return "ERROR: Must specify TheOPCTag";
                }

                if (tNewTag == null)
                {
                    tNewTag = new TheOPCUATagThing(pBaseThingForUaTag, pTag);
                    tNewTag.GetBaseThing().EngineName = MyBaseThing.EngineName;
                }

                tNewTag.Setup(pTag);
                pTag.IsSubscribedAsThing = true;

                tThing = TheThingRegistry.RegisterThing(tNewTag);
            }
            if (tThing != null || bPopulateSubscribedTagsOnly)
            {
                if (ShowTagsInProperties)
                {
                    MyTagSubscriptions.MyRecordsRWLock.RunUnderWriteLock(() =>
                    {
                        MyTagSubscriptions.UnregisterEvent(eStoreEvents.HasUpdates, OnTagThingUpdate);
                        if (!bPopulateSubscribedTagsOnly)
                        {
                            pTag.HostThingMID = tThing.cdeMID.ToString();   //CODE-REVIEW: Is this allowed as its needed to jump directly form Subscription table to the LiveTag Form -> No: HostTHingMID is used to determine prop vs. thing subscription!
                        }
                        MyTagSubscriptions.AddAnItem(pTag as TheOPCTag);
                        MyTagSubscriptions.RegisterEvent(eStoreEvents.HasUpdates, OnTagThingUpdate);
                    });
                }
                return tThing?.cdeMID.ToString();
            }
            else
            {
                return "ERROR: NO THING!";
            }
        }

        bool RegisterAndMonitorTagInHostThing(Subscription subscription, TheOPCMonitoredItemBase pTag, out string error, bool applySubscription = true, bool bReadInitialTagValue = true, bool bPopulateSubscribedTagsOnly = false)
        {
            if (!bPopulateSubscribedTagsOnly)
            {
                error = pTag.RegisterInHostThing();
                if (!String.IsNullOrEmpty(error))
                {
                    return false;
                }

                if (subscription != null) // Only attempt to create monitored items when connected
                {
                    if (pTag.MonitorTag(subscription, out error, applySubscription, bReadInitialTagValue))
                    {
                        this.LiveTagCnt++;
                        TheBaseAssets.MySYSLOG.WriteToLog(78102, TSM.L(eDEBUG_LEVELS.FULLVERBOSE) ? null : new TSM(MyBaseThing.EngineName, $"[{GetLogAddress()}] Monitored tag {pTag.ToString()}: {error}", eMsgLevel.l3_ImportantMessage, ""));
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(error))
                        {
                            TheBaseAssets.MySYSLOG.WriteToLog(78102, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM(MyBaseThing.EngineName, $"[{GetLogAddress()}] Error Monitoring tag {pTag.ToString()}: {error}", eMsgLevel.l1_Error, ""));
                            pTag.UnregisterInHostThing(subscription, applySubscription);
                            return false;
                        }
                        TheBaseAssets.MySYSLOG.WriteToLog(78102, TSM.L(eDEBUG_LEVELS.FULLVERBOSE) ? null : new TSM(MyBaseThing.EngineName, $"[{GetLogAddress()}] Monitored tag updated {pTag.ToString()}: {error}", eMsgLevel.l3_ImportantMessage, ""));
                    }
                }
                var childTags = pTag.GetChildTags();
                if (childTags != null)
                {
                    foreach (var childTag in childTags)
                    {
                        RegisterAndMonitorTagInHostThing(subscription, childTag, out error, applySubscription, bReadInitialTagValue, bPopulateSubscribedTagsOnly);
                    }
                }
            }
            else
            {
                error = null;
            }
            // Track the host thing in the TagHostThings property
            if (!string.IsNullOrEmpty(this.TagHostThings))
            {
                if (!this.TagHostThings.Contains(pTag.HostThingMID))
                {
                    this.TagHostThings += ";" + pTag.HostThingMID;
                }
            }
            else
            {
                this.TagHostThings = pTag.HostThingMID;
            }
            if (bPopulateSubscribedTagsOnly || (pTag is TheOPCTag && ShowTagsInProperties))
            {
                MyTagSubscriptions.MyRecordsRWLock.RunUnderUpgradeableReadLock(() =>
                {
                    MyTagSubscriptions.UnregisterEvent(eStoreEvents.HasUpdates, OnTagThingUpdate);
                    var hostThingMid = TheCommonUtils.CGuid(pTag.HostThingMID);
                    if (hostThingMid != Guid.Empty)
                    {
                        var existingItems = MyTagSubscriptions.MyMirrorCache.GetEntriesByFunc(tag =>
                                TheCommonUtils.CGuid(tag.HostThingMID) == hostThingMid
                            && tag.DisplayName == pTag.DisplayName);
                        if (existingItems.Any())
                        {
                            // Make sure we update an item that previously existed for the same hostthing/property (it well be effectively replaced)
                            pTag.cdeMID = existingItems.First().cdeMID;
                        }
                        var itemsToRemove = existingItems.Skip(1);
                        if (itemsToRemove.Any())
                        {
                            // If there any other duplicates, remove them
                            MyTagSubscriptions.RemoveItems(itemsToRemove.ToList(), null);
                        }
                    }
                    MyTagSubscriptions.AddAnItem(pTag as TheOPCTag); // Add or updates the item in the storage mirror
                    MyTagSubscriptions.RegisterEvent(eStoreEvents.HasUpdates, OnTagThingUpdate);
                });
            }
            return true;
        }

        object subscriptionCreateLock = new object(); // used only when creating a new session

        internal Subscription GetSubscription()
        {
            return GetOrCreateSubscription(DefSampleRate, false);
        }
        internal Subscription GetOrCreateSubscription(int sampleRate, bool Create = true)
        {
            var session = m_session;
            var subscriptionCount = session?.SubscriptionCount;
            if (subscriptionCount == 1)
            {
                return session.Subscriptions.FirstOrDefault();
            }
            if (subscriptionCount > 1)
            {
                // This should never happen
                TheBaseAssets.MySYSLOG.WriteToLog(78102, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, String.Format("[{0}] Internal error: unexpected number of subscriptions found {1}. Expected at most subscription. ", GetLogAddress(), subscriptionCount), eMsgLevel.l1_Error, ""));
                return session.Subscriptions.FirstOrDefault();
            }
            if (!Create)
            {
                return null;
            }

            lock (subscriptionCreateLock)
            {
                session = m_session;
                if (session?.SubscriptionCount > 0)
                {
                    return GetOrCreateSubscription(sampleRate, false);
                }
                if (session == null)
                {
                    // Handle race between m_session assignment and Subscription != null check so we don't cause null ref exception
                    return null;
                }

                if (session.Disposed)
                {
                    return null;
                }

                var mySubscription = new Subscription(m_session.DefaultSubscription);
                mySubscription.PublishingEnabled = true;
                if (PublishingInterval > 0)
                {
                    mySubscription.PublishingInterval = PublishingInterval;
                }
                else
                {
                    mySubscription.PublishingInterval = sampleRate >= 0 ? sampleRate : DefSampleRate;
                }
                mySubscription.KeepAliveCount = 10;
                mySubscription.LifetimeCount = 10;
                mySubscription.MaxNotificationsPerPublish = 0; //was 50 default is 100
                mySubscription.Priority = 100;
                //mySubscription.MaxMessageCount = 10000;
                m_session.AddSubscription(mySubscription);
                //m_session.SubscriptionsChanged -= OnSubscriptionsChanged;
                //m_session.SubscriptionsChanged += OnSubscriptionsChanged;
                try
                {
                    mySubscription.Create();
                    TheBaseAssets.MySYSLOG.WriteToLog(78102, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(MyBaseThing.EngineName, String.Format("[{0}] Created subscription", GetLogAddress()), eMsgLevel.l4_Message, mySubscription.ToString()));
                    return mySubscription;
                }
                catch (Exception e1)
                {
                    try
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(78102, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(MyBaseThing.EngineName, String.Format("[{0}] Error while creating subscription", GetLogAddress()), eMsgLevel.l1_Error, e1.ToString()));
                        m_session.RemoveSubscription(mySubscription);
                        mySubscription.Dispose();
                    }
                    catch (Exception e2)
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(78102, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, String.Format("[{0}] Internal error: unexpected error while cleaning up state after subscription create failure", GetLogAddress()), eMsgLevel.l1_Error, e2.ToString()));
                    }
                }
            }
            return null;
        }

        //private void OnSubscriptionsChanged(object sender, EventArgs e)
        //{
        //    if (m_session != null)
        //    {
        //        var subscription = m_session.Subscriptions.FirstOrDefault();
        //        if (subscription != m_subscription)
        //        {
        //            TheBaseAssets.MySYSLOG.WriteToLog(78102, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM(MyBaseThing.EngineName, String.Format("[{0}] Updated subscription.", GetLogAddress()), eMsgLevel.l6_Debug, ""));
        //            m_subscription = subscription;
        //        }
        //        int count = m_session.Subscriptions.Count();
        //        if (count != 1)
        //        {
        //            // This should never happen
        //            TheBaseAssets.MySYSLOG.WriteToLog(78102, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(MyBaseThing.EngineName, String.Format("[{0}] Internal error: unexpected number of subscriptions found {1}. Expected 1 subscription. ", GetLogAddress(), count), eMsgLevel.l1_Error, ""));
        //        }
        //    }
        //    else
        //    {
        //        if (m_subscription != null)
        //        {
        //            TheBaseAssets.MySYSLOG.WriteToLog(78102, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(MyBaseThing.EngineName, String.Format("[{0}] Error: session null when attempting to update subscriptions. Subscription exists - disposing", GetLogAddress(), m_subscription != null), eMsgLevel.l1_Error, ""));
        //            try
        //            {
        //                m_subscription.Dispose();
        //            }
        //            catch { }
        //            m_subscription = null;
        //        }
        //        else
        //        {
        //            TheBaseAssets.MySYSLOG.WriteToLog(78102, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(MyBaseThing.EngineName, String.Format("[{0}] Error: session null when attempting to update subscriptions.", GetLogAddress()), eMsgLevel.l1_Error, ""));
        //        }
        //    }
        //}

        /// <summary>
        /// opc.tcp://CX-0E1E60:4840
        /// </summary>
        internal Session m_session;
        object m_reconnectHandlerLock = new object();
        private SessionReconnectHandler m_reconnectHandler;
        private ApplicationConfiguration m_configuration;
        private CertificateValidationEventHandler m_CertificateValidation;
        //private Subscription m_subscription;

        private NodeId m_rootId = Opc.Ua.ObjectIds.ObjectsFolder;
        private NodeId[] m_referenceTypeIds = new NodeId[] { Opc.Ua.ReferenceTypeIds.HierarchicalReferences };
        //private Dictionary<NodeId, byte[]> m_typeImageMapping = new Dictionary<NodeId, byte[]>();
        /// <summary>
        /// The user identity to use when creating the session.
        /// </summary> 
        private IUserIdentity UserIdentity { get; set; }
        private ReferenceDescription currentRoot = null;

        // Connection State must only be modified while holding this lock!
        private object ConnectionStateLock = new object();

        // Only Connect() must transition from Connecting, as the state is used to prevent double connects
        // Only Disconnect() must transition from Disconnecting, as the state is used to preventdouble disconnects
        // Reconnect handlers can only change state from Reconnecting to Connected

        enum ConnectionStateEnum { Disconnected, Connecting, Reconnecting, Connected,Disconnecting }
        ConnectionStateEnum _connectionStatePrivate;
        ConnectionStateEnum ConnectionState
        {
            get
            {
                return _connectionStatePrivate;
            }
            set
            {
                switch (value)
                {
                    case ConnectionStateEnum.Connected:
                        IsConnected = true;
                        IsReconnecting = false;
                        break;
                    case ConnectionStateEnum.Connecting:
                        IsConnected = false;
                        IsReconnecting = false;
                        break;
                    case ConnectionStateEnum.Reconnecting:
                        IsConnected = false;
                        IsReconnecting = true;
                        break;
                    case ConnectionStateEnum.Disconnected:
                    case ConnectionStateEnum.Disconnecting:
                        IsConnected = false;
                        IsReconnecting = false;
                        break;
                    default:
                        // Invalid: need to update this is extending the state machine!
                        TheBaseAssets.MySYSLOG.WriteToLog(78101, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, $"[{GetLogAddress()}] Set ConnectionState: Invalid connection state {_connectionStatePrivate}", eMsgLevel.l1_Error, ""));
                        break;
                }
                _connectionStatePrivate = value;
            }
        }

        /// <summary>
        /// True if the client is supposed to be connected
        /// </summary>
        private bool IsTargetStateConnected
        {
            get
            {
                return ConnectionState == ConnectionStateEnum.Connected || ConnectionState == ConnectionStateEnum.Connecting || ConnectionState == ConnectionStateEnum.Reconnecting;
            }
        }

        class MyCertificateValidator : CertificateValidator
        {
            private TheOPCUARemoteServer _server;
            public MyCertificateValidator(TheOPCUARemoteServer server) : base()
            {
                _server = server;
            }
            public override void Validate(X509Certificate2Collection chain)
            {
                if (_server.AcceptInvalidCertificate)
                {
                    return;
                }
                base.Validate(chain);
            }
        }

        public string Connect(bool logEssentialOnly)
        {
            bool bConnected = false;
            string connectError = "Unknown";
            ConnectionStateEnum previousState;
            lock (ConnectionStateLock)
            {
                previousState = ConnectionState;
                if (previousState == ConnectionStateEnum.Connected)
                {
                    return "";
                }
                if (previousState == ConnectionStateEnum.Connecting)
                {
                    return "Connect already in progress";
                }
                if (previousState == ConnectionStateEnum.Reconnecting)
                {
                    return "Reconnect already in progress";
                }
                if (previousState == ConnectionStateEnum.Disconnecting)
                {
                    return "Disconnect in progress";
                }

                if (previousState == ConnectionStateEnum.Disconnected)
                {
                    ConnectionState = ConnectionStateEnum.Connecting;
                }
            }
            if (ConnectionState != ConnectionStateEnum.Connecting)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(78101, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, $"[{GetLogAddress()}] Connect: Invalid connection state {previousState}", eMsgLevel.l1_Error, ""));
                return "Internal error: invalid connection state"; // Someone extended the connection state enum and didn't update these checks
            }

            try
            {
                LastMessage = "Connecting at " + DateTimeOffset.Now.ToString();
                TheBaseAssets.MySYSLOG.WriteToLog(78101, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM(MyBaseThing.EngineName, String.Format("[{0}] Connecting to server.", GetLogAddress()), eMsgLevel.l4_Message, ""));

                DoDisconnectInternal(); // Call this just in case some state has not been cleaned up properly on earlier disconnects

                MyTagSubscriptions?.Mirror?.MyMirrorCache?.Reset();

                // determine the URL that was selected.
                string serverUrl = MyBaseThing.Address;

                // select the best endpoint.
                if (OperationTimeout == 0) //can be 0 here
                {
                    OperationTimeout = 60000;
                }
                EndpointDescription endpointDescription = ClientUtils.SelectEndpoint(serverUrl, !DisableSecurity, OperationTimeout);
                //TheBaseAssets.MySYSLOG.WriteToLog(78101, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, String.Format("[{0}] Selected endpoint", GetLogAddress()), eMsgLevel.l4_Message, ""));

                m_configuration = new ApplicationConfiguration();
                m_configuration.ApplicationType = ApplicationType.Client;
                m_configuration.ApplicationName = "C-MyOPCUA Client:" + MyBaseThing.FriendlyName;

                m_configuration.CertificateValidator = new MyCertificateValidator(this);

                var s = new SecurityConfiguration();

                s.ApplicationCertificate = new CertificateIdentifier();
                s.ApplicationCertificate.StoreType = "Directory";

                s.ApplicationCertificate.StorePath = Utils.ReplaceSpecialFolderNames(Path.Combine(Path.Combine(Path.Combine("%CommonApplicationData%", "C-Labs"), "CertificateStores"), "MachineDefault"));
                s.ApplicationCertificate.SubjectName = this.AppCertSubjectName;
                //if (!s.ApplicationCertificate.SubjectName.Contains("="))
                //{
                //    s.ApplicationCertificate.SubjectName = "CN=\"" + s.ApplicationCertificate.SubjectName + "\"";
                //}


                s.TrustedPeerCertificates = new CertificateTrustList();
                s.TrustedPeerCertificates.StoreType = "Directory";
                s.TrustedPeerCertificates.StorePath = Utils.ReplaceSpecialFolderNames(Path.Combine(Path.Combine(Path.Combine("%CommonApplicationData%", "C-Labs"), "CertificateStores"), "UA Applications"));
                //s.TrustedPeerCertificates.OpenStore().Enumerate();

                s.TrustedIssuerCertificates = new CertificateTrustList();
                s.TrustedIssuerCertificates.StoreType = "Directory";
                s.TrustedIssuerCertificates.StorePath = Utils.ReplaceSpecialFolderNames(Path.Combine(Path.Combine(Path.Combine("%CommonApplicationData%", "C-Labs"), "CertificateStores"), "UA Certificate Authorities"));

                s.RejectedCertificateStore = new CertificateStoreIdentifier();
                s.RejectedCertificateStore.StoreType = CertificateStoreType.Directory;
                s.RejectedCertificateStore.StorePath = Utils.ReplaceSpecialFolderNames(Path.Combine(Path.Combine(Path.Combine("%CommonApplicationData%", "C-Labs"), "CertificateStores"), "RejectedCertificates"));

                m_configuration.SecurityConfiguration = s;

                m_configuration.ClientConfiguration = new ClientConfiguration();
                m_configuration.ClientConfiguration.MinSubscriptionLifetime = 10000;
                m_configuration.ClientConfiguration.DefaultSessionTimeout = SessionTimeout; // 10000;
                m_configuration.ClientConfiguration.EndpointCacheFilePath = "Endpoints.xml";

                m_configuration.TransportQuotas = new TransportQuotas();
                m_configuration.TransportQuotas.OperationTimeout = OperationTimeout; //10000;
                m_configuration.TransportQuotas.MaxStringLength = 100 * 1024 * 1024; // 50MB - previous 2MB: 2097152;
                m_configuration.TransportQuotas.MaxByteStringLength = 100 * 1024 * 1024; // 100MB 9/22/2016, previous 50MB - previous 2MB: 2097152;
                m_configuration.TransportQuotas.MaxArrayLength = 65535;
                m_configuration.TransportQuotas.MaxMessageSize = 100 * 1024 * 1024; // 50MB - previous 4MB: 4194304;
                m_configuration.TransportQuotas.MaxBufferSize = 65535;
                m_configuration.TransportQuotas.ChannelLifetime = 300000;
                m_configuration.TransportQuotas.SecurityTokenLifetime = 3600000;


                //m_configuration.TransportQuotas.MaxByteStringLength = 2097152;

                m_configuration.Validate(ApplicationType.Client).Wait()
                    ;
                m_configuration.CertificateValidator.CertificateValidation += m_CertificateValidation;

                // check the application certificate. Create if it doesn't exist and Opc.Ua.CertificateGenerator.exe is installed
                ApplicationInstance application = new ApplicationInstance();
                application.ApplicationType = ApplicationType.Client;
                application.ApplicationConfiguration = m_configuration;
                if (!DisableSecurity)
                { 
                    application.CheckApplicationInstanceCertificate(true, 0).Wait()
                        ;
                }

                EndpointConfiguration endpointConfiguration = EndpointConfiguration.Create(m_configuration);
                ConfiguredEndpoint endpoint = new ConfiguredEndpoint(null, endpointDescription, endpointConfiguration);

                UserIdentity tIDent = null;
                if (Anonymous)
                {
                    tIDent = new UserIdentity();
                }
                else if (!string.IsNullOrEmpty(UserName))
                {
                    tIDent = new UserIdentity(UserName, Password);
                    //tIDent.PolicyId = "UsernamePassword";
                    //endpoint.SelectedUserTokenPolicy = endpoint.Description.FindUserTokenPolicy(tIDent.TokenType, tIDent.IssuedTokenType);
                }

                string[] preferredLocales;
                if (String.IsNullOrEmpty(PreferredLocales))
                {
                    preferredLocales = null;
                }
                else
                {
                    preferredLocales = TheCommonUtils.cdeSplit(PreferredLocales, ';', false, false);
                }
                //TheBaseAssets.MySYSLOG.WriteToLog(78101, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, String.Format("[{0}] Creating session", GetLogAddress()), eMsgLevel.l4_Message, ""));

                m_session = Session.Create(
                    m_configuration,
                    endpoint,
                    false,
                    !DisableDomainCheck,
                    (String.IsNullOrEmpty(SessionName)) ? m_configuration.ApplicationName : SessionName,
                    (uint)SessionTimeout, //10000,
                    tIDent != null ? tIDent : UserIdentity,
                    preferredLocales).Result;
                //TheBaseAssets.MySYSLOG.WriteToLog(78101, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, String.Format("[{0}] Created session", GetLogAddress()), eMsgLevel.l4_Message, ""));

                if (m_session.SessionTimeout / 2  < KeepAliveInterval)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(78102, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM(MyBaseThing.EngineName, String.Format("[{0}] Adjusting KeepAliveInterval to {1} instead of configured {2} due to server adjustment", GetLogAddress(), m_session.SessionTimeout, KeepAliveInterval), eMsgLevel.l3_ImportantMessage, ""));
                }
                m_session.KeepAliveInterval = Math.Min(KeepAliveInterval, (int) m_session.SessionTimeout / 2); // 10000;

#if OLD_OPCUA
                if (KeepAliveTimeout > 0)
                {
                    m_session.KeepAliveTimeout = KeepAliveTimeout;
                }
                else
                {
                    m_session.KeepAliveTimeout = m_session.KeepAliveInterval * 2;
                }
#endif

                m_session.PublishError += OnSessionPublishError;
                m_session.Notification += OnSessionNotification;
                m_session.RenewUserIdentity += OnSessionRenewUserIdentity;
                m_session.SessionClosing += OnSessionClosing;

                // set up keep alive callback.
                m_session.KeepAlive += new KeepAliveEventHandler(Session_KeepAlive);

                if (m_session != null)
                {
                    //TheBaseAssets.MySYSLOG.WriteToLog(78101, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, String.Format("[{0}] Finding root node", GetLogAddress()), eMsgLevel.l4_Message, ""));
                    INode node = m_session.NodeCache.Find(m_rootId);

                    if (node != null)
                    {
                        ReferenceDescription reference = new ReferenceDescription();
                        reference.NodeId = node.NodeId;
                        reference.NodeClass = node.NodeClass;
                        reference.BrowseName = node.BrowseName;
                        reference.DisplayName = node.DisplayName;
                        reference.TypeDefinition = node.TypeDefinitionId;

                        LastMessage = DateTimeOffset.Now + " Connected. Subscribing...";
                        TheBaseAssets.MySYSLOG.WriteToLog(78001, TSM.L(eDEBUG_LEVELS.FULLVERBOSE) ? null : new TSM(MyBaseThing.EngineName, String.Format("[{0}] Connected to server. Subscribing.", GetLogAddress()), eMsgLevel.l4_Message, ""));

                        LiveTagCnt = 0;

                        MonitorAllTagsInServer(false);

                        // MonitorAllEventsInServer(); // TagHostThingForSubscribeAll

                        LastMessage = DateTimeOffset.Now + " Subscriptions Active. " + LastMessage;
                        TheBaseAssets.MySYSLOG.WriteToLog(78002, TSM.L(logEssentialOnly ? eDEBUG_LEVELS.VERBOSE : eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, String.Format("[{0}] Connected to server. Subscriptions Active.", GetLogAddress()), eMsgLevel.l3_ImportantMessage, ""));
                        MyBaseThing.StatusLevel = 1;

                        currentRoot = reference;
                        ConnectionState = ConnectionStateEnum.Connected;

                        // raise an event.
                        //TheBaseAssets.MySYSLOG.WriteToLog(78101, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, String.Format("[{0}] Firing ConnectComplete event", GetLogAddress()), eMsgLevel.l4_Message, ""));
                        MyBaseThing.FireEvent("ConnectComplete", this, null, true);
                        //TheBaseAssets.MySYSLOG.WriteToLog(78101, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, String.Format("[{0}] Fired ConnectComplete event", GetLogAddress()), eMsgLevel.l4_Message, ""));
                        bConnected = true;
                    }
                    else
                    {
                        LastMessage = DateTimeOffset.Now + " Failed to find root node. " + LastMessage;
                        TheBaseAssets.MySYSLOG.WriteToLog(78103, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(MyBaseThing.EngineName, String.Format("[{0}] Failed to connect to server: Failed to find root node.", GetLogAddress()), eMsgLevel.l1_Error, null));
                        connectError = "Failed to find root node.";
                    }
                }
                else
                {
                    LastMessage = DateTimeOffset.Now + " Failed to create session. " + LastMessage;
                    TheBaseAssets.MySYSLOG.WriteToLog(78104, TSM.L(eDEBUG_LEVELS.OFF) ? null: new TSM(MyBaseThing.EngineName, String.Format("[{0}] Failed to connect to server. Failed to create session.", GetLogAddress()), eMsgLevel.l1_Error, null));
                    connectError = "Failed to create session.";
                }

                if (!bConnected)
                {
                    MyBaseThing.StatusLevel = 3;
                    DoDisconnectInternal();
                    //TheBaseAssets.MySYSLOG.WriteToLog(78101, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, String.Format("[{0}] Firing ConnectFailed event", GetLogAddress()), eMsgLevel.l4_Message, ""));
                    MyBaseThing.FireEvent("ConnectFailed", this, "Failed to connect to server", true);
                    connectError = "Failed to connect to server";
                    //TheBaseAssets.MySYSLOG.WriteToLog(78101, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, String.Format("[{0}] Fired ConnectFailed  event", GetLogAddress()), eMsgLevel.l4_Message, ""));
                }
            }
            catch (Exception e)
            {
                DoDisconnectInternal();
                //TheBaseAssets.MySYSLOG.WriteToLog(78101, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(MyBaseThing.EngineName, String.Format("[{0}] Internal error connecting", GetLogAddress()), eMsgLevel.l4_Message, e?.ToString()));
                var sr = (e is ServiceResultException) ? ((ServiceResultException)e).InnerResult : null;
                var message =
                    (sr != null ? sr.ToString() : "") + e.ToString();
                //+ (e.InnerException != null ? e.InnerException.ToString() : "")

                LastMessage = String.Format("{0} Connect Failed: {1}", DateTimeOffset.Now, message);
                TheBaseAssets.MySYSLOG.WriteToLog(78003, TSM.L(logEssentialOnly ? eDEBUG_LEVELS.ESSENTIALS : eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, String.Format("[{0}] Connect Failed for server", GetLogAddress()), eMsgLevel.l1_Error, message.Split('\r')[0]));
                MyBaseThing.FireEvent("ConnectFailed", this, message, true);
                MyBaseThing.StatusLevel = 3;
                connectError = message;
            }
            finally
            {
                if (bConnected)
                {
                    ConnectionState = ConnectionStateEnum.Connected;
                }
                else
                {
                    ConnectionState = ConnectionStateEnum.Disconnected;
                }
            }
            return bConnected ? "" : connectError;
        }

        private void OnSessionClosing(object sender, EventArgs e)
        {
            TheBaseAssets.MySYSLOG.WriteToLog(78104, TSM.L(eDEBUG_LEVELS.EVERYTHING) ? null : new TSM(MyBaseThing.EngineName, String.Format("[{0}] Session closing for server", GetLogAddress()), eMsgLevel.l4_Message));
        }

        private IUserIdentity OnSessionRenewUserIdentity(Session session, IUserIdentity identity)
        {
            TheBaseAssets.MySYSLOG.WriteToLog(78105, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM(MyBaseThing.EngineName, String.Format("[{0}] Session user identify not renewed for server", GetLogAddress()), eMsgLevel.l1_Error));
            return identity;
        }

        private void OnSessionNotification(Session session, NotificationEventArgs e)
        {
        }

        private void OnSessionPublishError(Session session, PublishErrorEventArgs e)
        {
            //TheBaseAssets.MySYSLOG.WriteToLog(78106, TSM.L(eDEBUG_LEVELS.FULLVERBOSE) ? null : new TSM(MyBaseThing.EngineName, String.Format("[{0}] Session publish error for server", GetLogAddress()), eMsgLevel.l1_Error, e.Status.ToLongString()));
        }

        private List<string> CreateTagSubscriptions(List<string> pSubs)
        {
            Subscription subscription = null;
            List<string> tRet = new List<string>();
            int count = 0;
            foreach (string tSub in pSubs)
            {
                string retVal = "";
                try
                {
                    count++;
                    // Parse tSub parameters
                    // Subscription format: <tagKind>:;:<displayName>:;:parentId:;:tagRef [ :;:sampleRate [ :;:hostThingMid [ :;:ChangeTrigger [ :;:deadbandvalue ] ]] ]
                    string[] tAdre = TheCommonUtils.cdeSplit(tSub, ":;:", false, false);
                    if (tAdre.Length < 4) continue;

                    string tagKind = tAdre[0];
                    string displayName = tAdre[1];
                    string parentId = tAdre[2];
                    string nodeIdName = NormalizeNodeIdName(tAdre[3]);
                    int sampleRate = -1;
                    if (tAdre.Length > 4)
                    {
                        sampleRate = TheCommonUtils.CInt(tAdre[4]);
                    }
                    string hostThingMID = null;
                    if (tAdre.Length > 5)
                    {
                        hostThingMID = tAdre[5];
                    }
                    int changeTrigger = 1;
                    if (tAdre.Length > 6 && tAdre[6].Length > 0)
                    {
                        changeTrigger = TheCommonUtils.CInt(tAdre[6]);
                    }
                    double deadBandValue = 0;
                    if (tAdre.Length > 7 && tAdre[7].Length > 0)
                    {
                        deadBandValue = TheCommonUtils.CDbl(tAdre[7]);
                    }
                    if (subscription == null)
                    {
                        subscription = GetOrCreateSubscription(sampleRate);
                    }

                    if (subscription != null)
                    {
                        retVal = CreateTagSubscription(subscription, tagKind, displayName, parentId, nodeIdName, sampleRate, hostThingMID, null, changeTrigger, deadBandValue, true, null); // count == pSubs.Count); // TODO Perf: Only apply the last tag?
                    }
                    else
                    {
                        retVal = "Error: not connected or failed to subscribe";
                    }
                }
                catch (Exception e)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(78107, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(MyBaseThing.EngineName, String.Format("[{1}] Create TagSubscriptions failed for {0}", tSub, GetLogAddress()), eMsgLevel.l1_Error, e.ToString()));
                    retVal = "Error: " + e.Message;
                }

                tRet.Add(retVal);
            }

            if (subscription != null)
            {
                try
                {
                    subscription.ApplyChanges(); // TODO Report any errors
                }
                catch (Exception e)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(78108, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(MyBaseThing.EngineName, String.Format("[{0}] Create TagSubscriptions failed", GetLogAddress()), eMsgLevel.l1_Error, e.ToString()));
                    tRet.Insert(0, "Error: " + e.Message);
                }
            }

            return tRet;
        }

        string CreateTagSubscription(Subscription subscription, string tagKind, string displayName, string parentId, string nodeIdName, int sampleRate, string hostThingMID, DateTimeOffset? historyStartTime, int changeTrigger, double deadBandFilterValue, bool bApplySubscription, TheEventSubscription eventSubscription)
        {
            string retVal = null;
            switch (tagKind)
            {
                case "UATAG":
                    {
                        //if (sampleRate <= 0)
                        //    sampleRate = 50;

                        var tHostThing = TheThingRegistry.GetThingByMID("*", TheCommonUtils.CGuid(hostThingMID));
                        if (tHostThing == null)
                        {
                            return "ERROR: Thing not found.";
                        }

                        // Create the tag and start monitoring it
                        var tTag = new TheOPCTag
                        {
                            MyOPCServer = this,
                            DisplayName = displayName,
                            Parent = parentId,
                            NodeIdName = nodeIdName,
                            SampleRate = sampleRate,
                            ChangeTrigger = changeTrigger,
                            DeadbandFilterValue = deadBandFilterValue,
                            HostThingMID = hostThingMID,
                        };
                        if (historyStartTime.HasValue)
                        {
                            tTag.HistoryStartTime = historyStartTime.Value;
                        }
                        // Always attempt to add the tag: duplicate monitored items are avoided inside
                        //if (!TagHostThings.Contains(TheCommonUtils.cdeGuidToString(tHostThing.cdeMID)) || !tTag.IsSubscribedAsPropertyInThing(tHostThing))
                        {
                            string error;
                            if (!RegisterAndMonitorTagInHostThing(subscription, tTag, out error, bApplySubscription, true, false))
                            {
                                // Note: error may be NULL when the tag already existed and no new monitored item was created: return success in that case as well
                                retVal = error;
                            }
                        }
                        if (retVal == null) // No error
                        {
                            retVal = tTag.HostThingMID;
                        }
                    }
                    break;

                case "UAMETHOD":
                    TheThing tThing = TheThingRegistry.GetThingByProperty(MyBaseThing.EngineName, Guid.Empty, "Address", string.Format("{0}:;:{1}:;:{2}", MyBaseThing.ID, parentId, nodeIdName));
                    if (tThing == null)
                    {
                        TheOPCUAMethodThing tNewTag = new TheOPCUAMethodThing(null, MyBaseThing.EngineName);
                        retVal = ReadMethodData(tNewTag, displayName, parentId, TheOPCMonitoredItemBase.ResolveNodeIdName(nodeIdName, subscription?.Session));
                        if (!String.IsNullOrEmpty(retVal))
                        {
                            return retVal;
                        }
                        TheThingRegistry.RegisterThing(tNewTag);
                        tThing = tNewTag.GetBaseThing();
                    }
                    if (tThing != null)
                    {
                        if (sampleRate > 0)
                        {
                            TheThing.SetSafePropertyNumber(tThing, "MethodCallTimeout", sampleRate);
                        }
                        var tMethodThing = tThing.GetObject() as TheOPCUAMethodThing;
                        if (tMethodThing != null)
                        {
                            if (!tMethodThing.HasMethodData())
                            {
                                retVal = ReadMethodData(tMethodThing, displayName, parentId, TheOPCMonitoredItemBase.ResolveNodeIdName(nodeIdName, subscription?.Session));
                                if (!string.IsNullOrEmpty(retVal))
                                {
                                    return retVal;
                                }
                            }
                            retVal = TheCommonUtils.cdeGuidToString(tThing.cdeMID);
                        }
                        else
                        {
                            retVal = "ERROR: Internal error - invalid method thing";
                        }
                    }
                    break;
                case "UAEVENT":
                    {
                        var tHostThing = TheThingRegistry.GetThingByMID("*", TheCommonUtils.CGuid(hostThingMID));
                        if (tHostThing == null)
                        {
                            return null;
                        }

                        // Create the Event subscriber and start monitoring it
                        var tEvent = new TheOPCEvent
                        {
                            MyOPCServer = this,
                            DisplayName = displayName,
                            Parent = parentId,
                            NodeIdName = nodeIdName,
                            SampleRate = sampleRate,
                            ChangeTrigger = changeTrigger, // Change Trigger = 0: write events as they come, 1: AggregateRetainedConditions 
                            DeadbandFilterValue = deadBandFilterValue,
                            HostThingMID = hostThingMID,
                            EventInfo = eventSubscription,
                        };
                        string error;
                        if (!RegisterAndMonitorTagInHostThing(subscription, tEvent, out error, bApplySubscription, true, false))
                        {
                            // Note: error may be NULL when the tag already existed and no new monitored item was created: return success in that case as well
                            retVal = error;
                        }
                        if (retVal == null) // No error
                        {
                            retVal = tEvent.HostThingMID;
                        }
                        break;
                    }
            }
            return retVal;
        }
        string ReadMethodData(TheOPCUAMethodThing tMethodThing, string displayName, NodeId parentId, NodeId tagRef)
        {
            string retVal;
            TheOPCMethod tMeth = new TheOPCMethod
            {
                DisplayName = displayName,
                ParentId = parentId,
                TagRef = tagRef,
                OPCServerID = MyBaseThing.ID,
                MyOPCServer = this,
                IsSubscribed = true,
            };
            retVal = MethodBrowser(tMeth.TagRef, tMeth.DisplayName, tMeth);
            if (!String.IsNullOrEmpty(retVal))
            {
                return retVal;
            }
            tMethodThing.Setup(tMeth);
            return null;
        }

        private void MonitorAllTagsInServer(bool bPopulateSubscribedTagsOnly)
        {
            LiveTagCnt = 0;

            // Connect the thing tags
            List<TheThing> tList = TheThingRegistry.GetThingsByProperty(MyBaseThing.EngineName, Guid.Empty, "ServerID", MyBaseThing.ID);
            Subscription subscription = null;
            if (tList != null && tList.Count > 0)
            {
                int tagCount = 0;
                foreach (TheThing tThing in tList)
                {
                    switch (tThing.DeviceType)
                    {
                        case eOPCDeviceTypes.OPCLiveTag:
                            string[] tAdre = TheCommonUtils.cdeSplit(tThing.Address, ":;:", false, false);
                            if (tAdre.Length > 1)
                            {
                                TheOPCTag tTag = new TheOPCTag
                                {
                                    MyOPCServer = this,
                                    DisplayName = tThing.FriendlyName,
                                    Parent = tAdre[0],
                                    NodeIdName = NormalizeNodeIdName(tAdre[1]),
                                    SampleRate = TheCommonUtils.CInt(TheThing.GetSafePropertyNumber(tThing, "SampleRate")),
                                    IsSubscribedAsThing = true,
                                };
                                if (tThing.GetObject() == null)
                                {
                                    subscription = GetOrCreateSubscription(tTag.SampleRate);
                                    if (subscription == null)
                                    {
                                        break;
                                    }
                                    RegisterAndMonitorTagThing(null, tTag, tThing, null, bPopulateSubscribedTagsOnly);
                                    tagCount++;
                                }
                                else
                                {
                                    TheOPCUATagThing tUATag = tThing.GetObject() as TheOPCUATagThing;
                                    if (!tUATag.IsActive)
                                    {
                                        subscription = GetOrCreateSubscription(tTag.SampleRate);
                                        if (subscription == null)
                                        {
                                            break;
                                        }
                                        RegisterAndMonitorTagThing(null, tTag, tThing, tUATag, bPopulateSubscribedTagsOnly);
                                        tagCount++;
                                    }
                                }
                            }
                            break;
                        case eOPCDeviceTypes.OPCMethod:
                            TheOPCMethod tMethod = new TheOPCMethod();
                            string[] tAdre1 = TheCommonUtils.cdeSplit(tThing.Address, ":;:", false, false);
                            if (tAdre1.Length > 2)
                            {
                                tMethod.TagRef = NodeId.Parse(tAdre1[2]);
                                tMethod.DisplayName = tThing.FriendlyName;
                                tMethod.ParentId = NodeId.Parse(tAdre1[1]);
                                tMethod.OPCServerID = MyBaseThing.ID;
                                tMethod.MyOPCServer = this;
                                tMethod.IsSubscribed = true;
                                var browseError = MethodBrowser(tMethod.TagRef, tMethod.DisplayName, tMethod);
                                if (!String.IsNullOrEmpty(browseError))
                                {
                                    // TODO Could not get method metadata: how do we handle?
                                    // For now tMethod.Args == null, and will be handled later on first use
                                }
                                TheThingRegistry.WaitForInitializeAsync(tThing).ContinueWith((t) =>
                                {
                                    TheOPCUAMethodThing tObj = tThing.GetObject() as TheOPCUAMethodThing;
                                    if (tObj != null)
                                        tObj.Setup(tMethod);
                                });
                            }
                            break;
                    }
                    if (tagCount > 50)
                    {
                        LiveTagCnt += tagCount;
                        tagCount = 0;
                    }
                }
                LiveTagCnt += tagCount;
                if (subscription != null)
                    subscription.ApplyChanges(); // TODO Report any errors
            }

            // Any host things are now tracked in the TagHostsThings property. TagHostThingForSubscribeAll is is only used temporarily for Subscribe All In Properties functionality
            //if (!string.IsNullOrEmpty(TagHostThingForSubscribeAll))
            //    RestoreSubscription(TagHostThingForSubscribeAll);

            if (ShowTagsInProperties)
            {
                MyTagSubscriptions.MyMirrorCache.Reset();
            }

            MonitorAllTagsInHostThing(bPopulateSubscribedTagsOnly);
        }

        private void MonitorAllTagsInHostThing(bool populateSubscribedTagsOnly)
        {
            if (!string.IsNullOrEmpty(TagHostThings))
            {
                var hostThings = TheCommonUtils.CStringToList(TagHostThings, ';');
                foreach (string hostThingMID in hostThings)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(78102, TSM.L(eDEBUG_LEVELS.FULLVERBOSE) ? null : new TSM(MyBaseThing.EngineName, $"[{GetLogAddress()}] UnmonitorAllTagsInServer for {hostThingMID}", eMsgLevel.l3_ImportantMessage, ""));
                    MonitorTagsInHostThing(hostThingMID, populateSubscribedTagsOnly);
                }
            }
        }

        private void MonitorTagsInHostThing(string hostThingMid, bool bPopulateSubscribedTagsOnly)
        {
            TheThing tHostThing = TheThingRegistry.GetThingByMID("*", TheCommonUtils.CGuid(hostThingMid));
            if (tHostThing == null) return;

            List<TheOPCSensorSubscription> tagPro = TheOPCMonitoredItemBase.GetHostThingOPCProperties(tHostThing, MyBaseThing);

            int cntTags = 0;
            int previousTagCount = this.LiveTagCnt;
            Subscription subscription = null;
            string errorText = "";

            foreach (TheOPCSensorSubscription tPropSubscription in tagPro)
            {
                try
                {
                    string parent = "";
                    //string[] tParts = tProp.Name.Split('.');
                    //if (tParts.Length > 1)
                    //    parent = tProp.Name.Substring(0, tProp.Name.Length - (tParts[tParts.Length - 1].Length + 1));
                    //else
                    //    parent = "";

                    //if (serverId == this.MyBaseThing.ID)
                    {
                        if (!bPopulateSubscribedTagsOnly && subscription == null)
                        {
                            subscription = GetOrCreateSubscription(tPropSubscription.SampleRate ?? 0);
                            if (subscription == null)
                            {
                                errorText = "Not connected or failed to subscribe";
                                break;
                            }
                        }
                        string error;
                        if (tPropSubscription.EventInfo == null && tPropSubscription.MethodInfo == null)
                        {
                            var tTag = new TheOPCTag
                            {
                                MyOPCServer = this,
                                DisplayName = tPropSubscription.TargetProperty,
                                Parent = parent,
                                NodeIdName = tPropSubscription.SensorId,
                                SampleRate = tPropSubscription.SampleRate ?? 0,
                                ChangeTrigger = tPropSubscription.ChangeTrigger ?? 1,
                                DeadbandFilterValue = tPropSubscription.DeadbandValue ?? 0,
                                StructTypeInfo = tPropSubscription.StructTypeInfo,
                                HostThingMID = hostThingMid,
                                HistoryStartTime = tPropSubscription.HistoryStartTime,
                            };

                            var existingSubscription = MyTagSubscriptions.TheValues.FirstOrDefault(t => t.GetTargetPropertyName() == tPropSubscription.TargetProperty);
                            if (existingSubscription != null)
                            {
                                tTag.DisplayName = existingSubscription.DisplayName;
                                tTag.Parent = existingSubscription.Parent;
                                tTag.HostPropertyNameOverride = existingSubscription.HostPropertyNameOverride;
                            }
                            else
                            {
                                var existingBrowsedTag = MyTags.TheValues.FirstOrDefault(t => t.GetTargetPropertyName() == tPropSubscription.TargetProperty);
                                if (existingBrowsedTag != null)
                                {
                                    tTag.DisplayName = existingBrowsedTag.DisplayName;
                                    tTag.Parent = existingBrowsedTag.Parent;
                                    tTag.HostPropertyNameOverride = existingBrowsedTag.HostPropertyNameOverride;
                                }

                            }

                            if (RegisterAndMonitorTagInHostThing(subscription, tTag, out error, false, true, bPopulateSubscribedTagsOnly))
                            {
                                // TODO log or count errors!
                                cntTags++;
                            }
                        }
                        else if (tPropSubscription.EventInfo != null)
                        { 
                                var tEvent = new TheOPCEvent
                                {
                                    MyOPCServer = this,
                                    DisplayName = tPropSubscription.TargetProperty,
                                    Parent = parent,
                                    NodeIdName = tPropSubscription.SensorId,
                                    SampleRate = tPropSubscription.SampleRate ?? 0,
                                    HostThingMID = hostThingMid,
                                    EventInfo = tPropSubscription.EventInfo,
                                };

                                if (RegisterAndMonitorTagInHostThing(subscription, tEvent, out error, false, false, bPopulateSubscribedTagsOnly) && !bPopulateSubscribedTagsOnly)
                                {
                                    // TODO log or count errors!
                                    cntTags++;
                                }
                        }
                        if (cntTags > 5000)
                        {
                            // Flush every 5000 tags or we'll exceed maximum message sizes
                            if (subscription != null)
                                subscription.ApplyChanges();

                            previousTagCount = previousTagCount + cntTags;
                            LiveTagCnt = previousTagCount;
                            cntTags = 0;
                        }
                        if (cntTags % 50 == 0)
                        {
                            LiveTagCnt = previousTagCount + cntTags;
                        }
                    }

                }
                catch (Exception e)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(78109, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(MyBaseThing.EngineName, String.Format("[{0}]  Property Subscription failed for property {1}", this.GetLogAddress(), tPropSubscription.TargetProperty), eMsgLevel.l1_Error, e.ToString()));
                }
            }
            LiveTagCnt = previousTagCount + cntTags;
            if (subscription != null)
            {
#if OLD_UA
                            var items = subscription.ApplyChanges();
                            foreach (var item in items)
#else
                subscription.ApplyChanges();
                foreach (var item in subscription.MonitoredItems)
#endif
                {
                    if (item.Status.Error != null)
                    {
                        errorText += $"{item.StartNodeId}:{item.Status.Error.ToLongString()},";
                    }
                }

                try
                {
                    TheOPCEvent.ConditionRefresh(subscription, this);
                }
                catch (Exception e)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(78102, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, $"Internal rrror requesting condition request for {this.GetLogAddress()}", eMsgLevel.l1_Error, TSM.L(eDEBUG_LEVELS.VERBOSE) ? e.Message : e.ToString()));
                }
            }
            if (!string.IsNullOrEmpty(errorText))
            {
                LastMessage = $"{DateTimeOffset.Now}: Errors while subscribing: {errorText}";
                TheBaseAssets.MySYSLOG.WriteToLog(78109, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, String.Format("[{0}]  Property Subscription failed for one or more tags", this.GetLogAddress()), eMsgLevel.l1_Error, errorText));
            }
        }

        private void UnsubscribeTagsInHostThing(string hostThingMid)
        {
            TheThing tHostThing = TheThingRegistry.GetThingByMID("*", TheCommonUtils.CGuid(hostThingMid));
            if (tHostThing == null) return;

            string prefix = "OPCUA";
            List<cdeP> tagPro = tHostThing.GetPropertiesMetaStartingWith(prefix);

            int previousTagCount = this.LiveTagCnt;

            foreach (cdeP tProp in tagPro)
            {
                string nodeIdName;
                string serverId;
                string propKind;
                int changeTrigger;
                double deadBandFilterValue;
                string structTypeInfo;
                TheEventSubscription eventFilter;

                TheOPCMonitoredItemBase.ParseOpcUaMetaString(tProp.cdeM, out propKind, out nodeIdName, out serverId, out changeTrigger, out deadBandFilterValue, out structTypeInfo, out DateTimeOffset? historyStartTime, out eventFilter);

                if (serverId == this.MyBaseThing.ID)
                {
                    tHostThing.GetBaseThing().RemoveProperty(tProp.Name);
                }

            }

        }

        /// <summary>
        /// Disconnects from the server.
        /// </summary>
        public string Disconnect(bool bDontFireDisconnectEvent, bool logAsError, string disconnectReason)
        {
            ConnectionStateEnum previousState;
            lock (ConnectionStateLock)
            {
                previousState = ConnectionState;
                if (previousState == ConnectionStateEnum.Disconnecting)
                {
                    return "Disconnect pending";
                }
                if (previousState == ConnectionStateEnum.Disconnected)
                {
                    return "";
                }
                if (previousState == ConnectionStateEnum.Connecting)
                {
                    return "Connect pending"; // TODO: Should we force the disconnect somehow in this state? Saw (seemingly) permanent connecting state on docker devicegate after laptop sleep
                }
                if (previousState == ConnectionStateEnum.Reconnecting || previousState == ConnectionStateEnum.Connected)
                {
                    ConnectionState = ConnectionStateEnum.Disconnecting;
                }
            }
            if (ConnectionState != ConnectionStateEnum.Disconnecting)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(78101, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, $"[{GetLogAddress()}] Disconnect: Invalid connection state {previousState}", eMsgLevel.l1_Error, ""));
                return "Internal error: invalid connection state"; // Someone extended the connection state enum and didn't update these checks
            }
            
            try
            {
                var lastMessageBeforeDisconnect = string.IsNullOrEmpty(disconnectReason) ? LastMessage :disconnectReason;
                LastMessage = "Disconnecting at " + DateTime.Now.ToString();

                DoDisconnectInternal();

                ConnectionState = ConnectionStateEnum.Disconnected;

                LastMessage = "Disconnected at " + DateTimeOffset.Now.ToString();
                if (!bDontFireDisconnectEvent)
                {
                    // raise an event.
                    MyBaseThing.FireEvent("DisconnectComplete", this, lastMessageBeforeDisconnect, true);
                    TheBaseAssets.MySYSLOG.WriteToLog(78004, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, String.Format("[{0}] Disconnected from server", GetLogAddress()), logAsError ? eMsgLevel.l1_Error : eMsgLevel.l3_ImportantMessage, ""));
                }
                MyBaseThing.StatusLevel = 0;
                return "";
            }
            catch (Exception e)
            {
                LastMessage = "Error disconnecting at " + DateTimeOffset.Now.ToString() + ": " + e.Message;
                TheBaseAssets.MySYSLOG.WriteToLog(78005, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, String.Format("[{0}] Error disconnecting from server", GetLogAddress()), eMsgLevel.l3_ImportantMessage, e.ToString()));
                MyBaseThing.StatusLevel = 3;
                return "Error disconnecting: " + e.Message;
            }
            finally
            {
                if (ConnectionState == ConnectionStateEnum.Disconnecting)
                {
                    ConnectionState = ConnectionStateEnum.Disconnected;
                }
            }
        }

        void DoDisconnectInternal()
        {
            var subscription = GetSubscription();
            if (subscription != null)
            {
                try
                {
                    subscription.Delete(false);
                }
                catch (Opc.Ua.ServiceResultException se)
                {
                    if (se.StatusCode != Opc.Ua.StatusCodes.BadConnectionClosed
                        && se.StatusCode != Opc.Ua.StatusCodes.BadNotConnected
                        && se.StatusCode != Opc.Ua.StatusCodes.BadSessionIdInvalid
                        && (se.StatusCode != Opc.Ua.StatusCodes.BadUnexpectedError && !(se.InnerException is ObjectDisposedException))
                        && (se.StatusCode != 0x80af0000)
                        )
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(78110, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, String.Format("[{0}] Unexpected exception from server. Deleting subscription on disconnect ", GetLogAddress()), eMsgLevel.l1_Error, se.ToString()));
                        //throw;
                    }
                }
                try
                {
                    subscription.Dispose();
                }
                catch { }
            }

            var session = m_session;
            if (session != null)
            {
                try
                {
                    var status = session.Close();
                }
                catch (Exception e)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(78110, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, String.Format("[{0}] Exception while closing session on disconnect: Possible session leak.", GetLogAddress()), eMsgLevel.l1_Error, e.ToString()));
                }
                try
                {
                    session.Dispose();
                }
                catch (Exception e)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(78110, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, String.Format("[{0}] Exception while disposing session on disconnect:  Possible session leak.", GetLogAddress()), eMsgLevel.l1_Error, e.ToString()));
                }
                this.m_session = null;
            }

            // stop any reconnect operation.
            lock (m_reconnectHandlerLock)
            {
                if (m_reconnectHandler != null)
                {
                    try
                    {
                        m_reconnectHandler.Dispose();
                    }
                    catch { }
                    finally
                    {
                        m_reconnectHandler = null;
                    }
                }
            }
            currentRoot = null;
        }

        /// <summary>
        /// Handles a keep alive event from a session.
        /// </summary>
        private void Session_KeepAlive(Session session, KeepAliveEventArgs e)
        {
            try
            {
                // check for events from discarded sessions.
                if (!Object.ReferenceEquals(session, m_session))
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(78006, TSM.L((ConnectionState == ConnectionStateEnum.Disconnecting)? eDEBUG_LEVELS.VERBOSE : eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, String.Format("[{0}] Received keep alive for old session.", GetLogAddress()), eMsgLevel.l4_Message));
                    return;
                }

                // start reconnect sequence on communication error.
                if (ServiceResult.IsBad(e.Status) || e.CurrentState == ServerState.CommunicationFault || e.CurrentState == ServerState.Failed)
                {
                    MyBaseThing.StatusLevel = 3;
                    var message = string.Format("Communication Error ({0}-{1})", e.CurrentState, e.Status);
                    if (ReconnectPeriod <= 0)
                    {
                        lock (m_reconnectHandlerLock)
                        {
                            try
                            {
                                if (m_reconnectHandler != null)
                                {
                                    m_reconnectHandler.Dispose();
                                    m_reconnectHandler = null;
                                }
                            }
                            catch { }
                        }
                        TheBaseAssets.MySYSLOG.WriteToLog(78006, TSM.L((ConnectionState == ConnectionStateEnum.Disconnecting) ? eDEBUG_LEVELS.VERBOSE : eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, String.Format("[{0}] Communication Error for OPC Server. Disconnecting.", GetLogAddress()), eMsgLevel.l1_Error, message));
                        LastMessage = DateTimeOffset.Now + "  " + message;
                        Disconnect(false, true, message); // IsConnected = false;
                        return;
                    }

                    lock (m_reconnectHandlerLock)
                    {
                        if (m_reconnectHandler == null)
                        {
                            lock (ConnectionStateLock)
                            {
                                if (ConnectionState != ConnectionStateEnum.Disconnecting && ConnectionState != ConnectionStateEnum.Disconnected)
                                {
                                    ConnectionState = ConnectionStateEnum.Reconnecting;
                                }
                            }

                            if (ConnectionState == ConnectionStateEnum.Reconnecting)
                            {
                                string fullMessage;
                                if (ReconnectCount > 0)
                                {
                                    fullMessage = $"{message}: Reconnecting in {ReconnectPeriod} ms for {ReconnectCount} attempts";
                                }
                                else
                                {
                                    fullMessage = $"{message}: Reconnecting every {ReconnectPeriod} ms";
                                }
                                TheBaseAssets.MySYSLOG.WriteToLog(78007, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, $"[{GetLogAddress()}] {fullMessage}", eMsgLevel.l2_Warning, message));
                                LastMessage = $"{DateTimeOffset.Now} {fullMessage}";

                                //if (m_ReconnectStarting != null)
                                //  m_ReconnectStarting(this, e);
                                MyBaseThing.StatusLevel = 2;
                                m_reconnectHandler = new SessionReconnectHandler();
                                m_reconnectHandler.BeginReconnect(m_session, ReconnectPeriod, Server_ReconnectComplete); //CM: , ReconnectCount);
                            }
                            else
                            {
                                var fullMessage = $"{message}: Disconnected or disconnecting: Not starting reconnect timer.";
                                TheBaseAssets.MySYSLOG.WriteToLog(78007, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(MyBaseThing.EngineName, $"[{GetLogAddress()}] {fullMessage}", eMsgLevel.l4_Message, message));
                            }
                        }
                        else
                        {
                            var fullMessage = $"{message}: Reconnect already in progress. Not starting another reconnect timer.";
                            TheBaseAssets.MySYSLOG.WriteToLog(78007, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, $"[{GetLogAddress()}] {fullMessage}", eMsgLevel.l2_Warning, message));
                        }
                    }

                    return;
                }
                else
                {
                    lock (m_reconnectHandlerLock)
                    {
                        try
                        {
                            if (m_reconnectHandler != null)
                            {
                                TheBaseAssets.MySYSLOG.WriteToLog(78111, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, "Stopping reconnect due to keepalive", eMsgLevel.l3_ImportantMessage));
                                m_reconnectHandler.Dispose();
                                m_reconnectHandler = null;
                            }
                        }
                        catch { }
                        lock (ConnectionStateLock)
                        {
                            if (ConnectionState == ConnectionStateEnum.Reconnecting)
                            {
                                ConnectionState = ConnectionStateEnum.Connected;
                            }
                        }
                    }
                    // update status.
                }

                // raise any additional notifications.
                //if (m_KeepAliveComplete != null)
                //  m_KeepAliveComplete(this, e);
            }
            catch (Exception exception)
            {
                var message = $"Reconnect/Keepalive processing failed with internal error: {exception.Message}. Forcing disconnect/reconnect.";
                LastMessage = DateTimeOffset.Now + ": " + message;
                TheBaseAssets.MySYSLOG.WriteToLog(78111, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, $"[{GetLogAddress()}] {message}", eMsgLevel.l1_Error, exception.ToString()));
                if (ConnectionState == ConnectionStateEnum.Connected)
                {
                    Disconnect(false, true, message);
                    Connect(false);
                }
            }
        }

        /// <summary>
        /// Handles a reconnect event complete from the reconnect handler.
        /// </summary>
        private void Server_ReconnectComplete(object sender, EventArgs e) //CM: SessionReconnectEventArgs e)
        {
            try
            {
                //SessionReconnectEventArgs e = ee as SessionReconnectEventArgs;
                // ignore callbacks from discarded objects.
                if (!Object.ReferenceEquals(sender, m_reconnectHandler))
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(78111, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, "Ignoring reconnect notification for previous handler", eMsgLevel.l4_Message));
                    return;
                }

                lock (m_reconnectHandlerLock)
                {
                    var newSession = m_reconnectHandler.Session;
                    if (e != null && m_session != newSession) //&& e.Success 
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(78111, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(MyBaseThing.EngineName, "Received new session on reconnect", eMsgLevel.l4_Message));
                        m_session = newSession;
                    }
                    try
                    {
                        m_reconnectHandler.Dispose();
                    }
                    catch { }
                    m_reconnectHandler = null;
                    lock (ConnectionStateLock)
                    {
                        if (ConnectionState == ConnectionStateEnum.Reconnecting)
                        {
                            ConnectionState = ConnectionStateEnum.Connected;
                        }
                    }
                }

                //CM: Does no longer exists in OPC UA Nugets
                //if (e != null && !e.Success)
                //{
                //    var message = $"Failed to Reconnect: {e?.Exception}";
                //    LastMessage = DateTimeOffset.Now + ": " + message;
                //    TheBaseAssets.MySYSLOG.WriteToLog(78008, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, $"[{GetLogAddress()}] Failed to Reconnect: {e?.Exception}", eMsgLevel.l1_Error, message));
                //    Disconnect(false, true, message);
                //}
                //else
                {
                    MyBaseThing.StatusLevel = 1;
                    var message = "Reconnected.";
                    LastMessage = DateTimeOffset.Now + ": " + message;
                    TheBaseAssets.MySYSLOG.WriteToLog(78009, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, String.Format("[{0}] Reconnected", GetLogAddress()), eMsgLevel.l3_ImportantMessage, message));
                    MyBaseThing.FireEvent("ConnectComplete", this, null, true);
                }
                // raise any additional notifications.
                //if (m_ReconnectComplete != null)
                //  m_ReconnectComplete(this, e);
            }
            catch (Exception exception)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(78112, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, String.Format("[{0}] Reconnect Failed - internal error", GetLogAddress()), eMsgLevel.l1_Error, exception.ToString()));
                lock (m_reconnectHandlerLock)
                {
                    try
                    {
                        if (m_reconnectHandler != null)
                        {
                            m_reconnectHandler.Dispose();
                        }
                    }
                    catch { }
                    m_reconnectHandler = new SessionReconnectHandler();
                    m_reconnectHandler.BeginReconnect(m_session, ReconnectPeriod, Server_ReconnectComplete); //CM: , ReconnectCount);
                }
            }
        }

        /// <summary>
        /// Handles a certificate validation error.
        /// </summary>
        private void CertificateValidator_CertificateValidation(CertificateValidator sender, CertificateValidationEventArgs e)
        {
            try
            {
                e.Accept = m_configuration.SecurityConfiguration.AutoAcceptUntrustedCertificates;

                if (!m_configuration.SecurityConfiguration.AutoAcceptUntrustedCertificates)
                    e.Accept = AcceptUntrustedCertificate;
            }
            catch (Exception exception)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(78113, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, String.Format("[{0}] Certificate Verification failed", GetLogAddress()), eMsgLevel.l1_Error, exception.ToString()));
            }
        }

        bool IsBrowsing = false;
        internal bool Browser(ReferenceDescription reference, string pParent, bool IsInitialRun, bool SubscribeToAll, TheThing multiTagHostThing, List<string> pFilterIDs=null)
        {
            if (IsBrowsing) return false;
            IsBrowsing = true;
            if (IsInitialRun) FlushStores();

            try
            {
                DoBrowser(null, reference, pParent, IsInitialRun, SubscribeToAll, multiTagHostThing, pFilterIDs);

                var eventTypeNodes = BrowseAllSubTypes(ObjectTypeIds.BaseEventType);

                foreach(var eventTypeNode in eventTypeNodes)
                {
                    NodeId eventTypeNodeId = (NodeId)eventTypeNode.NodeId;
                    if (MyEvents.MyMirrorCache.GetEntriesByFunc( e => e.GetResolvedNodeIdName() == eventTypeNodeId).Count() == 0)
                    {
                        MyEvents.AddAnItem(new TheOPCEvent
                        {
                            MyOPCServer = this,
                            DisplayName = eventTypeNode.DisplayName.ToString(),
                            IsSubscribedAsThing = false,
                            NodeIdName = eventTypeNode.NodeId.ToString(),
                            SampleRate = 0,
                        });
                    }
                }
            }
            catch (Exception e)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(78114, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, String.Format("[{0}]  Browsing Failed", GetLogAddress()), eMsgLevel.l1_Error, e.ToString()));
                IsBrowsing = false;
                return false;
            }
            IsBrowsing = false;
            return true;
        }

        private void DoBrowser(Subscription subscription, ReferenceDescription pRootNode, string pParent, bool IsInitialRun, bool SubscribeToAll, TheThing tMultiTagHostThing, List<string> pFilterIDs)
        {
            try
            {
                // build list of references to browse.
                BrowseDescriptionCollection nodesToBrowse = new BrowseDescriptionCollection();
                for (int ii = 0; ii < m_referenceTypeIds.Length; ii++)
                {
                    BrowseDescription nodeToBrowse = new BrowseDescription();
                    if (pRootNode != null)
                        nodeToBrowse.NodeId = (NodeId)pRootNode.NodeId;
                    else
                        nodeToBrowse.NodeId = m_rootId;
                    nodeToBrowse.BrowseDirection = BrowseDirection.Forward;
                    nodeToBrowse.ReferenceTypeId = m_referenceTypeIds[ii];
                    nodeToBrowse.IncludeSubtypes = true;
                    nodeToBrowse.NodeClassMask = 0; // 2; //was 0
                    nodeToBrowse.ResultMask = (uint)BrowseResultMask.All;
                    nodesToBrowse.Add(nodeToBrowse);
                }

                // add the childen to the control.
                SortedDictionary<ExpandedNodeId, ReferenceDescription> referencesByNodeId = new SortedDictionary<ExpandedNodeId, ReferenceDescription>();

                ReferenceDescriptionCollection references;
                if (m_session != null)
                {
                    references = ClientUtils.Browse(m_session, nodesToBrowse, true);
                }
                else
                {
                    throw new Exception("Session not created or closed.");
                }
                for (int ii = 0; references != null && ii < references.Count; ii++)
                {
                    ReferenceDescription reference = references[ii];
                    // ignore out of server references.
                    if (reference.NodeId.IsAbsolute)
                        continue;

                    if (referencesByNodeId.ContainsKey(reference.NodeId))
                        continue;
                    referencesByNodeId[reference.NodeId] = reference;
                }

                // add nodes to tree.
                int liveTags = 0;
                int currentLiveTags = this.LiveTagCnt;
                int browsedTags = 0;
                string multiTagHostThingMID = null;
                if (tMultiTagHostThing != null)
                {
                    multiTagHostThingMID = TheCommonUtils.cdeGuidToString(tMultiTagHostThing.cdeMID);
                }

                foreach (ReferenceDescription node in referencesByNodeId.Values.OrderBy(i => i.ToString()))
                {
                    switch (node.NodeClass)
                    {
                        case NodeClass.Method:
                            if (!string.IsNullOrEmpty(BrowseBranch) && !pParent.StartsWith(BrowseBranch)) continue;
                            if (!IsInitialRun && !MyMethods.MyMirrorCache.ContainsByFunc(s => s.TagRef == node.NodeId && s.ParentId == (NodeId)pRootNode.NodeId)) continue;
                            TheOPCMethod tMethod = new TheOPCMethod();
                            tMethod.TagRef = (NodeId)node.NodeId;
                            tMethod.DisplayName = node.DisplayName.ToString();
                            tMethod.ParentId = (NodeId)pRootNode.NodeId;
                            tMethod.OPCServerID = MyBaseThing.ID;
                            tMethod.MyOPCServer = this;
                            if (TheThingRegistry.HasThingsWithFunc(MyBaseEngine.GetEngineName(), s => TheThing.GetSafePropertyString(s, "ServerID") == MyBaseThing.ID && s.DeviceType == eOPCDeviceTypes.OPCMethod && s.Address == string.Format("{0}", node.NodeId)))
                                tMethod.IsSubscribed = true;
                            var browseError = MethodBrowser((NodeId)node.NodeId, node.DisplayName.ToString(), tMethod);
                            if (!String.IsNullOrEmpty(browseError))
                            {
                                // TODO How to handle missing method metadata?
                                // For now: tMethod.Arg == null and will be handled later on use
                            }
                            MyMethods.AddAnItem(tMethod);
                            break;
                        case NodeClass.Variable:
                            if (!node.NodeId.IsAbsolute)
                            {
                                if (!string.IsNullOrEmpty(BrowseBranch)
                                    && !pParent.StartsWith(BrowseBranch, StringComparison.InvariantCultureIgnoreCase)
                                    && !(pParent+"."+node.ToString()).StartsWith(BrowseBranch, StringComparison.InvariantCultureIgnoreCase)
                                    ) continue;

                                if (IsInitialRun || !MyTags.MyMirrorCache.ContainsByFunc(s => s.GetResolvedNodeIdName() == node.NodeId && s.Parent == pParent))
                                {
                                    if (pParent.StartsWith("Objects.Server"))
                                    {
                                        try
                                        {
                                            if (pParent.Length > "Objects.Server".Length)
                                            {
                                                List<TheOPCProperties> tList = ReadAttributes((NodeId)node.NodeId, true);
                                                TheOPCProperties tProp = tList.Find(s => s.BrowseName == "Value");
                                                if (tProp != null)
                                                {
                                                    string tPropName = pParent.Substring("Objects.Server".Length) + "." + node.DisplayName.ToString();
                                                    TheThing.SetSafePropertyString(MyBaseThing, tPropName, tProp.Value.WrappedValue.ToString());
                                                    //TheBaseAssets.MySYSLOG.WriteToLog(78115, new TSM(MyBaseThing.EngineName, string.Format("Server Node Found {0}={1}", tPropName, tProp.Value.WrappedValue)));
                                                }
                                            }
                                        }
                                        catch (Exception e)
                                        {
                                            TheBaseAssets.MySYSLOG.WriteToLog(78116, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(MyBaseThing.EngineName, String.Format("[{0}] Error browsing Server node. Continuing.", GetLogAddress()), eMsgLevel.l1_Error, e.ToString()));
                                        }
                                    }
                                    else
                                    {
                                        MyTagSubscriptions.MyRecordsRWLock.RunUnderUpgradeableReadLock(() =>
                                        {
                                            TheOPCTag pTag;
                                            var tagRef = (NodeId)node.NodeId;
                                            var parent = DoNotUseParentPath ? "" : pParent;
                                            pTag = MyTagSubscriptions.MyMirrorCache.GetEntryByFunc(t => t.GetResolvedNodeIdName() == tagRef);
                                            if (pTag != null)
                                            {
                                                // If already subscribed to this tag, use that tag and update it if necessary
                                                if (String.IsNullOrEmpty(pTag.Parent))
                                                {
                                                    pTag.Parent = parent;
                                                }
                                                if (String.IsNullOrEmpty(pTag.BrowseName))
                                                {
                                                    pTag.BrowseName = node.BrowseName.ToString();
                                                }
                                                if (string.IsNullOrEmpty(pTag.DisplayName))
                                                {
                                                    pTag.DisplayName = node.DisplayName.ToString();
                                                }
                                            }
                                            else
                                            {
                                                pTag = new TheOPCTag
                                                {
                                                    DisplayName = node.DisplayName.ToString(),
                                                    NodeIdName = node.NodeId.ToString(),
                                                    Parent = parent,
                                                    SampleRate = DefSampleRate,
                                                    TypeDefinition = node.TypeDefinition.ToString(),
                                                    BrowseName = node.BrowseName.ToString(),
                                                    MyOPCServer = this
                                                };
                                                if (node.ReferenceTypeId == Opc.Ua.ReferenceTypeIds.HasProperty && !DoNotUsePropsOfProps)
                                                {
                                                    // Make it a property of properties if it's a child property
                                                    pTag.HostPropertyNameOverride = $"[{parent}].[{pTag.DisplayName}]";
                                                    var parentTag = MyTags.MyMirrorCache.GetEntryByFunc(t => $"{t.Parent}.{t.DisplayName}" == parent);
                                                    if (parentTag != null)
                                                    {
                                                        parentTag.AddChildTag(pTag);
                                                    }
                                                }
                                            }
                                            if (SubscribeToAll && pFilterIDs != null && !pFilterIDs.Contains($"{parent}.{node.DisplayName.ToString()}"))
                                                return;
                                            try
                                            {
                                                //pTag.PropAttr = ReadAttributes((NodeId)node.NodeId, true);
                                            }
                                            catch (Exception eee)
                                            {
                                                TheBaseAssets.MySYSLOG.WriteToLog(78117, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, String.Format("[{0}] ReadAttributes Failed", GetLogAddress()), eMsgLevel.l2_Warning, eee.ToString()));
                                            }

                                            if (tMultiTagHostThing != null)
                                            {
                                                if (pTag.IsSubscribedAsPropertyInThing(tMultiTagHostThing))
                                                {
                                                    pTag.HostThingMID = multiTagHostThingMID;
                                                }
                                                else if (SubscribeToAll)
                                                {
                                                    if (subscription == null)
                                                    {
                                                        subscription = GetOrCreateSubscription(pTag.SampleRate);
                                                    }
                                                    pTag.HostThingMID = multiTagHostThingMID;
                                                    if (subscription != null)
                                                    {
                                                        string error;
                                                        if (RegisterAndMonitorTagInHostThing(subscription, pTag, out error, false, false, false))
                                                        {
                                                            // TODO Handle error
                                                            liveTags++;
                                                        }
                                                    }
                                                    else
                                                    {
                                                        // TODO Handle/report error
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                if (TheThingRegistry.HasThingsWithFunc(MyBaseEngine.GetEngineName(), s => TheThing.GetSafePropertyString(s, "ServerID") == MyBaseThing.ID && s.DeviceType == eOPCDeviceTypes.OPCLiveTag && s.Address == string.Format("{0}:;:{1}", pParent, node.NodeId)))
                                                {
                                                    pTag.IsSubscribedAsThing = true;
                                                }
                                                else
                                                {
                                                    if (SubscribeToAll)
                                                    {
                                                        if (subscription == null)
                                                        {
                                                            subscription = GetOrCreateSubscription(pTag.SampleRate);
                                                        }
                                                        if (subscription != null)
                                                        {
                                                            pTag.IsSubscribedAsThing = true;
                                                            RegisterAndMonitorTagThing(subscription, pTag, null, null, false);
                                                            liveTags++;
                                                        }
                                                        else
                                                        {
                                                            // TODO Report/handle error
                                                        }
                                                    }
                                                }
                                            }
                                            MyTags.UnregisterEvent(eStoreEvents.HasUpdates, OnTagThingUpdate);
                                            MyTags.AddAnItem(pTag);
                                            MyTags.RegisterEvent(eStoreEvents.HasUpdates, OnTagThingUpdate);

                                            if (liveTags > 5000 && subscription != null)
                                            {
                                                // Flush every 5000 tags or we'll exceed maximum message sizes
                                                subscription.ApplyChanges(); // TODO Report any errors
                                                currentLiveTags = currentLiveTags + liveTags;
                                                LiveTagCnt = currentLiveTags;
                                                liveTags = 0;
                                            }

                                            if (liveTags % 50 == 0)
                                            {
                                                LiveTagCnt = currentLiveTags + liveTags;
                                            }

                                            browsedTags++;
                                            if (browsedTags > 50)
                                            {
                                                BrowsedTagCnt += browsedTags;
                                                browsedTags = 0;
                                            }
                                        });
                                    }
                                }
                                else
                                {
                                    TheBaseAssets.MySYSLOG.WriteToLog(78118, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(MyBaseThing.EngineName, String.Format("[{0}] Duplicate Node Found: {1}", GetLogAddress(), node.DisplayName.ToString())));
                                }
                            }
                            // Continue browsing for further nodes/properties under the variable node
                            DoBrowser(subscription, node, pParent + "." + node.ToString(), IsInitialRun, SubscribeToAll, tMultiTagHostThing, pFilterIDs);
                            break;
                        default:
                            DoBrowser(subscription, node, pParent + "." + node.ToString(), IsInitialRun, SubscribeToAll, tMultiTagHostThing, pFilterIDs);
                            break;
                    }
                }
                LiveTagCnt = currentLiveTags + liveTags;
                BrowsedTagCnt += browsedTags;

                if (SubscribeToAll && subscription != null)
                {
                    subscription.ApplyChanges(); // TODO Report any errors
                }
            }
            catch (Exception exception)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(78119, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, String.Format("[{0}] Browser Exception", GetLogAddress()), eMsgLevel.l1_Error, exception.ToString()));
            }
        }

        IList<ReferenceDescription> BrowseAllSubTypes(NodeId nodeId)
        {
            var allSubTypes = new List<ReferenceDescription>();
            BrowseDescription typeNodeToBrowse = new BrowseDescription();
            typeNodeToBrowse.NodeId = nodeId;
            typeNodeToBrowse.BrowseDirection = BrowseDirection.Forward;
            typeNodeToBrowse.ReferenceTypeId = ReferenceTypeIds.HasSubtype;
            typeNodeToBrowse.IncludeSubtypes = true;
            typeNodeToBrowse.NodeClassMask = (int)NodeClass.ObjectType;
            typeNodeToBrowse.ResultMask = (uint)BrowseResultMask.All;

            ReferenceDescriptionCollection eventTypes = ClientUtils.Browse(m_session, typeNodeToBrowse, true);
            //var eventTypes = m_session.TypeTree.FindSubTypes(node);
            //var eventTypes = ClientUtils.CollectInstanceDeclarationsForType(m_session, node);

            foreach (var eventType in eventTypes)
            {
                allSubTypes.Add(eventType);
                allSubTypes.AddRange(BrowseAllSubTypes((NodeId)eventType.NodeId));
            }
            return allSubTypes;
        }


        public string MethodBrowser(NodeId pNodeID,string pParentDisplayName, TheOPCMethod pMethod)
        {
            if (pNodeID == null) return "ERROR: no nodeid specified";
            try
            {
                // build list of references to browse.
                BrowseDescriptionCollection nodesToBrowse = new BrowseDescriptionCollection();
                for (int ii = 0; ii < m_referenceTypeIds.Length; ii++)
                {
                    BrowseDescription nodeToBrowse = new BrowseDescription();
                    nodeToBrowse.NodeId = pNodeID;
                    nodeToBrowse.BrowseDirection = BrowseDirection.Forward;
                    nodeToBrowse.ReferenceTypeId = m_referenceTypeIds[ii];
                    nodeToBrowse.IncludeSubtypes = true;
                    nodeToBrowse.NodeClassMask = 0; // 2; //was 0
                    nodeToBrowse.ResultMask = (uint)BrowseResultMask.All;
                    nodesToBrowse.Add(nodeToBrowse);
                }

                // add the childen to the control.
                SortedDictionary<ExpandedNodeId, ReferenceDescription> dictionary = new SortedDictionary<ExpandedNodeId, ReferenceDescription>();

                ReferenceDescriptionCollection references = ClientUtils.Browse(m_session, nodesToBrowse, false);
                if (references == null)
                {
                    return "ERROR: Unable to read method metadata";
                }

                for (int ii = 0; references != null && ii < references.Count; ii++)
                {
                    ReferenceDescription reference = references[ii];
                    // ignore out of server references.
                    if (reference.NodeId.IsAbsolute)
                        continue;

                    if (dictionary.ContainsKey(reference.NodeId))
                        continue;
                    dictionary[reference.NodeId] = reference;
                }

                var methodArgs = new List<TheOPCTag>();
                foreach (ReferenceDescription node in dictionary.Values.OrderBy(i => i.ToString()))
                {
                    switch (node.NodeClass)
                    {
                        case NodeClass.Variable:
                            {
                                TheOPCTag pTag = new TheOPCTag()
                                {
                                    DisplayName = node.DisplayName.ToString(),
                                    //TagRef = node,
                                    NodeIdName = node.NodeId.ToString(),
                                    Parent = pParentDisplayName,
                                    SampleRate = DefSampleRate,
                                    TypeDefinition = node.TypeDefinition.ToString(),
                                    BrowseName = node.BrowseName.ToString(),
                                    MyOPCServer = this
                                };
                                try
                                {
                                    pTag.PropAttr = ReadAttributes((NodeId)node.NodeId, false, true);
                                }
                                catch (Exception eee)
                                {
                                    TheBaseAssets.MySYSLOG.WriteToLog(78120, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, String.Format("[{0}] ReadAttributes Failed", GetLogAddress()), eMsgLevel.l2_Warning, String.Format("Node: {0} Error: {1}", node.NodeId, eee.ToString())));
                                    return "ERROR: ReadAttributes failed: " + eee.Message;
                                }
                                methodArgs.Add(pTag);
                            }
                            break;
                    }
                }
                lock (pMethod)
                {
                    if (methodArgs.Count != 0)
                    {
                        pMethod.Args = methodArgs;
                    }
                    else
                    {
                        return "ERROR: No methods arguments found";
                    }
                }
                return null;
            }
            catch (Exception exception)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(78121, new TSM(MyBaseThing.EngineName, String.Format("[{0}] Browser Exception", GetLogAddress()), eMsgLevel.l1_Error, exception.ToString()));
                pMethod.Args = null;
                return "ERROR: Failure browsing Method meta data: " + exception.Message;
            }
        }


        /// <summary>
        /// Reads the attributes for the node.
        /// </summary>
        public List<TheOPCProperties> ReadAttributes(NodeId nodeId, bool showProperties, bool IsMethodArgs=false)
        {
            List<TheOPCProperties> pTagPropAttr = new List<TheOPCProperties>();

            if (NodeId.IsNull(nodeId))
            {
                return pTagPropAttr;
            }

            // build list of attributes to read.
            ReadValueIdCollection nodesToRead = new ReadValueIdCollection();

            foreach (uint attributeId in Attributes.GetIdentifiers())
            {
                ReadValueId nodeToRead = new ReadValueId();
                nodeToRead.NodeId = nodeId;
                nodeToRead.AttributeId = attributeId;
                nodesToRead.Add(nodeToRead);
            }

            // read the attributes.
            DataValueCollection results = null;
            DiagnosticInfoCollection diagnosticInfos = null;

            m_session.Read(
                null,
                0,
                TimestampsToReturn.Neither,
                nodesToRead,
                out results,
                out diagnosticInfos);

            ClientBase.ValidateResponse(results, nodesToRead);
            ClientBase.ValidateDiagnosticInfos(diagnosticInfos, nodesToRead);

            // add the results to the display.
            for (int ii = 0; ii < results.Count; ii++)
            {
                // check for error.
                if (StatusCode.IsBad(results[ii].StatusCode))
                {
                    if (results[ii].StatusCode == StatusCodes.BadAttributeIdInvalid)
                    {
                        continue;
                    }
                }

                TheOPCProperties tAtt = new TheOPCProperties();
                // add the metadata for the attribute.
                uint attributeId = nodesToRead[ii].AttributeId;
                tAtt.BrowseName = Attributes.GetBrowseName(attributeId);
                tAtt.DataType = TheOPCTag.GetCDETypeFromOPCType(Attributes.GetBuiltInType(attributeId));

                if (Attributes.GetValueRank(attributeId) >= 0)
                {
                    //tAtt.DataType += "[]";
                }

                // add the value.
                if (StatusCode.IsBad(results[ii].StatusCode))
                {
                    tAtt.StatusCode = results[ii].StatusCode.ToString();
                }
                else
                {
                    tAtt.StatusCode = ClientUtils.GetAttributeDisplayText(m_session, attributeId, results[ii].WrappedValue);
                }

                tAtt.NodeToRead = nodesToRead[ii];
                tAtt.Value = results[ii];

                if (IsMethodArgs)
                {
                    if (tAtt.BrowseName == "Value" && tAtt.Value.Value is Opc.Ua.ExtensionObject[])
                    {
                        Opc.Ua.ExtensionObject[] tAdd = (Opc.Ua.ExtensionObject[])tAtt.Value.Value;
                        if (tAdd != null && tAdd.Length > 0)
                        {
                            for (int i = 0; i < tAdd.Length; i++)
                            {
                                Opc.Ua.Argument tArg = tAdd[i].Body as Opc.Ua.Argument;
                                if (tArg != null)
                                {
                                    TheOPCProperties tAtt2 = new TheOPCProperties();
                                    tAtt2.BrowseName = tArg.Name;
                                    tAtt2.Description = tArg.Description.Text;
                                    tAtt2.OPCType = (BuiltInType)TheCommonUtils.CInt(tArg.DataType.Identifier);
                                    tAtt2.DataType = TheOPCTag.GetCDETypeFromOPCType(tAtt2.OPCType);
                                    //tAtt2.DataType = TheOPCTag.GetCDETypeFromIdType(tArg.DataType.IdType);
                                    tAtt2.Value = tArg.Value as DataValue;
                                    pTagPropAttr.Add(tAtt2);
                                }
                            }
                        }
                    }
                }
                else
                    pTagPropAttr.Add(tAtt);
            }
            if (showProperties)
                pTagPropAttr.AddRange(ReadProperties(nodeId));
            return pTagPropAttr;
        }

        /// <summary>
        /// Reads the properties for the node.
        /// </summary>
        private List<TheOPCProperties> ReadProperties(NodeId nodeId)
        {
            List<TheOPCProperties> pTagPropAttr = new List<TheOPCProperties>();

            // build list of references to browse.
            BrowseDescriptionCollection nodesToBrowse = new BrowseDescriptionCollection();

            BrowseDescription nodeToBrowse = new BrowseDescription();

            nodeToBrowse.NodeId = nodeId;
            nodeToBrowse.BrowseDirection = BrowseDirection.Forward;
            nodeToBrowse.ReferenceTypeId = Opc.Ua.ReferenceTypeIds.HasProperty;
            nodeToBrowse.IncludeSubtypes = true;
            nodeToBrowse.NodeClassMask = (uint)NodeClass.Variable;
            nodeToBrowse.ResultMask = (uint)BrowseResultMask.All;

            nodesToBrowse.Add(nodeToBrowse);

            // find properties.
            ReferenceDescriptionCollection references = ClientUtils.Browse(m_session, nodesToBrowse, false);

            // build list of properties to read.
            ReadValueIdCollection nodesToRead = new ReadValueIdCollection();

            for (int ii = 0; references != null && ii < references.Count; ii++)
            {
                ReferenceDescription reference = references[ii];

                // ignore out of server references.
                if (reference.NodeId.IsAbsolute)
                {
                    continue;
                }

                ReadValueId nodeToRead = new ReadValueId();
                nodeToRead.NodeId = (NodeId)reference.NodeId;
                nodeToRead.AttributeId = Attributes.Value;
                nodeToRead.Handle = reference;
                nodesToRead.Add(nodeToRead);
            }

            if (nodesToRead.Count == 0)
            {
                return pTagPropAttr;
            }

            // read the properties.
            DataValueCollection results = null;
            DiagnosticInfoCollection diagnosticInfos = null;

            m_session.Read(
                null,
                0,
                TimestampsToReturn.Neither,
                nodesToRead,
                out results,
                out diagnosticInfos);

            ClientBase.ValidateResponse(results, nodesToRead);
            ClientBase.ValidateDiagnosticInfos(diagnosticInfos, nodesToRead);

            // add the results to the display.
            for (int ii = 0; ii < results.Count; ii++)
            {
                ReferenceDescription reference = (ReferenceDescription)nodesToRead[ii].Handle;

                TypeInfo typeInfo = TypeInfo.Construct(results[ii].Value);

                // add the metadata for the attribute.
                TheOPCProperties tAtt = new TheOPCProperties();
                // add the metadata for the attribute.
                uint attributeId = nodesToRead[ii].AttributeId;
                tAtt.BrowseName = reference.ToString();
                tAtt.DataType = TheOPCTag.GetCDETypeFromOPCType(typeInfo.BuiltInType);

                if (typeInfo.ValueRank >= 0)
                {
                    //tAtt.DataType += "[]";
                }

                // add the value.
                if (StatusCode.IsBad(results[ii].StatusCode))
                {
                    tAtt.StatusCode=results[ii].StatusCode.ToString();
                }
                else
                {
                    tAtt.StatusCode=results[ii].WrappedValue.ToString();
                }

                tAtt.NodeToRead = nodesToRead[ii];
                tAtt.Value = results[ii];
                //item.ImageIndex = ClientUtils.GetImageIndex(m_session, NodeClass.Variable, Opc.Ua.VariableTypeIds.PropertyType, false);

                // display in list.
                pTagPropAttr.Add(tAtt);
            }
            return pTagPropAttr;
        }
    }
}
