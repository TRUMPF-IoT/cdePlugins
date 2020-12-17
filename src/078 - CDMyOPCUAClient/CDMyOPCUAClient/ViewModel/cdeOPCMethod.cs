// SPDX-FileCopyrightText: 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0
using CDMyOPCUAClient.Contracts;
using nsCDEngine.BaseClasses;
using nsCDEngine.Communication;
using nsCDEngine.Engines;
using nsCDEngine.Engines.NMIService;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.ViewModels;
using Opc.Ua;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CDMyOPCUAClient.ViewModel
{
    public class TheOPCMethod : TheDataBase
    {
        public bool IsSubscribed { get; set; }
        public string DisplayName { get; set; }
        public string OPCServerID { get; set; }

        public string NodeIdName
        {
            get
            {
                if (TagRef != null)
                    return TagRef.ToString();
                else
                    return "Not Set";
            }
        }

        public string ParentIdName
        {
            get
            {
                if (ParentId != null)
                    return ParentId.ToString();
                else
                    return "Not Set";
            }
        }

        public List<TheOPCTag> Args { get; set; }

        internal NodeId TagRef;
        internal NodeId ParentId;
        internal TheOPCUARemoteServer MyOPCServer = null;
        internal TheThing MyBaseThing = null;

        public TheOPCMethod()
        {
            Args = null; // new List<TheOPCTag>();
        }

        public void Reset()
        {
        }

        public override string ToString()
        {
            return string.Format("{0} ID:{1}", DisplayName, TagRef.ToString());
        }
    }

    [DeviceType(
        DeviceType = eOPCDeviceTypes.OPCMethod,
        Capabilities = new eThingCaps[] { eThingCaps.ConfigManagement },
        Description = "OPC UA Method")]
    [ConfigProperty(NameOverride = nameof(TheThing.Address), cdeT = ePropertyTypes.TString, Required = true)]
    [ConfigProperty(NameOverride = nameof(TheThing.FriendlyName), cdeT = ePropertyTypes.TString)]
    [ConfigProperty(NameOverride = nameof(TheOPCUATagThing.ServerID), cdeT = ePropertyTypes.TString)] // TODO: need to generalize/specialize this (Task 1334)
    [ConfigProperty(NameOverride = nameof(TheOPCUATagThing.ServerName), cdeT = ePropertyTypes.TString)]
    public class TheOPCUAMethodThing : TheNMILiveTag
    {
        private TheOPCMethod m_Method = null;

        public TheOPCUAMethodThing(TheThing pThing,string pEngineName)
            : base(pThing)
        {
            MyBaseThing.DeviceType = eOPCDeviceTypes.OPCMethod;
            MyBaseThing.EngineName = pEngineName;
            Reset();
            MyBaseThing.RegisterEvent("OnInitialized", sinkInit);
            MyBaseThing.SetIThingObject(this);
        }

        [ConfigProperty()]
        public int InputArgCnt
        {
            get { return TheCommonUtils.CInt(TheThing.GetSafePropertyString(MyBaseThing, "InputArgCnt")); }
            set
            {
                MyBaseThing.SetProperty("InputArgCnt", value.ToString(), ePropertyTypes.TNumber);
            }
        }
        [ConfigProperty()]
        public int OutputArgCnt
        {
            get { return TheCommonUtils.CInt(TheThing.GetSafePropertyString(MyBaseThing, "OutputArgCnt")); }
            set
            {
                MyBaseThing.SetProperty("OutputArgCnt", value.ToString(), ePropertyTypes.TNumber);
            }
        }

        [ConfigProperty()]
        public int MethodCallTimeout
        {
            get { return (int) TheThing.GetSafePropertyNumber(MyBaseThing, "MethodCallTimeout"); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, "MethodCallTimeout", value); }
        }

        public override cdeP SetProperty(string pName, object pValue)
        {
            if (!string.IsNullOrEmpty(pName) && pName == "Call")
                HandleMessage(this, new TheProcessMessage(new TSM(MyBaseThing.EngineName, "CALL_METHOD:"+TheCommonUtils.cdeGuidToString(Guid.NewGuid()),(pValue==null? "" : pValue.ToString()))));
            return base.SetProperty(pName, pValue);
        }

        public void Setup(TheOPCMethod pMethod)
        {
            m_Method = pMethod;
            IsActive = false;
            IsDynUXCreated = false;
            sinkInit(this, null);
        }

        public bool HasMethodData()
        {
            bool result;
            lock (m_Method)
            {
                result = m_Method.Args != null && m_Method.Args.Count > 0;
            }
            return result;
        }

        public void Reset()
        {
            IsActive = false;
            if (m_Method != null)
                m_Method.Reset();
        }

        public override bool DoInit()
        {
            InputArgCnt = 0;
            OutputArgCnt = 0;
            return true;
        }

        bool IsDynUXCreated = false;
        //protected TheFormInfo MyStatusForm;
        //protected TheDashPanelInfo SummaryForm;
        protected TheFieldInfo CountBar;
        public override bool DoCreateUX()
        {
            return true; //TODO: Make much better UX here...Remove Controls first then create (see OPC DA Plugin)
#pragma warning disable CS0162
            MyStatusForm = TheNMIEngine.AddForm(new TheFormInfo(MyBaseThing) { FormTitle = MyBaseThing.FriendlyName, DefaultView = eDefaultView.Form });
            SummaryForm = TheNMIEngine.AddFormToThingUX(MyBaseThing, MyStatusForm, "CMyForm", MyBaseThing.FriendlyName, 3, 1, 0x0, "OPC UA Methods", "FriendlyName", new ThePropertyBag() { });

            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.SingleEnded, 1, 0, MyBaseThing.cdeA, "OPC UA Method Name", "FriendlyName");

            TheFieldInfo mSendbutton = TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.TileButton, 2, 0x82, 0, "Call", false, "", null, new ThePropertyBag() { "PreventDefault", "TileWidth=2", "TileHeight=1", "Style=background-image:url('GlasButton.png');" });
            mSendbutton.RegisterUXEvent(MyBaseThing, eUXEvents.OnClick, "", (pThing, pObj) =>
            {
                TheProcessMessage pMsg = pObj as TheProcessMessage;
                if (m_Method == null || m_Method.MyOPCServer == null || !m_Method.MyOPCServer.IsConnected)
                {
                    TheCommCore.PublishToOriginator(pMsg.Message, new TSM(eEngineName.NMIService, "NMI_TOAST", "Server not connected - please connect first"));
                    return;
                }
                HandleMessage(this, new TheProcessMessage(new TSM(MyBaseThing.EngineName, "CALL_METHOD:" + TheCommonUtils.cdeGuidToString(Guid.NewGuid()))));
            });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.TextArea, 3, 0, 0xC0, "Last MSG", false, "LastMessage", null, new ThePropertyBag() { "TileWidth=6", "TileHeight=3" });

            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.SmartLabel, 100, 0, MyBaseThing.cdeA, "", false, null, null, new ThePropertyBag() { "Format=Input Arguments", "Style=font-size:20px;text-align: left;float:none;clear:left;background-color:black;color:white;" });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.SmartLabel, 200, 0, MyBaseThing.cdeA, "", false, null, null, new ThePropertyBag() { "Format=Output Arguments", "Style=font-size:20px;text-align: left;float:none;clear:left;background-color:black;color:white;" });

            CreateDynUX(false);
            return true;
