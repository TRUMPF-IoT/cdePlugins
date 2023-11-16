// SPDX-FileCopyrightText: 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0
using System;
using System.Collections.Generic;

using nsCDEngine.Engines;
using nsCDEngine.BaseClasses;
using nsCDEngine.Communication;
using nsCDEngine.ViewModels;
using nsCDEngine.Engines.NMIService;
using nsCDEngine.Engines.ThingService;
using CDMyMessages.ViewModel;
using nsCDEngine.Security;

namespace CDMyMessages
{
    public class eMessageTypes
    {
        public const string EMAIL = "eMail Message";
        public const string SMS = "SMS Message";
    }

    class myMessages : ThePluginBase
    {
        #region ICDEPlugin
        public override void InitEngineAssets(IBaseEngine pBase)
        {
            base.InitEngineAssets(pBase);
            MyBaseEngine.SetFriendlyName("My Messaging Service");  
            MyBaseEngine.SetEngineID(new Guid("{7B56E698-59B5-4A11-BFE7-FDB06A912FDC}")); 
            MyBaseEngine.SetPluginInfo("Send email and other messages to people, social media and other recipients", 0, null, "FA3:f658", "C-Labs", "http://www.c-labs.com", new List<string> { "Service" });
            MyBaseEngine.GetEngineState().IsAllowedUnscopedProcessing = true;
        }
        #endregion

        public override cdeP SetProperty(string pName, object pValue)
        {
            if (MyBaseThing != null)
            {
                if (pName == "LastMessage")
                    SetLastMessage(TheScopeManager.GetTokenFromScrambledScopeID(), pValue.ToString());
                return MyBaseThing.SetProperty(pName, pValue);
            }
            return null;
        }

        public override bool Init()
        {
            if (mIsInitCalled) return false;
            mIsInitCalled = true;

            MyBaseThing.StatusLevel = 4;
            MyBaseThing.RegisterEvent(eEngineEvents.IncomingMessage, HandleMessage);
            SetProperty("LastMessage", "Plugin has started");
            if (MyBaseEngine.GetEngineState().IsService)
            {
                InitServers();
                TheCommCore.MyHttpService.RegisterHttpInterceptorB4("/getmsgs", sinkProcessBand);
            }
            MyBaseEngine.ProcessInitialized();

            mIsInitialized = true;
            return true;
        }

        TheDashboardInfo MyDash = null;
        public override bool CreateUX()
        {
            if (mIsUXInitCalled) return false;
            mIsUXInitCalled = true;

            if (!TheBaseAssets.MyServiceHostInfo.IsCloudService)
            {
                MyDash = TheNMIEngine.AddDashboard(MyBaseThing, new TheDashboardInfo(MyBaseEngine, "Messaging") { PropertyBag = new ThePropertyBag() { "Category=Services", "Caption=Messaging", "Thumbnail=FA5:f658" } });

                var tFlds=TheNMIEngine.CreateEngineForms(MyBaseThing, TheThing.GetSafeThingGuid(MyBaseThing, "MSGT"), "Messaging Targets", null, 0, 0x0F, 0xF0, TheNMIEngine.GetNodeForCategory(), "REFFRESHME",false, $"{eMessageTypes.EMAIL}", eMessageTypes.EMAIL); //;{eMessageTypes.SMS} not ready - needs carrier servername+port+ssl
                TheFormInfo tForm = tFlds["Form"] as TheFormInfo;
                tForm.IsReadOnly = false;
                tForm.AddButtonText = "Add a new Messaging Target";
                (tFlds["Name"] as TheFieldInfo).Header = "Target Name";
                (tFlds["DashIcon"] as TheDashPanelInfo).PanelTitle = "Messaging Targets";
                TheNMIEngine.AddSmartControl(MyBaseThing, tForm, eFieldType.SingleEnded, 50, 2, 0, "Recipient", "Recipient", null).FldWidth = 3;
            }
            mIsUXInitialized = true;
            return true;
        }

        void InitServers()
        {
            List<TheThing> tDevList = TheThingRegistry.GetThingsOfEngine(MyBaseThing.EngineName); // TheThingRegistry.GetThingsByProperty("*", Guid.Empty, "DeviceType", eOPCDeviceTypes.OPCServer);
            if (tDevList.Count > 0)
            {
                foreach (TheThing tDev in tDevList)
                {
                    switch (tDev.DeviceType)
                    {
                        case eMessageTypes.EMAIL:
                            TheEmailMessage tS = null;
                            if (!tDev.HasLiveObject)
                            {
                                tS = new TheEmailMessage(tDev, this);
                                TheThingRegistry.RegisterThing(tS);
                            }
                            break;
                        case eMessageTypes.SMS:
                            TheSMSMessage tSMS = null;
                            if (!tDev.HasLiveObject)
                            {
                                tSMS = new TheSMSMessage(tDev, this);
                                TheThingRegistry.RegisterThing(tSMS);
                            }
                            break;
                    }
                }
            }
            MyBaseEngine.SetStatusLevel(-1);
        }

