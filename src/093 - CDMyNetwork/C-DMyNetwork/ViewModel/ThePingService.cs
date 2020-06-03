// SPDX-FileCopyrightText: 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0
using nsCDEngine.BaseClasses;
using nsCDEngine.Engines;
using nsCDEngine.Engines.NMIService;
using nsCDEngine.Engines.ThingService;
using System;
using System.Net.NetworkInformation;
using System.Threading.Tasks;

namespace CDMyNetwork.ViewModel
{
    class ThePingService : TheNetworkServiceBase
    {
        public ThePingService(TheThing tBaseThing, ICDEPlugin pPluginBase)
            : base(tBaseThing, pPluginBase)
        {
        }

        public override bool Init()
        {
            if (!base.Init(false)) return false;
            MyBaseThing.LastMessage = "Ping ready";
            if (FailureLimit < 1) FailureLimit = 1;
            if (PingDelay == 0) PingDelay = 3000;
            if (PingTimeOut == 0) PingTimeOut = 3000;
            if (PingDelay < 1000)
                PingDelay = 1000;
            if (PingTimeOut < 50)
                PingTimeOut = 50;
            if (AutoConnect)
                Connect();

            mIsInitialized = true;
            return true;
        }

        public override void Connect()
        {
            IsConnected = true;
            MyBaseThing.LastMessage = string.Format("Connected at {0}", DateTimeOffset.Now);
            MyBaseThing.StatusLevel = 1;
            TheCommonUtils.cdeRunAsync("Ping-" + MyBaseThing.FriendlyName, true, sinkPinger);
        }
        public override void Disconnect()
        {
            IsConnected = false;
            MyBaseThing.StatusLevel = 0;
            MyBaseThing.LastMessage = string.Format("Disconnected at {0}", DateTimeOffset.Now);
        }

        public override void InitUX()
        {
            const string pingParameterLabel = "###PingParameterLabel#Ping Parameter...###";
            const string pingDelayLabel = "###PingDelayLabel#Ping Delay###";
            const string pingTimeOutLabel = "###PingTimeOutLabel#Ping Time Out###";
            const string pingFailureLimitLabel = "###PingFailureLimitLabel#Failure Limit###";
            const string pingTripTimeLabel = "###PingTripTimeLabel#Trip Time###";
            const string pingSendRttLabel = "###PingSendRttLabel#Send RTT###";

            base.InitUX();
            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.CollapsibleGroup, 300, 2, 0xc0, pingParameterLabel, null, new nmiCtrlCollapsibleGroup { DoClose = true, ParentFld = 120, /*TileWidth = 11, TileFactorX=2, */IsSmall = true });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.Number, 301, 2, 0xc0, pingDelayLabel, false, "PingDelay", null, new nmiCtrlNumber { TileWidth=4, ParentFld=300 });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.Number, 302, 2, 0xc0, pingTimeOutLabel, false, "PingTimeOut", null, new nmiCtrlNumber { TileWidth=4, ParentFld=300 });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.Number, 303, 2, 0xc0, pingFailureLimitLabel, false, "FailureLimit", null, new nmiCtrlNumber { TileWidth=4, ParentFld=300 });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.Number, 304, 0, 0x00, pingTripTimeLabel, false, "RoundTripTime", null, new nmiCtrlNumber { TileWidth=4, ParentFld=300 });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.SingleCheck, 305, 2, 0x00, pingSendRttLabel, false, "AllowRTT", null, new nmiCtrlSingleCheck { TileWidth=3, ParentFld=300 });
        }



        public int PingTimeOut
        {
            get { return TheCommonUtils.CInt(TheThing.GetSafePropertyNumber(MyBaseThing, "PingTimeOut")); }
            private set { TheThing.SetSafePropertyNumber(MyBaseThing, "PingTimeOut", value); }
        }
        public int PingDelay
        {
            get { return TheCommonUtils.CInt(TheThing.GetSafePropertyNumber(MyBaseThing, "PingDelay")); }
            private set { TheThing.SetSafePropertyNumber(MyBaseThing, "PingDelay", value); }
        }
        public int FailureLimit
        {
            get { return TheCommonUtils.CInt(TheThing.GetSafePropertyNumber(MyBaseThing, "FailureLimit")); }
            private set { TheThing.SetSafePropertyNumber(MyBaseThing, "FailureLimit", value); }
        }
        public long RoundTripTime
        {
            get { return TheCommonUtils.CLng(TheThing.GetSafePropertyNumber(MyBaseThing, "RoundTripTime")); }
            private set {
                TheThing.SetSafePropertyNumber(MyBaseThing, "RoundTripTime", value);
                TheThing.SetSafePropertyNumber(MyBaseThing, "Value", value);
            }
        }

        int FailCounter;
        async void sinkPinger(object state)
        {
            Ping p = new Ping();
            while (IsConnected && TheBaseAssets.MasterSwitch)
            {
                try
                {
                    if (!IsConnected) return;
                    var reply = await SendPingAsync(p, MyBaseThing.Address, PingTimeOut).ConfigureAwait(false);
                    if (!IsConnected) return; //Do not change status if disconnected
                    if (reply.Status != IPStatus.Success)
                    {
                        MyBaseThing.LastMessage = string.Format("{0} at {1}", reply.Status, DateTimeOffset.Now);
                        FailCounter++;
                        MyBaseThing.StatusLevel = FailCounter > FailureLimit ? 3 : 2;
                        RoundTripTime = PingTimeOut;
                    }
                    else
                    {
                        if (TheThing.GetSafePropertyBool(MyBaseThing, "AllowRTT"))
                        {
                            RoundTripTime = reply.RoundtripTime;
                            MyBaseThing.LastUpdate = DateTimeOffset.Now;
                        }
                        MyBaseThing.StatusLevel = 1;
                        FailCounter = 0;
                    }
                }
                catch (Exception e) 
                {
                    if (!IsConnected) return;
                    MyBaseThing.LastMessage = string.Format("{0} at {1}", e, DateTimeOffset.Now);
                    MyBaseThing.StatusLevel = 3;
                    RoundTripTime = PingTimeOut;
                    if (!AutoConnect)
                    {
                        IsConnected = false;
                        return;
                    }
                }
                if (!TheBaseAssets.MasterSwitch) return;
                await TheCommonUtils.TaskDelayOneEye(PingDelay, 100).ConfigureAwait(false);
            }
            MyBaseThing.LastMessage = string.Format("Disconnected at {0}", DateTimeOffset.Now);
            MyBaseThing.LastUpdate = DateTimeOffset.Now;
        }

        Task<PingReply> SendPingAsync(Ping p, string hostnameOrAddress, int timeout)
        {
            p.PingCompleted -= OnPingCompleted;
            p.PingCompleted += OnPingCompleted;
            var pingTS = new TaskCompletionSource<PingReply>();
            p.SendAsync(MyBaseThing.Address, PingTimeOut, pingTS);
            return pingTS.Task;
        }

        private void OnPingCompleted(object sender, PingCompletedEventArgs e)
        {
            //if (e.Cancelled)
            //{
            //    if (!pingTS.TrySetCanceled())
            //    {
            //    }
            //}
            var pingTS = e.UserState as TaskCompletionSource<PingReply>;
            if (pingTS == null)
            {
                return;
            }
            if (e.Error != null)
            {
                if (!pingTS.TrySetException(e.Error))
                {
                }
            }
            else
            {
                if (!pingTS.TrySetResult(e.Reply))
                {
                }
            }
        }
    }
}
