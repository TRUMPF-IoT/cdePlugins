// SPDX-FileCopyrightText: 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0
using CDMyRulesEngine.ViewModel;
using nsCDEngine.BaseClasses;
using nsCDEngine.Communication;
using nsCDEngine.Engines;
using nsCDEngine.Engines.NMIService;
using nsCDEngine.Engines.StorageService;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.ViewModels;
using System;
using System.Collections.Generic;

namespace CDMyRulesEngine
{

    internal class TheRulesEngine : ThePluginBase, ICDERulesEngine
    {
        #region ICDEPlugin Methods
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pBase">The C-DEngine is creating a Host for this engine and hands it to the Plugin Service</param>
        public override void InitEngineAssets(IBaseEngine pBase)
        {
            base.InitEngineAssets(pBase);
            MyBaseEngine.SetFriendlyName("The Rules Engine");
            MyBaseEngine.AddCapability(eThingCaps.RulesEngine);
            MyBaseEngine.SetPluginInfo("The C-Labs Rules engine", 0, null, "toplogo-150.png", "C-Labs", "http://www.c-labs.com", null); //TODO: Describe your plugin - this will later be used in the Plugin-Store
            MyBaseEngine.SetEngineID(new Guid("{843B73BA-028F-4BDF-A102-D1E545204036}"));
        }
        #endregion

        public override bool Init()
        {
            if (TheCDEngines.MyThingEngine == null || mIsInitCalled) return false;
            mIsInitCalled = true;

            if (string.IsNullOrEmpty(MyBaseThing.ID))
                MyBaseThing.ID = Guid.NewGuid().ToString();

            if (TheBaseAssets.MyServiceHostInfo.IsCloudService)
            {
                mIsInitialized = true;
                return true;
            }

            if (!TheBaseAssets.MyServiceHostInfo.IsCloudService)//TODO: Allow Cloud Rules
            {
                TheCDEngines.MyThingEngine.RegisterEvent(eEngineEvents.ThingUpdated, sinkThingWasUpdated);
                //TheThingRegistry.eventThingUpdated += sinkThingWasUpdated;
                TheCDEngines.MyThingEngine.RegisterEvent(eEngineEvents.ThingRegistered, sinkThingWasRegistered);
                //TheThingRegistry.eventThingRegistered += sinkThingWasRegistered;
                TheCDEngines.MyThingEngine.RegisterEvent(eEngineEvents.ThingInitCalled, sinkActivateRules);
            }
            MyBaseThing.RegisterEvent(eEngineEvents.IncomingMessage, HandleMessage);
            mIsInitialized = true; // CODE REVIEW Markus: Is the rules engine really ready for consumption at this stage, or should we wait until storage is ready?
            //CM: The rules engine is ready but the Event Log might not be fully ready as of this time.
            TheBaseEngine.WaitForStorageReadiness(sinkStorageStationIsReadyFired, true);
            MyBaseEngine.ProcessInitialized();
            return true;
        }

        private void InitRules()
        {
            bool FoundOne = false;
            List<TheThing> tDevList = TheThingRegistry.GetThingsByProperty(MyBaseEngine.GetEngineName(), Guid.Empty, "DeviceType", eKnownDeviceTypes.TheThingRule); 
            if (tDevList != null && tDevList.Count > 0)
            {
                FoundOne = true;
                foreach (TheThing tDev in tDevList)
                {
                    if (tDev.GetObject() == null) 
                    {
                        TheRule tRule = new TheRule(tDev, this);
                        if (string.IsNullOrEmpty(tRule.TriggerObjectType))
                            tRule.TriggerObjectType = "CDE_THING";
                        tRule.RegisterEvent(eEngineEvents.ThingUpdated, sinkUpdated);
                        RegisterRule(tRule);
                    }
                }
            }

            if (!FoundOne)
            {
                TheRule MyRule = new TheRule(new TheThing() { cdeMID = new Guid("{0D9741E6-7915-434F-89E6-15FA584E5F66}") }, this)
                {
                    FriendlyName = "My First Rule",
                    TriggerObjectType = "CDE_THING",
                    TriggerObject = "",
                    TriggerProperty = "Value",
                    TriggerCondition = eRuleTrigger.Set,
                    TriggerValue = "",
                    ActionObjectType = "CDE_THING",
                    ActionObject = "",
                    ActionProperty = "Value",
                    ActionValue = "",
                    IsRuleActive = false,
                    Parent = MyBaseThing.ID
                };
                RegisterRule(MyRule);
            }
        }

