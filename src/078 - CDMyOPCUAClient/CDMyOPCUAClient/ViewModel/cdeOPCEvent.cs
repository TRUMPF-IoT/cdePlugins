// SPDX-FileCopyrightText: 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0
using nsCDEngine.BaseClasses;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.ViewModels;
using Opc.Ua;
using System;
using System.Collections.Generic;
using System.Linq;
using Opc.Ua.Client;
using Opc.Ua.Client.Controls;
using CDMyOPCUAClient.Contracts;

namespace CDMyOPCUAClient.ViewModel
{
    public class TheOPCEvent: TheOPCMonitoredItemBase
    {
        readonly List<string> requiredEventFields = new List<string> { BrowseNames.EventId, BrowseNames.EventType, BrowseNames.Time, BrowseNames.Retain };

        MonitoringFilter GetFilter()
        {
            aliasMap.Clear();
            var eventFilter = new EventFilter();
            eventFilter.AddSelectClause(Opc.Ua.ObjectTypeIds.BaseEventType, null, Attributes.NodeId); // Requests condition id

            // Ensure all required properties are in the event filter
            var propsToRequest = new List<TheEventProperty>(EventInfo.Properties);
            foreach (var prop in requiredEventFields)
            {
                if (EventInfo.Properties.FirstOrDefault(p => p.Name == prop && string.IsNullOrEmpty(p.CustomTypeId)) == null)
                {
                    propsToRequest.Add(new TheEventProperty { Name = prop });
                }
            }

            // Remove ConditionId, because it is added by requesting the BaseEventType attibute 1 above
            var conditionIdProp = EventInfo.Properties.FirstOrDefault(p => p.Name == "ConditionId" && string.IsNullOrEmpty(p.CustomTypeId));
            if (conditionIdProp != null)
            {
                propsToRequest.Remove(conditionIdProp);
            }

            var alarmConditionProps = typeof(AlarmConditionState).GetProperties();

            int index = eventFilter.SelectClauses.Count;
            foreach (var eventProp in propsToRequest)
            {
                if (eventProp.Name == "ConditionId" && string.IsNullOrEmpty(eventProp.CustomTypeId))
                {
                    continue;
                }
                var propertyType = Opc.Ua.ObjectTypeIds.BaseEventType;
                ushort nameSpaceId = 0;

                var propDef = alarmConditionProps.FirstOrDefault(p => p.Name == eventProp.Name && string.IsNullOrEmpty(eventProp.CustomTypeId));
                if (propDef != null)
                {
                    switch(propDef.DeclaringType.Name)
                    {
                        case nameof(AlarmConditionState):
                            propertyType = Opc.Ua.ObjectTypeIds.AlarmConditionType;
                            break;
                        case nameof(AcknowledgeableConditionState):
                            propertyType = Opc.Ua.ObjectTypeIds.AcknowledgeableConditionType;
                            break;
                        case nameof(ConditionState):
                            propertyType = Opc.Ua.ObjectTypeIds.ConditionType;
                            break;
                        case nameof(BaseEventState):
                        default:
                            propertyType = Opc.Ua.ObjectTypeIds.BaseEventType;
                            break;
                    }
                }
                else
                {
                    if (!string.IsNullOrEmpty(eventProp.CustomTypeId))
                    {
                        // Custom Type: must be specified in the request
                        var exNodeId = ExpandedNodeId.Parse(eventProp.CustomTypeId);
                        propertyType = ExpandedNodeId.ToNodeId(exNodeId, MyOPCServer?.m_session?.NamespaceUris);
                        nameSpaceId = propertyType.NamespaceIndex;
                    }
                    else
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(78122, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyOPCServer.GetBaseThing().EngineName, $"Unrecognized Event Property Name '{eventProp}' for {GetNodeIdForLogs()}. Use <nodeid>::<name> for custom types.", eMsgLevel.l2_Warning));
                    }
                }

