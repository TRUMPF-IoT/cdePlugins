// SPDX-FileCopyrightText: 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0
using System;
using nsCDEngine.BaseClasses;
using nsCDEngine.Engines;
using nsCDEngine.Engines.NMIService;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.ViewModels;
using System.Collections.Generic;
using nsCDEngine.Communication;
using System.Threading;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.XPath;
using System.IO;

namespace CDMyRulesEngine.ViewModel
{
    class TheRule : TheThingRule
    {
        // Base object references 
        private IBaseEngine MyBaseEngine;    // Base engine (service)

        // Initialization flags
        protected bool mIsInitStarted = false;
        protected bool mIsInitCompleted = false;
        protected bool mIsUXInitStarted = false;
        protected bool mIsUXInitCompleted = false;

        private const int idForm = 1;
        private const int idGroupStatus = 10;
        private const int idGroupTriggerObject = 100;
        private const int idGroupActionSettings = 500;
        private const int idGroupThingPropAction = 550;
        private const int idGroupTSMAction = 600;

        public TheRule(TheThing tBaseThing, ICDEPlugin pPlugin):base (tBaseThing)
        {
            MyBaseEngine = pPlugin.GetBaseEngine();
        }

        public override bool IsInit()
        {
            return mIsInitCompleted;
        }
        public override bool IsUXInit()
        {
            return mIsUXInitCompleted;
        }

        public override bool Init()
        {
            if (!mIsInitStarted)
            {
                mIsInitStarted = true;
                MyBaseThing.RegisterOnChange("IsRuleRunning", sinkUpdateRule);
                MyBaseThing.RegisterOnChange("IsTriggerObjectAlive", sinkUpdateRule);
                MyBaseThing.RegisterOnChange("LastTriggered", sinkUpdateRule);
                MyBaseThing.LastMessage = "Rule ready";
                MyBaseThing.StatusLevel = 0;
                mIsInitCompleted = true;
            }
            return true;
        }

        void sinkUpdateRule(cdeP prop)
        {
            UpdateStatus();
        }



        cdeConcurrentDictionary<string, TheMetaDataBase> mThisFormFields;