#pragma warning restore CS0162
        }

        void CreateDynUX(bool ForceAdd)
        {
            return; //TODO: Make much better UX here...Remove Controls first then create (see OPC DA Plugin)
#pragma warning disable CS0162
            if (mIsUXInitCalled && (!IsDynUXCreated || ForceAdd))
            {
                if (m_Method == null) return;
                IsDynUXCreated = true;
                int i = 0;
                foreach (TheOPCProperties tOPC in InputArgs)
                {
                    cdeP tP = tOPC.cdeProperty;
                    if (tP==null)
                    {
                        IsDynUXCreated = false;
                        return;
                    }
                    TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, TheNMIEngine.GetCtrlTypeFromCDET((ePropertyTypes)tP.cdeT), 101+i, 2, MyBaseThing.cdeA, tP.Name, tP.Name);
                    i++;
                }
                i = 0;
                foreach (TheOPCProperties tOPC in OutputArgs)
                {
                    cdeP tP = tOPC.cdeProperty;
                    if (tP == null)
                    {
                        IsDynUXCreated = false;
                        return;
                    }
                    TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, TheNMIEngine.GetCtrlTypeFromCDET((ePropertyTypes)tP.cdeT), 201 + i, 0, MyBaseThing.cdeA, tP.Name, tP.Name);
                    i++;
                }
                //HandleMessage(new TheProcessMessage(new TSM(MyBaseThing.EngineName, "CALL_METHOD:"+TheCommonUtils.cdeGuidToString(Guid.NewGuid()))));
            }
