// SPDX-FileCopyrightText: 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using CDMyModbus.ViewModel;
using Modbus.Device;
using NModbusExt.Config;
using nsCDEngine.Engines;
using nsCDEngine.Engines.NMIService;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.ViewModels;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace Modbus
{
    [DeviceType(DeviceType = eModbusType.ModbusTCPDevice, Description = "Represents an Modbus TCP connection", Capabilities = new[] { eThingCaps.ConfigManagement })]
    public class ModbusTCPDevice : TheBaseModbusDevice
    {
        [ConfigProperty]
        uint CustomPort
        {
            get { return (uint)TheThing.GetSafePropertyNumber(MyBaseThing, nameof(CustomPort)); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, nameof(CustomPort), value); }
        }

        public ModbusTCPDevice(TheThing tBaseThing, ICDEPlugin pPluginBase, DeviceDescription pModDeviceDescription):base(tBaseThing, pPluginBase, pModDeviceDescription)
        {
            MyBaseThing.DeviceType = eModbusType.ModbusTCPDevice;
        }

        public override void DoInit()
        {
            MyBaseThing.DeclareConfigProperty(new TheThing.TheConfigurationProperty { Name = nameof(CustomPort), cdeT = ePropertyTypes.TNumber, DefaultValue = 502, Description = "" });
            if (string.IsNullOrEmpty(MyBaseThing.ID))
            {
                if (GetProperty("CustomPort", false) == null)
                    CustomPort = 502;
            }
        }

        protected override void DoCreateUX()
        {
            MyDashIcon.Category = ".Modbus TCP";
            TheNMIEngine.GetFieldByFldOrder(MyModConnectForm, 200).Header = "Modbus TCP Connectivity";
            TheNMIEngine.AddSmartControl(MyBaseThing, MyModConnectForm, eFieldType.Number, 205, 2, 0, "Custom Port", nameof(CustomPort), new nmiCtrlSingleEnded() { TileWidth = 3, ParentFld = 200 });

        }

        TcpClient tcpClient;
        public override string OpenModBus()
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

        public override void CloseModBus()
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

        public override ushort[] ReadOneField(int tReadWay, byte tSlaveAddress, ushort address, FieldMapping field, Dictionary<string, object> dict)
        {
            ushort[] data = null;
            switch (tReadWay)
            {
                case 1:
                    {
                        bool[] datab = (MyModMaster as ModbusIpMaster)?.ReadCoils((ushort)address, (ushort)field.SourceSize);
                        for (int i = 0; i < field.SourceSize && i < datab.Length; i++)
                        {
                            dict[$"{field.PropertyName}_{i}"] = datab[i];
                        }
                        field.Value = dict[$"{field.PropertyName}_0"];
                    }
                    break;
                case 2:
                    {
                        bool[] datab = (MyModMaster as ModbusIpMaster)?.ReadInputs((ushort)address, (ushort)field.SourceSize);
                        for (int i = 0; i < field.SourceSize && i < datab.Length; i++)
                        {
                            dict[$"{field.PropertyName}_{i}"] = datab[i];
                        }
                        field.Value = dict[$"{field.PropertyName}_0"];
                    }
                    break;
                case 4:
                    data = (MyModMaster as ModbusIpMaster)?.ReadInputRegisters((byte)tSlaveAddress, (ushort)address, (ushort)field.SourceSize);
                    break;
                default:
                    data = (MyModMaster as ModbusIpMaster)?.ReadHoldingRegisters((byte)tSlaveAddress, (ushort)address, (ushort)field.SourceSize);
                    break;
            }
            return data;
        }
    }
}