        public override bool CreateUX()
        {
            if (!mIsUXInitStarted)
            {
                mIsUXInitStarted = true;

                // Creates a "portal" for each rule. This is how we create 
                // a tile for each rule on the rules engine's main page.
                mThisFormFields = TheNMIEngine.AddStandardForm(MyBaseThing, MyBaseThing.FriendlyName, 18);

                // Update our status.
                TheFormInfo tFormGuid = mThisFormFields["Form"] as TheFormInfo;
                tFormGuid.RegisterEvent2(eUXEvents.OnShow, (pMSG, para) => {
                    UpdateStatus();
                });

                // Create "Rule Status" settings group
                // Field Order = 10
                // Parent = 1 (main form)
                // Get standard Status Block.
                var tstFlds = TheNMIEngine.AddStatusBlock(MyBaseThing, tFormGuid, idGroupStatus);
                tstFlds["Group"].SetParent(idForm);
                tstFlds["Group"].Header = "Rule Status";
                // tstFlds["Group"].PropertyBag = new ThePropertyBag { "DoClose=true" };
                tstFlds["FriendlyName"].Header = "Rule Name";

                // When the Friendly Name changes, propogate to the other UI elements that use it.
                tstFlds["FriendlyName"].RegisterUXEvent(MyBaseThing, eUXEvents.OniValueChanged, null, (psender, pPara) =>
                {
                    (mThisFormFields["Header"] as TheFieldInfo).SetUXProperty(Guid.Empty, $"Title={MyBaseThing.FriendlyName}");
                    (mThisFormFields["DashIcon"] as TheDashPanelInfo).SetUXProperty(Guid.Empty, $"Caption={MyBaseThing.FriendlyName}");
                });

                // Add fields to Status Block that are specific to this plugin.
                TheNMIEngine.AddSmartControl(MyBaseThing, tFormGuid, eFieldType.SingleCheck, 60, 2, 0, "Activate Rule", "IsRuleActive", new nmiCtrlSingleCheck { ParentFld = idGroupStatus, TileWidth = 3 });
                TheNMIEngine.AddSmartControl(MyBaseThing, tFormGuid, eFieldType.SingleCheck, 70, 2, 0, "Log Action", "IsRuleLogged", new nmiCtrlSingleCheck { ParentFld = idGroupStatus, TileWidth = 3 });
                TheNMIEngine.AddSmartControl(MyBaseThing, tFormGuid, eFieldType.SingleCheck, 75, 2, 0, "Log Action to Eventlog", "IsEVTLogged", new nmiCtrlSingleCheck { ParentFld = idGroupStatus, TileWidth = 3 });
                TheFieldInfo tTriggerBut = TheNMIEngine.AddSmartControl(MyBaseThing, tFormGuid, eFieldType.TileButton, 65, 2, 0xC0, "Trigger Now", null, new nmiCtrlTileButton { ParentFld = idGroupStatus, ClassName = "cdeGoodActionButton", NoTE = true, TileWidth = 3 });
                tTriggerBut.RegisterUXEvent(MyBaseThing, eUXEvents.OnClick, "TriggerNow", (psender, pPara) =>
                {
                    TheProcessMessage pMSG = pPara as TheProcessMessage;
                    if (pMSG == null || pMSG.Message == null) return;

                    string[] cmd = pMSG.Message.PLS.Split(':');
                    if (cmd.Length > 2)
                    {
                        TheThing tThing = TheThingRegistry.GetThingByMID("*", TheCommonUtils.CGuid(cmd[2]));
                        if (tThing == null) return;
                        TheRule tRule = tThing.GetObject() as TheRule;
                        if (tRule != null)
                        {
                            //FireAction(tRule, true);
                            tRule.FireAction(true);
                            MyBaseEngine.ProcessMessage(new TheProcessMessage(new TSM(MyBaseEngine.GetEngineName(), "FIRE_RULE", MyBaseThing.cdeMID.ToString())));
                            TheCommCore.PublishToOriginator(pMSG.Message, new TSM(eEngineName.NMIService, "NMI_TOAST", string.Format("Rule {0} triggered", tRule.GetBaseThing().FriendlyName)));
                        }
                    }
                });

                // Create "Trigger Object" settings group (Field Order = idGroupTriggerObject (100), Parent = 1)
                TheNMIEngine.AddSmartControl(MyBaseThing, tFormGuid, eFieldType.CollapsibleGroup, idGroupTriggerObject, 2, 0, "Trigger Object", null, new nmiCtrlCollapsibleGroup { ParentFld = idForm, TileWidth = 6, IsSmall = true });

                // Create "Action Settings" settings group (Field Order = idGroupActionSettings (500), Parent = 1)
                TheNMIEngine.AddSmartControl(MyBaseThing, tFormGuid, eFieldType.CollapsibleGroup, idGroupActionSettings, 2, 0, "Action Settings", null, new nmiCtrlCollapsibleGroup { ParentFld = idForm, TileWidth = 6, IsSmall = true });

                // Create "Thing Property Action" settings group (Field Order = idGroupThingPropAction (550), Parent = 500)
                TheNMIEngine.AddSmartControl(MyBaseThing, tFormGuid, eFieldType.CollapsibleGroup, idGroupThingPropAction, 2, 0, "Thing/Property Action", null, new nmiCtrlCollapsibleGroup { ParentFld = idGroupActionSettings, TileWidth = 6, IsSmall = true});

                // Create "TSM Action" settings group (Field Order = idGroupTSMAction (600), Parent = 500)
                TheNMIEngine.AddSmartControl(MyBaseThing, tFormGuid, eFieldType.CollapsibleGroup, idGroupTSMAction, 2, 0, "TSM Action", null, new nmiCtrlCollapsibleGroup { ParentFld = idGroupActionSettings, TileWidth = 6, IsSmall = true, DoClose = true });

                // Create all other (non-group header) fields.
                TheNMIEngine.AddFields(tFormGuid, new List<TheFieldInfo>
            {
                /* Trigger Object Group */
                {  new TheFieldInfo() { FldOrder=140,DataItem="MyPropertyBag.TriggerObject.Value",Flags=2,Type=eFieldType.ThingPicker,Header="Trigger Object", PropertyBag=new nmiCtrlThingPicker() { ParentFld=idGroupTriggerObject, HelpText="If this objects...", IncludeEngines=true }  }},
                {  new TheFieldInfo() { FldOrder=150,DataItem="MyPropertyBag.TriggerProperty.Value",Flags=2,Type=eFieldType.PropertyPicker,Header="Trigger Property", DefaultValue="Value",PropertyBag=new nmiCtrlPropertyPicker() { ParentFld=idGroupTriggerObject, HelpText="...property is...", ThingFld=140 }   }},
                {  new TheFieldInfo() { FldOrder=160,DataItem="MyPropertyBag.TriggerCondition.Value",Flags=2,Type=eFieldType.ComboBox,Header="Trigger Condition", DefaultValue="2",PropertyBag=new nmiCtrlComboBox() { ParentFld=idGroupTriggerObject, HelpText="... then this value, this rule will fire...", DefaultValue="2", Options="Fire:0;State:1;Equals:2;Larger:3;Smaller:4;Not:5;Contains:6;Set:7;StartsWith:8;EndsWith:9;Flank:10" }}},
                {  new TheFieldInfo() { FldOrder=170,DataItem="MyPropertyBag.TriggerValue.Value",Flags=2,Type=eFieldType.SingleEnded,Header="Trigger Value", PropertyBag=new ThePropertyBag() { "ParentFld=100", "HelpText=...this objects..." }  }},

                /* Action Settings Group */
                { new TheFieldInfo() { FldOrder=505,DataItem="MyPropertyBag.ActionObjectType.Value",Flags=2,Type=eFieldType.ComboBox,Header="Action Object Type",PropertyBag = new nmiCtrlComboBox() { ParentFld=idGroupActionSettings, Options="Set Property on a Thing:CDE_THING;Publish Central:CDE_PUBLISHCENTRAL;Publish to Service:CDE_PUBLISH2SERVICE", DefaultValue="CDE_THING" } }},
                {  new TheFieldInfo() { FldOrder=506,DataItem="MyPropertyBag.ActionDelay.Value",Flags=2,Type=eFieldType.Number,Header="Delay", DefaultValue="0", PropertyBag=new ThePropertyBag() { "ParentFld=500", "HelpText=...after a delay of these seconds..." }  }},

                /* Thing / Property Action Sub-Group */
                {  new TheFieldInfo() { FldOrder=560,DataItem="MyPropertyBag.ActionObject.Value",Flags=2,Type=eFieldType.ThingPicker,Header="Action Object", PropertyBag=new nmiCtrlThingPicker() { ParentFld=550, HelpText="...this objects...", IncludeEngines=true }  }},
                {  new TheFieldInfo() { FldOrder=562,DataItem="MyPropertyBag.ActionProperty.Value",Flags=2,Type=eFieldType.PropertyPicker,Header="Action Property", DefaultValue="Value",PropertyBag=new nmiCtrlPropertyPicker() { ParentFld=idGroupThingPropAction, HelpText="...property will change to...", ThingFld=560 } }},
                {  new TheFieldInfo() { FldOrder=563,DataItem="MyPropertyBag.ActionValue.Value",Flags=2,Type=eFieldType.SingleEnded,Header="Action Value", PropertyBag=new nmiCtrlSingleEnded { ParentFld=idGroupThingPropAction, HelpText = "...this value", Style="text-overflow:ellipsis;overflow:hidden; max-width:400px" } }},

                /* TSM Action Sub-Group */
                { new TheFieldInfo() { FldOrder=630,DataItem="MyPropertyBag.TSMEngine.Value",Flags=2,Type=eFieldType.ThingPicker,Header="TSM Engine",PropertyBag = new nmiCtrlThingPicker() { ParentFld=600,ValueProperty="EngineName", IncludeEngines=true, Filter="DeviceType=IBaseEngine" } }},
                { new TheFieldInfo() { FldOrder=631,DataItem="MyPropertyBag.TSMText.Value",Flags=2,Type=eFieldType.SingleEnded,Header="TSM Text",PropertyBag = new ThePropertyBag() { "ParentFld=600", "HelpText=Command of the TSM" } }},
                { new TheFieldInfo() { FldOrder=632,DataItem="MyPropertyBag.TSMPayload.Value",Flags=2,Type=eFieldType.TextArea,Header="TSM Payload", PropertyBag = new nmiCtrlTextArea() { ParentFld=idGroupTSMAction, TileHeight=2, HelpText="Body of the TSM" } }},

            });

                mIsUXInitCompleted = true;
            }
            return true;
        }

