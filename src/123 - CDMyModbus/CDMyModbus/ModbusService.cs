// SPDX-FileCopyrightText: 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using System;
using System.Collections.Generic;

using nsCDEngine.Engines;
using nsCDEngine.BaseClasses;
using nsCDEngine.ViewModels;
using nsCDEngine.Engines.NMIService;
using nsCDEngine.Engines.ThingService;
using System.IO;
using NModbusExt.Config;
using nsCDEngine.Communication;
using System.IO.Ports;

namespace Modbus
{
    public class eModbusType : TheDeviceTypeEnum
    {
        public const string ModbusTCPDevice = "Modbus TCP Device";
        public const string ModbusRTUDevice = "Modbus RTU Device";
    }

    class ModbusService : ThePluginBase
    {
        /// <summary>
        /// This constructor is called by The C-DEngine during initialization in order to register this service
        /// </summary>
        /// <param name="pBase">The C-DEngine is creating a Host for this engine and hands it to the Plugin Service</param>
        public override void InitEngineAssets(IBaseEngine pBase)
        {
            base.InitEngineAssets(pBase);
            MyBaseEngine.SetFriendlyName("Modbus Protocol");
            MyBaseEngine.GetEngineState().IsAcceptingFilePush = true;
            MyBaseEngine.AddCapability(eThingCaps.SensorProvider);
            MyBaseEngine.AddCapability(eThingCaps.ConfigManagement);
            MyBaseEngine.SetDeviceTypes(new List<TheDeviceTypeInfo>
            {
                new TheDeviceTypeInfo { Capabilities = new eThingCaps[] { eThingCaps.SensorProvider, eThingCaps.ConfigManagement }, DeviceType = eModbusType.ModbusTCPDevice, Description = "Modbus TCP Device" },
                new TheDeviceTypeInfo { Capabilities = new eThingCaps[] { eThingCaps.SensorProvider, eThingCaps.ConfigManagement }, DeviceType = eModbusType.ModbusRTUDevice, Description = "Modbus RTU Device" } //TODO: Did I do this right?
            });

            MyBaseEngine.SetEngineID(new Guid("{7E749DF5-10CD-4671-AB58-05EF078C9125}")); 
            MyBaseEngine.SetPluginInfo("This service allows you to connect to devices via Modbus", 0, null, "toplogo-150.png", "C-Labs", "http://www.c-labs.com", new List<string>() { }); //TODO: Describe your plugin - this will later be used in the Plugin-Store
        }

        public override bool Init()
        {
            if (mIsInitCalled) return false;
            mIsInitCalled = true;

            MyBaseThing.StatusLevel = 4;
            MyBaseThing.LastMessage = "Plugin has started";

            MyBaseThing.RegisterEvent(eEngineEvents.IncomingMessage, HandleMessage);
            MyBaseEngine.RegisterEvent(eEngineEvents.ThingDeleted, (t, o) => OnThingDeleted(t, o));
            MyBaseEngine.RegisterEvent(eEngineEvents.ThingRegistered, sinkRegistered);
            MyBaseThing.RegisterEvent("FileReceived", sinkFileReceived);

            // Create any properties and set them to default values on first run only
            bool bFirstRun = MyBaseThing.ID == null; // Using the ID property to detect first run - any other property would work as well if you don't need/want an ID property
                                                     // Tip: If you want to reset all properties to their defaults just set the ID property to null

            SetPropertyIfNotExist("FriendlyName", "Modbus Plugin", bFirstRun); //Declare the Property and optionally Initalize (change null with initialization)
            if (bFirstRun)
            {
                MyBaseThing.ID = Guid.NewGuid().ToString();
            }

            TheCommonUtils.cdeRunAsync(MyBaseEngine.GetEngineName() + " Init Services", true, (o) =>
            {
                // Perform any long-running initialization (i.e. network access, file access) here
                InitServices();

                // Only then declare the service as initizlied
                if (MyBaseThing.StatusLevel == 4)
                    MyBaseThing.StatusLevel = 1;
                FireEvent(eThingEvents.Initialized, this, true, true);
                MyBaseEngine.ProcessInitialized(); // If not lengthy initialized you can remove cdeRunasync and call this synchronously
            });
            mIsInitialized = true;

            return IsInit();
        }

