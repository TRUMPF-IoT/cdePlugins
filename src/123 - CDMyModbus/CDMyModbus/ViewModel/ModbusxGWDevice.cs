// SPDX-FileCopyrightText: 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using CDMyModbus.ViewModel;
using Modbus.Device;
using Modbus.IO;
using NModbusExt.Config;
using nsCDEngine.BaseClasses;
using nsCDEngine.Engines;
using nsCDEngine.Engines.NMIService;
using nsCDEngine.Engines.StorageService;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.ViewModels;
using System;
using System.Diagnostics;
using System.IO.Ports;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Modbus
{
    [DeviceType(DeviceType = eModbusType.ModbusRTUDevice, Description = "Represents an Modbus RTU connection for xGATEWAY", Capabilities = new[] { eThingCaps.ConfigManagement })]
    public class ModbusXGWDevice : TheBaseModbusDevice, IStreamResource
    {
        public ModbusXGWDevice(TheThing tBaseThing, ICDEPlugin pPluginBase, DeviceDescription pModDeviceDescription):base(tBaseThing,pPluginBase,pModDeviceDescription)
        {
            MyBaseThing.DeviceType = eModbusType.ModbusXGWDevice;
        }

        public override void sinkStoreReady(StoreEventArgs e)
        {
            if (string.IsNullOrEmpty(MyBaseThing.Address) && !string.IsNullOrEmpty(TheBaseAssets.MySettings.GetSetting("DWAutoCreate")))
                MyBaseThing.Address = "127.0.0.1";
            base.sinkStoreReady(e);
        }

        public void Dispose()
        {
        }

        protected override void DoCreateUX()
        {
            MyDashIcon.Category = ".Modbus xGATEWAY";
            TheNMIEngine.GetFieldByFldOrder(MyModConnectForm, 200).Header = "xGATEWAY - Modbus RTU Connectivity";
            TheNMIEngine.GetFieldByFldOrder(MyModConnectForm, 204).Header = "xGATEWAY seriald IP:PORT";

            TheNMIEngine.AddSmartControl(MyBaseThing, MyModConnectForm, eFieldType.ComboBox, 205, 2, 0, "Baud", nameof(Baudrate), new nmiCtrlComboBox() { TileWidth = 3, ParentFld = 200, Options = "600:1;1200:2;1800:3;2400:4;4800:5;9600:6;19200:7;38400:8;57600:9;115200:10;230400:11;460800:12;500000:13;921600:15" });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyModConnectForm, eFieldType.ComboBox, 206, 2, 0, "Bit Format", nameof(BitFormat), new nmiCtrlComboBox() { TileWidth = 3, ParentFld = 200, Options = "8,N,1:0;8,E,1:1;8,O,1:2" });
            if (!string.IsNullOrEmpty(TheBaseAssets.MySettings.GetSetting("DWAutoCreate")))
            {
                var but = TheNMIEngine.AddSmartControl(MyBaseThing, MyModConnectForm, eFieldType.TileButton, 275, 2, 0, "Restart RS485 Daemon", null, new nmiCtrlTileButton() { NoTE = true, ParentFld = 200, AreYouSure="Are you sure?" });
                but.RegisterUXEvent(MyBaseThing, eUXEvents.OnClick, "rest", (sender, obj) =>
                {
                    CloseModBus();
                    "killall -9 seriald".Bash();
                });
            }
        }

        TcpClient mMainSocket;
        NetworkStream mSocketStream;
        public override string OpenModBus()
        {
            if (MyModMaster != null && mMainSocket?.Client != null)
                return null;
            string error = null;
            try
            {
                CloseModBus();
                MyBaseThing.LastMessage = $"{DateTimeOffset.Now}: Opening modbus connection...";

                mMainSocket = new TcpClient();
                var tParts = MyBaseThing.Address.Split(':');
                int port = 3244;
                if (tParts.Length > 1)
                    port = TheCommonUtils.CInt(tParts[1]);
                if (port == 0)
                    port = 3244;
                bool ConnectSuccess = false;
                int trial = 3;
                while (!ConnectSuccess && trial>0)
                {
                    if (IPAddress.TryParse(tParts[0], out IPAddress tad))
                        mMainSocket.Connect(tad, port);
                    else
                        mMainSocket.Connect(tParts[0], port);
                    if (ConnectSuccess=mMainSocket.Connected)
                        break;
                    MyBaseThing.SetStatus(2, $"{DateTimeOffset.Now}: Connect Failed...trying again");
                    TheCommonUtils.SleepOneEye(1000, 100);
                    trial--;
                }
                if (!ConnectSuccess)
                {
                    error = $"{DateTimeOffset.Now}: Connect Failed...check your IP and Port";
                    MyBaseThing.SetStatus(3, error);
                    return error;
                }
                if (!TheBaseAssets.MasterSwitch)
                {
                    CloseModBus();
                    return "shutdown";
                }
                mSocketStream = mMainSocket.GetStream();
                if (Interval > 0)
                {
                    mSocketStream.ReadTimeout = (int)Interval*2;
                    mSocketStream.WriteTimeout = (int)Interval*2;
                    ReadTimeout = (int)Interval*2;
                    WriteTimeout = (int)Interval*2;
                }
                byte[] cmd = new byte[] { 0x69, 0x88, 0x89, 0, 0x08, 0, 0, 0, 0x03, 0xE8 };
                cmd[3] = (byte)Baudrate;
                switch (BitFormat)
                {
                    case 2:
                        cmd[5] = 1;
                        break;
                    case 1:
                        cmd[5] = 0;
                        break;
                    default:
                        cmd[5] = 2;
                        break;
                }
                // Send the message to the connected TcpServer.
                mSocketStream.Write(cmd, 0, cmd.Length);
                //TheCommonUtils.SleepOneEye(1000, 100);
                byte[] recBuf = new byte[] { 0xff,0,0,0,0 };
                Int32 bytes = mSocketStream.Read(recBuf, 0, 1);
                switch (recBuf[0])
                {
                    case 1:
                        {
                            error = "Cannot connect - bad args provided";
                            MyBaseThing.SetStatus(3, error);
                            return error;
                        }
                    case 2:
                        {
                            error = "Cannot connect - error opening the Port";
                            MyBaseThing.SetStatus(3, error);
                            return error;
                        }
                }
                // create modbus slave
                MyModMaster = ModbusSerialMaster.CreateRtu(this);
            }
            catch (SocketException e)
            {
                error = e.Message;
                MyBaseThing.SetStatus(3, error);
                CloseModBus();
            }
            return error;
        }

        public override void CloseModBus()
        {
            MyBaseThing.LastMessage = $"{DateTimeOffset.Now}: closing modbus...";

            try
            {
                mSocketStream?.Close();
                mMainSocket?.Close();
            }
            catch { }
            mSocketStream = null;
            mMainSocket = null;
            MyBaseThing.LastMessage = $"{DateTimeOffset.Now}: RS485 connection closed";

            try
            {
                MyModMaster?.Dispose();
            }
            catch { }
            MyModMaster = null;
            MyBaseThing.LastMessage = $"{DateTimeOffset.Now}: modbus master closed";
            TheBaseAssets.MySYSLOG.WriteToLog(10000, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, MyBaseThing.LastMessage, eMsgLevel.l4_Message));
        }

        public int InfiniteTimeout
        {
            get { return SerialPort.InfiniteTimeout; }
        }

        int mReadTimeout = 0;
        int mWriteTimeout = 0;
        public int ReadTimeout
        {
            get { return mReadTimeout; }
            set { mWriteTimeout = value; }
        }

        public int WriteTimeout
        {
            get { return mWriteTimeout; }
            set { mWriteTimeout = value; }
        }

        public void DiscardInBuffer()
        {
            //nope
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            int resbytes = mSocketStream.Read(buffer, offset, count);
            TheBaseAssets.MySYSLOG.WriteToLog(6280, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(MyBaseThing.EngineName, $"({MyBaseThing.FriendlyName}) has received: {resbytes} bytes: {ByteArrayToString(buffer)}", eMsgLevel.l6_Debug));
            return resbytes;
        }

        public void Write(byte[] buffer, int offset, int count)
        {
            TheBaseAssets.MySYSLOG.WriteToLog(6280, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(MyBaseThing.EngineName, $"({MyBaseThing.FriendlyName}) is sending: {(count > 0 ? count: buffer.Length)} bytes", eMsgLevel.l6_Debug));

            // Send the message to the connected TcpServer.
            mSocketStream.Write(buffer, 0, count > 0 ? count : buffer.Length);
            //mMainSocket.Client.Send(buffer, offset, count > 0 ? count : buffer.Length, SocketFlags.None);
        }

        public static string ByteArrayToString(byte[] ba)
        {
            if (ba == null)
                return null;
            StringBuilder hex = new StringBuilder(ba.Length * 2);
            foreach (byte b in ba)
                hex.AppendFormat("{0:x2}:", b);
            return hex.ToString();
        }
    }

    public static class ShellHelper
    {
        public static string Bash(this string cmd)
        {
            var escapedArgs = cmd.Replace("\"", "\\\"");

            var process = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"-c \"{escapedArgs}\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };

            process.Start();
            string result = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            return result;
        }
    }
}