        void UpdateStatus()
        {
            if (IsRuleRunning)
            {
                if (IsTriggerObjectAlive)
                    MyBaseThing.StatusLevel = 1;
                else
                    MyBaseThing.StatusLevel = 2;
            }
            else
                MyBaseThing.StatusLevel = 0;
            MyBaseThing.LastMessage = $"Running:{IsRuleRunning} TrigerAlive:{IsTriggerObjectAlive} LastFired:{TheThing.GetSafePropertyBool(MyBaseThing, "LastTriggered")} Owner:{MyBaseThing.cdeO}";
            MyBaseThing.LastUpdate = DateTimeOffset.Now;
        }

        #region Message Handling
        public override void HandleMessage(ICDEThing sender, object pIncoming)
        {
            TheProcessMessage pMsg = pIncoming as TheProcessMessage;
            if (pMsg == null) return;

            string[] cmd = pMsg.Message.TXT.Split(':');
            switch (cmd[0])
            {
                default:
                    break;
            }
        }
        #endregion

        internal void RuleTrigger(string tVal, bool bForce = false)
        {
            MyBaseThing.Value = tVal;
            switch (TriggerCondition)
            {
                case eRuleTrigger.Not:
                    if (!TriggerValue.Equals(tVal, StringComparison.OrdinalIgnoreCase))
                        FireAction(bForce);
                    break;
                case eRuleTrigger.Larger:
                    if (TheCommonUtils.CDbl(TriggerValue) < TheCommonUtils.CDbl(tVal) && TheCommonUtils.CDbl(TriggerValue) >= TheCommonUtils.CDbl(TriggerOldValue))
                        FireAction(bForce);
                    break;
                case eRuleTrigger.Smaller:
                    if (TheCommonUtils.CDbl(TriggerValue) > TheCommonUtils.CDbl(tVal) && TheCommonUtils.CDbl(TriggerValue) <= TheCommonUtils.CDbl(TriggerOldValue))
                        FireAction(bForce);
                    break;
                case eRuleTrigger.Contains:
                    if (tVal.ToLower().Contains(TriggerValue.ToLower()))
                        FireAction(bForce);
                    break;
                case eRuleTrigger.StartsWith:
                    if (tVal.StartsWith(TriggerValue, StringComparison.OrdinalIgnoreCase))
                        FireAction(bForce);
                    break;
                case eRuleTrigger.EndsWith:
                    if (tVal.EndsWith(TriggerValue, StringComparison.OrdinalIgnoreCase))
                        FireAction(bForce);
                    break;
                case eRuleTrigger.Set:
                    if (!string.IsNullOrEmpty(TriggerValue))
                    {
                        string expression = tVal + TriggerValue;
                        ActionValue = Evaluate(expression).ToString(CultureInfo.InvariantCulture);
                    }
                    else
                        ActionValue = tVal;
                    FireAction(bForce);
                    break;
                case eRuleTrigger.Equals:
                    if (TriggerValue.Equals(tVal, StringComparison.OrdinalIgnoreCase))
                        FireAction(bForce);
                    break;
                case eRuleTrigger.Fire:
                    FireAction(bForce);
                    break;
                case eRuleTrigger.Flank:
                    if (TheCommonUtils.IsNullOrWhiteSpace(TriggerValue))
                    {
                        if (!TriggerOldValue.Equals(tVal, StringComparison.OrdinalIgnoreCase))
                            FireAction(bForce);
                        break;
                    }
                    string[] tTrigs = TriggerValue.Split(',');
                    if (tTrigs.Length == 1)
                    {
                        if (Math.Abs(TheCommonUtils.CDbl(TriggerValue) - TheCommonUtils.CDbl(tVal)) < double.Epsilon && Math.Abs(TheCommonUtils.CDbl(TriggerOldValue) - TheCommonUtils.CDbl(tVal)) > double.Epsilon)
                            FireAction(false);
                    }
                    else
                    {
                        if (Math.Abs(TheCommonUtils.CDbl(tVal) - TheCommonUtils.CDbl(TriggerOldValue)) > Double.Epsilon &&
                            Math.Abs(TheCommonUtils.CDbl(TriggerOldValue) - TheCommonUtils.CDbl(tTrigs[0])) < double.Epsilon &&
                             Math.Abs(TheCommonUtils.CDbl(tVal) - TheCommonUtils.CDbl(tTrigs[1])) < Double.Epsilon)
                            FireAction(bForce);
                    }
                    break;
                case eRuleTrigger.State:
                    if (TriggerOldValue.Equals(tVal, StringComparison.OrdinalIgnoreCase))
                        FireAction(bForce);
                    break;
            }
            TriggerOldValue = tVal;
        }