        private void sinkRegistered(ICDEThing pThing, object pPara)
        {
            TheThing tThing = pPara as TheThing;
            if (tThing != null && tThing.DeviceType == eModbusType.ModbusTCPDevice)
                InitServices();
            mMyDashboard?.Reload(null, false);
        }

        void sinkFileReceived(ICDEThing pThing, object pFileName)
        {
            TheProcessMessage pMsg = pFileName as TheProcessMessage;
            if (pMsg?.Message == null) return;
            try
            {
                LoadXMLDefinition(pMsg.Message.TXT);
                TheCommCore.PublishToOriginator(pMsg.Message, new TSM(eEngineName.NMIService, "NMI_TOAST", string.Format("Modbus Definition File ({0}) received - Creating UX", pMsg.Message.TXT)));
                mMyDashboard.Reload(pMsg, true);
            }
            catch (Exception)
            {
                TheCommCore.PublishToOriginator(pMsg.Message, new TSM(eEngineName.NMIService, "NMI_ERROR", $"Modbus Definition File ({pMsg.Message.TXT}) received but creating UX failed!"));
            }
        }

        private void SetPropertyIfNotExist(string propName, object defaultValue, bool setAlways = false)
        {
            if (setAlways || MyBaseThing.GetProperty(propName) != null)
            {
                MyBaseThing.SetProperty(propName, defaultValue);
            }
        }

        public override bool CreateUX()
        {
            if (mIsUXInitCalled) return false;
            mIsUXInitCalled = true;

            mMyDashboard = TheNMIEngine.AddDashboard(MyBaseThing, new TheDashboardInfo(MyBaseEngine, "Modbus Devices")
            {
                PropertyBag = new nmiDashboardTile { Thumbnail = "images/Modbus-Logo.png;0.5;cdeLargeIcon", Category = " Connectivity", TileWidth = 3, TileHeight = 4, ClassName = "cdeLiveTile cdeLargeTile" }
            });

            var tFlds=TheNMIEngine.CreateEngineForms(MyBaseThing, TheThing.GetSafeThingGuid(MyBaseThing, "MYNAME"), "Modbus Devices", null, 20, 0x0F, 0xF0, TheNMIEngine.GetNodeForCategory(), "REFFRESHME", true, new eModbusType(), eModbusType.ModbusTCPDevice);
            TheFormInfo tForm = tFlds["Form"] as TheFormInfo;
            tForm.AddButtonText = "Add Modbus Device";

            if (TheCommonUtils.CBool(TheBaseAssets.MySettings.GetSetting("AllowXMLUpload")))
            {
                TheFormInfo tMyFormUp = TheNMIEngine.AddForm(new TheFormInfo(MyBaseThing) { cdeMID = TheThing.GetSafeThingGuid(MyBaseThing, "XMLUPLOAD"), FormTitle = "XML Modbus Definition Uploader", DefaultView = eDefaultView.Form });
                TheNMIEngine.AddFormToThingUX(MyBaseThing, tMyFormUp, "CMyForm", "XML Definition Uploader", 3, 13, 0x80, TheNMIEngine.GetNodeForCategory(), null, null);
                TheNMIEngine.AddSmartControl(MyBaseThing, tMyFormUp, eFieldType.DropUploader, 3, 2, 128, "Drop a XML-file here", null,
                    new nmiCtrlDropUploader { TileHeight = 6, NoTE = true, TileWidth = 6, EngineName = MyBaseEngine.GetEngineName(), MaxFileSize = 10000000 });
            }
            AddConnectionWizard();
            TheNMIEngine.RegisterEngine(MyBaseEngine);      

            mIsUXInitialized = true;
            return true;
        }

        TheDashboardInfo mMyDashboard;