                QualifiedName eventPropQName;
                var indexOfNamespaceSeparator = eventProp.Name.LastIndexOf(':'); // namespace Uri typically contains ":" so must use last index instead of Split
                if (indexOfNamespaceSeparator > 0)
                {
                    var namespacePrefix = eventProp.Name.Substring(0, indexOfNamespaceSeparator);
                    var name = eventProp.Name.Substring(indexOfNamespaceSeparator + 1);
                    if (!char.IsDigit(namespacePrefix[0]))
                    {
                        eventPropQName = QualifiedName.Create(name, namespacePrefix, MyOPCServer?.m_session?.NamespaceUris);
                    }
                    else
                    {
                        eventPropQName = QualifiedName.Parse(eventProp.Name);
                    }
                }
                else
                {
                    eventPropQName = new QualifiedName(eventProp.Name, nameSpaceId);
                }
                eventFilter.AddSelectClause(propertyType, eventPropQName);
                if (!string.IsNullOrEmpty(eventProp.Alias))
                {
                    aliasMap.Add(index, eventProp.Alias);
                }
                index++;
            }

            // Incomplete/untested, syntax is very cumbersome. Leaving in for now as a starting point should this become a high priority
            //if (EventInfo.WhereClauses != null)
            //{
            //    try
            //    {
            //        var whereClause = new ContentFilter();
            //        var filterElements = new ContentFilterElementCollection(EventInfo.WhereClauses.Select((clause) =>
            //        {
            //            FilterOperator filterOperator;
            //            try
            //            {
            //                filterOperator = (FilterOperator)Enum.Parse(typeof(FilterOperator), clause.Operator);
            //            }
            //            catch
            //            {
            //                filterOperator = (FilterOperator)(-1); // invalid operator: assume that it will be rejected by server or client stack
            //            }

            //            var operands = clause.Operands.Select<TheFilterOperand, ExtensionObject>(operand =>
            //            {
            //                if (operand.Value != null)
            //                {
            //                    return new ExtensionObject(new LiteralOperand(operand.Value));
            //                }
            //                if (operand.SimpleAttribute != null)
            //                {
            //                    return new ExtensionObject(new SimpleAttributeOperand
            //                    {
            //                        AttributeId = operand.SimpleAttribute.AttributeId ?? Opc.Ua.Attributes.Value, // TODO support symbolic names
            //                        BrowsePath = new QualifiedNameCollection(operand.SimpleAttribute.BrowsePaths.Select(p => new QualifiedName(p))), // TODO support namespace qualifiers etc.
            //                        TypeDefinitionId = ExpandedNodeId.ToNodeId(ExpandedNodeId.Parse(operand.SimpleAttribute.TypeId), nameTable),
            //                        // TODO Why is SimpleAttributeOperand.IndexRange a string? Should be a Range. Not supported for now
            //                    });
            //                }
            //                //if (operand.Attribute != null)
            //                //{
            //                //    return new ExtensionObject(new AttributeOperand
            //                //    {
            //                //        // TODO implement, not supported for now
            //                //           //AttributeId = operand.Attribute.AttributeId, 
            //                //           // BrowsePath = operand.Attribute.BrowsePath,
            //                //    });
            //                //}
            //                return null;
            //            });

            //            var element = new ContentFilterElement
            //            {
            //                FilterOperator = filterOperator,
            //                FilterOperands = new ExtensionObjectCollection(operands),
            //            };
            //            return element;
            //        }));

            //        eventFilter.WhereClause = new ContentFilter { Elements = filterElements, };
            //    }
            //    catch (Exception e)
            //    {
            //        // Log errors, fail filter etc.
            //    }
            //}

            // Original approach relied on server providing proper type information for the events; not all OPC Servers do so
            //NodeId eventTypeId = this.TagRef;
            //var desc = new BrowseDescription
            //{
            //    NodeId = this.TagRef,
            //    ReferenceTypeId = Opc.Ua.ReferenceTypeIds.GeneratesEvent,
            //    ResultMask = (uint)BrowseResultMask.TargetInfo,
            //};
            //var references = ClientUtils.Browse(MyOPCServer.m_session, desc, false);
            //if (references.Count > 0)
            //{
            //    eventTypeId = (NodeId)references[0].NodeId;
            //}
            //TypeDeclaration type = new TypeDeclaration();
            //type.NodeId = eventTypeId;
            //type.Declarations = ClientUtils.CollectInstanceDeclarationsForType(MyOPCServer.m_session, type.NodeId);

            //// the filter to use.
            //var filter = new FilterDeclaration(type, null);
            //var eventFilter2 = filter.GetFilter();
            //eventFilter2.WhereClause = null;

