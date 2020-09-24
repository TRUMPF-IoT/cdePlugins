// SPDX-FileCopyrightText: 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0
using System;

using nsCDEngine.Engines;
using nsCDEngine.BaseClasses;
using nsCDEngine.ViewModels;
using nsCDEngine.Engines.NMIService;
using nsCDEngine.Engines.ThingService;

namespace CDMyNetwork.ViewModel
{
    abstract class TheNetworkServiceBase : TheThingBase
    {
        public abstract void Connect();
        public abstract void Disconnect();

        protected IBaseEngine MyBaseEngine;
        public TheNetworkServiceBase(TheThing tBaseThing, ICDEPlugin pPluginBase)
        {
            if (tBaseThing != null)
            {
                MyBaseThing = tBaseThing;
                
                // If we are changing objects due to DeviceType change, move the UX state over as we can't easily re-create it
                TheNetworkServiceBase previousInstance = MyBaseThing.GetObject() as TheNetworkServiceBase;
                if (previousInstance != null)
                {
                    mIsUXInitialized = previousInstance.mIsUXInitialized;
                    mIsInitialized = previousInstance.mIsInitialized;
                    previousInstance.CloseServer();
                }
            }
            else
            {
                MyBaseThing = new TheThing();
            }
            MyBaseEngine = pPluginBase.GetBaseEngine();
            // Now handled by the caller via GetOrCreateAndSetIThingObject 
            // MyBaseThing.SetIThingObject(this); 
        }

        public override bool Init()
        {
            return Init(true);
        }

        public virtual bool Init(bool declareInit)
        {
            if (mIsInitCalled) return false;
            mIsInitCalled = true;
            IsConnected = false;
            MyBaseThing.StatusLevel = 0;
            MyBaseThing.LastMessage = "Service is ready";
            MyBaseThing.RegisterStatusChanged(sinkUpdateUX);
            MyBaseThing.RegisterEvent(eEngineEvents.IncomingMessage, HandleMessage);
            if (string.IsNullOrEmpty(MyBaseThing.ID))
            {
                MyBaseThing.ID = Guid.NewGuid().ToString();
                TheThing.SetSafePropertyBool(MyBaseThing, "IsStateSensor", true);
                TheThing.SetSafePropertyString(MyBaseThing, "StateSensorType", "analog");
                TheThing.SetSafePropertyString(MyBaseThing, "StateSensorUnit", "ms");
                TheThing.SetSafePropertyNumber(MyBaseThing, "StateSensorMaxValue", 100);
                TheThing.SetSafePropertyNumber(MyBaseThing, "StateSensorAverage", 50);
                TheThing.SetSafePropertyNumber(MyBaseThing, "StateSensorMinValue", 0);
                //TheThing.SetSafePropertyString(MyBaseThing, "StateSensorName", MyBaseThing.FriendlyName);
                TheRulesFactory.CreateOrUpdateRule(new TheThingRule(Guid.NewGuid(), "NetService:" + MyBaseThing.FriendlyName + " Failed", MyBaseThing, "StatusLevel", eRuleTrigger.Larger, "1", true, true));
            }
            if (declareInit)
            {
                mIsInitialized = true;
            }
            return true;
        }

        public override bool Delete()
        {
            MyBaseThing.UnregisterStatusChanged(sinkUpdateUX);
            GetProperty("StatusLevel", true).UnregisterEvent(eThingEvents.PropertyChanged, sinkUpdateUX);
            MyBaseThing.UnregisterEvent(eEngineEvents.IncomingMessage, HandleMessage);
            Disconnect();
            mIsInitialized = false;
            return true;
        }

        public void CloseServer()
        {
            MyBaseThing.UnregisterEvent(eEngineEvents.IncomingMessage, HandleMessage);
        }

        public override bool CreateUX()
        {
            if (mIsUXInitCalled) return false;
            mIsUXInitCalled = true;

            InitUX();

            mIsUXInitialized = true;
            return true;
        }

        protected TheFormInfo MyStatusForm;
        protected TheFieldInfo MyCollapsHead;
        protected TheDashPanelInfo SummaryForm;
        public virtual void InitUX()
        {
            var tHead = TheNMIEngine.AddStandardForm(MyBaseThing, MyBaseThing.FriendlyName, null, 0x0, new nmiStandardForm { MaxTileWidth = 12, IconUpdateName = "StatusLevel", UseMargin = true });
            MyStatusForm = tHead["Form"] as TheFormInfo; // TheNMIEngine.AddForm(new TheFormInfo(MyBaseThing) { FormTitle = MyBaseThing.DeviceType, DefaultView = eDefaultView.Form, PropertyBag = new ThePropertyBag { "MaxTileWidth=6" } });
            SummaryForm = tHead["DashIcon"] as TheDashPanelInfo;
            MyCollapsHead = tHead["Header"] as TheFieldInfo;
            SummaryForm.PropertyBag = new nmiDashboardTile { Format = $"{MyBaseThing.FriendlyName}<br>Status Level: {{0}}", LabelForeground = "white", ClassName = "cdeLiveTile cdeLiveTileBar" };
            var tBlock = TheNMIEngine.AddStatusBlock(MyBaseThing, MyStatusForm, 2);
            tBlock["Group"].SetParent(1);
            tBlock["Group"].Flags = 0;
            tBlock["Group"].cdeA = 0;
            tBlock["Value"].cdeA = 0;
            tBlock = TheNMIEngine.AddConnectivityBlock(MyBaseThing, MyStatusForm, 120, sinkConnect);
            tBlock["Group"].SetParent(1);
            sinkUpdateUX(null);
        }

        void sinkConnect(TheProcessMessage pMsg, bool pDoConnect)
        {
            if (pDoConnect)
                Connect();
            else
                Disconnect();
        }

        public bool IsConnected
        {
            get { return TheThing.GetSafePropertyBool(MyBaseThing, "IsConnected"); }
            set { TheThing.SetSafePropertyBool(MyBaseThing, "IsConnected", value); }
        }

        [ConfigProperty(Generalize = true)]
        public string Address
        {
            get { return TheThing.MemberGetSafePropertyString(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyString(MyBaseThing, value); }
        }

        [ConfigProperty]
        public bool AutoConnect
        {
            get { return TheThing.MemberGetSafePropertyBool(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyBool(MyBaseThing, value); }
        }

        void sinkUpdateUX(cdeP pProp)
        {
            int i = MyBaseThing.StatusLevel; //Optimizing that GetProperty is not called 3 times
            SummaryForm?.SetUXProperty(Guid.Empty, string.Format("Background={0}", TheNMIEngine.GetStatusColor(i)));

            MyBaseEngine.ProcessMessage(new TheProcessMessage(new TSM(MyBaseEngine.GetEngineName(), "UPDATE_VALUE")));
        }
    }
}
    