#pragma warning restore CS0162

        }

        List<TheOPCProperties> InputArgs = null;
        List<TheOPCProperties> OutputArgs = null;

        private void sinkInit(ICDEThing sender, object pPara)
        {
            if (string.IsNullOrEmpty(MyBaseThing.ID))
                MyBaseThing.ID = Guid.NewGuid().ToString();

            if (m_Method != null)
            {
                if (m_Method.MyOPCServer == null) return;
                if (string.IsNullOrEmpty(MyBaseThing.Address))
                {
                    MyBaseThing.Address = string.Format("{2}:;:{0}:;:{1}", m_Method.ParentId, m_Method.TagRef, m_Method.MyOPCServer.GetBaseThing().ID);
                    FormTitle = "OPC UA Method: " + m_Method.DisplayName;
                    TileWidth = 3;
                    TileHeight = 1;
                    Flags = 0;
                    FldOrder = 0;
                }
                if (string.IsNullOrEmpty(MyBaseThing.FriendlyName))
                    MyBaseThing.FriendlyName = m_Method.DisplayName;
                if (ControlType == "")
                    ControlType = ((int)eFieldType.SingleEnded).ToString();

                m_Method.MyBaseThing = this.MyBaseThing;
                ServerID = m_Method.MyOPCServer.GetBaseThing().ID;
                ServerName = m_Method.MyOPCServer.GetBaseThing().FriendlyName;
                MyBaseThing.EngineName = m_Method.MyOPCServer.GetBaseThing().EngineName;

                lock (m_Method)
                {
                    if (m_Method.Args != null)
                    {
                        int tInputArgCnt = 0;
                        int tOutputArgCnt = 0;
                        InputArgs = new List<TheOPCProperties>();
                        OutputArgs = new List<TheOPCProperties>();
                        foreach (TheOPCTag tTag in m_Method.Args)
                        {
                            foreach (TheOPCProperties tpro in tTag.PropAttr)
                            {
                                cdeP pProp = MyBaseThing.GetProperty(tpro.BrowseName, false);
                                if (pProp == null)
                                    MyBaseThing.SetProperty(tpro.BrowseName, tpro.Value == null ? "" : tpro.Value.WrappedValue.ToString(), tpro.DataType);
                                else
                                {
                                    pProp.cdeT = (int)tpro.DataType;
                                    if (tpro.Value != null)
                                        pProp.Value = tpro.Value.WrappedValue.ToString();
                                }
                                if (tTag.BrowseName == "InputArguments")
                                {
                                    tInputArgCnt++;
                                    tpro.cdeProperty = pProp;
                                    InputArgs.Add(tpro);
                                }
                                else if (tTag.BrowseName == "OutputArguments")
                                {
                                    tOutputArgCnt++;
                                    tpro.cdeProperty = pProp;
                                    OutputArgs.Add(tpro);
                                }
                            }
                        }
                        bool ForceUX = false;
                        if (InputArgCnt != tInputArgCnt)
                        {
                            InputArgCnt = tInputArgCnt;
                            ForceUX = true;
                        }
                        if (OutputArgCnt != tOutputArgCnt)
                        {
                            ForceUX = true;
                            OutputArgCnt = tOutputArgCnt;
                        }
                        if (ForceUX || !IsDynUXCreated)
                            CreateDynUX(ForceUX);
                    }
                }
            }
        }

        public override void HandleMessage(ICDEThing sender, object pIncoming)
        {
            TheProcessMessage pMsg = pIncoming as TheProcessMessage;
            if (pMsg == null || pMsg.Message == null) return;

            var cmd = pMsg.Message.TXT.Split(':');
            switch (cmd[0])
            {
                case "CALL_METHOD":
                case nameof(MsgOPCUAMethodCall):
                    string error = "Unexpected";
                    string exceptionText = "";

                    MsgOPCUAMethodCall callInfo = null;
                    byte[] largeOutput = null;
                    string outParamsAsJson = null;
                    IList<object> outputArguments = null;
                    if (m_Method == null)
                    {
                        error = "Method meta data not initialized";
                    }
                    else if (m_Method.MyOPCServer == null)
                    {
                        error = "Method not inititialized";
                    }
                    else if (m_Method.MyOPCServer.m_session == null)
                    {
                        error = "OPC UA session not created";
                    }
                    else
                    {
                        try
                        {
                            if (TheCommonUtils.cdeIsLocked(m_Method))
                            {
                                TheBaseAssets.MySYSLOG.WriteToLog(78401, TSM.L(eDEBUG_LEVELS.FULLVERBOSE) ? null : new TSM(MyBaseThing.EngineName, String.Format("[{0}] Method called concurrently", m_Method.MyOPCServer.GetLogAddress()), eMsgLevel.l4_Message, String.Format("{0}", MyBaseThing.Address)));
                            }
                            lock (m_Method)
                            {
                                if (m_Method.Args == null)
                                {
                                    var browseError = m_Method.MyOPCServer.MethodBrowser(m_Method.TagRef, m_Method.DisplayName, m_Method);
                                    if (!string.IsNullOrEmpty(browseError))
                                    {
                                        error = "Unable to retrieve method metadata from server: " + browseError;
                                    }
                                }
                                if (m_Method.Args == null)
                                {
                                    error = "Unable to retrieve method metadata from server";
                                }
                                else
                                {
                                    if (!string.IsNullOrEmpty(pMsg.Message.PLS))
                                    {
                                        if (cmd[0] == nameof(MsgOPCUAMethodCall))
                                        {
                                            callInfo = TheCommRequestResponse.ParseRequestMessageJSON<MsgOPCUAMethodCall>(pMsg.Message);
                                            foreach (var argument in callInfo.Arguments)
                                            {
                                                TheThing.SetSafeProperty(this, argument.Key, argument.Value, ePropertyTypes.NOCHANGE);
                                            }
                                        }
                                        else
                                        {
                                            var tLst = TheCommonUtils.cdeSplit(pMsg.Message.PLS, ":;:", true, true).ToList();
                                            foreach (string t in tLst)
                                            {
                                                TheThing.SetPropertyFromBagItem(this, t);
                                            }
                                        }
                                    }
                                    object[] tArgs = new object[InputArgCnt];
                                    for (int i = 0; i < InputArgCnt; i++)
                                    {
                                        tArgs[i] = TheOPCTag.GetOPCValueFromCDEValue(InputArgs[i].cdeProperty == null ? null : InputArgs[i].cdeProperty.Value, InputArgs[i].OPCType);
                                    }
#if OLD_UA
                                    outputArguments = m_Method.MyOPCServer.m_session.CallWithTimeout(m_Method.ParentId, m_Method.TagRef, MethodCallTimeout, tArgs);
#else
                                    outputArguments = m_Method.MyOPCServer.m_session.Call(m_Method.ParentId, m_Method.TagRef, tArgs); //CM: C-labs extension: .CallWithTimeout(m_Method.ParentId, m_Method.TagRef, MethodCallTimeout, tArgs);
#endif
                                    if (cmd[0] != nameof(MsgOPCUAMethodCall))
                                    {
                                        if (TheThing.GetSafePropertyBool(this, "ReturnOutputAsJson"))
                                        {
                                            outParamsAsJson = TheCommonUtils.SerializeObjectToJSONString(outputArguments);
                                            //TheThing.SetSafePropertyString(this, "OutputAsJson", outParamsAsJson);
                                        }
                                        else
                                        {
                                            if (outputArguments != null && outputArguments.Count > 0)
                                            {
                                                for (int i = 0; i < outputArguments.Count; i++)
                                                {
                                                    if (i < OutputArgs.Count)
                                                    {
                                                        object value;
                                                        if (outputArguments[i] is byte[] && (outputArguments[i] as byte[]).Length > 4096 && largeOutput == null)
                                                        {
                                                            largeOutput = outputArguments[i] as byte[];
                                                            value = "";
                                                        }
                                                        else
                                                        {
                                                            value = outputArguments[i];
                                                        }

                                                        cdeP tP = OutputArgs[i].cdeProperty;
                                                        if (tP != null)
                                                        {
                                                            //TheOPCTag.UpdateValueProperty(outputArguments[i] as DataValue, tP, outputArguments[i] as DataValue);
                                                            tP.Value = value;
                                                            // tP.SetValue(outputArguments[i], pMsg.Message.GetOriginator().ToString()); // CODE REVIEW: Why did we set the originator here? It's only really needed for remote things to break update cycles...
                                                        }

                                                    }
                                                    else
                                                    {
                                                        TheBaseAssets.MySYSLOG.WriteToLog(78402, TSM.L(eDEBUG_LEVELS.FULLVERBOSE) ? null : new TSM(MyBaseThing.EngineName, String.Format("[{0}] Error processing method response for OPC Server", m_Method.MyOPCServer.GetLogAddress()), eMsgLevel.l2_Warning, String.Format("{0}: too many out parameters in method", MyBaseThing.Address)));
                                                    }
                                                }
                                            }
                                        }
                                        MyBaseThing.LastUpdate = DateTimeOffset.Now;
                                        LastMessage = string.Format("Success at {0}", MyBaseThing.LastUpdate);
                                    }
                                    error = "";
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            error = "Method Call failed: " + e.Message;
                            exceptionText = e.ToString();
                            LastMessage = error;
                            TheBaseAssets.MySYSLOG.WriteToLog(78403, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM(MyBaseThing.EngineName, String.Format("[{0}] Method Call failed", m_Method.MyOPCServer.GetLogAddress()), eMsgLevel.l1_Error, String.Format("{0}:{1}", MyBaseThing.Address, e.ToString())));
                        }
                    }
                    if (cmd[0] == nameof(MsgOPCUAMethodCall))
                    {
                        if (callInfo?.ReturnRawJSON == true)
                        {
                            TheCommRequestResponse.PublishResponseMessageJson(pMsg.Message, new MsgOPCUAMethodCallResponse { OutputArguments = new List<object> { TheCommonUtils.SerializeObjectToJSONString(outputArguments) }, Error = error });
                        }
                        else
                        {
                            TheCommRequestResponse.PublishResponseMessageJson(pMsg.Message, new MsgOPCUAMethodCallResponse { OutputArguments = (List<object>) outputArguments, Error = error });
                        }
                    }
                    else
                    {
                        TSM tTSN = new TSM(MyBaseThing.EngineName, string.Format(String.IsNullOrEmpty(error) ? "CALL_METHOD_RESPONSE:{0}:{1}" : "CALL_METHOD_RESPONSE:{0}:{1}:{2}:{3}", MyBaseThing.ID, cmd[1], error.Replace(":"," "), exceptionText.Replace(":"," ")));
                        if (largeOutput != null && String.IsNullOrEmpty(error))
                        {
                            tTSN.PLB = largeOutput;
                        }
                        if (outParamsAsJson != null && String.IsNullOrEmpty(error))
                        {
                            tTSN.PLS = outParamsAsJson;
                        }
                        if (pMsg.LocalCallback != null)
                            pMsg.LocalCallback(tTSN);
                        else
                            TheCommCore.PublishToOriginator(pMsg.Message, tTSN);
                    }
                    break;
            }

            base.HandleMessage(this, pMsg);
        }
       
    }
}
