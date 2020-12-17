// SPDX-FileCopyrightText: 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0
using nsCDEngine.BaseClasses;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.ViewModels;
using Opc.Ua;
using Opc.Ua.Client;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.IO;
using CDMyOPCUAClient.Contracts;

namespace CDMyOPCUAClient.ViewModel
{
    public abstract class TheOPCMonitoredItemBase : TheDataBase
    {
        public bool IsSubscribedAsThing { get; set; }
        public bool IsSubscribedAsProperty
        {
            get
            {
                {
                    return !String.IsNullOrEmpty(this.HostThingMID);
                }
            }
        }
        public string Parent { get; set; }
        public string DisplayName { get; set; }
        //public string OPCServerID { get; set; }
        public string BrowseName { get; set; }
        public string TypeDefinition { get; set; }
        //public List<TheOPCProperties> PropAttr { get; set; }

        /// <summary>
        ///  Sampling Interval = 0: Server assigns fastest practical sample rate
        ///  Sampling Interval = -1: Use sample rate of the subscription (OPC UA Spec: any value < 0 has this effect)
        ///  Sampling Interval <= -2: Do not subscribe
        /// </summary>
        public int SampleRate { get; set; }
        public string HostPropertyNameOverride { get; set; }
        public DateTimeOffset? HistoryStartTime { get; set; } // DateTimeOffset.MinValue or null = normal subscribe, otherwise use AggregateFilter for HDA

        public double DeadbandFilterValue = 0; // 0 = none, >0 = absolute, <0 = percentage


        public string StructTypeInfo;

        // Change Trigger
        public int ChangeTrigger = 1; // 0 = Status, 1 = Value, 2 = Value and Timestamp

        public string NodeIdName { get; set; }
        //{
        //    get
        //    {
        //        if (TagRef != null)
        //            return TagRef.ToString();
        //        else
        //            return "Not Set";
        //    }
        //    set
        //    {
        //        TagRef = NodeId.Parse(value);
        //    }
        //}

        NodeId _resolvedNodeId;
        string _inputForResolvedNodeId;
        public NodeId GetResolvedNodeIdName()
        {
            if (_resolvedNodeId == null || _inputForResolvedNodeId != NodeIdName)
            {
                var nodeIdName = NodeIdName;
                _resolvedNodeId = ResolveNodeIdName(nodeIdName, MyOPCServer?.m_session);
                if (_resolvedNodeId != null)
                {
                    _inputForResolvedNodeId = nodeIdName;
                }
                else
                {
                    _inputForResolvedNodeId = null;
                }
            }
            return _resolvedNodeId;
        }

        public static NodeId ResolveNodeIdName(string nodeIdName, Session session)
        {
            try
            {
                var exNodeId = ExpandedNodeId.Parse(nodeIdName);
                return ExpandedNodeId.ToNodeId(exNodeId, session?.NamespaceUris);
            }
            catch
            {
            }
            return null;
        }

        public string GetNodeIdForLogs()
        {
            var resolvedNodeId = GetResolvedNodeIdName();
            if (resolvedNodeId == NodeIdName)
            {
                return NodeIdName;
            }
            return $"{NodeIdName}/{resolvedNodeId}";
        }

        private string _hostThingMID = "";
        public string HostThingMID
        {
            get
            {
                return _hostThingMID;
            }
            set
            {
                _hostThingMID = value;
                _pHostThing = null;
            }
        }

        public bool HasActiveHostThing
        {
            get { return IsSubscribedAsProperty && this.GetHostThing() != null; }
        }

        List<TheOPCMonitoredItemBase> _childTags;
        public List<TheOPCMonitoredItemBase> GetChildTags()
        {
            return _childTags;
        }
        public void AddChildTag(TheOPCMonitoredItemBase childTag)
        {
            if (_childTags == null)
            {
                _childTags = new List<TheOPCMonitoredItemBase>();
            }
            _childTags.Add(childTag);
        }

        private TheThing _pHostThing = null;
        public TheThing GetHostThing()
        {
            if (_pHostThing == null)
            {
                _pHostThing = TheThingRegistry.GetThingByMID("*", TheCommonUtils.CGuid(this.HostThingMID));
            }
            return _pHostThing;
        }

