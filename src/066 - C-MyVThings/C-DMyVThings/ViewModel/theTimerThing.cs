// SPDX-FileCopyrightText: 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

﻿using nsCDEngine.BaseClasses;
using nsCDEngine.Engines;
using nsCDEngine.Engines.NMIService;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.ViewModels;
using System;
using System.Threading;

namespace CDMyVThings.ViewModel
{
    class TheVTimer: TheNMILiveTag
    {
        [ConfigProperty(DefaultValue = false)]
        public bool IsDisabled
        {
            get { return TheThing.MemberGetSafePropertyBool(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyBool(MyBaseThing, value); }
        }

        private IBaseEngine MyBaseEngine;

        public override cdeP SetProperty(string pName, object pValue)
        {
            if (pName=="TriggerTimer")
                sinkTriggered(GetProperty(pName,false));
            return base.SetProperty(pName, pValue);
        }

        private void sinkLoopChanged(cdeP pProp)
        {
            int tTime = TheCommonUtils.CInt(TheThing.GetSafePropertyNumber(this, "MsToTrigger"));
            //if (tTime > 0)
            {
                int tPeriod = TheCommonUtils.CInt(TheThing.GetSafePropertyNumber(this, "LoopPeriod"));
                if (tPeriod == 0) tPeriod = -1;
                if (tPeriod > 0 && tPeriod < 10) tPeriod = 10;
                MyBaseThing.Value = "0";
                if (mTimer != null)
                {
                    mTimer.Change(tTime, tPeriod);
                }
                else
                {
                    mTimer = new Timer(sinkTriggerTimeout, null, tTime, tPeriod);
                }
                IsActive = true;
                MyBaseThing.StatusLevel = 1;
                MyBaseThing.LastMessage = $"Timer started at {DateTimeOffset.Now}";
                SummaryForm?.SetUXProperty(Guid.Empty, string.Format("Background=green"));
            }
        }

        private void sinkTriggered(cdeP pProp)
        {
            if (mTimer != null)
                mTimer.Dispose();
            mTimer = null;
            if (pProp != null && pProp.ToString().Equals("stop", StringComparison.OrdinalIgnoreCase))
            {
                StopTimer();
                return;
            }
            sinkLoopChanged(pProp);
        }

        private void StopTimer()
        {
            if (mTimer != null)
                mTimer.Dispose();
            mTimer = null;
            IsActive = false;
            MyBaseThing.StatusLevel = 0;
            MyBaseThing.LastMessage = $"Timer stopped at {DateTimeOffset.Now}";
            SummaryForm?.SetUXProperty(Guid.Empty, string.Format("Background=gray"));
            return;
        }

        object tLock = new object();
        void sinkTriggerTimeout(object state)
        {
            if (TheCommonUtils.cdeIsLocked(tLock)) return;
            if (!TheBaseAssets.MasterSwitch)
            {
                mTimer?.Dispose();
                mTimer = null;
                return;
            }
            if (IsDisabled)
            {
                if (MyBaseThing.StatusLevel != 0)
                {
                    MyBaseThing.StatusLevel = 0;
                    MyBaseThing.LastMessage = "Timer disabled";
                }
                return;
            }
            else if (MyBaseThing.StatusLevel == 0)
            {
                MyBaseThing.StatusLevel = 1;
                MyBaseThing.LastMessage = "Timer enabled";
            }
            lock (tLock)
            {
                cdeP pV = MyBaseThing.SetProperty("Value", (TheCommonUtils.CInt(MyBaseThing.Value) + 1));
                pV.cdeE = 2;
            }
        }

        Timer mTimer = null;

        public TheVTimer(TheThing pThing, IBaseEngine pEngine)
            : base(pThing)
        {
            if (pThing != null)
                MyBaseThing = pThing;
            else
                MyBaseThing = new TheThing();

            cdeP tfirstTrigger = MyBaseThing.DeclareNMIProperty("MsToTrigger", ePropertyTypes.TNumber);
            tfirstTrigger.RegisterEvent(eThingEvents.PropertySet, sinkLoopChanged);

            cdeP tLoop = MyBaseThing.DeclareNMIProperty("LoopPeriod", ePropertyTypes.TNumber);
            tLoop.RegisterEvent(eThingEvents.PropertySet, sinkLoopChanged);
            cdeP tTrigger=MyBaseThing.DeclareNMIProperty("TriggerTimer", ePropertyTypes.TString);
            tTrigger.RegisterEvent(eThingEvents.PropertySet, sinkTriggered);

            MyBaseEngine = pEngine;
            MyBaseThing.DeviceType = eVThings.eVTimer;
            MyBaseThing.EngineName = pEngine.GetEngineName();
            MyBaseThing.SetIThingObject(this);

        }

        public override bool DoInit()
        {
            MyBaseThing.StatusLevel = 0;
            MyBaseThing.LastMessage = "Timer Ready";
            IsActive = false;

            if (string.IsNullOrEmpty(MyBaseThing.ID))
            {
                MyBaseThing.ID = Guid.NewGuid().ToString();
                TheThing.SetSafePropertyBool(MyBaseThing, "IsStateSensor", true);
                TheThing.SetSafePropertyString(MyBaseThing, "StateSensorType", "analog");
                TheThing.SetSafePropertyString(MyBaseThing, "StateSensorUnit", "ticks");
                TheThing.SetSafePropertyNumber(MyBaseThing, "StateSensorMaxValue", 60000);
                TheThing.SetSafePropertyNumber(MyBaseThing, "StateSensorAverage", 1000);
                TheThing.SetSafePropertyNumber(MyBaseThing, "StateSensorMinValue", 0);
            }
            if (TheThing.GetSafePropertyBool(MyBaseThing, "AutoStart"))
                sinkTriggered(null);
            return true;
        }



        public override bool DoCreateUX()
        {
            var tFlds = TheNMIEngine.AddStandardForm(MyBaseThing, null,12,null,"Value");
            MyStatusForm = tFlds["Form"] as TheFormInfo;
            SummaryForm = tFlds["DashIcon"] as TheDashPanelInfo;
            SummaryForm.PropertyBag=new nmiDashboardTile() { Format=$"{MyBaseThing.FriendlyName}<br>{{0}}" };

            var ts=TheNMIEngine.AddStatusBlock(MyBaseThing, MyStatusForm, 10);
            ts["Group"].SetParent(1);
            ts["Value"].SetParent(1);

            //CountBar = TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.Number, 100, 0, 0x0, "Trigger Cnt", "Value", new nmiCtrlNumber() { ParentFld=1 });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.CollapsibleGroup, 200, 2, 128, "###CDMyVThings.TheVThings#Settings901637#Settings...###", null, new nmiCtrlCollapsibleGroup() { ParentFld=1,TileWidth=6,IsSmall=true, DoClose = true });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.Number, 202, 2, 0x0, "###CDMyVThings.TheVThings#MstoTrigger260442#Ms to Trigger###", "MsToTrigger", new nmiCtrlNumber() { ParentFld=200, TileWidth=3 });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.Number, 203, 2, 0x0, "###CDMyVThings.TheVThings#LoopTime96821#Loop Time###", "LoopPeriod", new nmiCtrlNumber() { ParentFld=200, TileWidth=3 });

            var tc=TheNMIEngine.AddStartingBlock(MyBaseThing, MyStatusForm, 1000, (pMsg, DoConn) => {
                if (DoConn)
                {
                    MyBaseThing.Value = "0";
                    sinkTriggered(null);
                }
                else
                {
                    MyBaseThing.Value = "0";
                    StopTimer();
                }
            },128,"IsActive");
            tc["Group"].SetParent(1);
            tc["Group"].Header = "Timer Control";

            return true;
        }
    }
}
