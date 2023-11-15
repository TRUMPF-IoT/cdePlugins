// SPDX-FileCopyrightText: 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0
using CDMyOPCUAClient.Contracts;
using nsCDEngine.BaseClasses;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.ViewModels;
using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Schema.Binary;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace CDMyOPCUAClient.ViewModel
{
    public class TheOPCProperties
    {
        public string BrowseName { get; set; }
        public ePropertyTypes DataType { get; set; }
        public BuiltInType OPCType { get; set; }
        public string StatusCode { get; set; }
        public ReadValueId NodeToRead { get; set; }
        public DataValue Value { get; set; }

        internal cdeP cdeProperty;
        public string Description { get; set; }
        public override string ToString()
        {
            return string.Format("{0}={2} Type:{1} ", BrowseName, DataType, Value == null ? "null" : Value.ToString());
        }
    }

    public class TheOPCTag : TheOPCMonitoredItemBase
    {
        public List<TheOPCProperties> PropAttr { get; set; }

        public bool AllowWriteBack { get; private set; }

        public bool IsSubscribedAsPropertyInThing(TheThing cdeHostThing)
        {
            var tTargetProperty = cdeHostThing.GetProperty(GetTargetPropertyName(), false);

            return tTargetProperty != null && tTargetProperty.GetSensorProviderInfo().Provider == MyOPCServer.GetBaseThing();
        }

        public string GetTargetPropertyName()
        {
            if (!string.IsNullOrEmpty(this.HostPropertyNameOverride))
            {
                return this.HostPropertyNameOverride;
            }
            return string.IsNullOrEmpty(this.Parent) ? this.DisplayName : string.Format("{0}.{1}", this.Parent, this.DisplayName);
        }

       public TheOPCTag()
        {
            SampleRate = -1; // Use subscription's sample rate
        }

        public TheOPCTag(TheOPCTag tag)
        {
            MyOPCServer = tag.MyOPCServer;
            BrowseName = tag.BrowseName;
            ChangeTrigger = tag.ChangeTrigger;
            DeadbandFilterValue = tag.DeadbandFilterValue;
            DisplayName = tag.DisplayName;
            HostThingMID = tag.HostThingMID;
            HostPropertyNameOverride = tag.HostPropertyNameOverride;
            Parent = tag.Parent;
            PropAttr = tag.PropAttr;
            SampleRate = tag.SampleRate;
            HistoryStartTime = tag.HistoryStartTime;
            NodeIdName = tag.NodeIdName;
            TypeDefinition = tag.TypeDefinition;
        }

        //bool _bInit = false;
        internal override string RegisterInHostThing()
        {
            // TODO: Find way to break cycles (written value comes back from OPC Server, or OPC Server value change gets reflected back)
            AllowWriteBack = false; // TODO Expose in UI and change default to false
            string error = null;
            if (this.IsSubscribedAsProperty)
            {
                if (this.HasActiveHostThing && this.MyOPCServer != null && this.NodeIdName != null)
                {
                    if (this.TargetProperty != null)
                    {
                        this.TargetProperty.UnregisterEvent("Refresh", sinkRefresh);
                    }
                    var targetPropertyName = GetTargetPropertyName();

                    var targetThing = this.GetHostThing();
                    this.TargetProperty = targetThing?.GetProperty(targetPropertyName, true);

                    if (this.TargetProperty != null)
                    {
                        // TODO make this configurable, migrate cdeM info etc.
                        if (!targetPropertyName.StartsWith("[") && !targetPropertyName.EndsWith("[EngineerungUnits]") && !targetPropertyName.EndsWith("[EURange]"))
                        {

                            var sensorMeta = TargetProperty.GetSensorMeta();
                            this.GetHostThing().DeclareSensorProperty(cdeP.GetPropertyPath(this.TargetProperty), ePropertyTypes.NOCHANGE, sensorMeta);
                        }

                        var opcSubscriptionInfo = new TheOPCSensorSubscription
                        {
                            SampleRate = this.SampleRate,
                            SensorId = this.NodeIdName,
                            ChangeTrigger = this.ChangeTrigger,
                            DeadbandValue = this.DeadbandFilterValue,
                            StructTypeInfo = this.StructTypeInfo,
                            HistoryStartTime = this.HistoryStartTime,
                        };

                        var providerInfo = new cdeP.TheProviderInfo(this.MyOPCServer.GetBaseThing(), opcSubscriptionInfo);
                        this.TargetProperty.SetSensorProviderInfo(providerInfo);
                        this.TargetProperty.RegisterEvent("Refresh", sinkRefresh);
                    }
                    else
                    {
                        error = "ERROR: Property not found";
                    }
                }
                else
                {
                    error = "ERROR: Thing not found";
                }
            }
            else
            {
                this.TargetProperty = null;
            }
            //_bInit = true;
            return error;
        }

        internal override void UnregisterInHostThing(Subscription subscription, bool bApplyChanges)
        {
            base.UnregisterInHostThing(subscription, bApplyChanges);
            var tHostToUnregister = GetHostThing();
            if (tHostToUnregister != null)
            {
                // Remove any properties from the host thing
                var props = GetHostThingOPCProperties(tHostToUnregister, MyOPCServer.GetBaseThing());
                foreach (var prop in props)
                {
                    tHostToUnregister.RemoveProperty(prop.TargetProperty);
                }
            }
        }

        internal DataValue MyLastValue = null;
        internal cdeP TargetProperty = null;
        //internal ReferenceDescription TagRef = null;

        public override bool MonitorTag(Subscription subscription, out string error, bool bApplySubscription = true, bool bReadInitialTagValue = true)
        {
            if (base.MonitorTag(subscription, out error, bApplySubscription))
            {
                if (bReadInitialTagValue)
                {
                    ReadTag(subscription.Session);
                }

                if (IsSubscribedAsProperty && AllowWriteBack)
                {
                    this.TargetProperty.UnregisterEvent(eThingEvents.PropertyChanged, OnOpcPropertyWrittenBack);
                    this.TargetProperty.RegisterEvent(eThingEvents.PropertyChanged, OnOpcPropertyWrittenBack);
                }
                return true;
            }
            else
            {
                return false;
            }
        }

        private void OnOpcPropertyWrittenBack(cdeP obj)
        {
            // Called when a property that is mapped to a remote OPC server (through the OPC Client plug-in) has been modified
            if (AllowWriteBack && obj.GetUpdater() != this.cdeMID)
            {
                WriteTag(new DataValue(new Variant(obj.Value)));
            }
        }

        void sinkRefresh(cdeP prop)
        {
            var session = MyOPCServer.m_session;
            if (session != null)
            {
                ReadTag(session);
            }
        }

        //long _lastSequenceNumber = -1;
        /// <summary>
        /// Updates the display with a new value for a monitored variable. 
        /// </summary>
        override protected void MonitoredItem_Notification(MonitoredItem monitoredItem, MonitoredItemNotificationEventArgs e)
        {
            try
            {
                MonitoredItemNotification notification = e.NotificationValue as MonitoredItemNotification;
                if (notification == null)
                    return;
                // TODO Figure out why this triggers for structs: sequence numbers seem to be increased by 2 instead of 1
                //var sequenceNumber = notification.Message.SequenceNumber;
                //var lastSequenceNumber = System.Threading.Interlocked.Exchange(ref _lastSequenceNumber, (long) sequenceNumber);
                //if (lastSequenceNumber > 0)
                //{
                //    if (lastSequenceNumber == uint.MaxValue)
                //    {
                //        // rollover - ignore for now
                //    }
                //    else if (lastSequenceNumber + 1 != sequenceNumber)
                //    {
                //        //TheBaseAssets.MySYSLOG.WriteToLog(78301, new TSM("TheOPCTag", $"[{MyOPCServer.GetLogAddress()}] Sequence number out of order for Value: {TagRef}, {notification?.Value?.SourceTimestamp}: Last {lastSequenceNumber} Current {sequenceNumber}", eMsgLevel.l2_Warning));
                //    }
                //}

                UpdateValue(notification.Value, notification.Message.SequenceNumber);
            }
            catch (Exception exception)
            {
                TheThing.SetSafePropertyString(MyBaseThing, "LastMessage", "error during notification:" + exception.ToString());
                TheBaseAssets.MySYSLOG.WriteToLog(78301, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("TheOPCTag", $"[{MyOPCServer.GetLogAddress()}] Error processing source monitored item notification.", eMsgLevel.l2_Warning, $"{GetNodeIdForLogs()} {monitoredItem?.ClientHandle}: {exception.ToString()}"));
            }
        }

        private void UpdateValue(DataValue pValue, long? messageSequenceNumber)
        {
            if (pValue == null) return;

            if (pValue.StatusCode.Overflow)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(78301, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheOPCTag", $"[{MyOPCServer.GetLogAddress()}] Server Queue overflow reported for monitored item:{GetNodeIdForLogs()}, {pValue.SourceTimestamp}", eMsgLevel.l2_Warning));
            }

            MyLastValue = pValue;

            if (this.IsSubscribedAsThing)
            {
                // We have a dedicated TheOPCUATagThing associated with this tag: write the value into the Value property
                var newStructTypeInfo = UpdateValueProperty(MyOPCServer, MyBaseThing.GetProperty("Value", true), this.cdeMID, pValue, messageSequenceNumber, MyMonitoredItem);
                if (!String.IsNullOrEmpty(newStructTypeInfo))
                {
                    this.StructTypeInfo = newStructTypeInfo;
                }

                // Write additional data into separate properties
                MyBaseThing.SetProperty("StatusCode", pValue.StatusCode.ToString());
                TheThing.SetSafePropertyBool(MyBaseThing, "IsActive", true);
                TheThing.SetSafePropertyDate(MyBaseThing, "SourceTimeStamp", pValue.SourceTimestamp.ToLocalTime()); // CODE REVIEW: Use the actual property time?
                TheThing.SetSafePropertyNumber(MyBaseThing, "SourcePicoseconds", pValue.SourcePicoseconds);
            }
            if (this.TargetProperty != null)
            {
                // we have an external, potentially multi-tag thing. Write the value into the property indicated (essentially the BrowsePath)
                // Note that the same tag can be in use for both single and multi thing hosts, so we update both
                var newStructTypeInfo = UpdateValueProperty(MyOPCServer, TargetProperty, this.cdeMID, pValue, messageSequenceNumber, MyMonitoredItem);
                if (!String.IsNullOrEmpty(newStructTypeInfo))
                {
                    this.StructTypeInfo = newStructTypeInfo;
                }
            }
        }

        public static string UpdateValueProperty(TheOPCUARemoteServer opcServer, cdeP property, Guid updater, DataValue pValue, long? messageSequenceNumber, MonitoredItem myMonitoredItem = null)
        {
            string newStructTypeInfo = null;
            try
            {
                var propertyNamePath = cdeP.GetPropertyPath(property);
                DateTimeOffset timeStampForProperty;
                if (opcServer.UseLocalTimestamp)
                {
                    timeStampForProperty = DateTimeOffset.Now;
                }
                else
                {
                    try
                    {
                        timeStampForProperty = pValue.SourceTimestamp.ToLocalTime();
                    }
                    catch (ArgumentOutOfRangeException e)
                    {
                        // This can happen for (at least) DateTime.MinValue when converting to DateTimeOffset
                        timeStampForProperty = DateTimeOffset.MinValue;
                        TheBaseAssets.MySYSLOG.WriteToLog(78301, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("TheOPCTag", $"[{opcServer.GetLogAddress()}] Error converting source timestamp. Attempting to use ServerTimestamp.", eMsgLevel.l2_Warning, $"{propertyNamePath}, {pValue.SourceTimestamp}: {e.ToString()}"));
                    }
                    if (timeStampForProperty.Ticks == 0) // In some conversions a DateTime.MinValue is not converted exactly to DateTimeOffset.MinValue, so check for Ticks == 0
                    {
                        try
                        {
                            timeStampForProperty = pValue.ServerTimestamp.ToLocalTime();
                        }
                        catch (ArgumentOutOfRangeException e)
                        {
                            // This can happen for (at least) DateTime.MinValue when converting to DateTimeOffset
                            timeStampForProperty = DateTimeOffset.MinValue;
                            TheBaseAssets.MySYSLOG.WriteToLog(78301, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("TheOPCTag", $"[{opcServer.GetLogAddress()}] Error converting server timestamp. Using MinValue.", eMsgLevel.l2_Warning, $"{propertyNamePath}, {pValue.ServerTimestamp}: {e.ToString()}"));
                        }
                    }

                    try
                    {
                        if (timeStampForProperty.Ticks == 0 || timeStampForProperty == DateTimeOffset.MinValue)
                        {
                            TheBaseAssets.MySYSLOG.WriteToLog(78301, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("TheOPCTag", $"[{opcServer.GetLogAddress()}] No source timestamp for: { propertyNamePath}, {pValue.SourceTimestamp}", eMsgLevel.l2_Warning));
                        }
                    }
                    catch (Exception e)
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(78301, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("TheOPCTag", $"[{opcServer.GetLogAddress()}] Internal error validating timestamp for minvalue: {propertyNamePath}, {pValue.SourceTimestamp}", eMsgLevel.l1_Error, e.ToString()));
                    }
                }

                opcServer.LastDataReceivedTime = DateTimeOffset.Now;
                opcServer.IncrementDataReceivedCount();
                if (opcServer.EnableOPCDataLogging)
                {
                    var logInfo = new Dictionary<string, object>()
                            {
                                { "ReceiveTime", DateTimeOffset.Now },
                                { "TagId", property.Name },
                                { "Type", pValue.WrappedValue.TypeInfo?.BuiltInType },
                                { "Value", pValue.Value },
                                { "Status", pValue.StatusCode.Code },
                                { "Source", pValue.SourceTimestamp },
                                { "Server", pValue.ServerTimestamp },
                                { "PropertyTime", timeStampForProperty },
                                { "MonitoredItem",  myMonitoredItem?.ClientHandle },
                                { "SequenceNumber", messageSequenceNumber },
                            };
                    LogOPCData(logInfo, $"{opcServer.GetLogAddress()}", $"{propertyNamePath}, {pValue.SourceTimestamp}");
                }

                TheThing tThing = property.GetThing().GetBaseThing();

                property.SetUpdater(updater);

                // OPC Sequence numbers restart with every connection: not interesting to use as cdeSEQ?
                if (opcServer.UseSequenceNumberForThingSequence && messageSequenceNumber != null)
                {
                    tThing.cdeSEQ = messageSequenceNumber;
                }

                object valueToSet;
                cdeP.TheSensorMeta sensorMeta = null;
                TheOPCUARemoteServer.ExtensionTypeInfo structTypeInfo = null;

                if (pValue != null && pValue.Value != null &&
                    (pValue.Value is ExtensionObject || pValue.Value is ExtensionObject[]))
                {
                    var body = ((pValue.Value as ExtensionObject)?.Body ?? (pValue.Value as ExtensionObject[])?[0]?.Body);
                    if (body is EUInformation euInfo && property.Name == nameof(AnalogItemState.EngineeringUnits))
                    {
                        // Engineering units: attach to the parent
                        var parentProp = cdeP.GetParentProperty(property);
                        if (parentProp != null)
                        {
                            sensorMeta = parentProp.GetSensorMeta();
                            sensorMeta.Units = euInfo.DisplayName.ToString(null, CultureInfo.InvariantCulture);
                            sensorMeta.SourceUnits = TheCommonUtils.SerializeObjectToJSONString(euInfo);
                            parentProp.SetSensorMeta(sensorMeta);
                            // TODO log data
                            return null;
                        }
                    }
                    else if (body is Range rangeInfo && property.Name == nameof(AnalogItemState.EURange))
                    {
                        // Engineering unit range info: attach to the parent
                        var parentProp = cdeP.GetParentProperty(property);
                        if (parentProp != null)
                        {
                            sensorMeta = parentProp.GetSensorMeta();
                            sensorMeta.RangeMin = rangeInfo.Low;
                            sensorMeta.RangeMax = rangeInfo.High;
                            parentProp.SetSensorMeta(sensorMeta);
                            // TODO log data
                            return null;
                        }
                    }
                    // Custom data types/structs
                    string extensionAsJson = opcServer.DecodeExtensionObjectToJson(pValue.Value, out structTypeInfo);
                    if (structTypeInfo != null)
                    {
                        var providerInfo = property.GetSensorProviderInfo();
                        var opcSubscription = new TheOPCSensorSubscription(providerInfo.Subscription);
                        newStructTypeInfo = TheCommonUtils.SerializeObjectToJSONString(structTypeInfo);
                        if (opcSubscription.StructTypeInfo != newStructTypeInfo)
                        {
                            opcSubscription.StructTypeInfo = newStructTypeInfo;
                            providerInfo.Subscription = opcSubscription;
                            property.SetSensorProviderInfo(providerInfo);
                        }
                    }
                    valueToSet = extensionAsJson;
                }
                else
                {
                    ePropertyTypes? tType;
                    valueToSet = ChangeType(pValue, pValue, out tType);

                    if (tType.HasValue)
                    {
                        property.cdeT = (int)tType;
                    }

                    if (pValue != null && pValue.Value != null && pValue.Value is Array && !(pValue.Value is byte[]))
                    {
                        Array tArr = pValue.Value as Array;
                        if (tThing != null && !opcServer.DoNotWriteArrayElementsAsProperties)
                        {
                            int cnt = 0;
                            foreach (var t in tArr)
                            {
                                tThing.SetProperty(string.Format("{0}.{1}", propertyNamePath, cnt), t, timeStampForProperty);
                                cnt++;
                            }
                        }
                        //property.Value = pValue.WrappedValue.ToString();
                        valueToSet = pValue.WrappedValue.ToString(null, CultureInfo.InvariantCulture);
                    }
                }

                if (opcServer.DoNotUsePropsOfProps)
                {
                    // Legacy mechanism: embedded JSON
                    if (opcServer.SendStatusCode || opcServer.SendServerTimestamp || opcServer.SendPicoSeconds)
                    {
                        var opcValue = new TheOPCUAValue
                        {
                            value = valueToSet,
                        };
                        if (opcServer.SendStatusCode)
                        {
                            opcValue.statusCode = pValue.StatusCode.Code;
                        }
                        if (opcServer.SendPicoSeconds)
                        {
                            opcValue.sourcePicoseconds = pValue.SourcePicoseconds;
                        }
                        if (opcServer.SendServerTimestamp)
                        {
                            opcValue.serverTimestamp = pValue.ServerTimestamp;
                            if (opcServer.SendPicoSeconds)
                            {
                                opcValue.serverPicoseconds = pValue.ServerPicoseconds;
                            }
                        }
                        valueToSet = TheCommonUtils.SerializeObjectToJSONString(opcValue);
                        tThing.SetProperty(propertyNamePath, valueToSet, ePropertyTypes.TString, timeStampForProperty, -1, null);
                    }
                    else
                    {
                        // TODO We need a cleaner cdeP.SetProperty(..., datetimeoffset) solution
                        //property.Value = pValue.Value;
                        tThing.SetProperty(propertyNamePath, valueToSet, timeStampForProperty);
                    }
                }
                else
                {
                    var prop = tThing.SetProperty(propertyNamePath, valueToSet, timeStampForProperty);
                    if (opcServer.SendStatusCode)
                    {
                        var sProp = prop.SetProperty("statusCode", pValue.StatusCode.Code, ePropertyTypes.TNumber, timeStampForProperty, -1, null);
                    }
                    if (opcServer.SendPicoSeconds)
                    {
                        var sProp = prop.SetProperty("sourcePicoseconds", pValue.SourcePicoseconds, ePropertyTypes.TNumber, timeStampForProperty, -1, null);
                    }
                    if (opcServer.SendServerTimestamp)
                    {
                        var sProp = prop.SetProperty("serverTimestamp", pValue.ServerTimestamp, ePropertyTypes.TDate, timeStampForProperty, -1, null);
                        if (opcServer.SendPicoSeconds)
                        {
                            var spProp = prop.SetProperty("serverPicoseconds", pValue.ServerPicoseconds, ePropertyTypes.TNumber, timeStampForProperty, -1, null);
                        }
                    }
                    if (opcServer.SendSequenceNumber && messageSequenceNumber != null)
                    {
                        var sProp = prop.SetProperty("sequenceNumber", messageSequenceNumber, ePropertyTypes.TNumber, timeStampForProperty, -1, null);
                    }
                    if (opcServer.SendOpcDataType)
                    {
                        string opcType = null;
                        TypeInfo typeInfo = null;
                        typeInfo = pValue?.WrappedValue.TypeInfo;
                        if (structTypeInfo == null)
                        {
                            if (typeInfo != null)
                            {
                                opcType = TypeInfo.GetDataTypeId(typeInfo)?.ToString(null, CultureInfo.InvariantCulture);
                            }
                        }
                        else
                        {
                            opcType = structTypeInfo.TypeNodeID.ToString();
                        }
                        if (opcType != null)
                        {
                            var typeProp = prop.GetProperty("opcDataType", false);
                            if (!string.Equals(opcType, TheCommonUtils.CStr(typeProp)))
                            {
                                typeProp = prop.SetProperty("opcDataType", opcType, ePropertyTypes.TString, timeStampForProperty, -1, null);
                                // TODO Can we make this more streamlined? 
                                var meta = prop.GetSensorMeta();
                                meta.SourceType = opcType;
                                prop.SetSensorMeta(meta);
                            }

                            if (typeInfo?.ValueRank > 0)
                            {
                                if (TheCommonUtils.CInt(typeProp?.GetProperty("ValueRank")) != typeInfo.ValueRank)
                                {
                                    typeProp?.SetProperty("ValueRank", (double)typeInfo.ValueRank, ePropertyTypes.TNumber, timeStampForProperty, -1, null);
                                }
                            }
                            if (structTypeInfo != null)
                            {
                                var structTypeInfoJson = TheCommonUtils.SerializeObjectToJSONString(structTypeInfo);
                                if (!string.IsNullOrEmpty(structTypeInfoJson) && TheCommonUtils.CStr(typeProp?.GetProperty("StructTypeInfo")) != structTypeInfoJson)
                                {
                                    typeProp?.SetProperty("StructTypeInfo", structTypeInfoJson, ePropertyTypes.TString, timeStampForProperty, -1, null);
                                }
                            }
                        }
                    }
                }


            }
            catch (Exception e)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(78301, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("TheOPCTag", $"[{opcServer.GetLogAddress()}] Internal error for: { cdeP.GetPropertyPath(property)}, {pValue.SourceTimestamp}", eMsgLevel.l1_Error, e.ToString()));
            }
            finally
            {
                property.ResetUpdater();
            }
            return newStructTypeInfo;
        }


        public string ReadTag(ISession session)
        {
            string error = null;

            try
            {
                var results = ReadTags(session, new List<string> { NodeIdName }, true);

                if (StatusCode.IsGood(results[1].StatusCode) || !StatusCode.IsBad(results[0].StatusCode))
                {
                    var value = results[0];
                    if (value.Value == null && value.WrappedValue.TypeInfo == null && StatusCode.IsGood(results[1].StatusCode))
                    {
                        value.WrappedValue = new Variant(null, new TypeInfo(Opc.Ua.TypeInfo.GetBuiltInType(results[1].Value as NodeId), -1));
                    }
                    UpdateValue(value, null);
                }
                else
                {
                    error = results[0].StatusCode.ToString();
                }
            }
            catch (Exception e)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(78301, new TSM("TheOPCTag", string.Format("[{0}] Error Reading Value : {1}", MyOPCServer.GetLogAddress(), GetNodeIdForLogs()), eMsgLevel.l1_Error, e.ToString()));
                error = e.Message;
            }
            return error;
        }

        public static DataValueCollection ReadTags(ISession session, IEnumerable<string> tagRefs, bool readTypeInfo = false)
        {
            ReadValueIdCollection nodesToRead = new ReadValueIdCollection();
            foreach (var tagRef in tagRefs)
            {
                NodeId nodeId;
                try
                {
                    var exNodeId = ExpandedNodeId.Parse(tagRef);
                    nodeId = ExpandedNodeId.ToNodeId(exNodeId, session.NamespaceUris);
                }
                catch
                {
                    nodeId = new NodeId(tagRef); // Fall back to previous behavior for compatibility
                }
                ReadValueId nodeToRead = new ReadValueId();
                //nodeToRead.NodeId = (NodeId)TagRef.NodeId;
                nodeToRead.NodeId = nodeId;
                nodeToRead.AttributeId = Attributes.Value;
                nodesToRead.Add(nodeToRead);
                if (readTypeInfo)
                {
                    nodeToRead = new ReadValueId();
                    //nodeToRead.NodeId = (NodeId)TagRef.NodeId;
                    nodeToRead.NodeId = nodeId;
                    nodeToRead.AttributeId = Attributes.DataType;
                    nodesToRead.Add(nodeToRead);
                }
                //if (readEngineeingUnitInfo)
                //{
                //    nodeToRead = new ReadValueId();
                //    //nodeToRead.NodeId = (NodeId)TagRef.NodeId;
                //    nodeToRead.NodeId = nodeId;
                //    nodeToRead.AttributeId = Attributes.;
                //    nodesToRead.Add(nodeToRead);
                //}
            }
            // read current value.
            DataValueCollection results = null;
            DiagnosticInfoCollection diagnosticInfos = null;

            session.Read(
                null,
                0,
                TimestampsToReturn.Both,
                nodesToRead,
                out results,
                out diagnosticInfos);

            ClientBase.ValidateResponse(results, nodesToRead);
            ClientBase.ValidateDiagnosticInfos(diagnosticInfos, nodesToRead);

            return results;
        }

        /// <summary>
        /// Parses the value and writes it to server. Closes the dialog if successful.
        /// </summary>
        internal bool WriteTag(DataValue pValue)
        {
            if (MyLastValue == null || MyOPCServer == null || MyOPCServer.m_session == null) return false;
            try
            {
                WriteValue valueToWrite = new WriteValue();

                //valueToWrite.NodeId = (NodeId)TagRef.NodeId;
                valueToWrite.NodeId = GetResolvedNodeIdName();
                valueToWrite.AttributeId = Attributes.Value;
                ePropertyTypes? tType;
                valueToWrite.Value.Value = ChangeType(MyLastValue, pValue, out tType);
                valueToWrite.Value.StatusCode = StatusCodes.Good;
                valueToWrite.Value.ServerTimestamp = DateTime.MinValue;
                valueToWrite.Value.SourceTimestamp = DateTime.MinValue;

                WriteValueCollection valuesToWrite = new WriteValueCollection();
                valuesToWrite.Add(valueToWrite);

                // write current value.
                StatusCodeCollection results = null;
                DiagnosticInfoCollection diagnosticInfos = null;

                MyOPCServer.m_session.Write(
                    null,
                    valuesToWrite,
                    out results,
                    out diagnosticInfos);

                ClientBase.ValidateResponse(results, valuesToWrite);
                ClientBase.ValidateDiagnosticInfos(diagnosticInfos, valuesToWrite);

                if (StatusCode.IsBad(results[0]))
                {
                    TheThing.SetSafePropertyString(MyBaseThing, "LastMessage", string.Format("Bad during Write, reason: {0}", results[0]));
                    return false;
                }
            }
            catch (Exception e)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(78302, new TSM(MyBaseThing != null ? MyBaseThing.EngineName : "UnknownEngine", string.Format("[{0}] Error Writing Value : {1}", MyOPCServer.GetLogAddress(), GetNodeIdForLogs()), eMsgLevel.l1_Error, e.ToString()));
                return false;
            }
            return true;
        }

        internal override bool RefersToSamePropertyAndTag(object monitoredItemHandle)
        {
            if (!(monitoredItemHandle is TheOPCTag))
            {
                return false;
            }
            var opcTag = monitoredItemHandle as TheOPCTag;
            return NodeIdName == opcTag.NodeIdName &&
                    (
                    (opcTag.TargetProperty?.Name == TargetProperty?.Name && IsSubscribedAsProperty)
                    || (IsSubscribedAsThing && opcTag.HostThingMID == HostThingMID));
        }

        //private class Times
        //{
        //    public int GrossRunTime;
        //    public int NetProcessingTime;
        //    public int NetRunTime;
        //}

        #region Private Methods

        #endregion
    }

    enum BinaryEncodedTypes
    {
        CharArray = 100,
        WideCharArray = 101,
        WideString = 102,
    }

    static class OpcDecoderHelpers
    {
        static public object ReadValue(this IDecoder decoder, BuiltInType builtInType, BinaryEncodedTypes encodingHint, string fieldName)
        {
            object retval;
            switch (builtInType)
            {
                case BuiltInType.Null:
                    {
                        retval = null;
                        break;
                    }

                case BuiltInType.Boolean:
                    {
                        retval = decoder.ReadBoolean(fieldName);
                        break;
                    }

                case BuiltInType.SByte:
                    {
                        retval = decoder.ReadSByte(fieldName);
                        break;
                    }

                case BuiltInType.Byte:
                    {
                        retval = decoder.ReadByte(fieldName);
                        break;
                    }

                case BuiltInType.Int16:
                    {
                        retval = decoder.ReadInt16(fieldName);
                        break;
                    }

                case BuiltInType.UInt16:
                    {
                        retval = decoder.ReadUInt16(fieldName);
                        break;
                    }

                case BuiltInType.Int32:
                case BuiltInType.Enumeration:
                    {
                        retval = decoder.ReadInt32(fieldName);
                        break;
                    }

                case BuiltInType.UInt32:
                    {
                        retval = decoder.ReadUInt32(fieldName);
                        break;
                    }

                case BuiltInType.Int64:
                    {
                        retval = decoder.ReadInt64(fieldName);
                        break;
                    }

                case BuiltInType.UInt64:
                    {
                        retval = decoder.ReadUInt64(fieldName);
                        break;
                    }

                case BuiltInType.Float:
                    {
                        retval = decoder.ReadFloat(fieldName);
                        break;
                    }

                case BuiltInType.Double:
                    {
                        retval = decoder.ReadDouble(fieldName);
                        break;
                    }

                case BuiltInType.String:
                    {
                        if (encodingHint == BinaryEncodedTypes.CharArray)
                        {
                            // A UTF-8 string prefixed by its length in characters.
                            var bytes = decoder.ReadByteString(fieldName);
                            retval = Encoding.UTF8.GetString(bytes);
                        }
                        else if (encodingHint == BinaryEncodedTypes.WideCharArray)
                        {
                            // A UTF-16 string prefixed by its length in characters.
                            var lengthInChars = decoder.ReadInt32(fieldName);
                            if (lengthInChars >= 0)
                            {
                                byte[] bytes = new byte[lengthInChars];
                                for (int i = 0; i < lengthInChars; i++)
                                {
                                    bytes[i] = decoder.ReadByte(fieldName);
                                }
                                retval = new UnicodeEncoding().GetString(bytes);
                            }
                            else
                            {
                                retval = "";
                            }
                        }
                        else if (encodingHint == BinaryEncodedTypes.WideString)
                        {
                            // A UTF-16 null terminated string value.
                            byte[] bytes = new byte[100];
                            int i = 0;
                            do
                            {
                                var byte1 = decoder.ReadByte(fieldName);
                                var byte2 = decoder.ReadByte(fieldName);
                                if (byte1 == 0 && byte2 == 0)
                                {
                                    break;
                                }
                                if (i + 1 >= bytes.Length)
                                {
                                    var bytes2 = new Byte[bytes.Length + 100];
                                    bytes2.CopyTo(bytes, 0);
                                    bytes = bytes2;
                                }
                                bytes[i] = byte1;
                                bytes[i + 1] = byte2;
                                i += 2;
                            } while (true); // TODO check against max array limits etc.
                            retval = new UnicodeEncoding().GetString(bytes, 0, i);
                        }
                        else
                        {
                            retval = decoder.ReadString(fieldName);
                        }
                        break;
                    }

                case BuiltInType.DateTime:
                    {
                        retval = decoder.ReadDateTime(fieldName);
                        break;
                    }

                case BuiltInType.Guid:
                    {
                        retval = decoder.ReadGuid(fieldName);
                        break;
                    }

                case BuiltInType.ByteString:
                    {
                        retval = decoder.ReadByteString(fieldName);
                        break;
                    }

                case BuiltInType.XmlElement:
                    {
                        retval = decoder.ReadXmlElement(fieldName);
                        break;
                    }

                case BuiltInType.NodeId:
                    {
                        retval = decoder.ReadNodeId(fieldName);
                        break;
                    }

                case BuiltInType.ExpandedNodeId:
                    {
                        retval = decoder.ReadExpandedNodeId(fieldName);
                        break;
                    }

                case BuiltInType.StatusCode:
                    {
                        retval = decoder.ReadStatusCode(fieldName);
                        break;
                    }

                case BuiltInType.QualifiedName:
                    {
                        retval = decoder.ReadQualifiedName(fieldName);
                        break;
                    }

                case BuiltInType.LocalizedText:
                    {
                        retval = decoder.ReadLocalizedText(fieldName);
                        break;
                    }

                case BuiltInType.ExtensionObject:
                    {
                        retval = decoder.ReadExtensionObject(fieldName);
                        break;
                    }

                case BuiltInType.DataValue:
                    {
                        retval = decoder.ReadDataValue(fieldName);
                        break;
                    }

                default:
                    {
                        throw new ServiceResultException(
                            StatusCodes.BadDecodingError,
                            Utils.Format("Cannot decode unknown type in extension (0x{0:X2}).", builtInType));
                    }
            }
            return retval;
        }

    }

    partial class TheOPCUARemoteServer
    { 
        public string DecodeExtensionObjectToJson(object extensionOrExtensionArray, out ExtensionTypeInfo structTypeInfo)
        {
            ExtensionObject[] extensionObjects;
            
            var structData = new Dictionary<string,object>();
            List<Dictionary<string, object>> arrayOfStructsData = null;
            bool IsArray;
            if (extensionOrExtensionArray is ExtensionObject)
            {
                IsArray = false;
                extensionObjects = new ExtensionObject[] { extensionOrExtensionArray as ExtensionObject };
            }
            else
            {
                IsArray = true;
                extensionObjects = extensionOrExtensionArray as ExtensionObject[];
                arrayOfStructsData = new List<Dictionary<string, object>>();
            }

            if (extensionObjects.Length == 0)
            {
                structTypeInfo = null;
                return "";
            }
            NodeId typeNodeId = ExpandedNodeId.ToNodeId(extensionObjects[0].TypeId, m_session.NamespaceUris);
            //bool bIsTypeNodeId = true;
            //if (typeNodeId.IsNullNodeId) // TODO figure out why type node browsing doesn't work
            //{
            //    typeNodeId = this.TagRef;
            //    bIsTypeNodeId = false;
            //}

            var structTypeInfoInternal = GetOPCStructTypeInfo(typeNodeId);
            if (structTypeInfoInternal == null)
            {
                structTypeInfo = null;
                return "";
            }

            structTypeInfo = new ExtensionTypeInfo
            {
                IsArray = IsArray,
                Members = structTypeInfoInternal.Members,
                TypeNodeID = structTypeInfoInternal.TypeNodeID,
            };

            foreach (var extension in extensionObjects)
            {
                NodeId typeNodeId2 = ExpandedNodeId.ToNodeId(extensionObjects[0].TypeId, m_session.NamespaceUris);
                if (typeNodeId != typeNodeId2) // Only arrays of the same struct type are supported
                {
                    structTypeInfo = null;
                    return "";
                }
                IDecoder decoder = null;
                if (extension.Encoding == ExtensionObjectEncoding.Binary)
                {
                    decoder = new BinaryDecoder(extension.Body as byte[], m_session.MessageContext); // TODO Get a message context ?
                }
                else if (extension.Encoding == ExtensionObjectEncoding.Xml)
                {
                    decoder = new XmlDecoder(extension.Body as System.Xml.XmlElement, m_session.MessageContext);
                }
                else if (extension.Encoding == ExtensionObjectEncoding.EncodeableObject)
                {
                    var json = TheCommonUtils.SerializeObjectToJSONString(extension.Body);
                    if (extensionObjects.Count() == 1)
                    {
                        return json;
                    }
                    else
                    {
                        // inefficient but correct: may need to optimize in the future
                        structData = TheCommonUtils.DeserializeJSONStringToObject<Dictionary<string, object>>(json);
                    }
                    decoder = null;
                }
                if (decoder != null)
                {
                    foreach (StructMemberInfoInternal member in structTypeInfoInternal.Members)
                    {
                        var opcValue = decoder.ReadValue((BuiltInType)member.opcT, member.EncodingHint, member.Name);
                        ePropertyTypes? pType;
                        var opcValueVariant = opcValue is Variant ? (Variant)opcValue : new Variant(opcValue);
                        var cdeValue = TheOPCTag.ChangeType((BuiltInType)member.opcT, opcValueVariant, out pType);

                        // CODE REVIEW: Using a cdeP here would be nice, but introduces a lot of overhead (cdeN, cdeO etc. are always the same for each struct member). Need to revisit this once we do nested Things etc.
                        //var structValue = new StructMemberValue
                        //{
                        //    Name = member.DisplayName,
                        //    Value = cdeValue,
                        //    cdeT = (int) pType,
                        //    opcT = (int) member.OpcBuiltInType,
                        //};
                        structData.Add(member.Name, cdeValue);
                    }
                }
                if (arrayOfStructsData != null)
                {
                    arrayOfStructsData.Add(structData);
                    structData = new Dictionary<string, object>();
                }
            }
            string extensionAsJson;
            if (arrayOfStructsData == null)
            {
                extensionAsJson = TheCommonUtils.SerializeObjectToJSONString(structData);
            }
            else
            {
                extensionAsJson = TheCommonUtils.SerializeObjectToJSONString(arrayOfStructsData);
            }
            return extensionAsJson;
        }

        public class StructMemberInfo
        {
            public string Name;
            public int cdeT;
            public int opcT;
            public string TypeName;
        }
        private class StructMemberInfoInternal : StructMemberInfo
        {
            public BinaryEncodedTypes EncodingHint;
        }

        public class ExtensionTypeInfo
        {
            public bool IsArray;
            public NodeId TypeNodeID;
            public List<StructMemberInfo> Members = null;
        }

        public class StructTypeInfo
        {
            public NodeId TypeNodeID;
            public List<StructMemberInfo> Members = null;
        }

        cdeConcurrentDictionary<NodeId, StructTypeInfo> _structTypeInfoCache = new cdeConcurrentDictionary<NodeId, StructTypeInfo>();

        internal StructTypeInfo GetOPCStructTypeInfo(NodeId typeNodeId)
        {
            StructTypeInfo structTypeInfo;
            if (!_structTypeInfoCache.TryGetValue(typeNodeId, out structTypeInfo))
            {
                //NodeId typeNodeId;
                //ReferenceDescriptionCollection typeDefinitions = null;
                //if (!isTypeNodeId)
                //{

                //    BrowseDescriptionCollection typeReferenceNodesToBrowse = new BrowseDescriptionCollection();
                //    {
                //        NodeId[] structBrowseReferenceTypeIds = new NodeId[] { Opc.Ua.ReferenceTypeIds.HasTypeDefinition/*, ReferenceTypeIds.HasComponent*/ };

                //        for (int ii = 0; ii < structBrowseReferenceTypeIds.Length; ii++)
                //        {
                //            BrowseDescription nodeToBrowse = new BrowseDescription();
                //            nodeToBrowse.NodeId = typeOrValueNodeId;
                //            nodeToBrowse.BrowseDirection = BrowseDirection.Forward;
                //            nodeToBrowse.ReferenceTypeId = structBrowseReferenceTypeIds[ii];
                //            nodeToBrowse.IncludeSubtypes = true;
                //            nodeToBrowse.NodeClassMask = 0; // 2; //was 0
                //            nodeToBrowse.ResultMask = (uint)BrowseResultMask.All;
                //            typeReferenceNodesToBrowse.Add(nodeToBrowse);
                //        }
                //    }

                //    typeDefinitions = ClientUtils.Browse(MyOPCServer.m_session, typeReferenceNodesToBrowse, false);
                //    if (typeDefinitions == null || typeDefinitions.Count != 1 || typeDefinitions[0].NodeId == Opc.Ua.VariableTypes.BaseDataVariableType)
                //    {
                //        var nodeAttributes = MyOPCServer.ReadAttributes(typeOrValueNodeId, true, false);
                //        var dataTypeAttr = nodeAttributes.Find((a) => a.BrowseName == "DataType");
                //        if (dataTypeAttr != null && dataTypeAttr.Value != null && dataTypeAttr.Value.Value != null)
                //        {
                //            typeNodeId = dataTypeAttr.Value.Value as NodeId;
                //        }
                //        else
                //        {
                //            return null; // TODO cache the failure? Log it?
                //        }
                //    }
                //    else
                //    {
                //        typeNodeId = ExpandedNodeId.ToNodeId(typeDefinitions[0].NodeId, MyOPCServer.m_session.NamespaceUris);
                //    }
                //}
                //else
                //{
                //    typeNodeId = typeOrValueNodeId;
                //}

                //var typeInfo = MyOPCServer.m_session.ReadNode(typeNodeId);

                ////var attr = structTypeAttributes.Find((a) => a.BrowseName == "ValueRank");
                ////if (attr != null && attr.Value != null && attr.Value.Value != null && attr.Value.Value is int)
                ////{
                ////    structValueRank = (int)(attr.Value.Value);
                ////}

                ////{
                ////    var dataTypeAttr = structTypeAttributes.Find((a) => a.BrowseName == "DataType");
                ////    if (dataTypeAttr != null && dataTypeAttr.Value != null && dataTypeAttr.Value.Value != null)
                ////    {
                ////        typeNodeId = dataTypeAttr.Value.Value as NodeId;
                ////    }
                ////}

                //BrowseDescriptionCollection nodesToBrowse = new BrowseDescriptionCollection();
                //NodeId[] encodingBrowseReferenceTypeIds = new NodeId[] { ReferenceTypeIds.HasEncoding };//.HasComponent, ReferenceTypeIds.HasTypeDefinition, ReferenceTypeIds.HierarchicalReferences, ReferenceTypeIds.HasProperty, ReferenceTypeIds.NonHierarchicalReferences };

                //for (int ii = 0; ii < encodingBrowseReferenceTypeIds.Length; ii++)
                //{
                //    BrowseDescription nodeToBrowse = new BrowseDescription();
                //    nodeToBrowse.NodeId = typeNodeId;
                //    nodeToBrowse.BrowseDirection = BrowseDirection.Forward;
                //    nodeToBrowse.ReferenceTypeId = encodingBrowseReferenceTypeIds[ii];
                //    nodeToBrowse.IncludeSubtypes = true;
                //    nodeToBrowse.NodeClassMask = 0; // 2; //was 0
                //    nodeToBrowse.ResultMask = (uint)BrowseResultMask.All;
                //    nodesToBrowse.Add(nodeToBrowse);
                //}

                //ReferenceDescriptionCollection encodings = ClientUtils.Browse(MyOPCServer.m_session, nodesToBrowse, false);
                //if (encodings == null)
                //{
                //    return null; // TODO log error? Cache ?
                //}
                //var binaryEncoding = encodings.Find((e) => e.BrowseName.NamespaceIndex == 0 && e.BrowseName.Name == "Default Binary");
                //if (binaryEncoding == null)
                //{
                //    return null; // TODO log / cache
                //}
                //ReferenceDescriptionCollection descriptions = ClientUtils.Browse(MyOPCServer.m_session, new BrowseDescriptionCollection { new BrowseDescription { NodeId = (NodeId) binaryEncoding.NodeId } }, false);

                ReferenceDescription description;
                DataDictionary dataDictionary;
                try
                {
                    description = m_session.FindDataDescription(typeNodeId);
                    //var description = MyOPCServer.m_session.FindDataDescription((NodeId)binaryEncoding.NodeId);

                    ////var descriptionNode = MyOPCServer.m_session.ReadNode((NodeId) description.NodeId);

                    // This does all we need, jsut doesn't expose the validator
                    //var dataDictionary = MyOPCServer.m_session.FindDataDictionary(typeOrValueNodeId);
                    dataDictionary = m_session.FindDataDictionary((NodeId)description.NodeId).Result;
                }
                catch
                {
                    return null;
                }

                //var x = dataDictionary.GetSchema((NodeId) description.NodeId);


//var dictionaryDV = MyOPCServer.m_session.ReadValue(dataDictionary.DictionaryId);
//var dictionaryBytes = dictionaryDV.Value as byte[];
//var dictionaryXML = Encoding.UTF8.GetString(dictionaryBytes);
//BinarySchemaValidator bs = null;
//try
//{
//    bs = new BinarySchemaValidator();
//    bs.Validate(new System.IO.MemoryStream(dictionaryBytes));
//}
//catch
//{
//}

#if OLD_UA
                //CM: Not in NUGET Code
                var bs = dataDictionary.Validator as BinarySchemaValidator;

                if (bs == null)
                {
                    return null;
                }
                var typeDescription = bs.ValidatedDescriptions.FirstOrDefault((t) => t.Name == description.BrowseName.Name) as StructuredType;
#else
                StructuredType typeDescription = null;
#endif

                var structMembers = new List<StructMemberInfo>(); ;
                if (typeDescription != null)
                {
                    foreach (var field in typeDescription.Field)
                    {
                        BinaryEncodedTypes encodingHint;
                        var builtInType = GetOpcTypeFromUASchemaType(field.TypeName.Name, out encodingHint);
                        if (builtInType == null)
                        {
                            return null;
                        }
                        structMembers.Add(new StructMemberInfoInternal { Name = field.Name, cdeT = (int) TheOPCTag.GetCDETypeFromOPCType((BuiltInType) builtInType), TypeName = field.TypeName.ToString(), opcT = (int) (BuiltInType) builtInType, EncodingHint = encodingHint });
                    }
                }

                //var properties = ClientUtils.Browse(MyOPCServer.m_session, new BrowseDescriptionCollection { new BrowseDescription { NodeId = (NodeId)description.NodeId, ReferenceTypeId = ReferenceTypeIds.HasProperty } }, false);

                //var x = MyOPCServer.ReadAttributes((NodeId)description.NodeId, true, false);



                //var encoding = MyOPCServer.m_session.ReadNode((NodeId) binaryEncoding.NodeId);


                //foreach (var structMemberNode in structMemberNodes)
                //{
                //    var memberAttributes = MyOPCServer.ReadAttributes((NodeId)structMemberNode.NodeId, false, false);
                //    var displayName = memberAttributes.Find((a) => a.BrowseName == "DisplayName").Value.ToString();
                //    var dataType = (BuiltInType)TheCommonUtils.CInt(((Opc.Ua.NodeId)(memberAttributes.Find((a) => a.BrowseName == "DataType").Value.Value)).Identifier);
                //    var valueRank = (int) (memberAttributes.Find((a) => a.BrowseName == "ValueRank").Value.Value);
                //    structMembers.Add(new StructMemberInfo { DisplayName = displayName, OpcBuiltInType = dataType, ValueRank = valueRank });
                //}
                structTypeInfo = new StructTypeInfo { TypeNodeID = typeNodeId,  Members = structMembers };
                _structTypeInfoCache.TryAdd(typeNodeId, structTypeInfo); // Cached data is invariant: doesn't matter if this was already added
            }
            return structTypeInfo;
        }

        static BuiltInType? GetOpcTypeFromUASchemaType(string typeName, out BinaryEncodedTypes encodingHint)
        {
            encodingHint = 0;
            BuiltInType? builtInType = BuiltInType.String;
            switch (typeName)
            {
                case "Bit":
                    builtInType = BuiltInType.Boolean; // TODO There is no Bit type defined for the BuiltInType enumeration -> verify that it is actually encoded as 8 bits
                    break;
                case "Boolean":
                    builtInType = BuiltInType.Boolean;
                    break;
                case "SByte":
                    builtInType = BuiltInType.SByte;
                    break;
                case "Byte":
                    builtInType = BuiltInType.Byte;
                    break;
                case "Int16":
                    builtInType = BuiltInType.Int16;
                    break;
                case "Int32":
                    builtInType = BuiltInType.Int32;
                    break;
                case "UInt32":
                    builtInType = BuiltInType.UInt32;
                    break;
                case "Int64":
                    builtInType = BuiltInType.Int64;
                    break;
                case "UInt64":
                    builtInType = BuiltInType.UInt64;
                    break;
                case "Float":
                    builtInType = BuiltInType.Float;
                    break;
                case "Double":
                    builtInType = BuiltInType.Double;
                    break;
                case "Char":
                    builtInType = BuiltInType.Byte; // TODO No BuiltInType.Char!? Will still decode consistently but lose the type distinction
                    break;
                case "String":
                    builtInType = BuiltInType.String;
                    break;
                case "CharArray":
                    builtInType = BuiltInType.String;
                    encodingHint = BinaryEncodedTypes.CharArray; 
                    break;
                case "WideChar":
                    builtInType = BuiltInType.UInt16; // // TODO No BuiltInType.WideChar!? Will still decode consistently but lose the type distinction
                    break;
                case "WideString":
                    builtInType = BuiltInType.String;
                    encodingHint = BinaryEncodedTypes.WideString;
                    break;
                case "WideCharArray":
                    builtInType =  BuiltInType.String; 
                    encodingHint = BinaryEncodedTypes.WideCharArray;
                    break;
                case "ByteString":
                    builtInType = BuiltInType.ByteString;
                    break;
                case "DateTime":
                    builtInType = BuiltInType.DateTime;
                    break;
                case "Guid":
                    builtInType = BuiltInType.Guid;
                    break;
                case "LocalizedText":
                    builtInType = BuiltInType.LocalizedText;
                    break;
                default:
                    builtInType = null;
                    break;
            }
            return builtInType;
        }


    }

}