        public string[] GetDisplayNamePathArray()
        {
            return TheCommonUtils.cdeSplit(GetDisplayNamePath(), '.', false, false);
        }

        public string GetDisplayNamePath()
        {
            return !string.IsNullOrEmpty(this.Parent) ? $"{this.Parent}.{this.BrowseName}" : this.BrowseName;
        }

        public override string ToString()
        {
            return string.Format("{0} Type:{1} Id:{2} IsSubd:{3}", DisplayName, TypeDefinition, NodeIdName == null ? "null" : NodeIdName, IsSubscribedAsThing);
        }

        public TheOPCMonitoredItemBase()
        {
            SampleRate = -1; // Use subscription's sample rate
        }

        //internal ReferenceDescription TagRef = null;
        //internal NodeId TagRef = null;
        internal TheOPCUARemoteServer MyOPCServer = null;

        // The dedicated Thing for this tag; NULL if none exists
        internal TheThing MyBaseThing = null;

        protected MonitoredItem MyMonitoredItem = null;
        internal void Reset()
        {
            if (MyMonitoredItem != null)
            {
                MyMonitoredItem.Notification -= MonitoredItem_Notification;
                MyMonitoredItem = null;
            }
        }

        protected virtual void InitializeMonitoredItem(TheOPCMonitoredItemBase previousTag)
        {
        }

        internal virtual string RegisterInHostThing()
        {
            return null;
        }

        internal virtual void UnregisterInHostThing(Subscription subscription, bool bApplyChanges)
        {
            if (subscription == null)
            {
                return;
            }
            var existingItem = subscription.MonitoredItems.FirstOrDefault(item => item.Handle == this);
            if (existingItem != null)
            {
                subscription.RemoveItem(existingItem);
                if (bApplyChanges)
                {
                    subscription.ApplyChanges();
                }
            }
        }