        string LoadXMLDefinition(string pFile)
        {
            string path = TheCommonUtils.cdeFixupFileName(pFile);
            TheBaseAssets.MySYSLOG.WriteToLog(500, new TSM(MyBaseEngine.GetEngineName(), $"Trying to open Modbus configuration file: {pFile}"));

            ModbusConfiguration config = null;
            try
            {
                config = ModbusConfiguration.ReadFromFile(path);
            }
            catch (IOException)
            {
                return "File not found or parsing failed";
            }

            List<TheThing> tDevList = TheThingRegistry.GetThingsOfEngine(MyBaseThing.EngineName); 

            foreach (var dd in config.Devices)
            {
                var tDev = tDevList.Find((t) => t.FriendlyName == dd.Name);
                if (tDev==null || !tDev.HasLiveObject)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(500, new TSM(MyBaseEngine.GetEngineName(), $"Adding Modbus Device {dd.Name}"));
                    var pm = new ModbusTCPDevice(tDev, this, dd);
                    TheThingRegistry.RegisterThing(pm);
                }
            }

            return "XML Definition loaded correctly.";
        }


        void InitServices()
        { 
            List<TheThing> tDevList = TheThingRegistry.GetThingsOfEngine(MyBaseThing.EngineName); 
            if (tDevList.Count > 0)
            {
                foreach (TheThing tDev in tDevList)
                {
                    if (tDev.DeviceType!="IBaseEngine")
                    {
                        if (!tDev.HasLiveObject)
                        {
                            try
                            {
                                switch (tDev.DeviceType)
                                {
                                    case eModbusType.ModbusTCPDevice:
                                        {
                                            var tS = new ModbusTCPDevice(tDev, this, null);
                                            TheThingRegistry.RegisterThing(tS);
                                        }
                                        break;
                                    case eModbusType.ModbusRTUDevice:
                                        {
                                            var tS = new ModbusRTUDevice(tDev, this, null);
                                            TheThingRegistry.RegisterThing(tS);
                                        }
                                        break;
                                }
                            }
                            catch (Exception)
                            {
                                TheBaseAssets.MySYSLOG.WriteToLog(500, new TSM(MyBaseEngine.GetEngineName(), $"Failed to start Modbus Device {tDev.FriendlyName}", eMsgLevel.l1_Error));
                                MyBaseThing.StatusLevel = 2;
                            }
                        }
                    }
                }
            }
            MyBaseEngine.SetStatusLevel(-1); //Calculates the current statuslevel of the service/engine
        }

        private void OnThingDeleted(ICDEThing t, object o)
        {
            // TODO: Close any network connections or free up any other resources held
            List<TheThing> tDevList = TheThingRegistry.GetThingsOfEngine(MyBaseThing.EngineName); 
            if (tDevList.Count > 0)
            {
                foreach (TheThing tDev in tDevList)
                {
                    if (tDev.DeviceType != "IBaseEngine")
                    {
                        //TheThingClass tS = null;
                        //if (!tDev.HasLiveObject)
                        //{
                        //    try
                        //    {
                        //        tDev.OnThingDeleted(t, o); // Only needed if the thing needs to free up any resources
                        //    }
                    }
                }
            }
            MyBaseEngine.SetStatusLevel(-1); //Calculates the current statuslevel of the service/engine
        }


        #region Message Handling
        public override void HandleMessage(ICDEThing sender, object pIncoming)
        {
            TheProcessMessage pMsg = pIncoming as TheProcessMessage;
            if (pMsg == null) return;

            //Console.WriteLine("(PowerMeter Service) Message received: " + pMsg.Message.TXT);

            string[] cmd = pMsg.Message.TXT.Split(':');
            switch (cmd[0])
            {
                case "CDE_INITIALIZED":
                    MyBaseEngine.SetInitialized(pMsg.Message);      //Sets the Service to "Ready". ProcessInitialized() internally contains a call to this Handler and allows for checks right before SetInitialized() is called. 
                    break;
                case "REFFRESHME":
                    InitServices();
                    mMyDashboard.Reload(pMsg, false);
                    break;
            }
        }
        #endregion