        internal void FireAction(bool FireNow)
        {
            if (TheCDEngines.MyThingEngine == null || !TheBaseAssets.MasterSwitch) return;
            if (!FireNow)
            {
                int tDelay = ActionDelay;
                if (tDelay > 0)
                {
                    Timer tTimer = GetDelayTimer();
                    if (tTimer != null)
                    {
                        tTimer.Dispose();
                        tTimer = null;
                    }
                    tTimer = new Timer(sinkFireOnTimer,this, tDelay * 1000, Timeout.Infinite);
                    SetDelayTimer(tTimer);
                    return;
                }
            }
            switch (ActionObjectType)
            {
                case "CDE_PUBLISHCENTRAL":
                    SendRuleTSM(false);
                    break;
                case "CDE_PUBLISH2SERVICE":
                    SendRuleTSM(true);
                    break;
                default: //case "CDE_THING":
                    TheThing tActionThing = TheThingRegistry.GetThingByMID("*", TheCommonUtils.CGuid(ActionObject));
                    if (tActionThing != null)
                    {
                        string tActionValue = ActionValue;
                        if (!string.IsNullOrEmpty(tActionValue))
                        {
                            ICDEThing triggerThing = TheThingRegistry.GetThingByMID("*", TheCommonUtils.CGuid(TriggerObject)) as ICDEThing;
                            tActionValue = TheCommonUtils.GenerateFinalStr(tActionValue, triggerThing);
                            tActionValue = tActionValue.Replace("%OldValue%", TriggerOldValue);
                            tActionValue = TheCommonUtils.GenerateFinalStr(tActionValue, MyBaseThing);
                        }

                        ICDEThing tObject = tActionThing.GetObject() as ICDEThing;
                        if (tObject != null)
                            tObject.SetProperty(ActionProperty, tActionValue);
                        else
                            tActionThing.SetProperty(ActionProperty, tActionValue);
                        if (IsRuleLogged)
                            LogEvent(tActionValue);
                        if (TheThing.GetSafePropertyBool(MyBaseThing,"IsEVTLogged"))
                            TheLoggerFactory.LogEvent("Rule Fired", TheCommonUtils.GenerateFinalStr(MyBaseThing.FriendlyName, MyBaseThing), eMsgLevel.l4_Message, TheBaseAssets.MyServiceHostInfo.GetPrimaryStationURL(false), TriggerObject, tActionValue);
                    }
                    break;
            }
            TheThing.SetSafePropertyDate(MyBaseThing, "LastAction", DateTimeOffset.Now);
            FireEvent("RuleFired", this, this, true);
        }

