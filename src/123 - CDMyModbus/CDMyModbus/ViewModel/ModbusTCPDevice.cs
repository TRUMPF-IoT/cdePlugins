// SPDX-FileCopyrightText: 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using Modbus.Device;
using NModbusExt.Config;
using NModbusExt.DataTypes;
using nsCDEngine.BaseClasses;
using nsCDEngine.Communication;
using nsCDEngine.Engines;
using nsCDEngine.Engines.NMIService;
using nsCDEngine.Engines.StorageService;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace Modbus
{
    [DeviceType(DeviceType = eModbusType.ModbusTCPDevice, Description = "Represents an Modbus TCP connection", Capabilities = new[] { eThingCaps.ConfigManagement })]
    public class ModbusTCPDevice : TheThingBase
    {
        [ConfigProperty]
        bool AutoConnect
        {
            get { return TheThing.GetSafePropertyBool(MyBaseThing, nameof(AutoConnect)); }
            set { TheThing.SetSafePropertyBool(MyBaseThing, nameof(AutoConnect), value); }
        }
        bool IsConnected
        {
            get { return TheThing.GetSafePropertyBool(MyBaseThing, nameof(IsConnected)); }
            set { TheThing.SetSafePropertyBool(MyBaseThing, nameof(IsConnected), value); }
        }
        [ConfigProperty]
        bool KeepOpen
        {
            get { return TheThing.GetSafePropertyBool(MyBaseThing, nameof(KeepOpen)); }
            set { TheThing.SetSafePropertyBool(MyBaseThing, nameof(KeepOpen), value); }
        }

        [ConfigProperty]
        uint Interval
        {
            get { return (uint)TheThing.GetSafePropertyNumber(MyBaseThing, nameof(Interval)); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, nameof(Interval), value); }
        }
        [ConfigProperty]
        uint CustomPort
        {
            get { return (uint)TheThing.GetSafePropertyNumber(MyBaseThing, nameof(CustomPort)); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, nameof(CustomPort), value); }
        }
        [ConfigProperty]
        int SlaveAddress
        {
            get { return (int)TheThing.GetSafePropertyNumber(MyBaseThing, nameof(SlaveAddress)); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, nameof(SlaveAddress), value); }
        }
        [ConfigProperty]
        int Offset
        {
            get { return (int)TheThing.GetSafePropertyNumber(MyBaseThing, nameof(Offset)); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, nameof(Offset), value); }
        }
        [ConfigProperty]
        int ConnectionType
        {
            get { return (int)TheThing.GetSafePropertyNumber(MyBaseThing, nameof(ConnectionType)); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, nameof(ConnectionType), value); }
        }

        private IBaseEngine MyBaseEngine;

        public ModbusTCPDevice(TheThing tBaseThing, ICDEPlugin pPluginBase, DeviceDescription pModDeviceDescription)
        {
            if (tBaseThing != null)
                MyBaseThing = tBaseThing;
            else
                MyBaseThing = new TheThing();
            MyBaseEngine = pPluginBase.GetBaseEngine();
            MyBaseThing.DeviceType = eModbusType.ModbusTCPDevice;
            MyBaseThing.EngineName = MyBaseEngine.GetEngineName();
            MyBaseThing.SetIThingObject(this);
            MyDevice = pModDeviceDescription;
            if (MyDevice != null && !String.IsNullOrEmpty(MyDevice.Name))
                MyBaseThing.FriendlyName = MyDevice.Name;
            MyBaseThing.AddCapability(eThingCaps.SensorProvider);
        }

        public void sinkUpdated(StoreEventArgs e)
        {
            SetupModbusProperties(true, null);
        }

        public void sinkStoreReady(StoreEventArgs e)
        {
            if (MyDevice != null)
            {
                if (!string.IsNullOrEmpty(MyDevice.Name))
                    MyBaseThing.FriendlyName = MyDevice.Name;
                if (!string.IsNullOrEmpty(MyDevice.IpAddress))
                    MyBaseThing.Address = MyDevice.IpAddress;
                if (MyDevice.IpPort == 0)
                    MyDevice.IpPort = 502;
                if (ConnectionType == 0)
                    ConnectionType = 3;
                TheThing.SetSafePropertyNumber(MyBaseThing, "CustomPort", MyDevice.IpPort);
                TheThing.SetSafePropertyNumber(MyBaseThing, "SlaveAddress", MyDevice.SlaveAddress);
                if (MyDevice.Mapping != null)
                {
                    TheThing.SetSafePropertyNumber(MyBaseThing, "Offset", MyDevice.Mapping.Offset);
                    //TODO: Create Storage Mirror with Field Mapps
                    MyModFieldStore.FlushCache(true);
                    foreach (var tFld in MyDevice.Mapping.FieldList)
                    {
                        MyModFieldStore.AddAnItem(tFld);
                    }
                }
            }
            if (Interval < 100)
            {
                Interval = 100;
            }
            if (AutoConnect)
            {
                TheCommonUtils.cdeRunAsync("ModBusAutoConnect", true, async (o) =>
                {
                    while (AutoConnect && TheBaseAssets.MasterSwitch && !IsConnected && !Connect(null))
                    {
                        await TheCommonUtils.TaskDelayOneEye((int)Interval, 100);
                    }
                });
            }
            mIsInitialized = true;
            FireEvent(eThingEvents.Initialized, this, true, true);
        }

        TheStorageMirror<FieldMapping> MyModFieldStore;
        public override bool Init()
        {
            if (mIsInitCalled) return false;
            mIsInitCalled = true;

            MyBaseThing.LastMessage = "Device Ready";
            MyBaseThing.StatusLevel = 0;
            IsConnected = false;

            MyBaseThing.DeclareConfigProperty(new TheThing.TheConfigurationProperty { Name = nameof(MyBaseThing.Address), cdeT = ePropertyTypes.TString, Required = true, Description = "", Generalize = true });
            MyBaseThing.DeclareConfigProperty(new TheThing.TheConfigurationProperty { Name = nameof(MyBaseThing.FriendlyName), cdeT = ePropertyTypes.TString, Description = "" });
            MyBaseThing.DeclareConfigProperty(new TheThing.TheConfigurationProperty { Name = nameof(Interval), cdeT = ePropertyTypes.TNumber, DefaultValue = 1000, RangeMin = 100, Description = "Time interval at which to poll the sensor for values" });
            MyBaseThing.DeclareConfigProperty(new TheThing.TheConfigurationProperty { Name = nameof(CustomPort), cdeT = ePropertyTypes.TNumber, DefaultValue = 502, Description = "" });
            MyBaseThing.DeclareConfigProperty(new TheThing.TheConfigurationProperty { Name = nameof(SlaveAddress), cdeT = ePropertyTypes.TNumber, DefaultValue = 1, Description = "" });
            MyBaseThing.DeclareConfigProperty(new TheThing.TheConfigurationProperty { Name = nameof(AutoConnect), cdeT = ePropertyTypes.TBoolean, DefaultValue = false, Description = "" });
            MyBaseThing.DeclareConfigProperty(new TheThing.TheConfigurationProperty { Name = nameof(KeepOpen), cdeT = ePropertyTypes.TBoolean, DefaultValue = true, Description = "" });
            MyBaseThing.DeclareConfigProperty(new TheThing.TheConfigurationProperty { Name = nameof(Offset), cdeT = ePropertyTypes.TNumber, Description = "" });
            MyBaseThing.DeclareConfigProperty(new TheThing.TheConfigurationProperty { Name = nameof(ConnectionType), cdeT = ePropertyTypes.TNumber, DefaultValue = 3, Description = "Read Coils:1, Read Input:2, Holding Registers:3, Input Register:4, Read Multiple Register:23" });

            MyModFieldStore = new TheStorageMirror<FieldMapping>(TheCDEngines.MyIStorageService);
            MyModFieldStore.IsRAMStore = true;
            MyModFieldStore.IsCachePersistent = true;
            MyModFieldStore.IsStoreIntervalInSeconds = true;
            if (string.IsNullOrEmpty(MyBaseThing.ID))
            {
                MyBaseThing.ID = Guid.NewGuid().ToString();
                if (MyDevice != null && !string.IsNullOrEmpty(MyDevice.Id))
                    MyBaseThing.ID = MyDevice.Id;
                if (GetProperty("CustomPort", false) == null)
                    TheThing.SetSafePropertyNumber(MyBaseThing, "CustomPort", 502);
                if (GetProperty("SlaveAddress", false) == null)
                    TheThing.SetSafePropertyNumber(MyBaseThing, "SlaveAddress", 1);
                Interval = 1000;
            }
            MyModFieldStore.CacheTableName = $"MBFLDS{MyBaseThing.ID}";
            MyModFieldStore.RegisterEvent(eStoreEvents.StoreReady, sinkStoreReady);
            MyModFieldStore.RegisterEvent(eStoreEvents.UpdateRequested, sinkUpdated);
            MyModFieldStore.RegisterEvent(eStoreEvents.Inserted, sinkUpdated);
            MyModFieldStore.InitializeStore(true, false);
            return false;
        }

        public override bool Delete()
        {
            mIsInitialized = false;
            MyModFieldStore.FlushCache(false);
            Disconnect(null);
            return true;
        }

        bool Connect(TheProcessMessage pMsg)
        {
            bool bSuccess = false;
            try
            {
                MyBaseThing.StatusLevel = 4; // ReaderThread will set statuslevel to 1
                foreach (var field in MyModFieldStore.TheValues)
                {
                    var p = MyBaseThing.GetProperty(field.PropertyName, true);
                    p.cdeM = "MODPROP";
                    p.UnregisterEvent(eThingEvents.PropertyChangedByUX, null);
                    p.RegisterEvent(eThingEvents.PropertyChangedByUX, sinkPChanged);
                }
                SetupModbusProperties(true, pMsg);
                IsConnected = true;
                MyBaseThing.LastMessage = $"{DateTime.Now} - Device Connecting";
                TheBaseAssets.MySYSLOG.WriteToLog(10000, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(MyBaseThing.EngineName, MyBaseThing.LastMessage, eMsgLevel.l4_Message));
                TheCommonUtils.cdeRunAsync($"ModRunThread{MyBaseThing.FriendlyName}", true, (o) =>
                {
                    ReaderThread();
                });
                bSuccess = true;
            }
            catch (Exception e)
            {
                MyBaseThing.StatusLevel = 3;
                string error = $"Error connecting: {e.Message}";
                MyBaseThing.LastMessage = $"{DateTimeOffset.Now}: {error}";
                TheBaseAssets.MySYSLOG.WriteToLog(10000, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, error, eMsgLevel.l1_Error, e.ToString()));
                IsConnected = false;
            }
            return bSuccess;
        }

        void sinkPChanged(cdeP prop)
        {
            if (MyBaseEngine.GetEngineState().IsSimulated || !IsConnected) return;
            var field = MyModFieldStore.MyMirrorCache.GetEntryByFunc(s => s.PropertyName == prop.Name);
            if (field == null) return;

            var error = OpenModBus();
            if (!string.IsNullOrEmpty(error))
            {
                MyBaseThing.LastMessage = $"{DateTime.Now} - Modbus Device could not be opened: {error}";
                TheBaseAssets.MySYSLOG.WriteToLog(10000, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, MyBaseThing.LastMessage, eMsgLevel.l1_Error));
                return;
            }
            try
            {
                ushort tMainOffset = (ushort)(TheThing.GetSafePropertyNumber(MyBaseThing, "Offset") + field.SourceOffset);
                byte tSlaveAddress = (byte)TheThing.GetSafePropertyNumber(MyBaseThing, "SlaveAddress");
                int tReadWay = (int)TheThing.GetSafePropertyNumber(MyBaseThing, "ConnectionType");
                switch (tReadWay)
                {
                    case 1:
                        MyModMaster.WriteSingleCoil(tSlaveAddress, tMainOffset, TheCommonUtils.CBool(prop.ToString()));
                        break;
                    default:
                        MyModMaster.WriteSingleRegister(tSlaveAddress, tMainOffset, TheCommonUtils.CUShort(prop.ToString()));
                        break;
                }
            }
            catch (Exception e)
            {
                MyBaseThing.LastMessage = $"{DateTime.Now} - Failure during write of modbus property: {e.Message}";
                TheBaseAssets.MySYSLOG.WriteToLog(10000, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, MyBaseThing.LastMessage, eMsgLevel.l1_Error, e.ToString()));
            }
            if (!KeepOpen) // races with reader thread, but reader thread will retry/reopen so at most one data point is lost
            {
                CloseModBus();
            }
        }

        bool Disconnect(TheProcessMessage pMsg)
        {
            CloseModBus();
            MyBaseThing.StatusLevel = 0;
            if (MyBaseThing.LastMessage.Contains("- Device Connected"))
                MyBaseThing.LastMessage = $"{DateTime.Now} - Device Disconnected";
            IsConnected = false;
            return true;
        }

        protected TheFormInfo MyFldMapperTable = null;
        TheFormInfo MyModConnectForm = null;
        public override bool CreateUX()
        {
            if (mIsUXInitCalled) return false;
            mIsUXInitCalled = true;

            var tFlds = TheNMIEngine.AddStandardForm(MyBaseThing, MyBaseThing.FriendlyName, 18, null, null, 0, ".Modbus TCP");
            MyModConnectForm = tFlds["Form"] as TheFormInfo;

            var tStatusBlock = TheNMIEngine.AddStatusBlock(MyBaseThing, MyModConnectForm, 10);
            tStatusBlock["Group"].SetParent(1);

            var tConnectBlock = TheNMIEngine.AddConnectivityBlock(MyBaseThing, MyModConnectForm, 200, sinkConnect);
            tConnectBlock["Group"].SetParent(1);
            tConnectBlock["Group"].Header = "Modbus TCP Connectivity";
            tConnectBlock["ConnectButton"].FldOrder = 280;
            tConnectBlock["DisconnectButton"].FldOrder = 290;
            TheNMIEngine.AddSmartControl(MyBaseThing, MyModConnectForm, eFieldType.Number, 205, 2, 0, "Custom Port", nameof(CustomPort), new nmiCtrlSingleEnded() { TileWidth = 3, ParentFld = 200 });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyModConnectForm, eFieldType.Number, 206, 2, 0, "Slave Address", nameof(SlaveAddress), new nmiCtrlSingleEnded() { TileWidth = 3, ParentFld = 200 });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyModConnectForm, eFieldType.Number, 240, 2, 0, "Base Offset", nameof(Offset), new nmiCtrlSingleEnded() { TileWidth = 3, ParentFld = 200 });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyModConnectForm, eFieldType.Number, 250, 2, 0, "Polling Interval", nameof(Interval), new nmiCtrlNumber() { TileWidth = 3, MinValue = 100, ParentFld = 200 });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyModConnectForm, eFieldType.SingleCheck, 260, 2, 0, "Keep Open", nameof(KeepOpen), new nmiCtrlSingleEnded() { TileWidth = 3, ParentFld = 200 });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyModConnectForm, eFieldType.ComboBox, 270, 2, 0, "Address Type", nameof(ConnectionType), new nmiCtrlComboBox() { NoTE = true, Options = "Read Coils:1;Read Input:2;Holding Registers:3;Input Register:4;Read Multiple Register:23", DefaultValue = "3", TileWidth = 6, ParentFld = 200 });


            ////METHODS Form
            MyFldMapperTable = new TheFormInfo(TheThing.GetSafeThingGuid(MyBaseThing, "FLDMAP"), eEngineName.NMIService, "Field Mapper", $"MBFLDS{MyBaseThing.ID}") { AddButtonText = "Add Tag", AddACL = 128 };
            TheNMIEngine.AddFormToThingUX(MyBaseThing, MyFldMapperTable, "CMyTable", "Field Mapper", 1, 3, 0xF0, null, null, new ThePropertyBag() { "Visibility=false" });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyFldMapperTable, eFieldType.SingleEnded, 50, 2, 0, "Property Name", "PropertyName", new nmiCtrlSingleEnded() { TileWidth = 4, FldWidth = 3 });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyFldMapperTable, eFieldType.SingleEnded, 55, 2, 0, "Current Value", "Value", new nmiCtrlSingleEnded() { TileWidth = 4, FldWidth = 3, Disabled = true, GreyCondition = "cde.CBool('%AllowWrite%')==false" });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyFldMapperTable, eFieldType.Number, 60, 2, 0, "Source Offset", "SourceOffset", new nmiCtrlNumber() { TileWidth = 3, FldWidth = 1 });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyFldMapperTable, eFieldType.Number, 70, 2, 0, "Source Size", "SourceSize", new nmiCtrlNumber() { TileWidth = 3, FldWidth = 1, DefaultValue = "1" });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyFldMapperTable, eFieldType.Number, 75, 2, 0, "Scale Factor", "ScaleFactor", new nmiCtrlNumber() { TileWidth = 3, FldWidth = 1, DefaultValue = "1" });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyFldMapperTable, eFieldType.ComboBox, 80, 2, 0, "Source Type", "SourceType", new nmiCtrlComboBox() { Options = "float;double;int32;int64;float32;uint16;int16;utf8;byte;float-abcd;double-cdab", TileWidth = 2, FldWidth = 2 });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyFldMapperTable, eFieldType.SingleCheck, 90, 2, 0, "Allow Write", "AllowWrite", new nmiCtrlSingleEnded() { TileWidth = 1, FldWidth = 1 });
            TheNMIEngine.AddTableButtons(MyFldMapperTable);

            TheNMIEngine.AddSmartControl(MyBaseThing, MyModConnectForm, eFieldType.CollapsibleGroup, 500, 2, 0x0, "Modbus Tags", null, new nmiCtrlCollapsibleGroup() { IsSmall = true, DoClose = true, TileWidth = 6, ParentFld = 1 });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyModConnectForm, eFieldType.TileButton, 510, 2, 0xF0, "Show Field Mapper", null, new nmiCtrlTileButton() { OnClick = $"TTS:{MyFldMapperTable.cdeMID}", TileWidth = 6, TileHeight = 1, NoTE = true, ParentFld = 500, Background = "blue", Foreground = "white" });

            SetupModbusProperties(false, null);
            mIsUXInitialized = true;
            return true;
        }

        void SetupModbusProperties(bool bReload, TheProcessMessage pMsg)
        {
            if (MyModConnectForm != null)
            {
                List<TheFieldInfo> tLst = TheNMIEngine.GetFieldsByFunc(s => s.FormID == MyModConnectForm.cdeMID);
                foreach (TheFieldInfo tInfo in tLst)
                {
                    if (tInfo.FldOrder >= 600 && TheCommonUtils.CInt(tInfo.PropBagGetValue("ParentFld")) == 500)
                        TheNMIEngine.DeleteFieldById(tInfo.cdeMID);
                }

                List<cdeP> props = MyBaseThing.GetPropertiesMetaStartingWith("MODPROP");
                int fldCnt = 600;
                foreach (var p in props)
                {
                    var field = MyModFieldStore.MyMirrorCache.GetEntryByFunc(s => s.PropertyName == p.Name);
                    if (field != null)
                    {
                        TheNMIEngine.AddSmartControl(MyBaseThing, MyModConnectForm, eFieldType.SingleEnded, fldCnt++, field.AllowWrite ? 2 : 0, 0, p.Name, p.Name, new nmiCtrlSingleEnded() { TileWidth = 6, ParentFld = 500 });
                    }
                }
                MyModConnectForm.Reload(pMsg, bReload);
            }
        }

        void sinkConnect(TheProcessMessage pMsg, bool isConnected)
        {
            if (isConnected)
                Connect(pMsg);
            else
                Disconnect(pMsg);
        }

        #region Message Handling
        /// <summary>
        /// Handles Messages sent from a host sub-engine to its clients
        /// </summary>
        /// <param name="Command"></param>
        /// <param name="pMessage"></param>
        public override void HandleMessage(ICDEThing sender, object pIncoming)
        {
            TheProcessMessage pMsg = pIncoming as TheProcessMessage;
            if (pMsg == null) return;

            string[] cmd = pMsg.Message.TXT.Split(':');
            switch (cmd[0])
            {
                case nameof(TheThing.MsgBrowseSensors):
                    var browseRequest = TheCommRequestResponse.ParseRequestMessageJSON<TheThing.MsgBrowseSensors>(pMsg.Message);
                    var browseResponse = new TheThing.MsgBrowseSensorsResponse { Error = "Internal error", Sensors = new List<TheThing.TheSensorSourceInfo>() };
                    foreach (FieldMapping fld in MyModFieldStore.TheValues)
                    {
                        browseResponse.Sensors.Add(new TheThing.TheSensorSourceInfo
                        {
                            SourceType = fld.SourceType,
                            cdeType = ePropertyTypes.TNumber,
                            SensorId = TheCommonUtils.CStr(fld.cdeMID),
                            ExtensionData = new Dictionary<string, object>
                            {
                                { nameof(FieldMapping.SourceOffset), fld.SourceOffset },
                                { nameof(FieldMapping.SourceSize), fld.SourceSize },
                                { nameof(FieldMapping.AllowWrite), fld.AllowWrite }
                            },
                            DisplayNamePath = new string[] { MyBaseEngine.GetEngineName(), MyBaseThing.FriendlyName, fld.PropertyName }
                        });
                    }
                    browseResponse.Error = null;
                    TheCommRequestResponse.PublishResponseMessageJson(pMsg.Message, browseResponse);
                    break;
                case nameof(TheThing.MsgSubscribeSensors):
                    var subscribeRequest = TheCommRequestResponse.ParseRequestMessageJSON<TheThing.MsgSubscribeSensors>(pMsg.Message);
                    var subscribeResponse = new TheThing.MsgSubscribeSensorsResponse { Error = "Internal error", SubscriptionStatus = new List<TheThing.TheSensorSubscriptionStatus>() };
                    if (subscribeRequest.ReplaceAll)
                    {
                        MyModFieldStore.RemoveAllItems();
                    }
                    var subscriptionStatus = new List<TheThing.TheSensorSubscriptionStatus>();
                    foreach (TheThing.TheSensorSubscription sub in subscribeRequest.SubscriptionRequests)
                    {
                        FieldMapping fld = new FieldMapping()
                        {
                            PropertyName = sub.TargetProperty,
                            cdeMID = TheCommonUtils.CGuid(sub.SensorId)
                        };
                        if (fld.cdeMID == Guid.Empty)
                            fld.cdeMID = Guid.NewGuid();
                        object sourceType;
                        if (sub.ExtensionData != null)
                        {
                            if (sub.ExtensionData.TryGetValue(nameof(TheThing.TheSensorSourceInfo.SourceType), out sourceType))
                                fld.SourceType = TheCommonUtils.CStr(sourceType);
                            object offset;
                            if (sub.ExtensionData.TryGetValue("SourceOffset", out offset))
                                fld.SourceOffset = TheCommonUtils.CInt(offset);
                            object size;
                            if (sub.ExtensionData.TryGetValue("SourceSize", out size))
                                fld.SourceSize = TheCommonUtils.CInt(size);
                            object allowWrite;
                            if (sub.ExtensionData.TryGetValue("AllowWrite", out allowWrite))
                                fld.AllowWrite = TheCommonUtils.CBool(allowWrite);
                            MyModFieldStore.AddAnItem(fld);
                            subscriptionStatus.Add(CreateSubscriptionStatusFromFieldMapping(fld));
                        }
                        else
                        {
                            subscriptionStatus.Add(new TheThing.TheSensorSubscriptionStatus
                            {
                                Error = "Missing source info",
                                Subscription = sub,
                            });
                        }
                    }
                    subscribeResponse.SubscriptionStatus = subscriptionStatus;
                    subscribeResponse.Error = null;
                    TheCommRequestResponse.PublishResponseMessageJson(pMsg.Message, subscribeResponse);
                    break;
                case nameof(TheThing.MsgGetSensorSubscriptions):
                    var getResponse = new TheThing.MsgGetSensorSubscriptionsResponse { Error = "Internal error" };
                    getResponse.Subscriptions = MyModFieldStore.TheValues.Select(fld => CreateSubscriptionStatusFromFieldMapping(fld).Subscription).ToList();
                    getResponse.Error = null;
                    TheCommRequestResponse.PublishResponseMessageJson(pMsg.Message, getResponse);
                    break;
                case nameof(TheThing.MsgUnsubscribeSensors):
                    var unsubscribeRequest = TheCommRequestResponse.ParseRequestMessageJSON<TheThing.MsgUnsubscribeSensors>(pMsg.Message);
                    var unsubscribeResponse = new TheThing.MsgUnsubscribeSensorsResponse { Error = "Internal error", Failed = new List<TheThing.TheSensorSubscriptionStatus>() };
                    if (unsubscribeRequest.UnsubscribeAll)
                    {
                        MyModFieldStore.RemoveAllItems();
                        if (MyModFieldStore.GetCount() > 0)
                            unsubscribeResponse.Failed = MyModFieldStore.TheValues.Select(fld => CreateSubscriptionStatusFromFieldMapping(fld)).ToList();
                    }
                    else
                    {
                        List<FieldMapping> toRemove = MyModFieldStore.TheValues.FindAll(fld => unsubscribeRequest.SubscriptionIds.Contains(fld.cdeMID));
                        MyModFieldStore.RemoveItems(toRemove, null);
                        foreach (FieldMapping fld in MyModFieldStore.TheValues)
                        {
                            if (toRemove.Any(t => t.cdeMID == fld.cdeMID))
                            {
                                unsubscribeResponse.Failed.Add(CreateSubscriptionStatusFromFieldMapping(fld));
                            }
                        }
                    }
                    unsubscribeResponse.Error = null;
                    TheCommRequestResponse.PublishResponseMessageJson(pMsg.Message, unsubscribeResponse);
                    break;
                case nameof(TheThing.MsgExportConfig):
                    var exportRequest = TheCommRequestResponse.ParseRequestMessageJSON<TheThing.MsgExportConfig>(pMsg.Message);
                    var exportResponse = new TheThing.MsgExportConfigResponse { Error = "Internal error" };

                    // No custom config beyond config properties and subscriptions
                    exportResponse.Error = null;

                    TheCommRequestResponse.PublishResponseMessageJson(pMsg.Message, exportResponse);
                    break;
                case nameof(TheThing.MsgApplyConfig):
                    break;
                default:
                    break;
            }

            TheThing.TheSensorSubscriptionStatus CreateSubscriptionStatusFromFieldMapping(FieldMapping fld)
            {
                return new TheThing.TheSensorSubscriptionStatus
                {
                    Subscription = new TheThing.TheSensorSubscription
                    {
                        TargetProperty = fld.PropertyName,
                        SensorId = TheCommonUtils.CStr(fld.cdeMID),
                        SubscriptionId = fld.cdeMID,
                        ExtensionData = new Dictionary<string, object>
                    {
                        { nameof(FieldMapping.SourceType), fld.SourceType },
                        { nameof(FieldMapping.SourceOffset), fld.SourceOffset },
                        { nameof(FieldMapping.SourceSize), fld.SourceSize },
                        { nameof(FieldMapping.AllowWrite), fld.AllowWrite }
                    },
                        TargetThing = new TheThingReference(MyBaseThing),
                        SampleRate = (int?)this.Interval
                    },
                    Error = null,
                };
            }
        }
        #endregion

        public ModbusConfiguration ModbusConfig { get; set; }
        public DeviceDescription MyDevice { get; set; }

        bool bReaderLoopRunning;
        object readerLoopLock = new object();
        public async void ReaderThread()
        {
            lock (readerLoopLock)
            {
                if (bReaderLoopRunning)
                {
                    return;
                }
                bReaderLoopRunning = true;
            }
            try
            {
                bool bPreviousError = false;
                while (TheBaseAssets.MasterSwitch && IsConnected)
                {
                    if (!MyBaseEngine.GetEngineState().IsSimulated)
                    {
                        var error = OpenModBus();
                        if (!string.IsNullOrEmpty(error))
                        {
                            MyBaseThing.LastMessage = $"{DateTime.Now} - Modbus Device could not be opened: {error}";
                            if (bPreviousError)
                            {
                                TheBaseAssets.MySYSLOG.WriteToLog(10000, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, MyBaseThing.LastMessage, eMsgLevel.l1_Error));
                                MyBaseThing.StatusLevel = 3;
                            }
                            else
                            {
                                TheBaseAssets.MySYSLOG.WriteToLog(10000, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(MyBaseThing.EngineName, MyBaseThing.LastMessage, eMsgLevel.l2_Warning));
                                MyBaseThing.StatusLevel = 2;
                            }
                            bPreviousError = true;
                        }
                        else
                        {
                            try
                            {
                                if (bPreviousError)
                                {
                                    MyBaseThing.LastMessage = $"{DateTime.Now} - Modbus Device connected after previous error.";
                                    TheBaseAssets.MySYSLOG.WriteToLog(10000, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, MyBaseThing.LastMessage, eMsgLevel.l4_Message));
                                    bPreviousError = false;
                                }
                                else
                                {
                                    if (MyBaseThing.StatusLevel != 1)
                                    {
                                        MyBaseThing.LastMessage = $"{DateTime.Now} - Modbus Device connected.";
                                        TheBaseAssets.MySYSLOG.WriteToLog(10000, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, MyBaseThing.LastMessage, eMsgLevel.l4_Message));
                                    }
                                }
                                MyBaseThing.StatusLevel = 1;
                                Dictionary<string, object> dict = ReadAll();
                                var timestamp = DateTimeOffset.Now;
                                TheBaseAssets.MySYSLOG.WriteToLog(10000, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM(MyBaseThing.EngineName, String.Format("Setting properties for {0}", MyBaseThing.FriendlyName), eMsgLevel.l4_Message, String.Format("{0}: {1}", timestamp, dict.Aggregate("", (s, kv) => s + string.Format("{0}={1};", kv.Key, kv.Value)))));
                                MyBaseThing.SetProperties(dict, timestamp);
                                if (!KeepOpen)
                                {
                                    CloseModBus();
                                }
                            }
                            catch (Exception e)
                            {
                                MyBaseThing.StatusLevel = 2;
                                MyBaseThing.LastMessage = $"{DateTime.Now} - Failure during read of modbus properties: {e.Message}";
                                TheBaseAssets.MySYSLOG.WriteToLog(10000, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, MyBaseThing.LastMessage, eMsgLevel.l2_Warning, e.ToString()));
                                CloseModBus(); // Close the connection just in case there's an internal socket error that we didn't detect (yet)
                            }
                        }
                    }
                    else
                    {
                        ExecuteSimulation();
                    }
                    await TheCommonUtils.TaskDelayOneEye((int)Interval, 100);
                }
            }
            catch (Exception e)
            {
                MyBaseThing.LastMessage = $"{DateTime.Now} - Error during read or processing of modbus properties: {e.Message}";
                TheBaseAssets.MySYSLOG.WriteToLog(10000, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, MyBaseThing.LastMessage, eMsgLevel.l1_Error, e.ToString()));
            }
            finally
            {
                lock (readerLoopLock)
                {
                    if (bReaderLoopRunning)
                    {
                        bReaderLoopRunning = false;
                    }
                }
                Disconnect(null);
            }
        }



        TcpClient tcpClient;
        ModbusIpMaster MyModMaster;
        public string OpenModBus()
        {
            if (MyModMaster != null && tcpClient != null && tcpClient.Connected) return null;
            string error = null;
            try
            {
                CloseModBus();
                tcpClient = new TcpClient();
                int port = (int)TheThing.GetSafePropertyNumber(MyBaseThing, "CustomPort");
                if (port == 0)
                {
                    port = 502; //MOdbus default Port
                    TheThing.SetSafePropertyNumber(MyBaseThing, "CustomPort", port);
                }
                tcpClient.Connect(IPAddress.Parse(MyBaseThing.Address), port);
                MyModMaster = ModbusIpMaster.CreateIp(tcpClient);

            }
            catch (SocketException e)
            {
                error = e.Message;
                CloseModBus();
            }
            return error;
        }

        public void CloseModBus()
        {
            try
            {
                MyModMaster?.Dispose();
            }
            catch { }
            MyModMaster = null;

            try
            {
                tcpClient?.Close();
            }
            catch { }
            tcpClient = null;
        }

        public Dictionary<string, object> ReadAll()
        {
            if (MyModFieldStore == null || MyModFieldStore.TheValues.Count == 0) return null;
            var timestamp = DateTimeOffset.Now;
            var dict = new Dictionary<string, object>();
            dict["Timestamp"] = timestamp;

            // Read configured data items via Modbus
            int tMainOffset = (int)TheThing.GetSafePropertyNumber(MyBaseThing, "Offset");
            int tSlaveAddress = (int)TheThing.GetSafePropertyNumber(MyBaseThing, "SlaveAddress");
            int tReadWay = (int)TheThing.GetSafePropertyNumber(MyBaseThing, "ConnectionType");
            foreach (var field in MyModFieldStore.TheValues)
            {
                try
                {
                    int address = field.SourceOffset + tMainOffset;

                    ushort[] data = null;

                    float scale = field.ScaleFactor;
                    if (scale == 0) scale = 1.0f;

                    switch (tReadWay)
                    {
                        case 1:
                            {
                                bool[] datab = MyModMaster.ReadCoils((ushort)address, (ushort)field.SourceSize);
                                for (int i = 0; i < field.SourceSize && i < datab.Length; i++)
                                {
                                    dict[$"{field.PropertyName}_{i}"] = datab[i];
                                }
                                field.Value = dict[$"{field.PropertyName}_0"];
                            }
                            continue;
                        case 2:
                            {
                                bool[] datab = MyModMaster.ReadInputs((ushort)address, (ushort)field.SourceSize);
                                for (int i = 0; i < field.SourceSize && i < datab.Length; i++)
                                {
                                    dict[$"{field.PropertyName}_{i}"] = datab[i];
                                }
                                field.Value = dict[$"{field.PropertyName}_0"];
                            }
                            continue;
                        case 4:
                            data = MyModMaster.ReadInputRegisters((byte)tSlaveAddress, (ushort)address, (ushort)field.SourceSize);
                            break;
                        default:
                            data = MyModMaster.ReadHoldingRegisters((byte)tSlaveAddress, (ushort)address, (ushort)field.SourceSize);
                            break;
                    }
                    if (data == null) continue;
                    if (field.SourceType == "float")
                    {
                        var value1 = TypeFloat.Convert(data, TypeFloat.ByteOrder.CDAB);
                        dict[field.PropertyName] = value1 / scale;
                    }
                    else if (field.SourceType == "float-abcd")
                    {
                        var value1 = TypeFloat.Convert(data, TypeFloat.ByteOrder.ABCD);
                        dict[field.PropertyName] = value1 / scale;
                    }
                    else if (field.SourceType == "double")
                    {
                        var value1 = TypeDouble.Convert(data, TypeDouble.ByteOrder.ABCD);
                        dict[field.PropertyName] = value1 / scale;
                    }
                    else if (field.SourceType == "double-cdab")
                    {
                        var value1 = TypeDouble.Convert(data, TypeDouble.ByteOrder.CDAB);
                        dict[field.PropertyName] = value1 / scale;
                    }
                    else if (field.SourceType == "int32")
                    {
                        var value = TypeInt32.Convert(data);
                        var dblValue = Convert.ToDouble(value);
                        dict[field.PropertyName] = dblValue / scale;
                    }
                    else if (field.SourceType == "int64")
                    {
                        var value = TypeInt64.Convert(data);
                        var dblValue = Convert.ToDouble(value);
                        dict[field.PropertyName] = dblValue / scale;
                    }
                    else if (field.SourceType == "float32")
                    {
                        var value = TypeFloat.Convert(data, TypeFloat.ByteOrder.SinglePrecIEEE);
                        dict[field.PropertyName] = value / scale;
                    }
                    else if (field.SourceType == "uint16")
                    {
                        var value = TypeUInt16.Convert(data);
                        dict[field.PropertyName] = value / scale;
                    }
                    else if (field.SourceType == "int16")
                    {
                        var value = TheCommonUtils.CInt(data[0]);
                        dict[field.PropertyName] = value / scale;
                    }
                    else if (field.SourceType == "utf8")
                    {
                        var value = TypeUTF8.Convert(data);
                        dict[field.PropertyName] = value;
                    }
                    else if (field.SourceType == "byte")
                    {
                        byte value = (byte)(data[0] & 255);
                        dict[field.PropertyName] = value;
                    }
                    field.Value = dict[field.PropertyName];

                    //dict[$"[{field.PropertyName}].[Status]"] = $"";
                }
                catch (Exception e)
                {
                    try
                    {
                        // Future: convey per-tag status similar to OPC statuscode?
                        //dict[$"[{field.PropertyName}].[Status]"] = $"##cdeError: {e.Message}";
                        TheBaseAssets.MySYSLOG.WriteToLog(10000, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(MyBaseThing.EngineName, $"Error reading property {field.PropertyName}", eMsgLevel.l2_Warning, e.ToString()));
                    }
                    catch { }
                }
            }
            return dict;
        }

        #region Simulation
        public void ExecuteSimulation()
        {
            try
            {
                var timestamp = DateTimeOffset.Now;

                var dict = new Dictionary<string, object>();
                dict["Timestamp"] = timestamp;

                foreach (var field in MyModFieldStore.TheValues)
                {
                    double rndVal = TheCommonUtils.GetRandomDouble() * 1000.0;
                    if (field.SourceType == "double")
                    {
                        dict[field.PropertyName] = rndVal;
                    }
                    else if (field.SourceType == "float")
                    {
                        dict[field.PropertyName] = rndVal;
                    }
                }
                MyBaseThing.SetProperties(dict, timestamp);
            }
            catch (Exception)
            {
                // Console.WriteLine("Ignoring exception: " + ex.Message);
                //Console.WriteLine(ex.StackTrace);
            }
        }
        #endregion
    }
}