        public void AddConnectionWizard()
        {
            var flds = TheNMIEngine.AddNewWizard<TheModbusWiz>(new Guid("{824858DA-061A-4A70-92E3-A31B2D8C7800}"), Guid.Empty, TheNMIEngine.GetEngineDashBoardByThing(MyBaseThing).cdeMID, "Welcome to the Modbus Connection Wizard",
    new nmiCtrlWizard { PanelTitle = "<i class='fa faIcon fa-5x'>&#xf0d0;</i></br>New Modbus Connection", SideBarTitle = "New Modbus Connection Wizard", SideBarIconFA = "&#xf545;", TileThumbnail = "FA5:f545" },
    (myClass, pClientInfo) =>
    {
        myClass.cdeMID = Guid.Empty;
        TheThing tMemTag = null;
        if (myClass.DeviceType=="RTU")
        {
            var tReq = new TheThingRegistry.MsgCreateThingRequestV1()
            {
                EngineName = MyBaseEngine.GetEngineName(),
                DeviceType = eModbusType.ModbusRTUDevice,
                FriendlyName = myClass.ClientName,
                OwnerAddress = MyBaseThing,
                InstanceId = Guid.NewGuid().ToString(),
                Address=myClass.Address,
                Properties= new Dictionary<string, object> { 
                    { "Baudrate", myClass.Baudrate },
                    { "BitFormat", myClass.BitFormat },
                    { "AutoConnect", myClass.AutoConnect },
                    { "SlaveAddress", myClass.SlaveAddress },
                },
                CreateIfNotExist = true
            };
            tMemTag = TheThingRegistry.CreateOwnedThingAsync(tReq).Result;
        }
        else
        {
            var tReq = new TheThingRegistry.MsgCreateThingRequestV1()
            {
                EngineName = MyBaseEngine.GetEngineName(),
                DeviceType = eModbusType.ModbusTCPDevice,
                FriendlyName = myClass.ClientName,
                OwnerAddress = MyBaseThing,
                InstanceId = Guid.NewGuid().ToString(),
                Address = myClass.Address,
                Properties = new Dictionary<string, object> {
                    { "IpPort", myClass.Port },
                    { "AutoConnect", myClass.AutoConnect },
                    { "SlaveAddress", myClass.SlaveAddress },
                },
                CreateIfNotExist = true
            };
            tMemTag = TheThingRegistry.CreateOwnedThingAsync(tReq).Result;

        }
        tTargetButton.SetUXProperty(pClientInfo.NodeID, $"OnClick=TTS:{tMemTag.GetBaseThing().cdeMID}");
    });
            var tMyForm2 = flds["Form"] as TheFormInfo;
            tMyForm2.RegisterEvent2(eUXEvents.OnShow, (para, obj) =>
            {
                var pMsg = para as TheProcessMessage;
                if (pMsg?.Message == null) return;
            });

            var tFlds = TheNMIEngine.AddNewWizardPage(MyBaseThing, tMyForm2, 0, 1, 2, null /*"Name and Address"*/, "3:'<%DeviceType%>'=='RTU'");
            TheNMIEngine.AddWizardControl(MyBaseThing, tMyForm2, eFieldType.SingleEnded, 1, 1, 2, 0, "Connection Name", "ClientName", new TheNMIBaseControl { Explainer = "1. Enter name for the new Modbus connection.", });
            TheNMIEngine.AddWizardControl(MyBaseThing, tMyForm2, eFieldType.ComboBox, 1, 2, 2, 0, "Modbus Type", "DeviceType", new nmiCtrlComboBox { Options="RTU;TCP", Explainer = "2. Select the Type of Modbus Connection.", });

            tFlds = TheNMIEngine.AddNewWizardPage(MyBaseThing, tMyForm2, 1, 2, 0, null /*"Name and Address"*/);
            TheNMIEngine.AddWizardControl(MyBaseThing, tMyForm2, eFieldType.SingleEnded, 2, 1, 2, 0, "IP Address or DNS", "Address", new TheNMIBaseControl { Explainer = "1. Enter IP Address or DNS name of Server.", });
            TheNMIEngine.AddWizardControl(MyBaseThing, tMyForm2, eFieldType.Number,    2, 2, 2, 0, "Port", "Port", new TheNMIBaseControl { DefaultValue = "502", Explainer = "2. Enter port of the modbus server.", });
            TheNMIEngine.AddWizardControl(MyBaseThing, tMyForm2, eFieldType.Number,      2, 3, 2, 0, "Slave Address", "SlaveAddress", new nmiCtrlNumber() { DefaultValue="1", TileWidth = 3, ParentFld = 200, MaxValue = 255, MinValue = 0 });
            TheNMIEngine.AddWizardControl(MyBaseThing, tMyForm2, eFieldType.SingleCheck, 2, 4, 2, 0, "Auto-Connect", "AutoConnect", new nmiCtrlSingleCheck { DefaultValue = "true", Explainer = "2. If selected the connection will auto-connect.", });

            tFlds = TheNMIEngine.AddNewWizardPage(MyBaseThing, tMyForm2, 1, 3, 0, null /*"Name and Address"*/);
            var ComSelect=TheNMIEngine.AddWizardControl(MyBaseThing, tMyForm2, eFieldType.ComboBox, 3, 1, 2, 0, "Select COM Port", "Address", new TheNMIBaseControl { Explainer = "1. Select the COM port connected to your Server.", });
            try
            {
                var sports = SerialPort.GetPortNames();
                string tp = TheCommonUtils.CListToString(sports, ";");
                ComSelect.PropertyBag = new nmiCtrlComboBox { Options = tp };
            }
            catch (Exception eee)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(10000, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, $"GetPortNames failed : {eee}", eMsgLevel.l1_Error));
            }

            TheNMIEngine.AddWizardControl(MyBaseThing, tMyForm2, eFieldType.ComboBox, 3, 2,2, 0, "Baud", "Baudrate", new nmiCtrlComboBox() { TileWidth = 3, ParentFld = 200, Options = "2400;4800;9600;19200;38400;57600" });
            TheNMIEngine.AddWizardControl(MyBaseThing, tMyForm2, eFieldType.ComboBox, 3, 3,2, 0, "Bit Format", "BitFormat", new nmiCtrlComboBox() { TileWidth = 3, ParentFld = 200, Options = "8,N,1:0;8,E,1:1;8,O,1:2" });
            TheNMIEngine.AddWizardControl(MyBaseThing, tMyForm2, eFieldType.Number, 3, 4, 2, 0, "Slave Address", "SlaveAddress", new nmiCtrlNumber() { DefaultValue = "1", TileWidth = 3, ParentFld = 200, MaxValue = 255, MinValue = 0 });
            TheNMIEngine.AddWizardControl(MyBaseThing, tMyForm2, eFieldType.SingleCheck, 3, 5, 2, 0, "Auto-Connect", "AutoConnect", new nmiCtrlSingleCheck { DefaultValue = "true", Explainer = "2. If selected the connection will auto-connect.", });

            TheNMIEngine.AddWizardFinishPage(MyBaseThing, tMyForm2, 5);
            TheNMIEngine.AddWizardControl(MyBaseThing, tMyForm2, eFieldType.SmartLabel, 5, 1, 0, 0, null, null, new nmiCtrlSmartLabel { NoTE = true, TileWidth = 7, Text = "Done...what do you want to do next?", TileHeight = 2 });
            TheNMIEngine.AddWizardControl(MyBaseThing, tMyForm2, eFieldType.TileGroup, 5, 2, 0, 0, null, null, new nmiCtrlTileGroup { TileWidth = 1, TileHeight = 2, TileFactorX = 2 });
            TheNMIEngine.AddWizardControl(MyBaseThing, tMyForm2, eFieldType.TileButton, 5, 3, 2, 0, "Go to Modbus Dashboard", null, new nmiCtrlTileButton { NoTE = true, TileHeight = 2, TileWidth = 3, OnClick = $"TTS:{mMyDashboard.cdeMID}", ClassName = "cdeTransitButton" });
            TheNMIEngine.AddWizardControl(MyBaseThing, tMyForm2, eFieldType.TileGroup, 5, 4, 0, 0, null, null, new nmiCtrlTileGroup { TileWidth = 1, TileHeight = 2 });
            tTargetButton= TheNMIEngine.AddWizardControl(MyBaseThing, tMyForm2, eFieldType.TileButton, 5, 5, 2, 0, "Go to New Connection", null, new nmiCtrlTileButton { NoTE = true, TileHeight = 2, TileWidth = 3, ClassName = "cdeTransitButton" });
        }
        TheFieldInfo tTargetButton;
    }

    public class TheModbusWiz : TheDataBase
    {
        public string ClientName { get; set; }
        public string Address { get; set; }
        public string DeviceType { get; set; }
        public int Port { get; set; }
        public string Baudrate { get; set; }
        public string BitFormat { get; set; }
        public string SlaveAddress { get; set; }
        public bool AutoConnect { get; set; }
    }
}