        public bool LogEvent(string ActionText)
        {
            TheEventLogData tSec = new TheEventLogData
            {
                EventTime = DateTimeOffset.Now,
                EventLevel = eMsgLevel.l4_Message,
                StationName = TheBaseAssets.MyServiceHostInfo.GetPrimaryStationURL(false),
                EventName = TheCommonUtils.GenerateFinalStr(MyBaseThing.FriendlyName, MyBaseThing)
            };
            if (!string.IsNullOrEmpty(tSec.EventName))
            {
                tSec.EventName = tSec.EventName.Replace("%OldValue%", TriggerOldValue);
            }
            tSec.EventTrigger = TriggerObject;
            tSec.ActionObject = ActionText;
            var tEng=MyBaseEngine.GetBaseThing().GetObject() as TheRulesEngine;
            tEng?.LogEvent(tSec);
            return true;
        }

        static void sinkFireOnTimer(object pRule)
        {
            TheRule tRule = pRule as TheRule;
            if (tRule != null)
            {
                Timer tTIm = tRule.GetDelayTimer();
                if (tTIm != null)
                {
                    tRule.FireAction(true);
                    tTIm.Dispose();
                    tTIm = null;
                    tRule.SetDelayTimer(null);
                }
            }
        }

        void SendRuleTSM(bool serviceOnly)
        {
            string engine = TheCommonUtils.CStr(GetProperty("TSMEngine", false));
            string text = TheCommonUtils.CStr(GetProperty("TSMText", false));
            string payload = TheCommonUtils.CStr(GetProperty("TSMPayload", false));

            //payload = payload.Replace("%DTO%", TheCommonUtils.CStr(DateTimeOffset.Now));
            //text = text.Replace("%DTO%", TheCommonUtils.CStr(DateTimeOffset.Now));
            ICDEThing triggerThing = TheThingRegistry.GetThingByMID("*", TheCommonUtils.CGuid(TriggerObject)) as ICDEThing;
            string escPayload = TheCommonUtils.GenerateFinalStr(payload, triggerThing);
            escPayload = TheCommonUtils.GenerateFinalStr(escPayload, MyBaseThing);
            string escText = TheCommonUtils.GenerateFinalStr(text, triggerThing);
            escText = TheCommonUtils.GenerateFinalStr(escText, MyBaseThing);

            if (!string.IsNullOrEmpty(engine) && !string.IsNullOrEmpty(text))
            {
                TSM customTSM = new TSM(engine, escText, escPayload);
                if (serviceOnly)
                {
                    customTSM.SetToServiceOnly(true);
                }
                TheCommCore.PublishCentral(customTSM, true);
            }
            if (IsRuleLogged)
                LogEvent(escPayload);
            if (TheThing.GetSafePropertyBool(MyBaseThing, "IsEVTLogged"))
                TheLoggerFactory.LogEvent("Rule Fired", TheCommonUtils.GenerateFinalStr(MyBaseThing.FriendlyName, MyBaseThing), eMsgLevel.l4_Message, TheBaseAssets.MyServiceHostInfo.GetPrimaryStationURL(false), escText, escPayload);
        }

        public static double Evaluate(string expression)
        {
            double res = 0;
            try
            {
                var xsltExpression =
                    string.Format("number({0})",
                        new Regex(@"([\+\-\*])").Replace(expression, " ${1} ")
                                                .Replace("/", " div ")
                                                .Replace("%", " mod "));

                // ReSharper disable PossibleNullReferenceException
                res = (double)new XPathDocument
                    (new StringReader("<r/>"))
                        .CreateNavigator()
                        .Evaluate(xsltExpression);
            }
            catch
            {
                //ignored
            }
            return res;
        }

    }
}
