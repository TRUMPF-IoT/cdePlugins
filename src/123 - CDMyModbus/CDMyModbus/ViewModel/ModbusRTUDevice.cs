﻿// SPDX-FileCopyrightText: 2009-2024 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using CDMyModbus.ViewModel;
using Modbus.Device;
using Modbus.Serial;
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
using System.IO.Ports;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Modbus
{
    [DeviceType(DeviceType = eModbusType.ModbusRTUDevice, Description = "Represents an Modbus RTU connection", Capabilities = new[] { eThingCaps.ConfigManagement })]
    public class ModbusRTUDevice : ModbusBase
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
        int WatchDog
        {
            get { return (int)TheThing.MemberGetSafePropertyNumber(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyNumber(MyBaseThing, value); }
        }

        [ConfigProperty]
        uint Interval
        {
            get { return (uint)TheThing.GetSafePropertyNumber(MyBaseThing, nameof(Interval)); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, nameof(Interval), value); }
        }

        [ConfigProperty]
        int Baudrate
        {
            get { return (int)TheThing.GetSafePropertyNumber(MyBaseThing, nameof(Baudrate)); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, nameof(Baudrate), value); }
        }
        [ConfigProperty]
        int BitFormat
        {
            get { return (int)TheThing.GetSafePropertyNumber(MyBaseThing, nameof(BitFormat)); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, nameof(BitFormat), value); }
        }

        [ConfigProperty]
        byte SlaveAddress
        {
            get { return (byte)TheThing.GetSafePropertyNumber(MyBaseThing, nameof(SlaveAddress)); }
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

        private readonly IBaseEngine MyBaseEngine;

        public ModbusRTUDevice(TheThing tBaseThing, ICDEPlugin pPluginBase, DeviceDescription pModDeviceDescription)
        {
            if (tBaseThing != null)
                MyBaseThing = tBaseThing;
            else
                MyBaseThing = new TheThing();
            MyBaseEngine = pPluginBase.GetBaseEngine();
            MyBaseThing.DeviceType = eModbusType.ModbusRTUDevice;
            MyBaseThing.EngineName = MyBaseEngine.GetEngineName();
            MyBaseThing.SetIThingObject(this);
            MyDevice = pModDeviceDescription;
            if (MyDevice != null && !String.IsNullOrEmpty(MyDevice.Name))
                MyBaseThing.FriendlyName = MyDevice.Name;
            MyBaseThing.AddCapability(eThingCaps.SensorProvider);
        }

        public override cdeP SetProperty(string pName, object pValue)
        {
            var pr = base.SetProperty(pName, pValue);
            sinkPChanged(pr);
            return pr;
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
                if (ConnectionType == 0)
                    ConnectionType = 3;
                TheThing.SetSafePropertyNumber(MyBaseThing, "SlaveAddress", MyDevice.SlaveAddress);
                if (MyDevice.Mapping != null)
                {
                    TheThing.SetSafePropertyNumber(MyBaseThing, "Offset", MyDevice.Mapping.Offset);
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
            MyBaseThing.DeclareConfigProperty(new TheThing.TheConfigurationProperty { Name = nameof(Baudrate), cdeT = ePropertyTypes.TNumber, DefaultValue = 9600, Description = "Serial Baud Rate" });
            MyBaseThing.DeclareConfigProperty(new TheThing.TheConfigurationProperty { Name = nameof(BitFormat), cdeT = ePropertyTypes.TNumber, DefaultValue = 0, Description = "Serial Bit Format" });
            MyBaseThing.DeclareConfigProperty(new TheThing.TheConfigurationProperty { Name = nameof(SlaveAddress), cdeT = ePropertyTypes.TNumber, DefaultValue = 1, Description = "" });
            MyBaseThing.DeclareConfigProperty(new TheThing.TheConfigurationProperty { Name = nameof(AutoConnect), cdeT = ePropertyTypes.TBoolean, DefaultValue = false, Description = "" });
            MyBaseThing.DeclareConfigProperty(new TheThing.TheConfigurationProperty { Name = nameof(KeepOpen), cdeT = ePropertyTypes.TBoolean, DefaultValue = true, Description = "" });
            MyBaseThing.DeclareConfigProperty(new TheThing.TheConfigurationProperty { Name = nameof(Offset), cdeT = ePropertyTypes.TNumber, Description = "" });
            MyBaseThing.DeclareConfigProperty(new TheThing.TheConfigurationProperty { Name = nameof(ConnectionType), cdeT = ePropertyTypes.TNumber, DefaultValue = 3, Description = "Read Coils:1, Read Input:2, Holding Registers:3, Input Register:4, Read Multiple Register:23" });

            MyModFieldStore = new TheStorageMirror<FieldMapping>(TheCDEngines.MyIStorageService)
            { 
                IsRAMStore = true,
                IsCachePersistent = true,
                IsStoreIntervalInSeconds = true
            };
            if (string.IsNullOrEmpty(MyBaseThing.ID))
            {
                MyBaseThing.ID = Guid.NewGuid().ToString();
                if (MyDevice != null && !string.IsNullOrEmpty(MyDevice.Id))
                    MyBaseThing.ID = MyDevice.Id;
                if (GetProperty("SlaveAddress", false) == null)
                    TheThing.SetSafePropertyNumber(MyBaseThing, "SlaveAddress", 1);
                Interval = 1000;
            }
            MyModFieldStore.CacheTableName = $"MBFLDS{MyBaseThing.ID}";
            MyModFieldStore.RegisterEvent(eStoreEvents.StoreReady, sinkStoreReady);
            MyModFieldStore.RegisterEvent(eStoreEvents.UpdateRequested, sinkUpdated);
            MyModFieldStore.RegisterEvent(eStoreEvents.Inserted, sinkUpdated);
            MyModFieldStore.InitializeStore(true, false);

            TheQueuedSenderRegistry.RegisterHealthTimer(sinkWatchDog);
            return false;
        }

        DateTimeOffset lastCheck= DateTimeOffset.MinValue;
        bool InWatchdog = false;
        void sinkWatchDog(long tick)
        {
            if (!IsConnected || WatchDog < Interval * 3) return;

            if (!InWatchdog && lastCheck != DateTimeOffset.MinValue && DateTimeOffset.Now.Subtract(lastCheck).TotalMilliseconds > WatchDog)
            {
                InWatchdog = true;
                SetMessage($"{DateTime.Now} - Watchdog triggered","Modbus Watchdog", 2, DateTimeOffset.Now, 123002, eMsgLevel.l2_Warning);
                Disconnect(null);
                TheCommonUtils.SleepOneEye((uint)WatchDog, 300);
                if (AutoConnect)
                {
                    TheCommonUtils.cdeRunAsync("ModBusAutoConnect", true, async (o) =>
                    {
                        try
                        {
                            while (AutoConnect && TheBaseAssets.MasterSwitch && !IsConnected && !Connect(null))
                            {
                                await TheCommonUtils.TaskDelayOneEye((int)Interval, 100);
                            }
                        }
                        catch (Exception)
                        {
                            Disconnect(null);
                        }
                    });
                }
                InWatchdog = false;
            }
            lastCheck = MyBaseThing.LastUpdate;

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
                    p.Value = null;
                    p.UnregisterEvent(eThingEvents.PropertyChangedByUX, null);
                    p.RegisterEvent(eThingEvents.PropertyChangedByUX, sinkPChanged);
                }
                SetupModbusProperties(true, pMsg);
                IsConnected = true;
                MyBaseThing.LastMessage = $"{DateTime.Now} - Device Connecting";
                SetMessage($"{DateTime.Now} - Device Connecting", 1, DateTimeOffset.Now, 12300, eMsgLevel.l4_Message);
                TheCommonUtils.cdeRunAsync($"ModRunThread{MyBaseThing.FriendlyName}", true, async (o) =>
                {
                    await ReaderThread();
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

        void Disconnect(TheProcessMessage _)
        {
            IsConnected = false;
            CloseModBus();
            SetMessage($"{DateTime.Now} - Device Disconnected", 0, DateTimeOffset.Now, 123001, eMsgLevel.l4_Message);
        }

        protected TheFormInfo MyFldMapperTable = null;
        TheFormInfo MyModConnectForm = null;
        public override bool CreateUX()
        {
            if (mIsUXInitCalled) return false;
            mIsUXInitCalled = true;

            var tFlds = TheNMIEngine.AddStandardForm(MyBaseThing, MyBaseThing.FriendlyName, 18, null, null, 0, ".Modbus RTU");
            MyModConnectForm = tFlds["Form"] as TheFormInfo;

            var tStatusBlock = TheNMIEngine.AddStatusBlock(MyBaseThing, MyModConnectForm, 10);
            tStatusBlock["Group"].SetParent(1);

            var tConnectBlock = TheNMIEngine.AddConnectivityBlock(MyBaseThing, MyModConnectForm, 200, sinkConnect);
            tConnectBlock["Group"].SetParent(1);
            tConnectBlock["Group"].Header = "Modbus TCP Connectivity";
            tConnectBlock["ConnectButton"].FldOrder = 280;
            tConnectBlock["DisconnectButton"].FldOrder = 290;
            tConnectBlock["Address"].Type = eFieldType.ComboBox;
            try
            {
                var sports = SerialPort.GetPortNames();
                string tp = TheCommonUtils.CListToString(sports, ";");
                tConnectBlock["Address"].PropertyBag = new nmiCtrlComboBox { Options = tp };
            }
            catch (Exception eee) 
            {
                TheBaseAssets.MySYSLOG.WriteToLog(10000, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, $"GetPortNames failed : {eee}", eMsgLevel.l1_Error));
            }
            TheNMIEngine.AddSmartControl(MyBaseThing, MyModConnectForm, eFieldType.ComboBox, 205, 2, 0, "Baud", nameof(Baudrate), new nmiCtrlComboBox() { TileWidth = 3, ParentFld = 200, Options = "2400;4800;9600;19200;38400;57600" });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyModConnectForm, eFieldType.ComboBox, 206, 2, 0, "Bit Format", nameof(BitFormat), new nmiCtrlComboBox() { TileWidth = 3, ParentFld = 200, Options = "8,N,1:0;8,E,1:1;8,O,1:2" });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyModConnectForm, eFieldType.Number, 207, 2, 0, "Slave Address", nameof(SlaveAddress), new nmiCtrlNumber() { TileWidth = 3, ParentFld = 200, MaxValue = 255, MinValue = 0 });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyModConnectForm, eFieldType.Number, 240, 2, 0, "Base Offset", nameof(Offset), new nmiCtrlSingleEnded() { TileWidth = 3, ParentFld = 200 });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyModConnectForm, eFieldType.Number, 250, 2, 0, "Polling Interval", nameof(Interval), new nmiCtrlNumber() { TileWidth = 2, NoTE=true, MinValue = 100, ParentFld = 200 });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyModConnectForm, eFieldType.SingleCheck, 260, 2, 0, "Keep Open", nameof(KeepOpen), new nmiCtrlSingleCheck() { TileWidth = 1, Label="Keep Open", NoTE=true, ParentFld = 200 });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyModConnectForm, eFieldType.Number, 261, 2, 0, "Watch Dog", nameof(WatchDog), new nmiCtrlNumber() { TileWidth = 2, NoTE = true, ParentFld = 200 });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyModConnectForm, eFieldType.ComboBox, 270, 2, 0, "Address Type", nameof(ConnectionType), new nmiCtrlComboBox() { Options = "Read Coils:1;Read Input:2;Holding Registers:3;Input Register:4;Read Multiple Register:23", DefaultValue = "3", ParentFld = 200 });
            AddThingTarget(MyModConnectForm, 271, 200); 

            ////METHODS Form
            MyFldMapperTable = new TheFormInfo(TheThing.GetSafeThingGuid(MyBaseThing, "FLDMAP"), eEngineName.NMIService, "Field Mapper", $"MBFLDS{MyBaseThing.ID}") { PropertyBag=new nmiCtrlTableView {ShowExportButton=true, ShowFilterField = true },  AddButtonText = "Add Tag",  AddACL = 128 };
            TheNMIEngine.AddFormToThingUX(MyBaseThing, MyFldMapperTable, "CMyTable", "Field Mapper", 1, 3, 0xF0, null, null, new nmiCtrlTableView { Visibility=false});
            TheNMIEngine.AddSmartControl(MyBaseThing, MyFldMapperTable, eFieldType.SingleEnded, 50, 2, 0, "Property Name", "PropertyName", new nmiCtrlSingleEnded() { TileWidth = 4, FldWidth = 3 });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyFldMapperTable, eFieldType.SingleEnded, 55, 2, 0, "Current Value", "Value", new nmiCtrlSingleEnded() { TileWidth = 4, FldWidth = 3, Disabled = true, GreyCondition = "cde.CBool('%AllowWrite%')==false" });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyFldMapperTable, eFieldType.Number, 60, 2, 0, "Source Offset", "SourceOffset", new nmiCtrlNumber() { TileWidth = 3, FldWidth = 1 });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyFldMapperTable, eFieldType.Number, 70, 2, 0, "Source Size", "SourceSize", new nmiCtrlNumber() { TileWidth = 3, FldWidth = 1, DefaultValue = "1" });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyFldMapperTable, eFieldType.Number, 75, 2, 0, "Scale Factor", "ScaleFactor", new nmiCtrlNumber() { TileWidth = 3, FldWidth = 1, DefaultValue = "1" });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyFldMapperTable, eFieldType.ComboBox, 80, 2, 0, "Source Type", "SourceType", new nmiCtrlComboBox() { DefaultValue="byte", Options = "float;double;int32;int64;float32;uint16;int16;utf8;byte;float-abcd;double-cdab", TileWidth = 2, FldWidth = 2 });
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

                TheThing tTargetThing = null;
                if (TargetThing != Guid.Empty)
                    tTargetThing= TheThingRegistry.GetThingByMID(TargetThing);

                    List<cdeP> props = MyBaseThing.GetPropertiesMetaStartingWith("MODPROP");
                int fldCnt = 600;
                foreach (var p in props.Select(s=>s.Name))
                {
                    var field = MyModFieldStore.MyMirrorCache.GetEntryByFunc(s => s.PropertyName == p);
                    if (field != null)
                    {
                        TheNMIEngine.AddSmartControl(MyBaseThing, MyModConnectForm, eFieldType.SingleEnded, fldCnt++, field.AllowWrite ? 2 : 0, 0, p, p, new nmiCtrlSingleEnded() { TileWidth = 6, ParentFld = 500 });
                        if (field.AllowWrite && tTargetThing!= null)
                        {
                            var tProp = tTargetThing.GetProperty(p, true);
                            tProp.UnregisterEvent(eThingEvents.PropertyChanged, sinkPChanged);
                            tProp.RegisterEvent(eThingEvents.PropertyChanged, sinkPChanged);
                        }
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
            if (!(pIncoming is TheProcessMessage pMsg)) return;

            string[] cmd = pMsg.Message.TXT.Split(':');
            switch (cmd[0])
            {
                case nameof(TheThing.MsgBrowseSensors):
                    //var browseRequest = TheCommRequestResponse.ParseRequestMessageJSON<TheThing.MsgBrowseSensors>(pMsg.Message)
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
                        if (sub.ExtensionData != null)
                        {
                            if (sub.ExtensionData.TryGetValue(nameof(TheThing.TheSensorSourceInfo.SourceType), out object sourceType))
                                fld.SourceType = TheCommonUtils.CStr(sourceType);
                            if (sub.ExtensionData.TryGetValue("SourceOffset", out object offset))
                                fld.SourceOffset = TheCommonUtils.CInt(offset);
                            if (sub.ExtensionData.TryGetValue("SourceSize", out object size))
                                fld.SourceSize = TheCommonUtils.CInt(size);
                            if (sub.ExtensionData.TryGetValue("AllowWrite", out object allowWrite))
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
                    var getResponse = new TheThing.MsgGetSensorSubscriptionsResponse
                    {
                        Error = "Internal error",
                        Subscriptions = MyModFieldStore.TheValues.Select(fld => CreateSubscriptionStatusFromFieldMapping(fld).Subscription).ToList()
                    };
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
                            if (toRemove.Exists(t => t.cdeMID == fld.cdeMID))
                            {
                                unsubscribeResponse.Failed.Add(CreateSubscriptionStatusFromFieldMapping(fld));
                            }
                        }
                    }
                    unsubscribeResponse.Error = null;
                    TheCommRequestResponse.PublishResponseMessageJson(pMsg.Message, unsubscribeResponse);
                    break;
                case nameof(TheThing.MsgExportConfig):
                    //var exportRequest = TheCommRequestResponse.ParseRequestMessageJSON<TheThing.MsgExportConfig>(pMsg.Message)
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
        readonly object readerLoopLock = new object();
        public async Task ReaderThread()
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
                                MyBaseThing.LastMessage = $"{timestamp} - {dict?.Count} tags read from Modbus Device";
                                MyBaseThing.LastUpdate = timestamp;
                                PushProperties(dict, timestamp);
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

        SerialPort slavePort;
        ModbusMaster MyModMaster;
        public string OpenModBus()
        {
            if (MyModMaster != null && slavePort != null && slavePort.IsOpen)
                return null;
            string error = null;
            try
            {
                CloseModBus();
                if (KeepOpen)
                    MyBaseThing.LastMessage = $"{DateTimeOffset.Now}: Opening modbus connection...";

                slavePort = new SerialPort(MyBaseThing.Address); 
                if (Baudrate == 0) Baudrate = 9600;
                slavePort.BaudRate = Baudrate; 
                slavePort.DataBits = 8;
                switch (BitFormat)
                {
                    case 2:
                        slavePort.Parity = Parity.Odd;
                        break;
                    case 1:
                        slavePort.Parity = Parity.Even;
                        break;
                    default:
                        slavePort.Parity = Parity.None;
                        break;
                }
                slavePort.StopBits = StopBits.One;
                slavePort.Open();

                var adapter = new SerialPortAdapter(slavePort);
                // create modbus slave
                MyModMaster = ModbusSerialMaster.CreateRtu(adapter);
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
            if (KeepOpen)
                MyBaseThing.LastMessage = $"{DateTimeOffset.Now}: closing modbus...";

            try
            {
                slavePort?.Close();
            }
            catch { 
                //intended
            }
            slavePort = null;
            if (KeepOpen)
                MyBaseThing.LastMessage = $"{DateTimeOffset.Now}: modbus closed";

            try
            {
                MyModMaster?.Dispose();
            }
            catch { 
                //intended
            }
            MyModMaster = null;
            if (KeepOpen)
            {
                MyBaseThing.LastMessage = $"{DateTimeOffset.Now}: modbus master closed";
                TheBaseAssets.MySYSLOG.WriteToLog(10000, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, MyBaseThing.LastMessage, eMsgLevel.l4_Message));
            }
        }

        public Dictionary<string, object> ReadAll()
        {
            var dict = new Dictionary<string, object>();
            if (MyModFieldStore == null || MyModFieldStore.TheValues.Count == 0) return dict;
            var timestamp = DateTimeOffset.Now;
            dict["Timestamp"] = timestamp;

            // Read configured data items via Modbus
            int tMainOffset = Offset;
            byte tSlaveAddress = SlaveAddress;
            int tReadWay = ConnectionType;
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
                                bool[] datab = MyModMaster.ReadCoils(tSlaveAddress, (ushort)address, (ushort)field.SourceSize);
                                for (int i = 0; i < field.SourceSize && i < datab.Length; i++)
                                {
                                    dict[$"{field.PropertyName}_{i}"] = datab[i];
                                }
                                field.Value = dict[$"{field.PropertyName}_0"];
                            }
                            continue;
                        case 2:
                            {
                                bool[] datab = MyModMaster.ReadInputs(tSlaveAddress, (ushort)address, (ushort)field.SourceSize);
                                for (int i = 0; i < field.SourceSize && i < datab.Length; i++)
                                {
                                    dict[$"{field.PropertyName}_{i}"] = datab[i];
                                }
                                field.Value = dict[$"{field.PropertyName}_0"];
                            }
                            continue;
                        case 4:
                            data = MyModMaster.ReadInputRegisters(tSlaveAddress, (ushort)address, (ushort)field.SourceSize);
                            break;
                        default:
                            data = MyModMaster.ReadHoldingRegisters(tSlaveAddress, (ushort)address, (ushort)field.SourceSize);
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
                        dict[field.PropertyName] = value / scale;
                    }
                    field.Value = dict[field.PropertyName];

                    //dict[$"[{field.PropertyName}].[Status]"] = $""
                }
                catch (Exception e)
                {
                    try
                    {
                        // Future: convey per-tag status similar to OPC statuscode?
                        //dict[$"[{field.PropertyName}].[Status]"] = $"##cdeError: {e.Message}"
                        TheBaseAssets.MySYSLOG.WriteToLog(10000, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(MyBaseThing.EngineName, $"Error reading property {field.PropertyName}", eMsgLevel.l2_Warning, e.ToString()));
                    }
                    catch { 
                        //intended
                    }
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

                var dict = new Dictionary<string, object>
                {
                    ["Timestamp"] = timestamp
                };

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
                //intended
            }
        }
        #endregion
    }
}