        void sinkThingWasRegistered(ICDEThing sender, object pPara)
        {
            if (TheBaseAssets.MyServiceHostInfo.IsCloudService) return;//TODO: Allow Cloud Rules
            TheThing pThing = sender as TheThing;
            if (pThing != null && TheThing.GetSafePropertyString(pThing, "DeviceType") == eKnownDeviceTypes.TheThingRule)
            {
                TheRule tRule = new TheRule(pThing, this) {Parent = MyBaseThing.ID};
                tRule.RegisterEvent(eEngineEvents.ThingUpdated, sinkUpdated);
                RegisterRule(tRule);
                ActivateRules();
            }
        }

        void sinkActivateRules(ICDEThing sender, object pPara)
        {
            ActivateRules();
        }

        void sinkThingWasUpdated(ICDEThing sender, object pPara)
        {
            if (TheBaseAssets.MyServiceHostInfo.IsCloudService) return; //TODO: Allow Cloud Rules

            TheThing pThing = sender as TheThing;
            if (pThing != null && TheThing.GetSafePropertyString(pThing, "DeviceType") == eKnownDeviceTypes.TheThingRule)
            {
                TheRule tRule = pThing.GetObject() as TheRule;
                if (tRule == null)
                {
                    tRule = new TheRule(pThing, this);
                    tRule.RegisterEvent(eEngineEvents.ThingUpdated, sinkUpdated);
                    RegisterRule(tRule);
                }
                
                {
                    tRule.IsRuleWaiting = true;
                    tRule.IsRuleRunning = false;
                    TheSystemMessageLog.WriteLog(4445, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM(eKnownDeviceTypes.TheThingRule, $"Rule {tRule.FriendlyName} stopped on Rule Update"), false);

                    TheThingRegistry.UpdateThing(tRule, false);
                }
                ActivateRules();
            }
        }