            return eventFilter;
        }
        Dictionary<int, string> aliasMap = new Dictionary<int, string>();

        //public List<string> Properties { get; set; }
        internal TheEventSubscription EventInfo { get; set; }

        public TheOPCEvent()
        {
        }

        internal override string RegisterInHostThing()
        {
            string error = null;

            string propertyName = DisplayName;
            TheThing targetThing = null;

            if (this.IsSubscribedAsThing)
            {
                // Dedicated event thing
                targetThing = MyBaseThing;
            }
            if (this.IsSubscribedAsProperty)
            {
                // This is an external, multi-event thing: use the browsepath to avoid collisions with properties from multiple events
                targetThing = this.GetHostThing().GetBaseThing();
            }
            if (targetThing != null)
            {
                var prop = targetThing.GetProperty(propertyName, true);
                var sensorMeta = prop.GetSensorMeta();
                prop = targetThing.DeclareSensorProperty(propertyName, ePropertyTypes.NOCHANGE, sensorMeta);
                if (prop != null)
                {
                    var opcSubscriptionInfo = new TheOPCSensorSubscription
                    {
                        SampleRate = this.SampleRate,
                        SensorId = this.NodeIdName,
                        ChangeTrigger = this.ChangeTrigger,
                        DeadbandValue = this.DeadbandFilterValue,
                        EventInfo = this.EventInfo,
                    };

                    var providerInfo = new cdeP.TheProviderInfo(this.MyOPCServer.GetBaseThing(), opcSubscriptionInfo);
                    prop.SetSensorProviderInfo(providerInfo);
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
            return error;
        }

        public override bool MonitorTag(Subscription subscription, out string error, bool bApplySubscription = true, bool bReadInitialValue = true)
        {
            if (base.MonitorTag(subscription, out error, bApplySubscription))
            {
                if (this.IsSubscribedAsProperty || this.IsSubscribedAsThing)
                {
                    if (bApplySubscription && EventInfo.AggregateRetainedConditions)
                    {
                        TheOPCEvent.ConditionRefresh(subscription, MyOPCServer);
                    }
                }
                return true;
            }
            return false;
        }

        public static void ConditionRefresh(Subscription subscription, TheOPCUARemoteServer serverForLogging)
        {
            bool refreshNeeded = false;
            foreach (var monitoredItem in subscription.MonitoredItems)
            {
                if (monitoredItem.Handle is TheOPCEvent eventTag)
                {
                    if (eventTag.RefreshNeeded)
                    {
                        refreshNeeded = true;
                        eventTag.RefreshNeeded = false;
                        TheBaseAssets.MySYSLOG.WriteToLog(78102, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(serverForLogging.GetBaseThing()?.EngineName, $"Will trigger refresh for {serverForLogging.GetLogAddress()}, {eventTag.GetInfoForLog()}", eMsgLevel.l6_Debug));
                    }
                    else
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(78102, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(serverForLogging.GetBaseThing()?.EngineName, $"Skipping condition refresh for {serverForLogging.GetLogAddress()}, {eventTag.GetInfoForLog()}", eMsgLevel.l6_Debug));
                    }
                }
            }
            if (refreshNeeded)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(78102, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(serverForLogging.GetBaseThing()?.EngineName, $"Refreshing conditions for {serverForLogging.GetLogAddress()}", eMsgLevel.l6_Debug));
                try
                {
                    subscription.ConditionRefresh();
                    TheBaseAssets.MySYSLOG.WriteToLog(78102, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(serverForLogging.GetBaseThing()?.EngineName, $"Requested condition refresh for {serverForLogging.GetLogAddress()}", eMsgLevel.l6_Debug));
                }
                catch (Exception e)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(78102, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(serverForLogging.GetBaseThing()?.EngineName, $"Internal error requesting condition request for {serverForLogging.GetLogAddress()}", eMsgLevel.l1_Error, TSM.L(eDEBUG_LEVELS.VERBOSE) ? e.Message : e.ToString()));
                }
            }
        }

        private string GetInfoForLog()
        {
            return $"{DisplayName} {GetNodeIdForLogs()} {EventInfo.AggregateRetainedConditions} - {MyMonitoredItem}";
        }

        //public bool RegisterOrUnregisterThingForOpcEvent(TheOPCUARemoteServer server)
        //{
        //    var tEventHost = this.GetThing();
        //    if (tEventHost == null)
        //    {
        //        return false;
        //    }
        //    if (this.m_monitoredItem != null && this.m_monitoredItem.Handle != tEventHost)
        //    {
        //        UnregisterThingForOpcEvents(server);
        //    }

        //    return RegisterThingForOpcEvents(server);
        //}


        //void UnregisterThingForOpcEvents(TheOPCUARemoteServer server)
        //{
        //    var tEventHost = this.m_monitoredItem.Handle as ICDEThing;

        //    if (tEventHost != null)
        //    {
        //        // Stop the monitored item from updating the host thing
        //        this.m_monitoredItem.Handle = null;

        //        // Remove any properties from the host thing
        //        var cdeMValue = TheOPCMonitoredItemBase.GetOpcUaMetaString(server, this.EventTypeId);
        //        foreach (var cdeProp in tEventHost.GetBaseThing().GetPropertiesMetaStartingWith("OPCUA"))
        //        {
        //            if (cdeProp.cdeM == cdeMValue)
        //            {
        //                cdeProp.cdeM = null;
        //                tEventHost.GetBaseThing().RemoveProperty(cdeProp.Name);
        //            }
        //        }
        //    }

        //    // Remove the monitored item
        //    try
        //    {
        //        if (this.m_monitoredItem != null && this.m_monitoredItem.Subscription != null)
        //        {
        //            this.m_monitoredItem.Subscription.RemoveItem(this.m_monitoredItem);
        //            this.m_monitoredItem.Subscription.ApplyChanges();
        //            this.m_monitoredItem = null;
        //        }
        //    }
        //    catch (Exception e)
        //    {
        //        // Not much we can do if this fails other than leak the subscription until the next restart?
        //    }
        //}

        //bool RegisterThingForOpcEvents(TheOPCUARemoteServer server)
        //{
        //    var tEventHost = this.GetThing();
        //    if (tEventHost == null)
        //    {
        //        // TODO: log
        //        return false; // throw new Exception("Invalid host thing");
        //    }

        //    var subscription = server.Subscription;
        //    if (subscription != null)
        //    {
        //        // TODO: log
        //        return false; // throw new Exception("No Subscription");
        //    }
        //    var session = subscription.Session;
        //    if (session != null)
        //    {
        //        return false; // throw new Exception("No Session");
        //    }

        //    try
        //    {
        //        TypeDeclaration type = new TypeDeclaration();
        //        type.NodeId = ExpandedNodeId.ToNodeId(this.EventTypeId, session.NamespaceUris);
        //        type.Declarations = ClientUtils.CollectInstanceDeclarationsForType(session, type.NodeId);

        //        // the filter to use.
        //        var filter = new FilterDeclaration(type, null);

        //        // create a monitored item based on the current filter settings.            
        //        var monitoredItem = new MonitoredItem();
        //        monitoredItem.StartNodeId = Opc.Ua.ObjectIds.Server;
        //        monitoredItem.AttributeId = Attributes.EventNotifier;
        //        monitoredItem.SamplingInterval = 0;
        //        monitoredItem.QueueSize = 1000;
        //        monitoredItem.DiscardOldest = true;
        //        monitoredItem.Filter = filter.GetFilter();
        //        monitoredItem.Handle = tEventHost; // We use this to detect changes in the host thing, so we can unregister properties in that old host thing

        //        // set up callback for notifications.
        //        monitoredItem.Notification += MonitoredItem_Notification;

        //        //CreateSubscription(null, null);
        //        subscription.AddItem(monitoredItem);
        //        subscription.ApplyChanges();

        //        this.m_monitoredItem = monitoredItem;
        //    }
        //    catch (Exception e)
        //    {
        //        // TODO: log
        //        return false;
        //    }

        //    // TODO: Pre-create the properties for the event and add the cdeM information

        //    return true;
        //}

        protected override void InitializeMonitoredItem(TheOPCMonitoredItemBase previousTag)
        {
            RefreshNeeded = EventInfo.AggregateRetainedConditions;

            MyMonitoredItem.AttributeId = Attributes.EventNotifier;
            MyMonitoredItem.QueueSize = 1000;
            MyMonitoredItem.SamplingInterval = 0;
            MyMonitoredItem.Filter = GetFilter();

            if (previousTag is TheOPCEvent previousEventTag)
            {
                if (EventInfo.IsEqual(previousEventTag.EventInfo))
                {
                    if (!previousEventTag.RefreshNeeded)
                    {
                        // The item has not changed: no need to consider for refresh
                        RefreshNeeded = false;
                    }
                }
            }
        }

        override protected void MonitoredItem_Notification(MonitoredItem monitoredItem, MonitoredItemNotificationEventArgs e)
        {
            try
            {
                var tEventTag = monitoredItem.Handle as TheOPCEvent;
                if (tEventTag == null)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(78102, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyOPCServer.GetBaseThing().EngineName, $"Internal error: invalid monitored item handle in Event", eMsgLevel.l1_Error, $"{DisplayName} {GetNodeIdForLogs()} {EventInfo.AggregateRetainedConditions}"));
                    return;
                }

                var tEventHost = tEventTag.GetHostThing();
                if (tEventHost == null)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(78102, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyOPCServer.GetBaseThing().EngineName, $"Event host thing not found", eMsgLevel.l1_Error, $"{DisplayName} {GetNodeIdForLogs()} {EventInfo.AggregateRetainedConditions}"));
                    return;
                }

                EventFieldList notification = e.NotificationValue as EventFieldList;

                if (notification == null)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(78102, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyOPCServer.GetBaseThing().EngineName, $"Internal error: notification is not an EventFieldList", eMsgLevel.l1_Error, $"{DisplayName} {GetNodeIdForLogs()} {EventInfo.AggregateRetainedConditions}"));
                    return;
                }

                // check the type of event.
                NodeId eventTypeId = ClientUtils.FindEventType(monitoredItem, notification);

                if (MyOPCServer.EnableOPCDataLogging)
                {
                    var logInfo = new Dictionary<string, object>
                    {
                                { "ReceiveTime", DateTimeOffset.Now },
                                { "TagId", DisplayName },
                                { "EventTypeId", eventTypeId},
                                { "Value", notification.EventFields.Aggregate("", (s, ef) => $"{s} [{ef.TypeInfo},{ef.Value}]") },
                                { "Server", notification.Message.PublishTime },
                                { "MonitoredItem",  monitoredItem?.ClientHandle },
                                { "SequenceNumber", notification.Message?.SequenceNumber },

                    };
                    TheOPCTag.LogOPCData(logInfo, MyOPCServer.GetLogAddress(), $"{DisplayName} {GetNodeIdForLogs()} {EventInfo.AggregateRetainedConditions}");
                }


                // ignore unknown events.
                if (NodeId.IsNull(eventTypeId))
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(78102, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyOPCServer.GetBaseThing().EngineName, $"Unknown eventTypeId", eMsgLevel.l1_Error, $"{DisplayName} {GetNodeIdForLogs()} {EventInfo.AggregateRetainedConditions}: received {eventTypeId}"));
                    return;
                }

                EventFilter filter = monitoredItem.Status.Filter as EventFilter;

                bool isRefreshEvent = false;

                var eventData = new Dictionary<string, object>();
                DateTimeOffset sourceTime = DateTimeOffset.Now;
                string conditionId = null;
                bool? bRetain = null;

                int index = 0;
                foreach (var field in filter.SelectClauses)
                {
                    var value = index < notification?.EventFields.Count ? notification?.EventFields[index].Value : null;
                    if (value is ExtensionObject || value is ExtensionObject[])
                    {
                        value = MyOPCServer.DecodeExtensionObjectToJson(value, out var ignored);
                    }
                    var name = field?.BrowsePath?.Count > 0 ? field.BrowsePath[0].Name : null;

                    if (value is NodeId)
                    {
                        if (name == "EventType")
                        {
                            var eventType = value as NodeId;
                            if (eventType == Opc.Ua.ObjectTypeIds.RefreshStartEventType)
                            {
                                TheBaseAssets.MySYSLOG.WriteToLog(78102, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(MyOPCServer.GetBaseThing().EngineName, $"Received Refresh Start event", eMsgLevel.l4_Message, $"{DisplayName} {GetNodeIdForLogs()} {EventInfo.AggregateRetainedConditions}"));

                                _currentConditionsByConditionId.Clear();
                                if (_bRefreshing)
                                {
                                    // Two overlapping refresh starts received
                                    TheBaseAssets.MySYSLOG.WriteToLog(78102, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyOPCServer.GetBaseThing().EngineName, $"Received more than one Refresh Start event", eMsgLevel.l2_Warning, $"{DisplayName} {GetNodeIdForLogs()} {EventInfo.AggregateRetainedConditions}"));
                                }
                                _bRefreshing = true;
                                _lastRefreshStartTime = DateTimeOffset.Now;
                                isRefreshEvent = true;
                            }
                            else if (eventType == Opc.Ua.ObjectTypeIds.RefreshEndEventType)
                            {
                                TheBaseAssets.MySYSLOG.WriteToLog(78102, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(MyOPCServer.GetBaseThing().EngineName, $"Received Refresh End event", eMsgLevel.l4_Message, $"{DisplayName} {GetNodeIdForLogs()} {EventInfo.AggregateRetainedConditions}"));
                                if (!_bRefreshing)
                                {
                                    TheBaseAssets.MySYSLOG.WriteToLog(78102, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyOPCServer.GetBaseThing().EngineName, $"Received Refresh End event without matching start event", eMsgLevel.l2_Warning, $"{DisplayName} {GetNodeIdForLogs()} {EventInfo.AggregateRetainedConditions}"));
                                    // refresh end received without refresh start
                                }
                                _bRefreshing = false;
                                isRefreshEvent = true;
                                if (EventInfo.AggregateRetainedConditions)
                                {
                                    WriteAggregatedConditionsToProperty(tEventHost, TheCommonUtils.CDate(notification.Message.PublishTime));
                                }
                            }
                        }
                        value = value.ToString();
                    }
                    if (name == null)
                    {
                        if (field.TypeDefinitionId == Opc.Ua.ObjectTypeIds.BaseEventType)
                        {
                            name = "ConditionId";
                        }

                    }
                    if (name != null)
                    {
                        if (aliasMap.TryGetValue(index, out var alias))
                        {
                            eventData[alias] = value;
                        }
                        else
                        {
                            eventData[name] = value;
                        }
                        switch(name)
                        {
                            case "ConditionId":
                                conditionId = value?.ToString();
                                break;
                            case "Retain":
                                bRetain = TheCommonUtils.CBool(value);
                                break;
                            case "Time":
                                sourceTime = TheCommonUtils.CDate(value);
                                break;
                        }
                    }
                    index++;
                }
                if (!isRefreshEvent)
                {
                    var eventInfoProperties = EventInfo.GetPropertyNames();
                    var filteredEventProps = EventInfo.Properties?.Count > 0 ? eventData.Where(pk => !requiredEventFields.Contains(pk.Key) || eventInfoProperties.Contains(pk.Key))
                        //.Select(kv => 
                        //{
                        //    if (aliasMap.TryGetValue(kv.Key, out var alias))
                        //    {
                        //        return new KeyValuePair<string, object>(alias, kv.Value);
                        //    }
                        //    return kv;
                        //})
                        .ToDictionary(kv => kv.Key, kv => kv.Value) : eventData;

                    //if (_bRefreshing)
                    //{
                    //    filteredEventProps["Refresh"] = true;
                    //}

                    if (!EventInfo.AggregateRetainedConditions)
                    {
                        // Raw events
                        // TODO avoid resending events due to a refresh?
                        var eventAsJson = TheCommonUtils.SerializeObjectToJSONString(filteredEventProps);
                        TheBaseAssets.MySYSLOG.WriteToLog(78102, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM(MyOPCServer.GetBaseThing().EngineName, $"Received raw event {eventAsJson}", eMsgLevel.l6_Debug, $"{DisplayName} {GetNodeIdForLogs()} {EventInfo.AggregateRetainedConditions}"));
                        tEventHost.GetBaseThing().SetProperty(DisplayName, eventAsJson, TheCommonUtils.CDate(notification.Message.PublishTime));
                    }
                    else
                    {
                        // Aggregated Condition State
                        if (conditionId != null)
                        {
                            if (bRetain == false)
                            {
                                TheBaseAssets.MySYSLOG.WriteToLog(78102, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(MyOPCServer.GetBaseThing().EngineName, $"Removed current event {conditionId}", eMsgLevel.l6_Debug, $"{DisplayName} {GetNodeIdForLogs()} {EventInfo.AggregateRetainedConditions}"));
                                _currentConditionsByConditionId.RemoveNoCare(conditionId);
                            }
                            else
                            {
                                TheBaseAssets.MySYSLOG.WriteToLog(78102, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(MyOPCServer.GetBaseThing().EngineName, $"Added current event {conditionId}", eMsgLevel.l6_Debug, $"{DisplayName} {GetNodeIdForLogs()} {EventInfo.AggregateRetainedConditions}"));
                                _currentConditionsByConditionId[conditionId] = filteredEventProps;
                            }
                            if (!_bRefreshing)
                            {
                                WriteAggregatedConditionsToProperty(tEventHost, TheCommonUtils.CDate(notification.Message.PublishTime));
                            }
                        }
                    }

                    // Legacy format: do we still need to support this? Existing event support was not really usable...
                    //foreach (var eventField in eventData)
                    //{
                    //    string propertyName;
                    //    if (tEventHost is TheOPCUATagThing) // TODO Create a TheOPCUAEventThing
                    //    {
                    //        // If this is a dedicated event thing, use the original value name
                    //        // TODO Is this really what we want to do or do we also want to use the full browsepath for dedicated event things?
                    //        throw new NotImplementedException("Should never get here");
                    //        //propertyName = field.BrowsePath[0].Name;
                    //    }
                    //    else
                    //    {
                    //        // This is an external, multi-event thing: use the browsepath to avoid collisions with properties from multiple events
                    //        propertyName = DisplayName + "." + eventField.Key;
                    //    }
                    //    SetPropertyFromVariant(tEventHost, propertyName, eventField.Value, sourceTime);
                    //}

                }
            }
            catch (Exception ex)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(78102, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyOPCServer.GetBaseThing().EngineName, $"Internal error processing event notification", eMsgLevel.l1_Error, $"{DisplayName} {GetNodeIdForLogs()} {EventInfo.AggregateRetainedConditions}: {ex.ToString()}"));
            }
        }

        private void WriteAggregatedConditionsToProperty(ICDEThing tEventHost, DateTimeOffset time)
        {
            var currentConditionsAsJson = TheCommonUtils.SerializeObjectToJSONString(_currentConditionsByConditionId.Values.OrderByDescending(evnt  =>
                {
                    if (evnt.TryGetValue("Time", out var evntTime))
                    {
                        return evntTime;
                    }
                    return null;
                }).ToList());
            if (TheThing.GetSafePropertyString(tEventHost.GetBaseThing(), DisplayName) != currentConditionsAsJson)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(78102, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM(MyOPCServer.GetBaseThing().EngineName, $"Updated current events {_currentConditionsByConditionId.Count} - {currentConditionsAsJson}", eMsgLevel.l6_Debug, $"{DisplayName} {GetNodeIdForLogs()} {EventInfo.AggregateRetainedConditions}"));
                // Using publish time because the event timestamps could be way in the past (i.e. when a recent event just was removed)
                tEventHost.GetBaseThing().SetProperty(DisplayName, currentConditionsAsJson, time);
            }
            else
            {
                TheBaseAssets.MySYSLOG.WriteToLog(78102, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM(MyOPCServer.GetBaseThing().EngineName, $"No change to current events {_currentConditionsByConditionId.Count} - {currentConditionsAsJson}", eMsgLevel.l6_Debug, $"{DisplayName} {GetNodeIdForLogs()} {EventInfo.AggregateRetainedConditions}"));
            }
        }

        cdeConcurrentDictionary<string, Dictionary<string, object>> _currentConditionsByConditionId = new cdeConcurrentDictionary<string, Dictionary<string, object>>();
        bool _bRefreshing;
        public bool RefreshNeeded;
        DateTimeOffset _lastRefreshStartTime;

        void SetPropertyFromVariant(ICDEThing thing, string name, Variant variant, DateTimeOffset sourceTime)
        {
            var prop = thing.GetProperty(name, true);
            var dataValue = new DataValue(variant);
            ePropertyTypes? tType;
            var cdeValue = TheOPCTag.ChangeType(dataValue, dataValue, out tType);
            prop.cdeT = (int) (tType ?? ePropertyTypes.TString);
            prop.cdeCTIM = sourceTime;
            prop.Value = cdeValue;
        }

        internal override bool RefersToSamePropertyAndTag(object monitoredItemHandle)
        {
            if (!(monitoredItemHandle is TheOPCEvent))
            {
                return false;
            }
            var opcTag = monitoredItemHandle as TheOPCEvent;
            return NodeIdName == opcTag.NodeIdName && opcTag.GetHostThing() == GetHostThing() && DisplayName == opcTag.DisplayName;
        }
    }

}
