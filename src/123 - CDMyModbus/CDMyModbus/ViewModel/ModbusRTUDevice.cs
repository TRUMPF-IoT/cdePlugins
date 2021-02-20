// SPDX-FileCopyrightText: 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using CDMyModbus.ViewModel;
using Modbus.Device;
using Modbus.Serial;
using NModbusExt.Config;
using nsCDEngine.BaseClasses;
using nsCDEngine.Engines;
using nsCDEngine.Engines.NMIService;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.ViewModels;
using System;
using System.IO.Ports;
using System.Net.Sockets;

namespace Modbus
{
    [DeviceType(DeviceType = eModbusType.ModbusRTUDevice, Description = "Represents an Modbus RTU connection", Capabilities = new[] { eThingCaps.ConfigManagement })]
    public class ModbusRTUDevice : TheBaseModbusDevice
    {
        public ModbusRTUDevice(TheThing tBaseThing, ICDEPlugin pPluginBase, DeviceDescription pModDeviceDescription):base(tBaseThing,pPluginBase,pModDeviceDescription)
        {
            MyBaseThing.DeviceType = eModbusType.ModbusRTUDevice;
        }

        protected override void DoCreateUX()
        {
            MyDashIcon.Category = ".Modbus RTU";
            TheNMIEngine.GetFieldByFldOrder(MyModConnectForm, 200).Header = "Modbus RTU Connectivity";
            var tAdrField = TheNMIEngine.GetFieldByFldOrder(MyModConnectForm, 204);
            tAdrField.Header = "COM Port";
            tAdrField.Type = eFieldType.ComboBox;
            try
            {
                var sports = SerialPort.GetPortNames();
                string tp = TheCommonUtils.CListToString(sports, ";");
                tAdrField.PropertyBag = new nmiCtrlComboBox { Options = tp };
            }
            catch (Exception eee) 
            {
                TheBaseAssets.MySYSLOG.WriteToLog(10000, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, $"GetPortNames failed : {eee}", eMsgLevel.l1_Error));
            }
            TheNMIEngine.AddSmartControl(MyBaseThing, MyModConnectForm, eFieldType.ComboBox, 205, 2, 0, "Baud", nameof(Baudrate), new nmiCtrlComboBox() { TileWidth = 3, ParentFld = 200, Options = "2400;4800;9600;19200;38400;57600" });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyModConnectForm, eFieldType.ComboBox, 206, 2, 0, "Bit Format", nameof(BitFormat), new nmiCtrlComboBox() { TileWidth = 3, ParentFld = 200, Options = "8,N,1:0;8,E,1:1;8,O,1:2" });
        }

        SerialPort slavePort;
        public override string OpenModBus()
        {
            if (MyModMaster != null && slavePort != null && slavePort.IsOpen)
                return null;
            string error = null;
            try
            {
                CloseModBus();
                MyBaseThing.LastMessage = $"{DateTimeOffset.Now}: Opening modbus connection...";

                slavePort = new SerialPort(MyBaseThing.Address);
                if (Baudrate == 0) Baudrate = 9600;
                slavePort.BaudRate = Baudrate; //Todo: get from UX
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
                if (Interval > 0)
                {
                    slavePort.ReadTimeout = (int)Interval * 2;
                    slavePort.WriteTimeout = (int)Interval * 2;
                }

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

        public override void CloseModBus()
        {
            MyBaseThing.SetStatus(0,$"{DateTimeOffset.Now}: closing modbus...");

            try
            {
                slavePort?.Close();
            }
            catch { }
            slavePort = null;
            MyBaseThing.SetStatus(0, $"{DateTimeOffset.Now}: modbus closed");

            try
            {
                MyModMaster?.Dispose();
            }
            catch { }
            MyModMaster = null;
            MyBaseThing.SetStatus(0,$"{DateTimeOffset.Now}: modbus master closed");
        }
    }
}