        /// <summary>
        /// Handles Messages sent from a host sub-engine to its clients
        /// </summary>
        public override void HandleMessage(ICDEThing sender, object pIncoming)
        {
            TheProcessMessage pMsg = pIncoming as TheProcessMessage;
            if (pMsg == null) return;
            string[] cmd = pMsg.Message.TXT.Split(':');
            switch (cmd[0])
            {
                case "CDE_INITIALIZED":
                    MyBaseEngine.SetInitialized(pMsg.Message);
                    break;
                case "CDE_INITIALIZE":
                    if (MyBaseEngine.GetEngineState().IsService && MyBaseEngine.GetEngineState().IsLiveEngine)
                    {
                        if (!MyBaseEngine.GetEngineState().IsEngineReady)
                            MyBaseEngine.SetEngineReadiness(true, null);
                    }
                    MyBaseEngine.ReplyInitialized(pMsg.Message);
                    break;
                case "REFFRESHME":
                    InitServers();
                    if (MyDash != null)
                        MyDash.Reload(pMsg, false);
                    break;

                case "SET_LAST_MSG":
                    SetLastMessage(TheScopeManager.GetTokenFromScrambledScopeID(pMsg.Message.SID),pMsg.Message.PLS);
                    break;
                case "GET_LAST_MSG":
                    TheCommCore.PublishToOriginator(pMsg.Message, new TSM(MyBaseEngine.GetEngineName(), "SET_LAST_MSG", ReturnLastMessage()));
                    break;
            }
        }

        private void SetLastMessage(string tEsay, string pMessage)
        {
            if (!string.IsNullOrEmpty(tEsay) && !string.IsNullOrEmpty(pMessage))
            {
                lock (LastMsgs.MyLock)
                {
                    LastMsgs[tEsay + "@"] = pMessage;
                }
            }
        }

        cdeConcurrentDictionary<string,string> LastMsgs = new cdeConcurrentDictionary<string, string>();
        DateTimeOffset LastProcessed = DateTimeOffset.Now;
        bool IsInterceptorProcessing = false;
        private void sinkProcessBand(TheRequestData pRequest)
        {
            if (pRequest == null) return;
            if (IsInterceptorProcessing) return;

            IsInterceptorProcessing = true;
            try
            {
                if (pRequest.RequestUri != null && !string.IsNullOrEmpty(pRequest.RequestUri.Query) && pRequest.RequestUri.Query.Length > 1)
                {
                    string[] QParts = pRequest.RequestUri.Query.Split('=');
                    if (QParts.Length > 1 && QParts[0].ToUpper() == "?SID")
                    {
                        lock (LastMsgs.MyLock)
                        {
                            string msg = "ERR: No Message from target, yet";
                            string token = TheScopeManager.GetTokenFromScrambledScopeID(TheScopeManager.GetScrambledScopeIDFromEasyID(QParts[1])) + "@";
                            if (TheScopeManager.IsValidScopeID(TheScopeManager.GetScrambledScopeIDFromEasyID(QParts[1])))
                            {
                                msg = ReturnLastMessage();
                                LastMsgs[token]=msg;
                            }
                            else
                            {
                                TSM tTSM = new TSM(MyBaseEngine.GetEngineName(), "GET_LAST_MSG");
                                tTSM.SID = TheScopeManager.GetScrambledScopeIDFromEasyID(QParts[1]);
                                TheCommCore.PublishToService(tTSM);
                                //TODO: Wait here until SET_LAST_MSG returns
                                if (LastMsgs.ContainsKey(token))
                                    msg = LastMsgs[token];
                            }
                            pRequest.ResponseBuffer = TheCommonUtils.CUTF8String2Array(msg);
                            pRequest.StatusCode = 200;
                            pRequest.ResponseMimeType = "text/html";
                            LastMsgs.RemoveNoCare(token);
                        }
                    }
                }
            }
            catch { }
            IsInterceptorProcessing = false;
        }

        private string ReturnLastMessage()
        {
            string token = TheScopeManager.GetTokenFromScrambledScopeID() +"@";
            string retStr = "";
            if (LastMsgs.ContainsKey(token))
            {
                retStr = LastMsgs[token];
                LastMsgs.RemoveNoCare(token);
            }
            return retStr;
                
        }
    }
}