        TheDashboardInfo MyDash;
        public override bool CreateUX()
        {
            if (TheBaseAssets.MyServiceHostInfo.IsCloudService)
            {
                mIsUXInitialized = true;
                return true;
            }
            if (!TheNMIEngine.IsInitialized()) return false;
            if (mIsUXInitCalled) return false;
            mIsUXInitCalled = true;

            MyDash=TheNMIEngine.AddDashboard(MyBaseThing, new TheDashboardInfo(MyBaseEngine, "Rules Engine") { FldOrder = 5000,cdeA=0xC0, PropertyBag = new ThePropertyBag { "Caption=Rules Engine","Thumbnail=FA5:f546", "Category=Services" } });

            TheFormInfo tFormGuid = new TheFormInfo(new Guid("{6C34871C-D49B-4D7A-84E0-35E25B1F18B0}") /*TheThing.GetSafeThingGuid(MyBaseThing, "RULESREG")*/, eEngineName.NMIService, "The Rules Registry", $"TheThing;:;0;:;True;:;DeviceType={eKnownDeviceTypes.TheThingRule};EngineName={MyBaseEngine.GetEngineName()}")
            { GetFromFirstNodeOnly = true, IsNotAutoLoading = true, AddButtonText = "Add Rule", AddTemplateType= "44444444-6AD1-45AE-BE61-96AF02329613" };
            //TheNMIEngine.AddForm(tFormGuid);
            TheNMIEngine.AddFormToThingUX(MyDash, MyBaseThing, tFormGuid, "CMyTable", "Rules Registry", 5, 9, 128, TheNMIEngine.GetNodeForCategory(), null, new ThePropertyBag { "Thumbnail=FA5:f07b" });
            TheNMIEngine.AddFields(tFormGuid, new List<TheFieldInfo>
            {
                {  new TheFieldInfo() { FldOrder=10,DataItem="MyPropertyBag.IsRuleActive.Value",Flags=2,Type=eFieldType.SingleCheck,Header="Active", FldWidth=1, TileLeft=2,TileTop=6,TileWidth=4,TileHeight=1 }},
                {  new TheFieldInfo() { FldOrder=20,DataItem="MyPropertyBag.IsRuleLogged.Value",Flags=2,Type=eFieldType.SingleCheck,Header="Logged", FldWidth=1, TileLeft=6,TileTop=6,TileWidth=4,TileHeight=1 }},

                {  new TheFieldInfo() { FldOrder=30,DataItem="MyPropertyBag.FriendlyName.Value",Flags=2,Type=eFieldType.SingleEnded,Header="Rule Name", FldWidth=3, TileLeft=0,TileTop=1,TileWidth=11,TileHeight=1, PropertyBag=new nmiCtrlSingleEnded() { HelpText="Give this rule a friendly name" }   }},
                {  new TheFieldInfo() { FldOrder=137,DataItem="cdeO",Flags=16,Type=eFieldType.SingleEnded,Header="Status", FldWidth=3, Format="<span style='font-size:xx-small'>Running:%MyPropertyBag.IsRuleRunning.Value%<br>TrigerAlive:%MyPropertyBag.IsTriggerObjectAlive.Value%</span>" }},

            });
            TheNMIEngine.AddSmartControl(MyBaseThing, tFormGuid, eFieldType.StatusLight, 35, 0x40, 0x0, "Status", "StatusLevel", new TheNMIBaseControl() { FldWidth = 1 });


            TheNMIEngine.AddTableButtons(tFormGuid, true, 1000,0xA2);

            TheFieldInfo tTriggerBut = TheNMIEngine.AddSmartControl(MyBaseThing, tFormGuid, eFieldType.TileButton, 5, 2, 0xC0, "Trigger Now", null, new nmiCtrlTileButton { ClassName = "cdeGoodActionButton", TileLeft = 1, TileTop = 6, TileWidth = 1, TileHeight = 1, NoTE = true });
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
                        tRule.FireAction(true);
                        TheCommCore.PublishToOriginator(pMSG.Message, new TSM(eEngineName.NMIService, "NMI_TOAST", string.Format("Rule {0} triggered", tRule.FriendlyName)));
                    }
                }
            });

            TheNMIEngine.AddAboutButton4(MyBaseThing, MyDash, null, true,false,"REFRESH_DASH");



            tFormGuid = new TheFormInfo() { cdeMID = new Guid("{4EA67262-4F66-4EFF-B7AD-51B98DAF376C}"), FormTitle = "Event Log", defDataSource = "EventLog", IsReadOnly = true, IsNotAutoLoading = true }; 
            TheNMIEngine.AddFormToThingUX(MyDash, MyBaseThing, tFormGuid, "CMyTable", "Event Log", 6, 3, 128, TheNMIEngine.GetNodeForCategory(), null, new ThePropertyBag { "Thumbnail=FA5:f073" }); //;:;50;:;True
            //TheNMIEngine.AddForm(tFormGuid);
            TheNMIEngine.AddFields(tFormGuid, new List<TheFieldInfo> {
                {  new TheFieldInfo() { FldOrder=1,DataItem="EventTime",Flags=0,Type=eFieldType.DateTime,Header="Event Time",FldWidth=2 }},
                {  new TheFieldInfo() { FldOrder=2,DataItem="EventName",Flags=0,Type=eFieldType.SingleEnded,Header="Event Name",FldWidth=3 }},
                {  new TheFieldInfo() { FldOrder=3,DataItem="StationName",Flags=0,Type=eFieldType.SingleEnded,Header="Node Name",FldWidth=2 }},
                {  new TheFieldInfo() { FldOrder=4,DataItem="EventTrigger",Flags=0,Type=eFieldType.ThingPicker,Header="Trigger Object",FldWidth=2,  PropertyBag=new nmiCtrlThingPicker() { IncludeEngines=true } }},
                {  new TheFieldInfo() { FldOrder=5,DataItem="ActionObject",Flags=0,Type=eFieldType.PropertyPicker,Header="Action Object",FldWidth=2,  PropertyBag=new nmiCtrlPropertyPicker() { ThingFld=4 } }},
                });

            AddRulesWizard();

            mIsUXInitialized = true;
            return true;
        }

        void sinkUpdated(ICDEThing sender, object inObj)
        {
            if (TheBaseAssets.MyServiceHostInfo.IsCloudService) return;

            TheThing pThing = sender.GetBaseThing();
            if (pThing!=null)
            {
                TheRule tRule = pThing.GetObject() as TheRule;
                if (tRule!=null)
                {
                    if (tRule.IsRuleActive)
                        ActivateRules();
                    else
                        RemoveTrigger(tRule,true);
                }
            }
        }

        public override void HandleMessage(ICDEThing sender, object pIncoming)
        {
            TheProcessMessage pMsg = pIncoming as TheProcessMessage;
            if (pMsg == null || pMsg.Message == null) return;
            string[] tCmd = pMsg.Message.TXT.Split(':');
            switch (tCmd[0])   //string 2 cases
            {
                case "CDE_INITIALIZED":
                    MyBaseEngine.SetInitialized(pMsg.Message);
                    break;
                case "CDE_INITIALIZE":
                    if (!MyBaseEngine.GetEngineState().IsEngineReady)
                        MyBaseEngine.SetInitialized(pMsg.Message);
                    MyBaseEngine.ReplyInitialized(pMsg.Message);
                    break;
                case "CDE_REGISTERRULE":
                    TheRule tRule = TheCommonUtils.DeserializeJSONStringToObject<TheRule>(pMsg.Message.PLS);
                    if (tRule != null)
                        RegisterRule(tRule);
                    break;
                case "CLAIM_RULES":
                    List<TheThing> tList = TheThingRegistry.GetThingsByFunc(eEngineName.ThingService, s => TheThing.GetSafePropertyString(s, "DeviceType").Equals(eKnownDeviceTypes.TheThingRule));
                    if (tList != null && tList.Count > 0)
                    {
                        foreach (TheThing tThing in tList)
                        {
                            tThing.Parent = MyBaseThing.ID;
                            tThing.EngineName = MyBaseEngine.GetEngineName();
                        }
                    }
                    ActivateRules();
                    MyDash.Reload(null, false);
                    TheCommCore.PublishToOriginator(pMsg.Message, new TSM(eEngineName.NMIService, "NMI_TOAST", $"{tList.Count} New Rules claimed"));
                    break;
                case "REFRESH_DASH":
                    ActivateRules();
                    MyDash.Reload(null, false);
                    break;
                case "FIRE_RULE":
                    var t = TheThingRegistry.GetThingByMID(MyBaseEngine.GetEngineName(), TheCommonUtils.CGuid(pMsg?.Message?.PLS));
                    if (t!=null)
                    {
                        TheRule tR=t.GetObject() as TheRule;
                        if (tR!=null)
                            tR.RuleTrigger(tR.TriggerOldValue, true);
                    }
                    break;
            }
        }

        public bool RegisterRule(TheThingRule pRule)
        {
            if (pRule == null || TheBaseAssets.MyServiceHostInfo.IsCloudService)
                return false;

            pRule.GetBaseThing().EngineName = MyBaseEngine.GetEngineName();
            if (pRule.TriggerCondition == eRuleTrigger.Set)
                pRule.ActionValue = null;
            pRule.IsRuleRunning = false;
            pRule.IsRuleWaiting = true;
            pRule.IsIllegal = false;
            pRule.IsTriggerObjectAlive = false;
            pRule.Parent = MyBaseThing.ID;
            pRule.GetBaseThing().EngineName = MyBaseEngine.GetEngineName();
            TheSystemMessageLog.WriteLog(4445, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM(eKnownDeviceTypes.TheThingRule, $"Rule {pRule.FriendlyName} stopped during Register Rule - waiting for startup"), false);

            TheThing.SetSafePropertyDate(pRule.GetBaseThing(), "LastTriggered", DateTimeOffset.MinValue);
            TheThing.SetSafePropertyDate(pRule.GetBaseThing(), "LastAction", DateTimeOffset.MinValue);
            TheThingRegistry.RegisterThing(pRule);
            return true;
        }

        private readonly object LockRules = new object();
        public bool ActivateRules()
        {
            if (TheCDEngines.MyThingEngine == null || TheBaseAssets.MyServiceHostInfo.IsCloudService || TheCommonUtils.cdeIsLocked(LockRules) || !mIsInitialized) return false;

            lock (LockRules)
            {
                if (mIsInitialized)
                    InitRules();
                //List<TheRule> tList = MyRulesStore.MyMirrorCache.GetEntriesByFunc(s => s.IsRuleActive && !s.IsRuleRunning && s.IsRuleWaiting);
                List<TheThing> tList = TheThingRegistry.GetThingsByFunc("*", s =>
                    TheThing.GetSafePropertyString(s,"Parent").Equals(MyBaseThing.ID) && //TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID) &&
                    TheThing.GetSafePropertyBool(s, "IsRuleActive") &&
                    !TheThing.GetSafePropertyBool(s, "IsRuleRunning") &&
                    TheThing.GetSafePropertyBool(s, "IsRuleWaiting"));
                if (tList != null && tList.Count > 0)
                {
                    foreach (TheThing tThing in tList)
                    {
                        TheRule tRule = tThing.GetObject() as TheRule;
                        if (tRule == null) continue;
                        if (string.IsNullOrEmpty(tRule.TriggerProperty))
                        {
                            tRule.IsIllegal = true;
                            tRule.IsRuleWaiting = false;
                            continue;
                        }
                        if (tRule.TriggerStartTime > DateTimeOffset.Now) continue;
                        if (tRule.TriggerEndTime < DateTimeOffset.Now)
                        {
                            RemoveTrigger(tRule, false);
                            continue;
                        }
                        switch (tRule.TriggerObjectType)
                        {
                            case "CDE_ENGINE":
                                TheThing tBase = TheThingRegistry.GetBaseEngineAsThing(tRule.TriggerObject);
                                if (tBase != null)
                                {
                                    tBase.RegisterEvent(eEngineEvents.IncomingMessage, sinkRuleIncoming);
                                    tRule.IsRuleWaiting = false;
                                    tRule.IsRuleRunning = true;
                                    tRule.IsTriggerObjectAlive = true;
                                    TheSystemMessageLog.WriteLog(4445, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM(eKnownDeviceTypes.TheThingRule, $"Rule {tRule.FriendlyName} started with TriggerType: {tRule.TriggerObjectType}"), false);

                                }
                                break;
                            case "CDE_EVENTFIRED":
                                TheThing tBaseE = TheThingRegistry.GetBaseEngineAsThing(tRule.TriggerObject);
                                if (tBaseE != null)
                                {
                                    tBaseE.RegisterEvent(tRule.TriggerProperty, sinkRuleThingEvent);
                                    tRule.IsRuleWaiting = false;
                                    tRule.IsRuleRunning = true;
                                    tRule.IsTriggerObjectAlive = true;
                                    TheSystemMessageLog.WriteLog(4445, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM(eKnownDeviceTypes.TheThingRule, $"Rule {tRule.FriendlyName} started with TriggerType: {tRule.TriggerObjectType}"), false);
                                }
                                break;
                            default:
                                TheThing tTriggerThing = TheThingRegistry.GetThingByMID("*", TheCommonUtils.CGuid(tRule.TriggerObject));
                                if (tTriggerThing != null)
                                {
                                    //if (tTriggerThing.GetObject() == null) continue;  //TODO: Verify if this can stay removed
                                    //if (tTriggerThing.GetProperty("FriendlyName").Value.ToString().Contains("Motion")) continue;
                                    cdeP tProp = tTriggerThing.GetProperty(tRule.TriggerProperty, true);
                                    if (tProp != null)
                                    {
                                        tProp.UnregisterEvent(eThingEvents.PropertyChanged, sinkRuleAction);
                                        tProp.RegisterEvent(eThingEvents.PropertyChanged, sinkRuleAction);
                                    }
                                    tRule.IsRuleWaiting = false;
                                    tRule.IsRuleRunning = true;
                                    tRule.IsTriggerObjectAlive = true;
                                    tRule.RuleTrigger(tProp.ToString(), true);
                                    TheSystemMessageLog.WriteLog(4445, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM(eKnownDeviceTypes.TheThingRule, $"Rule {tRule.FriendlyName} started with TriggerType: {tRule.TriggerObjectType}"), false);
                                }
                                break;
                        }
                    }
                }
            }
            return true;
        }

        private void RemoveTrigger(TheRule tRule, bool DoForce)
        {
            if (TheCDEngines.MyThingEngine == null || !TheBaseAssets.MasterSwitch) return;
            if (tRule.IsRuleActive || DoForce)
            {
                if (TheThingRegistry.HasThingsWithFunc(MyBaseEngine.GetEngineName(), s => s.cdeMID != tRule.GetBaseThing().cdeMID &&
                    TheThing.GetSafePropertyString(s, "TriggerObject") == tRule.TriggerObject && TheThing.GetSafePropertyBool(s,"IsRuleActive")))
                    return;
                switch (tRule.TriggerObjectType)
                {
                    default:
                        TheThing tTriggerThing = TheThingRegistry.GetThingByMID("*", TheCommonUtils.CGuid(tRule.TriggerObject));
                        if (tTriggerThing != null)
                        {
                            cdeP tProp = tTriggerThing.GetProperty(tRule.TriggerProperty);
                            if (tProp != null)
                                tProp.UnregisterEvent(eThingEvents.PropertyChanged, sinkRuleAction);
                        }
                        break;
                    case "CDE_ENGINE":
                        TheThing tBase = TheThingRegistry.GetBaseEngineAsThing(tRule.TriggerObject);
                        if (tBase != null)
                            tBase.UnregisterEvent(eEngineEvents.IncomingMessage, sinkRuleIncoming);
                        break;
                    case "CDE_EVENTFIRED":
                        TheThing tBaseE = TheThingRegistry.GetThingByID("*",tRule.TriggerObject);
                        if (tBaseE != null)
                            tBaseE.UnregisterEvent(tRule.TriggerProperty, sinkRuleThingEvent);
                        break;
                }
                tRule.IsRuleActive = false;
                tRule.IsRuleRunning = false;
                tRule.IsRuleWaiting = true;
            }
        }

        void sinkRuleIncoming(ICDEThing sender, object pIncoming)
        {
            TheProcessMessage pMsg = pIncoming as TheProcessMessage;
            if (pMsg == null || pMsg.Message == null) return;
            //System.Diagnostics.Debug.WriteLine(string.Format("{0} {1} {2}", pMsg.Message.ENG, pMsg.Message.TXT, pMsg.Message.PLS));
            List<TheThing> tList = TheThingRegistry.GetThingsByFunc("*", s => TheThing.GetSafePropertyString(s, "TriggerObject") == pMsg.Message.ENG);
            if (tList != null)
            {
                foreach (TheThing tThing in tList)
                {
                    TheRule tRule = tThing.GetObject() as TheRule;
                    if (tRule == null || !tRule.IsRuleActive) continue;
                    if (tRule.TriggerStartTime > DateTimeOffset.Now) continue;
                    if (tRule.TriggerEndTime < DateTimeOffset.Now)
                    {
                        RemoveTrigger(tRule,false);
                        continue;
                    }
                    if (pMsg.Message.TXT.StartsWith(tRule.TriggerProperty))
                        tRule.RuleTrigger(pMsg.Message.PLS);
                }
            }
        }
        void sinkRuleThingEvent(ICDEThing sender, object pIncoming)
        {
            if (sender == null) return;
            TheThing tThing = TheThingRegistry.GetThingByProperty("*",Guid.Empty, "TriggerObject", TheThing.GetSafePropertyString(sender, "ID"));
            if (tThing != null)
            {
                TheRule tRule = tThing.GetObject() as TheRule;
                if (tRule == null || !tRule.IsRuleActive) return;
                if (tRule.TriggerStartTime > DateTimeOffset.Now) return;
                if (tRule.TriggerEndTime < DateTimeOffset.Now)
                {
                    RemoveTrigger(tRule, false);
                    return;
                }
                tRule.FireAction(false);
            }
        }

        void sinkRuleAction(cdeP pProp)
        {
            //List<TheRule> tList = MyRulesStore.MyMirrorCache.GetEntriesByFunc(s => TheCommonUtils.CGuid(s.TriggerObject) != Guid.Empty && TheCommonUtils.CGuid(s.TriggerObject) == TheCommonUtils.CGuid(pEvent.TXT));
            List<TheThing> tList = TheThingRegistry.GetThingsByFunc("*", s => pProp.cdeO != Guid.Empty && TheCommonUtils.CGuid(TheThing.GetSafePropertyString(s, "TriggerObject")) == pProp.cdeO);
            if (tList != null)
            {
                foreach (TheThing tThing in tList)
                {
                    TheRule tRule = tThing.GetObject() as TheRule;
                    if (tRule == null || !tRule.IsRuleActive) continue;
                    if (tRule.TriggerStartTime > DateTimeOffset.Now) continue;
                    if (tRule.TriggerEndTime < DateTimeOffset.Now)
                    {
                        RemoveTrigger(tRule,true);
                        continue;
                    }
                    if (string.IsNullOrEmpty(pProp.Name) || string.IsNullOrEmpty(pProp.ToString()))
                        continue;
                    if (!pProp.Name.Equals(tRule.TriggerProperty))
                        continue;
                    tRule.RuleTrigger(pProp.ToString());
                    TheThing.SetSafePropertyDate(tRule.GetBaseThing(), "LastTriggered", DateTimeOffset.Now);
                }
            }
        }





        public bool LogEvent(string pEventName, string tTrigger,string tAction)
        {
            if (!MyRuleEventLog.IsReady) return false;
            TheEventLogData tSec = new TheEventLogData
            {
                EventTime = DateTimeOffset.Now,
                StationName = TheBaseAssets.MyServiceHostInfo.GetPrimaryStationURL(false),
                EventName = pEventName,
                EventTrigger = tTrigger,
                ActionObject = tAction
            };
            MyRuleEventLog.AddAnItem(tSec);
            return true;
        }
        public bool LogEvent(TheEventLogData pData)
        {
            if (!MyRuleEventLog.IsReady) return false;
            MyRuleEventLog.AddAnItem(pData);
            return true;
        }

        private void sinkStorageStationIsReadyFired(ICDEThing sender, object pReady)
        {
            if (pReady != null)
            {
                if (MyRuleEventLog == null)
                {
                    MyRuleEventLog = new TheStorageMirror<TheEventLogData>(TheCDEngines.MyIStorageService);
                    MyRuleEventLog.CacheTableName = "EventLog";                 
                    MyRuleEventLog.UseSafeSave = true;
                    MyRuleEventLog.SetRecordExpiration(604800, null);
                    MyRuleEventLog.CacheStoreInterval = 15;
                    MyRuleEventLog.IsStoreIntervalInSeconds = true;
                    MyRuleEventLog.AppendOnly = true;

                    if (!MyRuleEventLog.IsRAMStore)
                        MyRuleEventLog.CreateStore("The Event Log", "History of events fired by the RulesEngine", null, true, TheBaseAssets.MyServiceHostInfo.IsNewDevice);
                    else
                        MyRuleEventLog.InitializeStore(true, TheBaseAssets.MyServiceHostInfo.IsNewDevice);
                    LogEvent("Event Log Started", MyBaseThing.cdeMID.ToString(), "");
                    ActivateRules();
                }
            }
        }
        internal TheStorageMirror<TheEventLogData> MyRuleEventLog;   //The Storage Container for data to store






        public void AddRulesWizard()
        {
            var flds = TheNMIEngine.AddNewWizard(MyBaseThing, new Guid("6C34871C-D49B-4D7A-84E0-35E25B1F18B0"), "Welcome to the Rule Wizard Demo", new nmiCtrlWizard { SideBarTitle = "The Rules Wizard", PanelTitle = "<i class='fa faIcon fa-5x'>&#xf0d0;</i></br>Create new Rule" });
            var tMyForm2 = flds["Form"] as TheFormInfo;
            AddWizardHeader(tMyForm2); //Not Required but looks nice. Might become a later addition to the APIs

            var tFlds = TheNMIEngine.AddNewWizardPage(MyBaseThing, tMyForm2, 0, 1, 2, null /*"TEST"*/);
            TheNMIEngine.AddWizardControl(MyBaseThing, tMyForm2, eFieldType.SingleEnded, 1, 1, 2, 0, "Rule Name", "FriendlyName", new TheNMIBaseControl { Explainer = "1. Enter name for the new rule." });

            tFlds = TheNMIEngine.AddNewWizardPage(MyBaseThing, tMyForm2, 1, 2, 3, null /* "Rule Trigger"*/);
            var tTrig = TheNMIEngine.AddWizardControl(MyBaseThing, tMyForm2, eFieldType.ThingPicker, 2, 1, 2, 0, "Trigger Thing", "TriggerObject", new nmiCtrlThingPicker { IncludeEngines = true, Explainer = "1. Click to see all available trigger objects" });
            TheNMIEngine.AddWizardControl(MyBaseThing, tMyForm2, eFieldType.PropertyPicker, 2, 2, 2, 0, "Trigger Property", "TriggerProperty", new nmiCtrlPropertyPicker { ThingFld = tTrig.FldOrder, Explainer = "2. Select Trigger property" });
            TheNMIEngine.AddWizardControl(MyBaseThing, tMyForm2, eFieldType.ComboBox, 2, 3, 2, 0, "Trigger Condition", "TriggerCondition", new nmiCtrlComboBox { DefaultValue = "2", Options = "Fire:0;State:1;Equals:2;Larger:3;Smaller:4;Not:5;Contains:6;Set:7;StartsWith:8;EndsWith:9;Flank:10", Explainer = "3. Select Trigger condition" });
            TheNMIEngine.AddWizardControl(MyBaseThing, tMyForm2, eFieldType.SingleEnded, 2, 4, 2, 0, "Trigger Value", "TriggerValue", new nmiCtrlSingleEnded { Explainer = "4. Enter trigger value" });

            tFlds = TheNMIEngine.AddNewWizardPage(MyBaseThing, tMyForm2, 2, 3, 4, null/*"Action Type"*/, "5:'<%MyPropertyBag.ActionObjectType.Value%>'!='CDE_THING' && '<%MyPropertyBag.ActionObjectType.Value%>'!=''");
            TheNMIEngine.AddWizardControl(MyBaseThing, tMyForm2, eFieldType.ComboBox, 3, 1, 2, 0, "What Action to do", "ActionObjectType", new nmiCtrlComboBox { DefaultValue = "CDE_THING", Options = "Set Property on a Thing:CDE_THING;Publish Central:CDE_PUBLISHCENTRAL;Publish to Service:CDE_PUBLISH2SERVICE" });
            //HELP SECTION action type help section
            TheNMIEngine.AddWizardExplainer(MyBaseThing, tMyForm2, 3, 2, 0, "Action Object Type: Defines how the action should be executed:", new nmiCtrlSmartLabel { });
            TheNMIEngine.AddWizardExplainer(MyBaseThing, tMyForm2, 3, 3, 0, "DEFAULT:Set Property on a Thing: sets a property of a Thing to the Action Value", new nmiCtrlSmartLabel { });
            TheNMIEngine.AddWizardExplainer(MyBaseThing, tMyForm2, 3, 4, 0, "Publish Central: sends a message to all nodes in the mesh with the given parameters", new nmiCtrlSmartLabel { });
            TheNMIEngine.AddWizardExplainer(MyBaseThing, tMyForm2, 3, 5, 0, "Publish to Service: sends a message to a specific service in the mesh", new nmiCtrlSmartLabel { });


            tFlds = TheNMIEngine.AddNewWizardPage(MyBaseThing, tMyForm2, 3, 4, 6, null  /*"Action Object"*/);
            var tAThing=TheNMIEngine.AddWizardControl(MyBaseThing, tMyForm2, eFieldType.ThingPicker, 4, 1, 2, 0, "Action Thing", "ActionObject", new nmiCtrlThingPicker { IncludeEngines = true, Explainer = "1. Select the “Thing” (object) that contains the property to be changed with the action" });
            TheNMIEngine.AddWizardControl(MyBaseThing, tMyForm2, eFieldType.PropertyPicker, 4, 2, 2, 0, "Action Property", "ActionProperty", new nmiCtrlPropertyPicker { ThingFld = tAThing.FldOrder, Explainer = "2. Select action property" });
            TheNMIEngine.AddWizardControl(MyBaseThing, tMyForm2, eFieldType.SingleEnded, 4, 3, 2, 0, "Action Value", "ActionValue", new nmiCtrlSingleEnded { Explainer = "3. Select Action Value" });

            tFlds = TheNMIEngine.AddNewWizardPage(MyBaseThing, tMyForm2, 3, 5, 6, null /* "Action TSM"*/ );
            TheNMIEngine.AddWizardControl(MyBaseThing, tMyForm2, eFieldType.ThingPicker, 5, 1, 2, 0, "TSM Engine", "TSMEngine", new nmiCtrlThingPicker { IncludeEngines = true,  Filter = "EngineNames", Explainer = "1. Specify the Target Engine the message will be send to." });
            TheNMIEngine.AddWizardControl(MyBaseThing, tMyForm2, eFieldType.SingleEnded, 5, 2, 2, 0, "TSM Text", "TSMText", new nmiCtrlSingleEnded { Explainer = "2. Specify the command/text of the message.Target plugins are using this to parse the content of the payload" });
            TheNMIEngine.AddWizardControl(MyBaseThing, tMyForm2, eFieldType.TextArea, 5, 3, 2, 0, "TSM Payload", "TSMPayload", new nmiCtrlSingleEnded { TileHeight = 2, Explainer = "3. specify the payload message (TSM.PLS)" });

            tFlds = TheNMIEngine.AddNewWizardPage(MyBaseThing, tMyForm2, 3, 6, 0, null  /*"Final Settings"*/);
            TheNMIEngine.AddWizardControl(MyBaseThing, tMyForm2, eFieldType.SingleCheck, 6, 1, 2, 0, "Activate Rule Now", "IsRuleActive", new nmiCtrlSingleCheck { Explainer = "1. Click to make rule active." });
            TheNMIEngine.AddWizardControl(MyBaseThing, tMyForm2, eFieldType.SingleCheck, 6, 2, 2, 0, "Log Rule", "IsRuleLogged", new nmiCtrlSingleCheck { Explainer = "2. Click to log rule." });
        }

        private void AddWizardHeader(TheFormInfo tMyForm2)
        {
            //Remove this section if we decide no direct navivation to a page
            TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm2, eFieldType.TileButton, 10, 2, 0, "Welcome", null, new nmiCtrlTileButton { ParentFld = 200, OnClick = "GRP:WizarePage:1", TileWidth = 2, TileHeight = 1, TileFactorY = 2, NoTE = true });
            TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm2, eFieldType.TileButton, 20, 2, 0, "Rule Trigger", null, new nmiCtrlTileButton { ParentFld = 200, OnClick = "GRP:WizarePage:2", TileWidth = 2, TileHeight = 1, TileFactorY = 2, NoTE = true });
            TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm2, eFieldType.TileButton, 30, 2, 0, "Action Type", null, new nmiCtrlTileButton { ParentFld = 200, OnClick = "GRP:WizarePage:3", TileWidth = 2, TileHeight = 1, TileFactorY = 2, NoTE = true });
            TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm2, eFieldType.TileButton, 40, 2, 0, "Action Object", null, new nmiCtrlTileButton { ParentFld = 200, OnClick = "GRP:WizarePage:4", TileWidth = 2, TileHeight = 1, TileFactorY = 2, NoTE = true });
            TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm2, eFieldType.TileButton, 50, 2, 0, "Action TSM", null, new nmiCtrlTileButton { ParentFld = 200, OnClick = "GRP:WizarePage:5", TileWidth = 2, TileHeight = 1, TileFactorY = 2, NoTE = true });
            TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm2, eFieldType.TileButton, 60, 2, 0, "Finish", null, new nmiCtrlTileButton { ParentFld = 200, OnClick = "GRP:WizarePage:6", TileWidth = 2, TileHeight = 1, TileFactorY = 2, NoTE = true });
        }
    }
}
