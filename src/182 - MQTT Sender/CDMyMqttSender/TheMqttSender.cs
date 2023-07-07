// SPDX-FileCopyrightText: 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Threading.Tasks;
using System.Threading;

using nsCDEngine.Engines;
using nsCDEngine.BaseClasses;
using nsCDEngine.ViewModels;
using nsCDEngine.Engines.NMIService;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.Communication;

using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;
using System.IO;
using nsTheSenderBase;
using nsTheEventConverters;
using System.Security.Cryptography.X509Certificates;

namespace CDMyMqttSender.ViewModel
{
    [DeviceType(DeviceType = MqttDeviceTypes.MqttSender, Capabilities = new[] { eThingCaps.ConfigManagement, eThingCaps.SensorConsumer })]
    public class TheMqttSender : TheSenderBase
    {

        #region ThingProperties

        [ConfigProperty(Secure = true)]
        public string MqttSslCertificate
        {
            get { return TheThing.MemberGetSafePropertyString(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyString(MyBaseThing, value); }
        }
        [ConfigProperty(DefaultValue = "127.0.0.1")]
        public string MqttHostName
        {
            get { return TheThing.MemberGetSafePropertyString(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyString(MyBaseThing, value); }
        }

        [ConfigProperty(DefaultValue = 1883)]
        public int MqttPort
        {
            get { return TheCommonUtils.CInt(TheThing.MemberGetSafePropertyNumber(MyBaseThing)); }
            set { TheThing.MemberSetSafePropertyNumber(MyBaseThing, value); }
        }
        [ConfigProperty()]
        public bool MqttSecure
        {
            get { return TheThing.MemberGetSafePropertyBool(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyBool(MyBaseThing, value); }
        }
        [ConfigProperty(DefaultValue = "/$thingFriendlyName$/THINGUPDATES")]
        public string MqttTopicTemplate
        {
            get { return TheThing.MemberGetSafePropertyString(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyString(MyBaseThing, value); }
        }
        [ConfigProperty()]
        public string MqttClientId
        {
            get { return TheThing.MemberGetSafePropertyString(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyString(MyBaseThing, value); }
        }
        [ConfigProperty(DefaultValue = (double) MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE)]
        public byte MqttQoS
        {
            get { return TheCommonUtils.CByte(TheThing.MemberGetSafePropertyNumber(MyBaseThing)); }
            set { TheThing.MemberSetSafePropertyNumber(MyBaseThing, value); }
        }
        [ConfigProperty()]
        public bool MqttRetain
        {
            get { return TheThing.MemberGetSafePropertyBool(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyBool(MyBaseThing, value); }
        }
        [ConfigProperty()]
        public string MqttUsername
        {
            get { return TheThing.MemberGetSafePropertyString(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyString(MyBaseThing, value); }
        }
        [ConfigProperty(Secure = true)]
        public string MqttPassword
        {
            get { return TheThing.MemberGetSafePropertyString(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyString(MyBaseThing, value); }
        }
        [ConfigProperty(DefaultValue = 60)]
        public ushort MqttKeepAlivePeriodInSeconds
        {
            get { return TheCommonUtils.CUShort(TheThing.MemberGetSafePropertyNumber(MyBaseThing)); }
            set { TheThing.MemberSetSafePropertyNumber(MyBaseThing, value); }
        }
        [ConfigProperty()]
        public bool MqttWillFlag
        {
            get { return TheThing.MemberGetSafePropertyBool(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyBool(MyBaseThing, value); }
        }
        [ConfigProperty()]
        public bool MqttWillRetain
        {
            get { return TheThing.MemberGetSafePropertyBool(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyBool(MyBaseThing, value); }
        }
        [ConfigProperty(DefaultValue = (double)MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE)]
        public byte MqttWillQoS
        {
            get { return TheCommonUtils.CByte(TheThing.MemberGetSafePropertyNumber(MyBaseThing)); }
            set { TheThing.MemberSetSafePropertyNumber(MyBaseThing, value); }
        }
        [ConfigProperty()]
        public string MqttWillTopic
        {
            get { return TheThing.MemberGetSafePropertyString(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyString(MyBaseThing, value); }
        }
        [ConfigProperty()]
        public string MqttWillMessage
        {
            get { return TheThing.MemberGetSafePropertyString(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyString(MyBaseThing, value); }
        }
        [ConfigProperty()]
        public string MqttConnectTopicTemplate
        {
            get { return TheThing.MemberGetSafePropertyString(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyString(MyBaseThing, value); }
        }
        [ConfigProperty()]
        public string MqttDisconnectTopicTemplate
        {
            get { return TheThing.MemberGetSafePropertyString(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyString(MyBaseThing, value); }
        }

        [ConfigProperty()]
        public bool SendAsTSM
        {
            get { return TheThing.MemberGetSafePropertyBool(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyBool(MyBaseThing, value); }
        }
        [ConfigProperty()]
        public bool SendAsTDM
        {
            get { return TheThing.MemberGetSafePropertyBool(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyBool(MyBaseThing, value); }
        }

        [ConfigProperty()]
        public int MQTTMaxPayloadSizeForTDM
        {
            get { return (int)TheThing.MemberGetSafePropertyNumber(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyNumber(MyBaseThing, value); }
        }

        [ConfigProperty()]
        public string FilePushUrl
        {
            get { return TheThing.MemberGetSafePropertyString(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyString(MyBaseThing, value); }
        }

        [ConfigProperty()]
        public int KPILogIntervalInMinutes
        {
            get { return TheCommonUtils.CInt(TheThing.MemberGetSafePropertyNumber(MyBaseThing)); }
            set { TheThing.MemberSetSafePropertyNumber(MyBaseThing, value); }
        }

        [ConfigProperty()]
        public int WatchDogIdleRecycleIntervalInMinutes
        {
            get { return TheCommonUtils.CInt(TheThing.MemberGetSafePropertyNumber(MyBaseThing)); }
            set { TheThing.MemberSetSafePropertyNumber(MyBaseThing, value); }
        }

        [ConfigProperty()]
        public string KPIPublishPropertyName
        {
            get { return TheThing.MemberGetSafePropertyString(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyString(MyBaseThing, value); }
        }

        [ConfigProperty()]
        public bool EnableLogSentPayloads
        {
            get { return TheThing.MemberGetSafePropertyBool(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyBool(MyBaseThing, value); }
        }

        [ConfigProperty(DefaultValue = int.MaxValue)]
        public int MaxEventDataSize
        {
            get { return TheCommonUtils.CInt(TheThing.MemberGetSafePropertyNumber(MyBaseThing)); }
            set { TheThing.MemberSetSafePropertyNumber(MyBaseThing, value); }
        }

#endregion

        public override void HandleMessage(ICDEThing sender, object pIncoming)
        {
            TheProcessMessage pMsg = pIncoming as TheProcessMessage;
            if (pMsg == null || pMsg.Message == null) return;
            var cmd = pMsg.Message.TXT.Split(':');
            switch (cmd[0])
            {
                default:
                    base.HandleMessage(sender, pIncoming);
                    break;
            }
        }

        public TheMqttSender(TheThing pThing, ICDEPlugin pPluginBase) : base(pThing, pPluginBase)
        {
        }

        const string strMqttSender = "Mqtt Sender";

        public override bool Init()
        {
            base.Init();
            return InitAsync().Result;
        }

        public Task<bool> InitAsync()
        {
            MyBaseThing.RegisterOnChange("LastSendAttemptTime", OnSendAttempt);

            var result = InitBase(MqttDeviceTypes.MqttSender);

            return TheCommonUtils.TaskFromResult(IsInit());
        }

        DateTimeOffset lastKPILogTime = DateTimeOffset.MinValue;
        private void OnSendAttempt(cdeP notUsed)
        {
            if (KPILogIntervalInMinutes > 0 && DateTimeOffset.Now - lastKPILogTime > new TimeSpan(0, 0, KPILogIntervalInMinutes, 0))
            {
                lastKPILogTime = DateTimeOffset.Now - new TimeSpan(0, 0, 0, 5);  // Give a few seconds leeway so the next timer doesn't miss the window
                LogKPIs();
            }
        }

        private void LogKPIs()
        {
            var kpis = new Dictionary<string, object>();
            foreach (var kpiPropName in new List<string> {
                nameof(EventsSentSinceStart), nameof(PendingEvents), nameof(EventsSentErrorCountSinceStart),
                nameof(LastSendTime), nameof(LastSendAttemptTime),
            })
            {
                kpis.Add(kpiPropName, TheThing.GetSafeProperty(MyBaseThing, kpiPropName));
            }
            var message = new TSM(strMqttSender, "Mqtt Sender KPIs", eMsgLevel.l4_Message, TheCommonUtils.SerializeObjectToJSONString(kpis));

            TheBaseAssets.MySYSLOG.WriteToLog(95013, TSM.L(eDEBUG_LEVELS.OFF) ? null : message);
            if (!String.IsNullOrEmpty(KPIPublishPropertyName))
            {
                foreach (var senderThing in MySenderThings.MyMirrorCache.TheValues)
                {
                    TheThing.SetSafeProperty(senderThing.GetThing(), KPIPublishPropertyName, new TSMObjectPayload(message, kpis), ePropertyTypes.NOCHANGE); // CODE REVIEW: OK to put an entire message here? This will be serialized to JSON properly, but...
                }
            }
        }

        // Helper class to generate JSON with the KPI as JSON, not an embedded string (PLS). TODO v4: Move this capability into TSM? V3.2: Move to common utils?
        class TSMObjectPayload : TSM
        {
            public TSMObjectPayload(TSM tsm, object payloadObject)
            {
                this.PLO = payloadObject;
                this.PLS = null;

                this.TXT = tsm.TXT;
                this.TIM = tsm.TIM;
                this.FLG = tsm.FLG;
                this.ORG = tsm.ORG;//ORG-OK
                this.QDX = tsm.QDX;
                this.LVL = tsm.LVL;
                this.ENG = tsm.ENG;
                this.FID = tsm.FID;
                this.SID = tsm.SID;
                this.SEID = tsm.SEID;
                this.CST = tsm.CST;
                this.UID = tsm.UID;
                this.OWN = tsm.OWN;
                this.PLB = tsm.PLB;

            }
            public object PLO;
        }


        private async void OnWatchDogTimer(object state)
        {
            try
            {
                OnSendAttempt(null);
                var timeSinceLastAttempt = DateTimeOffset.Now - LastSendAttemptTime;
                if (timeSinceLastAttempt > new TimeSpan(0, 0, WatchDogIdleRecycleIntervalInMinutes, 0))
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(95014, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(strMqttSender, $"WatchDog: No activity since {LastSendAttemptTime}. Disconnecting Mqtt Sender and waiting 5 seconds to reconnect.", eMsgLevel.l4_Message));
                    Disconnect(true);
                    try
                    {
                        await TheCommonUtils.TaskDelayOneEye(5000, 100);
                    }
                    catch (TaskCanceledException) { }

                    if (TheBaseAssets.MasterSwitch)
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(95015, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(strMqttSender, $"WatchDog: Reconnecting Mqtt Sender.", eMsgLevel.l4_Message));
                        Connect();
                    }
                }
            }
            catch (Exception ex)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(95302, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(strMqttSender, $"WatchDog: Internal error.", eMsgLevel.l1_Error, ex.ToString()));

            }
        }

        public override bool CreateUX()
        {
            UXNoAdvancedConfig = true;
            CreateUXBase(strMqttSender);
            if (MyForm != null)
            {
                //NUI Definition for All clients
                TheNMIEngine.AddSmartControl(MyBaseThing, MyForm, eFieldType.CollapsibleGroup, 40, 2, 0xC0, "MQTT Configurations...", null, ThePropertyBag.Create(new nmiCtrlCollapsibleGroup() { ParentFld = 1, TileWidth = 6, DoClose = true, IsSmall = true }));//() { "TileWidth=6", "Format=Advanced Configurations", "Style=font-size:26px;text-align: left" });

                TheNMIEngine.AddSmartControl(MyBaseThing, MyForm, eFieldType.SingleEnded, 41, 2, 0xC0, "Mqtt Hostname", nameof(MqttHostName), new ThePropertyBag() { "ParentFld=40" });
                TheNMIEngine.AddSmartControl(MyBaseThing, MyForm, eFieldType.Number, 42, 2, 0xC0, "Mqtt Port", nameof(MqttPort), new ThePropertyBag() { "ParentFld=40", "TileWidth=3" });
                TheNMIEngine.AddSmartControl(MyBaseThing, MyForm, eFieldType.ComboBox, 43, 2, 0xC0, "Mqtt QoS", nameof(MqttQoS), new nmiCtrlComboBox { Options = "0;1;2", ParentFld = 40, TileWidth = 3 });
                TheNMIEngine.AddSmartControl(MyBaseThing, MyForm, eFieldType.SingleCheck, 45, 2, 0xC0, "Use SSL", nameof(MqttSecure), new nmiCtrlSingleCheck { ParentFld = 40, TileWidth = 3 });
                TheNMIEngine.AddSmartControl(MyBaseThing, MyForm, eFieldType.SingleEnded, 46, 2, 0xC0, "Mqtt Topic Template", nameof(MqttTopicTemplate), new ThePropertyBag() { "ParentFld=40" });
                TheNMIEngine.AddSmartControl(MyBaseThing, MyForm, eFieldType.SingleEnded, 47, 2, 0xC0, "Username", nameof(MqttUsername), new ThePropertyBag() { "ParentFld=40" });
                TheNMIEngine.AddSmartControl(MyBaseThing, MyForm, eFieldType.Password, 48, 3, 0xC0, "Password", nameof(MqttPassword), new nmiCtrlPassword() { ParentFld=40, HideMTL=true });
                TheNMIEngine.AddSmartControl(MyBaseThing, MyForm, eFieldType.SingleEnded, 49, 2, 0xC0, "Mqtt Client Id", nameof(MqttClientId), new ThePropertyBag() { "ParentFld=40" });
                TheNMIEngine.AddSmartControl(MyBaseThing, MyForm, eFieldType.Number, 50, 2, 0xC0, "Mqtt Keep Alive (s)", nameof(MqttKeepAlivePeriodInSeconds), new ThePropertyBag() { "ParentFld=40", "TileWidth=3" });
                TheNMIEngine.AddSmartControl(MyBaseThing, MyForm, eFieldType.SingleCheck, 52, 2, 0xC0, "Mqtt Retain", nameof(MqttRetain), new nmiCtrlSingleCheck { ParentFld = 40, TileWidth = 3 });

                TheNMIEngine.AddSmartControl(MyBaseThing, MyForm, eFieldType.CollapsibleGroup, 64, 2, 0xC0, "Mqtt 'Will' options", null, new nmiCtrlCollapsibleGroup { ParentFld = 40, IsSmall = true, DoClose = true });
                TheNMIEngine.AddSmartControl(MyBaseThing, MyForm, eFieldType.SingleCheck, 65, 2, 0xC0, "Mqtt Will Flag", nameof(MqttWillFlag), new nmiCtrlSingleCheck { ParentFld = 64, TileWidth = 3 });
                TheNMIEngine.AddSmartControl(MyBaseThing, MyForm, eFieldType.SingleCheck, 66, 2, 0xC0, "Mqtt Will Retain", nameof(MqttWillRetain), new nmiCtrlSingleCheck { ParentFld = 64, TileWidth = 3 });
                TheNMIEngine.AddSmartControl(MyBaseThing, MyForm, eFieldType.SingleEnded, 67, 2, 0xC0, "Mqtt Will Topic", nameof(MqttWillTopic), new ThePropertyBag() { "ParentFld=64" });
                TheNMIEngine.AddSmartControl(MyBaseThing, MyForm, eFieldType.SingleEnded, 68, 2, 0xC0, "Mqtt Will Message", nameof(MqttWillMessage), new ThePropertyBag() { "ParentFld=64" });
                TheNMIEngine.AddSmartControl(MyBaseThing, MyForm, eFieldType.ComboBox, 69, 2, 0xC0, "Mqtt Will QoS", nameof(MqttWillQoS), new nmiCtrlComboBox { Options = "0;1;2", ParentFld = 64 });

                TheNMIEngine.AddSmartControl(MyBaseThing, MyForm, eFieldType.CollapsibleGroup, 70, 2, 0xC0, "Additional options", null, new nmiCtrlCollapsibleGroup { ParentFld = 40, IsSmall = true, DoClose = true });
                TheNMIEngine.AddSmartControl(MyBaseThing, MyForm, eFieldType.SingleEnded, 71, 2, 0xC0, "Mqtt Connect Topic Template", nameof(MqttConnectTopicTemplate), new ThePropertyBag() { "ParentFld=70" });
                TheNMIEngine.AddSmartControl(MyBaseThing, MyForm, eFieldType.SingleEnded, 72, 2, 0xC0, "Mqtt Disconnect Topic Template", nameof(MqttDisconnectTopicTemplate), new ThePropertyBag() { "ParentFld=70" });
                TheNMIEngine.AddSmartControl(MyBaseThing, MyForm, eFieldType.SingleCheck, 73, 2, 0xC0, "Send as System Message", nameof(SendAsTSM), new nmiCtrlSingleCheck { ParentFld = 70, TileWidth = 3 });
                TheNMIEngine.AddSmartControl(MyBaseThing, MyForm, eFieldType.SingleCheck, 74, 2, 0xC0, "Send as Device Message", nameof(SendAsTDM), new nmiCtrlSingleCheck { ParentFld = 70, TileWidth = 3 });
                TheNMIEngine.AddSmartControl(MyBaseThing, MyForm, eFieldType.Number, 75, 2, 0xC0, "Max Payload Size TDM", nameof(MQTTMaxPayloadSizeForTDM), new nmiCtrlNumber { ParentFld = 70 });
                TheNMIEngine.AddSmartControl(MyBaseThing, MyForm, eFieldType.SingleEnded, 76, 2, 0xC0, "File Push Url", nameof(FilePushUrl), new ThePropertyBag() { "ParentFld=70" });

                TheNMIEngine.AddSmartControl(MyBaseThing, MyForm, eFieldType.Number, 210, 2, 0xC0, "KPI Log Interval (minutes)", nameof(KPILogIntervalInMinutes), new ThePropertyBag() { "ParentFld=202", "TileHeight=1", "TileWidth=3" });
                TheNMIEngine.AddSmartControl(MyBaseThing, MyForm, eFieldType.SingleEnded, 211, 2, 0xC0, "KPI Property Name", nameof(KPIPublishPropertyName), new nmiCtrlSingleEnded { ParentFld = 202, TileHeight = 1, TileWidth = 6 });
                TheNMIEngine.AddSmartControl(MyBaseThing, MyForm, eFieldType.Number, 212, 2, 0xC0, "WatchDog Interval (minutes)", nameof(WatchDogIdleRecycleIntervalInMinutes), new nmiCtrlNumber { ParentFld = 202, TileHeight = 1, TileWidth = 3 });
                TheNMIEngine.AddSmartControl(MyBaseThing, MyForm, eFieldType.SingleCheck, 213, 2, 0xC0, "Log Sent Payload Data", nameof(EnableLogSentPayloads), new nmiCtrlSingleCheck { ParentFld = 202, TileHeight = 1, TileWidth = 3 });

                MyForm.DeleteByOrder(24); //Address field
                tSenderThingsForm.DeleteByOrder(160); //EventHub Partition
                tSenderThingsForm.DeleteByOrder(320); //Token LifeTime

                mIsUXInitialized = true;
                return true;
            }
            return false;
        }

        private MqttClient _client;

        protected override bool DoConnect()
        {
            if (KPILogIntervalInMinutes > 0)
            {
                myWatchDogTimer = new Timer(OnWatchDogTimer, null, KPILogIntervalInMinutes * 60 * 1000, KPILogIntervalInMinutes * 60 * 1000);
            }

            MqttClient client = null;
            if (!string.IsNullOrEmpty(MqttSslCertificate))
            {
                var caCertificate = new X509Certificate(Convert.FromBase64String(MqttSslCertificate)); 
                client = new MqttClient(MqttHostName, MqttPort, MqttSecure, caCertificate, null, MqttSslProtocols.TLSv1_2);
            }
            else
            {
                client = new MqttClient(MqttHostName, MqttPort, MqttSecure, MqttSslProtocols.TLSv1_2, null, null);
            }
            client.MqttMsgPublished += _client_MqttMsgPublished;
            client.ConnectionClosed += _client_ConnectionClosed;
            var connAck = client.Connect(MqttClientId, String.IsNullOrEmpty(MqttUsername) ? null : MqttUsername, String.IsNullOrEmpty(MqttPassword) ? null : MqttPassword,
                MqttWillRetain, MqttWillQoS, MqttWillFlag, MqttWillTopic, MqttWillMessage, true, MqttKeepAlivePeriodInSeconds);
            if (connAck == MqttMsgConnack.CONN_ACCEPTED)
            {
                _client = client;
                TheBaseAssets.MySYSLOG.WriteToLog(95302, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(strMqttSender, $"Reconnected", eMsgLevel.l3_ImportantMessage));
                if (!String.IsNullOrEmpty(MqttConnectTopicTemplate))
                {
                    foreach (var senderThing in MySenderThings.TheValues)
                    {
                        var topic = CreateStringFromTemplate(MqttConnectTopicTemplate, this, senderThing, null);
                        if (topic != null)
                        {
                            SendMqttMessageAsync(_client, topic, Encoding.UTF8.GetBytes("")).Wait();
                        }
                    }
                }
            }
            else
            {
                TheBaseAssets.MySYSLOG.WriteToLog(95302, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(strMqttSender, $"Connect failure", eMsgLevel.l1_Error, $"ConnAck: {connAck}"));
                client.Close();
                throw new Exception($"MQTT Connect failure. ConnAck:{connAck}");
            }
            return true;
        }

        private void _client_ConnectionClosed(object sender, EventArgs e)
        {
            if (IsConnected)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(95302, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(strMqttSender, $"MQTT connection was closed, likely by broker", eMsgLevel.l1_Error));
            }
            if (ReconnectInterval > 0)
            {
                TheCommonUtils.cdeRunAsync("MQTTReconnect", true, ReconnectLoop);
            }
        }

        int ReconnectInterval = 5000;
        private async void ReconnectLoop(object notused)
        {
            byte connAck = 255;
            do
            {
                try
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(95302, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM(strMqttSender, $"MQTT Reconnect: waiting {ReconnectInterval} ms", eMsgLevel.l4_Message));
                    await TheCommonUtils.TaskDelayOneEye(ReconnectInterval, 100);
                    var client = _client;
                    if (client != null)
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(95302, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM(strMqttSender, $"MQTT Reconnect: initiating reconnect after {ReconnectInterval} ms", eMsgLevel.l4_Message));
                        connAck = client.Connect(MqttClientId, String.IsNullOrEmpty(MqttUsername) ? null : MqttUsername, String.IsNullOrEmpty(MqttPassword) ? null : MqttPassword);
                        if (connAck == MqttMsgConnack.CONN_ACCEPTED)
                        {
                            TheBaseAssets.MySYSLOG.WriteToLog(95302, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(strMqttSender, $"Reconnected", eMsgLevel.l3_ImportantMessage));
                        }
                        else
                        {
                            TheBaseAssets.MySYSLOG.WriteToLog(95302, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(strMqttSender, $"Reconnect failure", eMsgLevel.l1_Error, $"ConnAck: {connAck}"));
                        }
                    }
                    else
                    {
                        connAck = 255;
                    }
                }
                catch (Exception ex)
                {
                    connAck = 255;
                    TheBaseAssets.MySYSLOG.WriteToLog(95302, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(strMqttSender, $"Error while reconnecting", eMsgLevel.l1_Error, ex.ToString()));
                }
            } while (connAck != MqttMsgConnack.CONN_ACCEPTED && ReconnectInterval > 0 && IsConnected && AutoConnect && !Disconnecting && TheBaseAssets.MasterSwitch);
        }

        protected override bool DoDisconnect(bool bDrain)
        {
            if (myWatchDogTimer != null)
            {
                var wd = myWatchDogTimer;
                myWatchDogTimer = null;
                if (wd != null)
                {
                    try
                    {
                        wd.Change(Timeout.Infinite, Timeout.Infinite);
                        wd.Dispose();
                    }
                    catch { }
                }
            }
            Disconnecting = true;
            var client = _client;
            if (client != null)
            {
                if (!String.IsNullOrEmpty(MqttDisconnectTopicTemplate))
                {
                    foreach (var senderThing in MySenderThings.TheValues)
                    {
                        var topic = CreateStringFromTemplate(MqttDisconnectTopicTemplate, this, senderThing, null);
                        if (topic != null)
                        {
                            SendMqttMessageAsync(_client, topic, Encoding.UTF8.GetBytes("")).Wait();
                        }
                    }
                }

                _client = null;
                try
                {
                    if (pendingMqttMessageTasks?.Count > 0)
                    {
                        foreach (var tcs in pendingMqttMessageTasks.Values)
                        {
                            tcs.TrySetCanceled();
                        }
                        pendingMqttMessageTasks.Clear();
                    }
                }
                catch { }
                try
                {
                    client.Disconnect();
                }
                catch { }
                try
                {
                    client.Close();
                }
                catch { }
            }
            Disconnecting = false;
            return true;
        }

        protected override object GetNextConnection()
        {
            return _client; // Connection multi-plexing not used in Http Sender
        }

        cdeConcurrentDictionary<ushort, TaskCompletionSource<bool>> pendingMqttMessageTasks = new cdeConcurrentDictionary<ushort, TaskCompletionSource<bool>>();
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        async override protected Task<SendEventResults> SendEventsAsync(object myClient, TheSenderThing azureThing, CancellationToken cancelToken, IEnumerable<TheThingStore> thingUpdatesToSend, IEventConverter eventConverter)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            var client = myClient as MqttClient;
            if (client == null)
            {
                throw new Exception("Internal error: Invalid or null client");
            }
            var results = new SendEventResults();
            results.SendTasks = new List<Task>();
            long batchLength = 0;

            var updatesByTopic = thingUpdatesToSend.GroupBy(u => CreateStringFromTemplate(MqttTopicTemplate, this, azureThing, u));
            if (!updatesByTopic.Any())
            {
                TheBaseAssets.MySYSLOG.WriteToLog(95308, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("Mqtt Sender", $"No topic found for thing updates: {this.MyBaseThing?.Address} - update count: {thingUpdatesToSend.Count()}, property count: {thingUpdatesToSend.Aggregate(0, (s, u) => s + u.PB.Count)}", eMsgLevel.l2_Warning));
            }
            foreach (var topicGroup in updatesByTopic)
            {
                var messagePayloads = eventConverter.GetEventData(topicGroup, azureThing, MaxEventDataSize, false);
                if (!messagePayloads.Any())
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(95308, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("Mqtt Sender", $"No message payloads in thing updates: {this.MyBaseThing?.Address} - Topic: {topicGroup.Key}", eMsgLevel.l2_Warning));
                }
                foreach (var msgObj in messagePayloads)
                {
                    var msgString = msgObj as string;
                    var correlationId = Guid.NewGuid();

                    try
                    {
                        //Task sendTask = postCS.Task;
                        if (msgString != null)
                        {
                            var payloadString = msgString;
                            if (SendAsTDM || SendAsTSM)
                            {
                                var tTSM = new TSM("CDMyMeshReceiver.MeshReceiverService", $"MESHSENDER_DATA:{Guid.NewGuid().ToString()}:{TheEventConverters.GetDisplayName(eventConverter)}", msgString);
                                if (SendAsTDM)
                                {
                                    var tDevMsg = new TheDeviceMessage
                                    {
                                        CNT = 1,
                                        DID = TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID.ToString(),
                                        FID = "1",
                                        MSG = tTSM,
                                        // ISB integration requires access to internal CDEngine structures: was only enabled for specific PoC (MSB for D) but it was decided to integrate without ISB semantics
#if MQTT_ISB
                                    SID = TheBaseAssets.MyScopeManager.GetScrambledScopeID(),
                                    TOP = TheBaseAssets.MyScopeManager.AddScopeID("CDE_CONNECT"),
#endif
                                    };
                                    payloadString = TheCommonUtils.SerializeObjectToJSONString(new List<TheDeviceMessage> { tDevMsg });
                                }
                                else
                                {
                                    payloadString = TheCommonUtils.SerializeObjectToJSONString(new List<TSM> { tTSM }); // Use list to enable batching in the future
                                }
                            }

                            var payload = Encoding.UTF8.GetBytes(payloadString);
                            var sendTask = SendMqttMessageAsync(client, topicGroup.Key, payload);
                            results.SendTasks.Add(sendTask);

                            batchLength += msgString.Length;
                            if (EnableLogSentPayloads)
                            {
                                try
                                {
                                    var logEntry = new Dictionary<string, object> { { "TimePublished", DateTimeOffset.Now }, { "Topic", topicGroup.Key }, { "Body", msgString } };
                                    var logText = TheCommonUtils.SerializeObjectToJSONString(logEntry);
                                    string strFilePath = TheCommonUtils.cdeFixupFileName("mqttsenderdata.log");
                                    System.IO.File.AppendAllText(strFilePath, logText + "\r\n");
                                }
                                catch (Exception e)
                                {
                                    TheBaseAssets.MySYSLOG.WriteToLog(95307, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("Mqtt Sender", $"Unable to log data to file: {this.MyBaseThing?.Address} - {topicGroup.Count()}", eMsgLevel.l3_ImportantMessage, e.ToString()));
                                }

                            }

                            Interlocked.Increment(ref _pendingKPIs.EventScheduledCount);
                        }
                        else
                        {
                            var postCS = new TaskCompletionSource<bool>();
                            results.SendTasks.Add(postCS.Task);
                            postCS.TrySetException(new InvalidTransportEncodingException());
                        }
                    }
                    catch (Exception e)
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(95307, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("Mqtt Sender", $"Internal error: {this.MyBaseThing?.Address} - {topicGroup.Count()}", eMsgLevel.l3_ImportantMessage, e.ToString()));
                    }

                    cancelToken.ThrowIfCancellationRequested();
                }
            }
            results.SizeSent = batchLength;
            return results;
        }

        Task<bool> SendMqttMessageAsync(MqttClient client, string topic, byte[] payload)
        {
            var msgId = client.Publish(topic, payload, MqttQoS, MqttRetain);
            if (MqttQoS == MqttMsgBase.QOS_LEVEL_AT_MOST_ONCE)
            {
                return TheCommonUtils.TaskFromResult(true);
            }
            var taskCS = pendingMqttMessageTasks.GetOrAdd(msgId, id => new TaskCompletionSource<bool>(id));
            if (taskCS.Task.IsCompleted)
            {
                pendingMqttMessageTasks.RemoveNoCare(msgId);
            }
            return taskCS.Task;
        }

        private void _client_MqttMsgPublished(object sender, uPLibrary.Networking.M2Mqtt.Messages.MqttMsgPublishedEventArgs e)
        {
            var msgId = e.MessageId;
            lock (pendingMqttMessageTasks)
            {
                TaskCompletionSource<bool> taskCS;
                if (!pendingMqttMessageTasks.TryRemove(msgId, out taskCS))
                {
                    taskCS = pendingMqttMessageTasks.GetOrAdd(msgId, id =>
                    {
                        // Publish ack came before SendMqttMessagAsync could add the TCS: add it already completed, so SendMqttMessageAsync will remove it
                        var cs = new TaskCompletionSource<bool>(id);
                        if (e.IsPublished)
                        {
                            cs.TrySetResult(true);
                        }
                        else
                        {
                            cs.TrySetException(new Exception("Message was not published"));
                        }
                        return cs;
                    });
                    if (!taskCS.Task.IsCompleted)
                    {
                        // TCS was added by SendMqttMessageAsync right after we failed to remove it: we'll complete it, so it no longer needs to remain pending
                        pendingMqttMessageTasks.RemoveNoCare(msgId);
                    }
                }
                if (e.IsPublished)
                {
                    taskCS.TrySetResult(true);
                }
                else
                {
                    taskCS.TrySetException(new Exception("Message was not published"));
                }
            }
        }


        protected override long SendTSMAsync(object client, TheSenderTSM senderTSM, CancellationToken cancelToken, TheProcessMessage tpmToSend, out List<Task> sendTasks)
        {
            var tsmToSend = tpmToSend.Message;
            long sentPayloadSize;

            sendTasks = new List<Task>();
            var parameters = new Dictionary<string, string>();
            string topic = CreateStringFromTemplate(senderTSM.MQTTTopicTemplate, this, senderTSM, tsmToSend, parameters);

            // Example: for CDE_FILEPUSH:<filename>:<cdeO>:<cookie>:<fileMetaInfo>
            // $TXTPart1$ = <filename> becomes part of the topic
            // $TXTPart4$ = <fileMetaInfo> becomes part of the topic
            // $TXTPart.F.1$ = <filename> - will send file to MSB with <filename>, will not become part of the topic
            // $TXTPart.R.1$ = <filename> - will stream file contents instead of TSM/TDM/PLB/PLS etc. as message payload, will not become part of the topic

            var readFile = false;
            parameters.TryGetValue("F", out var fileName);
            if (string.IsNullOrEmpty(fileName))
            {
                parameters.TryGetValue("R", out fileName);
                readFile = true;
            }

            byte[] payload;

            if (SendAsTDM)
            {
                var tDevMsg = new TheDeviceMessage
                {
                    CNT = 1,
                    DID = TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID.ToString(),
                    FID = TheCommonUtils.CStr(tsmToSend.FID),
                    MSG = tsmToSend,
                    TOP = tpmToSend.Topic,

#if MQTT_ISB
                    SID = TheBaseAssets.MyScopeManager.GetScrambledScopeID(),
                    TOP = TheBaseAssets.MyScopeManager.AddScopeID("CDE_CONNECT"),
#endif
                };
                payload = Encoding.UTF8.GetBytes(TheCommonUtils.SerializeObjectToJSONString(new List<TheDeviceMessage> { tDevMsg }));
            }
            else if (senderTSM.SerializeTSM)
            {
                payload = Encoding.UTF8.GetBytes(TheCommonUtils.SerializeObjectToJSONString(new List<TSM> { tsmToSend }));
            }
            else if (tsmToSend.PLB != null)
            {
                payload = tsmToSend.PLB;
            }
            else
            {
                payload = Encoding.UTF8.GetBytes(tsmToSend.PLS);
            }

            if (!senderTSM.SendAsFile)
            {
                if (SendAsTDM && MQTTMaxPayloadSizeForTDM > 0 && payload.Length > MQTTMaxPayloadSizeForTDM) // Chunking requires TDM as the TOP contains the chunking information
                {
                    // chunk it
                    payload = null; // Don't need the original payload anymore. Future optimization: avoid creating it in the first place
                    var MsgToQueue = TSM.Clone(tsmToSend, true);
                    sentPayloadSize = 0;
                    // This code is a clone of parts of TheQueuedSender.SendQueued as of 2018/09/27
                    byte[] PayLoadBytes = null;
                    int PayLoadBytesLength = 0;
                    bool IsPLSCompressed = false;

                    // Skipping encryption (not desired in MSB scenario and crypto functions are not public)
                    //if (!string.IsNullOrEmpty(MsgToQueue.PLS) && (MsgToQueue.ReadyToEncryptPLS() && !MsgToQueue.IsPLSEncrypted()))
                    //{
                    //    MsgToQueue.PLS = TheCommonUtils.cdeEncrypt(MsgToQueue.PLS, cdeSecrets.cdeAI);     //3.083: Must be cdeAI
                    //    MsgToQueue.PLSWasEncrypted();
                    //}
                    if (MsgToQueue.PLB == null || MsgToQueue.PLB.Length == 0)                   //NEW: New Architecture allow to send raw bytes...need to have "CDEBIN" in PLS
                    {
                        if (MsgToQueue != null && !string.IsNullOrEmpty(MsgToQueue.PLS) && MsgToQueue.PLS.Length > 512/* && MyTargetNodeChannel != null && MyTargetNodeChannel.SenderType != cdeSenderType.CDE_JAVAJASON*/)
                        {
                            PayLoadBytes = TheCommonUtils.cdeCompressString(MsgToQueue.PLS);
                            IsPLSCompressed = true;
                            PayLoadBytesLength = PayLoadBytes.Length;
                            TheCDEKPIs.QSCompressedPLS++;
                        }
                    }
                    else
                    {
                        PayLoadBytes = MsgToQueue.PLB;
                        PayLoadBytesLength = PayLoadBytes.Length;
                    }
                    int PayLen = PayLoadBytesLength;
                    int tTeles = 1;
                    int curLen = PayLen;
                    tTeles = ((PayLen / MQTTMaxPayloadSizeForTDM) + 1);
                    curLen = MQTTMaxPayloadSizeForTDM;
            
                    int curPos = 0;
                    int PackCnt = 0;
                    string tGuid = Guid.NewGuid().ToString();
                    TSM tFinalTSM = MsgToQueue;
                    while (curPos < PayLen || PayLen == 0)
                    {
                        if (curPos + curLen > PayLen)
                            curLen = PayLen - curPos;
                        var tDevMsgChunk = new TheDeviceMessage()
                        {
                            CNT = PackCnt,
                            DID = TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID.ToString(),
                            FID = TheCommonUtils.CStr(tsmToSend.FID),
                        };
                        if (tTeles > 1)
                        {
                            tFinalTSM = TSM.Clone(MsgToQueue, false);
                        }
                        tDevMsgChunk.TOP = tpmToSend.Topic + ":,:" + PackCnt.ToString() + ":,:" + tTeles.ToString() + ":,:" + tGuid;
                        tDevMsgChunk.MSG = tFinalTSM;
                      
                        if (curLen > 0 && PayLoadBytes != null)
                        {
                            // CODE REVIEW - MH: Avoid this copy, i.e.
                            if (curPos == 0 && curLen == PayLoadBytes.Length)
                            {
                                tFinalTSM.PLB = PayLoadBytes;
                            }
                            else
                            {
                                tFinalTSM.PLB = new byte[curLen];
                                TheCommonUtils.cdeBlockCopy(PayLoadBytes, curPos, tFinalTSM.PLB, 0, curLen);
                            }
                            if (IsPLSCompressed)
                                tFinalTSM.PLS = "";
                        }

                        var chunkPayload = Encoding.UTF8.GetBytes(TheCommonUtils.SerializeObjectToJSONString(new List<TheDeviceMessage> { tDevMsgChunk }));
                        var t = SendMqttMessageAsync(client as MqttClient, topic, chunkPayload);
                        sendTasks.Add(t);
                        sentPayloadSize += chunkPayload.Length;

                        curPos += curLen;
                        if (PayLen == 0) break;
                        PackCnt++;
                    }
                }
                else
                {
                    var t = SendMqttMessageAsync(client as MqttClient, topic, payload);
                    sendTasks.Add(t);
                    return payload.Length;
                }
            }
            else
            {
                if (!String.IsNullOrEmpty(FilePushUrl))
                {
                    Stream payloadStream;
                    if (readFile)
                    {
                        var localFileName = TheCommonUtils.cdeFixupFileName(fileName);
                        payloadStream = new FileStream(localFileName, FileMode.Open);
                    }
                    else
                    {
                        if (String.IsNullOrEmpty(fileName))
                        {
                            fileName = "unknown";
                        }
                        payloadStream = new MemoryStream(payload);
                    }
                    var t = SendMSBFileMessageAsync(FilePushUrl, topic, payloadStream, fileName);
                    sendTasks.Add(t);
                    sentPayloadSize = payload.Length;
                }
                else
                {
                    var tcs = new TaskCompletionSource<bool>();
                    tcs.TrySetException(new Exception("No file push url configured"));
                    sendTasks.Add(tcs.Task);
                    sentPayloadSize = 0;
                }
            }
            return sentPayloadSize;
        }

        private static Task SendMSBFileMessageAsync(string url, string topic, Stream payloadStream, string fileName)
        {
            try
            {
                string boundary = $"----CDE4-{Guid.NewGuid().ToString()}";
                var contentType = $"multipart/form-data; boundary={boundary}";
                /*
                Request URL:https//localhost:9043/msb/fileinterface/uploadFile
                Referrer Policy:no-referrer-when-downgrade
                Request Headers
                Provisional headers are shown
                accept:application/json
                content-type:multipart/form-data; boundary=----WebKitFormBoundarycc3BaI240Dfub1zy
                Origin:http://editor.swagger.io
                Referer:http://editor.swagger.io/
                User-Agent:Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/59.0.3071.115 Safari/537.36
                Request Payload
                ------WebKitFormBoundarycc3BaI240Dfub1zy
                Content-Disposition: form-data; name="topic"

                some/topic
                ------WebKitFormBoundarycc3BaI240Dfub1zy
                Content-Disposition: form-data; name="firstFile"; filename="horstmann.png"
                Content-Type: image/png


                ------WebKitFormBoundarycc3BaI240Dfub1zy--  
                */
                string payload1 = $"{boundary}\r\nContentType-Disposition: form-data; name=\"topic\"\r\n\r\n{topic}\r\n";
                payload1 += $"{boundary}\r\nContentType-Disposition: form-data; name=\"firstFile\"; filename=\"{fileName}\"\r\nContent-Type: application/octet-stream\r\n\r\n";

                string payload2 = $"\r\n{boundary}\r\n";

                var mpStream = new MyMultiPartStream(Encoding.UTF8.GetBytes(payload1), payloadStream, Encoding.UTF8.GetBytes(payload2));

                return TheMqttSender.SendHttpAsync(url, contentType, mpStream, false);
            }
            catch (Exception e)
            {
                var tcs = new TaskCompletionSource<bool>();
                tcs.TrySetException(e);
                return tcs.Task;
            }
        }


        private Timer myWatchDogTimer;

        class MyMultiPartStream : Stream
        {

            byte[] _part1;
            Stream _content;
            byte[] _part2;
            public MyMultiPartStream(byte[] part1, Stream content, byte[] part2)
            {
                _part1 = part1;
                _content = content;
                _part2 = part2;
            }
            long _position;

            public override bool CanRead { get { return true; } }

            public override bool CanSeek { get { return true; } }

            public override bool CanWrite { get { return false; } }

            public override long Length { get { return _part1.Length + _content.Length + _part2.Length; } }

            public override long Position { get { return _position; } set { _position = value; } }

            public override void Flush()
            {
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                if (_position < 0)
                {
                    _position = 0;
                }
                if (_position < _part1.Length)
                {
                    int bytesRead = 0;
                    for (long i = _position; i < _part1.Length; i++)
                    {
                        buffer[offset + bytesRead] = _part1[_position];
                        _position++;
                        bytesRead++;
                    }
                    return bytesRead;
                }
                if (_position < _part1.Length + _content.Length)
                {
                    _content.Seek(_position - _part1.Length, SeekOrigin.Begin);
                    var bytesRead = _content.Read(buffer, offset, count);
                    _position += bytesRead;
                    return bytesRead;
                }
                if (_position < _part1.Length + _content.Length + _part2.Length)
                {
                    int bytesRead = 0;
                    for (long i = _position - _part1.Length - _content.Length; i < _part2.Length; i++)
                    {
                        buffer[offset + bytesRead] = _part2[i];
                        bytesRead++;
                    }
                    _position += bytesRead;
                    return bytesRead;
                }
                return 0;
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                switch (origin)
                {
                    case SeekOrigin.Begin:
                        _position = offset;
                        break;
                    case SeekOrigin.Current:
                        _position += offset;
                        break;
                    case SeekOrigin.End:
                        _position = Length - offset;
                        break;
                }
                return _position;
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }
        }


        internal static Task SendHttpAsync(string url, string contentType, Stream payload, bool EnableLogSentPayloads)
        {
            var client = new TheREST();

            var postCS = new TaskCompletionSource<bool>();
            Task sendTask = postCS.Task;
            if (payload != null)
            {
                client.PostRESTAsync(new Uri($"{url}"),
                    rd =>
                    {
                        postCS.TrySetResult(true);
                    }, payload, contentType, Guid.Empty, null, rd =>
                    {
                        if (EnableLogSentPayloads)
                        {
                            try
                            {
                                string strFilePath = TheCommonUtils.cdeFixupFileName("httpsenderdata.log");
                                System.IO.File.AppendAllText(strFilePath, $"{{\"TimePublished\":\"{DateTimeOffset.Now:O}\", \"PLS\": {rd?.ErrorDescription}}},\r\n");
                            }
                            catch (Exception e)
                            {
                                TheBaseAssets.MySYSLOG.WriteToLog(95307, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("Http Sender", $"Unable to log data to file: {url} : {payload.Length}", eMsgLevel.l3_ImportantMessage, e.ToString()));
                            }

                        }
                        postCS.TrySetException(new Exception($"PostRESTAsync Failed: {rd?.ErrorDescription}"));
                    }, null);

                if (EnableLogSentPayloads)
                {
                    try
                    {
                        string strFilePath = TheCommonUtils.cdeFixupFileName("httpsenderdata.log");
                        System.IO.File.AppendAllText(strFilePath, $"{{\"TimePublished\":\"{DateTimeOffset.Now:O}\", \"Body\": {payload.Length}}},\r\n");
                    }
                    catch (Exception e)
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(95307, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("Http Sender", $"Unable to log data to file: {url} : {payload.Length}", eMsgLevel.l3_ImportantMessage, e.ToString()));
                    }

                }
            }
            else
            {
                postCS.TrySetException(new InvalidTransportEncodingException());
            }
            return sendTask;
        }
    }
}