        public virtual bool MonitorTag(Subscription subscription, out string error, bool bApplySubscription = true, bool bReadInitialValue = true)
        {
            error = null;
            if (subscription == null || subscription.Session == null)
            {
                error = "Error: No OPC session.";
                return false;
            }

            lock (subscription.MonitoredItems)
            {

                if (MyMonitoredItem != null && !subscription.MonitoredItems.Contains(MyMonitoredItem))
                {
                    // Monitored item was removed: recreate from scratch. Otherwise modify in place
                    MyMonitoredItem = null;
                }


                if (!this.IsSubscribedAsThing && !this.IsSubscribedAsProperty)
                {
                    // Nothing to be monitored
                    error = "Error: Nothing to be monitored";
                    return false;
                }

                if (TheThing.GetSafePropertyBool(MyBaseThing, "DontMonitor") || SampleRate < -1)
                {
                    return false;
                }
                // can only subscribe to local variables. 
                //if (TagRef == null || TagRef.NodeId.IsAbsolute || TagRef.NodeClass != NodeClass.Variable)
                var resolvedNodeId = GetResolvedNodeIdName();
                if (resolvedNodeId == null) // || NodeId.IsAbsolute || TagRef.NodeClass != NodeClass.Variable)
                {
                    error = "Error: No or invalid NodeId";
                    return false;
                }


                var previousMonitoredItem = subscription.MonitoredItems.FirstOrDefault(mi => mi.Handle == this || this.RefersToSamePropertyAndTag(mi.Handle));
                if (previousMonitoredItem != null)
                {
                    if (!previousMonitoredItem.StartNodeId.Equals(resolvedNodeId))
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(78201, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("OPC", $"[{MyOPCServer.GetLogAddress()}] Internal Error - monitored item {previousMonitoredItem.StartNodeId} replaced with {resolvedNodeId}. Change will not take effect!", eMsgLevel.l4_Message));
                    }
                    MyMonitoredItem = previousMonitoredItem;
                }
                else
                {
                    MyMonitoredItem = new MonitoredItem(subscription.DefaultItem);
                }
                //MyMonitoredItem.StartNodeId = (NodeId)TagRef.NodeId;
                MyMonitoredItem.StartNodeId = resolvedNodeId;
                MyMonitoredItem.AttributeId = Attributes.Value;
                MyMonitoredItem.DisplayName = DisplayName; // Utils.Format("{0}", TagRef);
                MyMonitoredItem.MonitoringMode = MonitoringMode.Reporting;
                if ((!this.HistoryStartTime.HasValue || this.HistoryStartTime.Value == DateTimeOffset.MinValue) && MyOPCServer.DefHistoryStartTime != DateTimeOffset.MinValue)
                {
                    this.HistoryStartTime = MyOPCServer.DefHistoryStartTime;
                }

                if (this.HistoryStartTime.HasValue && this.HistoryStartTime.Value != DateTimeOffset.MinValue)
                {
                    MyMonitoredItem.Filter = new AggregateFilter()
                    {
                        StartTime = this.HistoryStartTime.Value.UtcDateTime,
                        ProcessingInterval = this.SampleRate,
                        AggregateType = ObjectIds.AggregateFunction_Interpolative,
                    };
                    MyMonitoredItem.QueueSize = uint.MaxValue;
                    MyMonitoredItem.SamplingInterval = 0;
                }
                else
                {
                    DataChangeFilter filter = null;
                    // TODO Remove this special case: pass parameters for deadband filters and StatusValueTimestamp (this breaks P08!!!)
                    if (this.DeadbandFilterValue != 0)
                    {
                        filter = new DataChangeFilter
                        {
                            DeadbandType = (uint)(this.DeadbandFilterValue > 0 ? DeadbandType.Absolute : DeadbandType.Percent),
                            DeadbandValue = Math.Abs(this.DeadbandFilterValue)
                        };
                    }

                    if (ChangeTrigger != 1)
                    {
                        if (filter == null)
                        {
                            filter = new DataChangeFilter();
                        }
                        filter.Trigger = (DataChangeTrigger)ChangeTrigger;
                    }

                    if (filter != null)
                    {
                        MyMonitoredItem.Filter = filter;
                    }
                    MyMonitoredItem.SamplingInterval = SampleRate;

                    // For Events, the sample rate should be 0 (per spec), but is really ignored and thus should not affect the aggregate sample rate for the server
                    // All other sample rates are at least 50ms per other checks

                    if (SampleRate <= subscription.PublishingInterval * 2 && SampleRate > 0)
                    {
                        // 3.220: PublishingInterval is now independent of the sample rate: it only affects the frequency of the traffic from the server, not the content
                        //MyOPCServer.Subscription.PublishingInterval = SampleRate;

                        // Request the QueueSize to be 50 times the expected data points, so that no data is lost in normal operation
                        MyMonitoredItem.QueueSize = (uint)Math.Ceiling((((double)subscription.PublishingInterval) / SampleRate) * 50);
                        if (MyMonitoredItem.QueueSize < 50)
                        {
                            MyMonitoredItem.QueueSize = 50;
                        }
                    }
                    else
                    {
                        MyMonitoredItem.QueueSize = 50; // Request at least 50 
                    }
                }
                MyMonitoredItem.DiscardOldest = true;

                MyMonitoredItem.Notification -= MonitoredItem_Notification;
                MyMonitoredItem.Notification += MonitoredItem_Notification;

            TheOPCMonitoredItemBase previousTag = null;
            if (previousMonitoredItem != null && previousMonitoredItem.Handle != this)
            {
                previousTag = previousMonitoredItem.Handle as TheOPCMonitoredItemBase;
                if (previousTag != null)
                {
                    previousTag.ReleaseMonitoredItem();
                }
            }

                MyMonitoredItem.Handle = this;

            InitializeMonitoredItem(previousTag);

                if (previousMonitoredItem == null)
                {
                    subscription.AddItem(MyMonitoredItem);
                }
            }
            if (bApplySubscription)
            {
#if OLD_UA
                var items = subscription.ApplyChanges();
                if (!items.Contains(MyMonitoredItem))
#else
                subscription.ApplyChanges();
                if (!subscription.MonitoredItems.Contains(MyMonitoredItem))
#endif
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(78201, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("OPC", $"[{MyOPCServer.GetLogAddress()}] Internal Error: Monitored item not found after applying changes {GetNodeIdForLogs()}. Actual values: Sampling {MyMonitoredItem.Status.SamplingInterval}, Queue {MyMonitoredItem.Status.QueueSize}", eMsgLevel.l1_Error));
                    error = "Error: Monitored item not found after applying changes";
                    return false;
                }
                else
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(78201, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("OPC", $"[{MyOPCServer.GetLogAddress()}] Added monitored item {GetNodeIdForLogs()}. Actual values: Sampling {MyMonitoredItem.Status.SamplingInterval}, Queue {MyMonitoredItem.Status.QueueSize}", eMsgLevel.l4_Message));
                }
            }

            if (ServiceResult.IsBad(MyMonitoredItem.Status.Error))
            {
                TheThing.SetSafePropertyString(MyBaseThing, "LastMessage", MyMonitoredItem.Status.Error.StatusCode.ToString());
                TheBaseAssets.MySYSLOG.WriteToLog(78201, TSM.L(eDEBUG_LEVELS.FULLVERBOSE) ? null : new TSM("OPC", $"[{MyOPCServer.GetLogAddress()}] Error adding monitored item {GetNodeIdForLogs()}", eMsgLevel.l4_Message, MyMonitoredItem.Status.Error.ToString()));
                error = "Error: " + MyMonitoredItem.Status.Error.ToString();
                return false;
            }
            else
                MyOPCServer.RegisterEvent("DisconnectComplete", sinkDisconnected);

            TheThing.SetSafePropertyBool(MyBaseThing, "IsActive", true);
            return true;
        }

        /// <summary>
        /// Called when a new instance of the tag is being created and will take over the existing monitored item: must release all event registrations etc.
        /// </summary>
        protected virtual void ReleaseMonitoredItem()
        {
            MyOPCServer?.UnregisterEvent("DisconnectComplete", sinkDisconnected);
            MyMonitoredItem.Notification -= MonitoredItem_Notification;
        }

        internal abstract bool RefersToSamePropertyAndTag(object monitoredItemHandle);

        private static bool CompareMonitoredItems(MonitoredItem mi, MonitoredItem mi2)
        {
            return
                    mi.StartNodeId.Equals(mi2.StartNodeId)
                    && mi.AttributeId == mi2.AttributeId
                    && mi.DisplayName.Equals(mi2.DisplayName, StringComparison.Ordinal)
                    && mi.MonitoringMode == mi2.MonitoringMode
                    && (mi.Filter as DataChangeFilter)?.Trigger == (mi2.Filter as DataChangeFilter)?.Trigger
                    && (mi.Filter as DataChangeFilter)?.DeadbandType == (mi2.Filter as DataChangeFilter)?.DeadbandType
                    && (mi.Filter as DataChangeFilter)?.DeadbandValue == (mi2.Filter as DataChangeFilter)?.DeadbandValue
                    && mi.SamplingInterval == mi2.SamplingInterval
                    && mi.Handle == mi2.Handle
                    ;
        }

        protected void sinkDisconnected(ICDEThing pThing, object p)
        {
            Reset();
            TheThing.SetSafePropertyBool(MyBaseThing, "IsActive", false);
        }

        internal bool UnmonitorTag()
        {
            if (MyMonitoredItem != null)
            {
                var subscription = MyOPCServer.GetSubscription();
                if (subscription != null)
                {
                    subscription.RemoveItem(MyMonitoredItem);
                    subscription.ApplyChanges(); // TODO Report any errors
                }
                MyMonitoredItem = null;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Updates the display with a new value for a monitored variable. 
        /// </summary>
        protected abstract void MonitoredItem_Notification(MonitoredItem monitoredItem, MonitoredItemNotificationEventArgs e);

        internal static List<TheOPCSensorSubscription> GetHostThingOPCProperties(TheThing tHostThing, TheThing tProviderThing)
        {
            string prefix = "OPCUA";
            var legacyProps = tHostThing.GetPropertiesMetaStartingWith(prefix, true);
            if (legacyProps.Any())
            {
                foreach(var prop in legacyProps)
                {
                    if (ParseOpcUaMetaString(prop.cdeM, out var propKind, out var nodeIdName, out var serverId, out var changeTrigger, out var deadBandValue, out var structTypeInfo, out var historyStartTime, out var eventFilter))
                    {
                        if (serverId == tProviderThing.ID)
                        {
                            // TODO log migration start
                            var opcSubscriptionInfo = new TheOPCSensorSubscription
                            {
                                SampleRate = (int)prop.cdeEXP,
                                SensorId = nodeIdName,
                                ChangeTrigger = changeTrigger,
                                DeadbandValue = deadBandValue,
                                EventInfo = eventFilter,
                                StructTypeInfo = structTypeInfo,
                                //HistoryStartTime = historyStartTime, // TODO
                            };

                            var providerInfo = new cdeP.TheProviderInfo(tProviderThing, opcSubscriptionInfo);
                            prop.SetSensorProviderInfo(providerInfo);
                            prop.cdeM = null;
                            // TODO Log migration finish
                        }
                        else
                        {
                            // tag from a different opc client
                        }
                    }
                    else
                    {
                        // Log migration error
                    }
                }
            }
            var tagPro = tHostThing.GetSensorProviderProperties()
                .Select(p => p.GetSensorProviderInfo())
                .Where(p => p.ProviderMid == tProviderThing.cdeMID)
                .Select(p => new TheOPCSensorSubscription(p.Subscription))
                .ToList();
            return tagPro;
        }

        internal static bool ParseOpcUaMetaString(string cdeMValue, out string propKind, out string nodeIdName, out string serverId, out int changeTrigger, out double deadBandFilterValue, out string structTypeInfo, out DateTimeOffset? historyStartTime, out TheEventSubscription eventFilter)
        {
            nodeIdName = null;
            serverId = null;
            propKind = null;
            changeTrigger = 1;
            deadBandFilterValue = 0;
            structTypeInfo = "";
            historyStartTime = null;
            eventFilter = null;
            string[] tDef = TheCommonUtils.cdeSplit(cdeMValue, ":;:", false, false);
            if (tDef.Length < 3)
            {
                return false;
            }
            if (!tDef[0].StartsWith("OPCUA"))
            {
                return false;
            }

            propKind = tDef[0].Substring("OPCUA".Length);
            nodeIdName = tDef[2];
            serverId = tDef[1];
            if (tDef.Length > 3 && tDef[3].Length > 0)
            {
                changeTrigger = TheCommonUtils.CInt(tDef[3]);
            }
            if (tDef.Length > 4 && tDef[4].Length > 0)
            {
                deadBandFilterValue = TheCommonUtils.CDbl(tDef[4]);
            }
            if (tDef.Length > 5 && tDef[5].Length > 0)
            {
                structTypeInfo = tDef[5];
            }
            if (tDef.Length > 6 && tDef[6].Length > 0)
            {
                historyStartTime = TheCommonUtils.CDate(tDef[6]);
            }
            if (tDef.Length > 7 && tDef[7].Length > 0)
            {
                eventFilter = TheCommonUtils.DeserializeJSONStringToObject<TheEventSubscription>(tDef[7]);
            }
            return true;
        }

        //internal static string GetOpcUaMetaString(TheOPCUARemoteServer opcServer, String propKind, string nodeIdName, int changeTrigger, double deadBandFilterValue, string structTypeInfo, DateTimeOffset? historyStartTime, TheEventSubscription eventFilter)
        //{
        //    var retVal = string.Format("OPCUA{0}:;:{1}:;:{2}", propKind, opcServer.GetBaseThing().ID, nodeIdName);
        //    if (eventFilter != null)
        //    {
        //        retVal += string.Format(":;:{0}:;:{1}:;:{2}:;:{3}:;:{4}", changeTrigger, deadBandFilterValue, structTypeInfo, TheCommonUtils.CStr(historyStartTime.Value), TheCommonUtils.SerializeObjectToJSONString(eventFilter));
        //    }
        //    else if (historyStartTime.HasValue && historyStartTime.Value != DateTimeOffset.MinValue)
        //    {
        //        retVal += string.Format(":;:{0}:;:{1}:;:{2}:;:{3}", changeTrigger, deadBandFilterValue, structTypeInfo, TheCommonUtils.CStr(historyStartTime.Value));
        //    }
        //    else if (!String.IsNullOrEmpty(structTypeInfo))
        //    {
        //        retVal += string.Format(":;:{0}:;:{1}:;:{2}", changeTrigger, deadBandFilterValue, structTypeInfo);
        //    }
        //    else if (deadBandFilterValue != 0)
        //    {
        //        retVal += string.Format(":;:{0}:;:{1}", changeTrigger, deadBandFilterValue);
        //    }
        //    else if (changeTrigger != 1)
        //    {
        //        retVal += string.Format(":;:{0}", changeTrigger);
        //    }
        //    return retVal;
        //}

        static object _logFileLock = new object();
        public static void LogOPCData(Dictionary<string, object> logInfo, string logAddress, string sourceItem)
        {
            var logFileName = string.IsNullOrEmpty(TheBaseAssets.MySYSLOG.LogFilePath) ? 
                TheCommonUtils.cdeFixupFileName("opcclientdata.log") : 
                Path.Combine(TheBaseAssets.MySYSLOG.LogFilePath, "opcclientdata.log");
            int retriesLeft = 0;
            do
            {
                try
                {
                    if (retriesLeft > 0)
                    {
                        logInfo["LogRetryCount"] = retriesLeft == 0 ? retriesLeft : 100 - retriesLeft;
                    }
                    var loginfoJson = TheCommonUtils.SerializeObjectToJSONString(logInfo);
                    lock (_logFileLock)
                    {
                        File.AppendAllText(logFileName, $"{loginfoJson},\r\n");
                    }
                    //File.AppendAllText("opcclientdata.log", $"{DateTimeOffset.Now:O},{property.Name},0x{((int?)(pValue.WrappedValue.TypeInfo?.BuiltInType)) ?? 0:x4},{pValue.Value},0x{pValue.StatusCode.Code:x8},{timeStampForProperty.UtcDateTime:yyyy-MM-ddTHH:mm:ss.fffZ},{pValue.ServerTimestamp.ToUniversalTime():yyyy-MM-ddTHH:mm:ss.fffZ}\r\n");
                    retriesLeft = 0;
                }
                catch (Exception e)
                {
                    if (retriesLeft <= 0)
                    {
                        retriesLeft = 100;
                    }
                    else
                    {
                        retriesLeft--;
                        if (retriesLeft < 50)
                        {
                            System.Threading.Thread.Sleep(100);
                        }
                        if (retriesLeft <= 0)
                        {
                            TheBaseAssets.MySYSLOG.WriteToLog(78301, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheOPCTag", $"[{logAddress}] Unable to log data to file after retries: {sourceItem}", eMsgLevel.l2_Warning, e.ToString()));
                        }
                    }
                }
            } while (retriesLeft > 0);

        }


        public static object ChangeType(DataValue baseValueWithOriginalType, DataValue ValueTBText, out ePropertyTypes? pType)
        {
            return ChangeType(baseValueWithOriginalType.WrappedValue.TypeInfo?.BuiltInType, ValueTBText?.WrappedValue, out pType);
        }

        public static object ChangeType(BuiltInType? baseType, Variant? valueVariant, out ePropertyTypes? pType)
        {
            object value = null;
            object newValue = valueVariant?.Value;

            pType = null; // ePropertyTypes.TString;

            try
            {
                if (baseType != null)
                {
                    switch (baseType)
                    {
                        case BuiltInType.ByteString:
                            {
                                value = newValue;
                                pType = ePropertyTypes.TBinary;
                                break;
                            }
                        case BuiltInType.Byte:
                        case BuiltInType.SByte:
                            {
                                if (newValue is Array)
                                {
                                    value = newValue;
                                    pType = ePropertyTypes.TBinary;
                                }
                                else
                                {
                                    value = newValue is int || newValue == null ? newValue : TheCommonUtils.CInt(valueVariant?.ToString(null, CultureInfo.InvariantCulture));
                                    pType = ePropertyTypes.TNumber;
                                }
                                break;
                            }

                        default:
                            {
                                if (newValue is Array)
                                {
                                    value = valueVariant?.ToString(null, CultureInfo.InvariantCulture);
                                    pType = ePropertyTypes.TString;
                                }
                                else
                                {
                                    switch (baseType)
                                    {
                                        case BuiltInType.Boolean:
                                            {
                                                value = newValue is bool || newValue == null ? newValue : TheCommonUtils.CBool(valueVariant?.ToString(null, CultureInfo.InvariantCulture));
                                                pType = ePropertyTypes.TBoolean;
                                                break;
                                            }

                                        case BuiltInType.Int16:
                                            {
                                                value = newValue is short || newValue == null ? newValue : TheCommonUtils.CShort(valueVariant?.ToString(null, CultureInfo.InvariantCulture));
                                                pType = ePropertyTypes.TNumber;
                                                break;
                                            }

                                        case BuiltInType.UInt16:
                                            {
                                                value = newValue is ushort || newValue == null ? newValue : TheCommonUtils.CUShort(valueVariant?.ToString(null, CultureInfo.InvariantCulture));
                                                pType = ePropertyTypes.TNumber;
                                                break;
                                            }

                                        case BuiltInType.Int32:
                                            {
                                                value = newValue is int || newValue == null ? newValue : TheCommonUtils.CInt(valueVariant?.ToString(null, CultureInfo.InvariantCulture));
                                                pType = ePropertyTypes.TNumber;
                                                break;
                                            }
                                        case BuiltInType.Integer:
                                        case BuiltInType.UInt32:
                                            {
                                                value = newValue is uint || newValue == null ? newValue : TheCommonUtils.CUInt(valueVariant?.ToString(null, CultureInfo.InvariantCulture));
                                                pType = ePropertyTypes.TNumber;
                                                break;
                                            }
                                        case BuiltInType.UInteger:
                                        case BuiltInType.Int64:
                                            {
                                                value = newValue is long || newValue == null ? newValue : TheCommonUtils.CLng(valueVariant?.ToString(null, CultureInfo.InvariantCulture));
                                                pType = ePropertyTypes.TNumber;
                                                break;
                                            }

                                        case BuiltInType.UInt64:
                                            {
                                                value = newValue is ulong || newValue == null ? newValue : TheCommonUtils.CULng(valueVariant?.ToString(null, CultureInfo.InvariantCulture));
                                                pType = ePropertyTypes.TNumber;
                                                break;
                                            }

                                        case BuiltInType.Float:
                                            {
                                                value = newValue is float || newValue == null ? newValue : TheCommonUtils.CFloat(valueVariant?.ToString(null, CultureInfo.InvariantCulture));
                                                pType = ePropertyTypes.TNumber;
                                                break;
                                            }
                                        case BuiltInType.Number:
                                        case BuiltInType.Double:
                                            {
                                                value = newValue is double || newValue == null ? newValue : TheCommonUtils.CDbl(valueVariant?.ToString(null, CultureInfo.InvariantCulture));
                                                pType = ePropertyTypes.TNumber;
                                                break;
                                            }
                                        case BuiltInType.DateTime:
                                            {
                                                value = newValue is DateTimeOffset || newValue == null ? newValue :
                                                    newValue is DateTime ? TheCommonUtils.CDate((DateTime)newValue)
                                                    : TheCommonUtils.CDate(valueVariant?.ToString(null, CultureInfo.InvariantCulture));
                                                pType = ePropertyTypes.TDate;
                                                break;
                                            }
                                        case BuiltInType.Guid:
                                            {
                                                value = newValue is Guid || newValue == null ? newValue : TheCommonUtils.CGuid(valueVariant?.ToString(null, CultureInfo.InvariantCulture));
                                                pType = ePropertyTypes.TGuid;
                                                break;
                                            }
                                        case BuiltInType.Null:
                                            {
                                                value = null;
                                            }
                                            break;
                                        default:
                                            value = valueVariant?.ToString(null, CultureInfo.InvariantCulture);
                                            break;
                                    }

                                }
                                break;
                            }
                    }
                }
                else
                {
                    value = value = valueVariant?.ToString(null, CultureInfo.InvariantCulture);
                }
            }
            catch (Exception e)
            {
                try
                {
                    value = valueVariant?.ToString(null, CultureInfo.InvariantCulture);
                }
                catch { }
                TheBaseAssets.MySYSLOG.WriteToLog(78202, new TSM("OpcChangeValue", String.Format("[{0}] Error Change Value: {1}", "" , value), eMsgLevel.l1_Error, e.ToString()));
            }
            return value;
        }

        public static object GetOPCValueFromCDEValue(object ValueTBText, BuiltInType pBuiltInType)
        {
            object value = null;

            try
            {
                switch (pBuiltInType)
                {
                    case BuiltInType.Boolean:
                        {
                            value = TheCommonUtils.CBool(ValueTBText);
                            break;
                        }

                    case BuiltInType.Byte:
                        {
                            value = TheCommonUtils.CByte(ValueTBText);
                            break;
                        }
                    case BuiltInType.Int16:
                        {
                            value = TheCommonUtils.CShort(ValueTBText);
                            break;
                        }

                    case BuiltInType.UInt16:
                        {
                            value = TheCommonUtils.CUShort(ValueTBText);
                            break;
                        }

                    case BuiltInType.Int32:
                        {
                            value = TheCommonUtils.CInt(ValueTBText);
                            break;
                        }
                    case BuiltInType.Integer:
                    case BuiltInType.UInt32:
                        {
                            value = (uint)TheCommonUtils.CUInt(ValueTBText);
                            break;
                        }
                    case BuiltInType.UInteger:
                    case BuiltInType.Int64:
                        {
                            value = TheCommonUtils.CLng(ValueTBText);
                            break;
                        }

                    case BuiltInType.UInt64:
                        {
                            value = TheCommonUtils.CULng(ValueTBText);
                            break;
                        }

                    case BuiltInType.Float:
                        {
                            value = TheCommonUtils.CFloat(ValueTBText);
                            break;
                        }
                    case BuiltInType.Number:
                    case BuiltInType.Double:
                        {
                            value = TheCommonUtils.CDbl(ValueTBText);
                            break;
                        }
                    case BuiltInType.DateTime:
                        {
                            value = TheCommonUtils.CDate(ValueTBText).LocalDateTime;
                            break;
                        }
                    case BuiltInType.Guid:
                        {
                            value = TheCommonUtils.CGuid(ValueTBText);
                            break;
                        }
                    default:
                        value = ValueTBText;
                        break;
                }
            }
            catch (Exception e)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(78201, new TSM("GetOPCValueFromCDEValue", "Error Change Value :" + (ValueTBText != null ? ValueTBText.ToString() : "<null>"), eMsgLevel.l1_Error, e.ToString()));
            }
            return value;
        }

        public static ePropertyTypes GetCDETypeFromOPCType(BuiltInType pUAType)
        {
            ePropertyTypes pType = ePropertyTypes.TString;
            switch (pUAType)
            {
                case BuiltInType.Boolean:
                    pType = ePropertyTypes.TBoolean;
                    break;
                case BuiltInType.Int16:
                case BuiltInType.UInt16:
                case BuiltInType.Int32:
                case BuiltInType.Integer:
                case BuiltInType.UInt32:
                case BuiltInType.UInteger:
                case BuiltInType.Int64:
                case BuiltInType.UInt64:
                case BuiltInType.Float:
                case BuiltInType.Number:
                case BuiltInType.Double:
                    pType = ePropertyTypes.TNumber;
                    break;
                case BuiltInType.DateTime:
                    pType = ePropertyTypes.TDate;
                    break;
                case BuiltInType.Guid:
                    pType = ePropertyTypes.TGuid;
                    break;
                default:
                    break;
            }
            return pType;
        }

        public static ePropertyTypes GetCDETypeFromIdType(IdType pUAType)
        {
            ePropertyTypes pType = ePropertyTypes.TString;
            switch (pUAType)
            {
                case IdType.Opaque:
                    pType = ePropertyTypes.TBinary;
                    break;
                case IdType.Numeric:
                    pType = ePropertyTypes.TNumber;
                    break;
                case IdType.Guid:
                    pType = ePropertyTypes.TGuid;
                    break;
                default:
                    break;
            }
            return pType;
        }
    }


}
