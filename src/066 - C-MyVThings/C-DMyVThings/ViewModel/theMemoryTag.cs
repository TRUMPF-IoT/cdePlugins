// SPDX-FileCopyrightText: 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

﻿using nsCDEngine.BaseClasses;
using nsCDEngine.Communication;
using nsCDEngine.Engines;
using nsCDEngine.Engines.NMIService;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.ViewModels;
using System;
// ReSharper disable RedundantEmptyObjectOrCollectionInitializer
// ReSharper disable NotAccessedField.Local

namespace CDMyVThings.ViewModel
{
    public class TheMemoryTag : ICDEThing
    {
        #region ICDEThing Methods
        public void SetBaseThing(TheThing pThing)
        {
            MyBaseThing = pThing;
        }
        public TheThing GetBaseThing()
        {
            return MyBaseThing;
        }
        public virtual cdeP GetProperty(string pName, bool DoCreate)
        {
            return MyBaseThing?.GetProperty(pName, DoCreate);
        }
        public virtual cdeP SetProperty(string pName, object pValue)
        {
            return MyBaseThing?.SetProperty(pName, pValue);
        }

        public void RegisterEvent(string pName, Action<ICDEThing, object> pCallBack)
        {
            MyBaseThing?.RegisterEvent(pName, pCallBack);
        }
        public void UnregisterEvent(string pName, Action<ICDEThing, object> pCallBack)
        {
            MyBaseThing?.UnregisterEvent(pName, pCallBack);
        }
        public void FireEvent(string pEventName, ICDEThing sender, object pPara, bool FireAsync)
        {
            MyBaseThing?.FireEvent(pEventName, sender, pPara, FireAsync);
        }
        public bool HasRegisteredEvents(string pEventName)
        {
            if (MyBaseThing != null)
                return MyBaseThing.HasRegisteredEvents(pEventName);
            return false;
        }

        public virtual void HandleMessage(ICDEThing sender, object pMsg)
        { }


        protected TheThing MyBaseThing;

        protected bool mIsUXInitCalled;
        protected bool mIsUXInitialized;
        protected bool mIsInitCalled;
        protected bool mIsInitialized;
        public bool IsUXInit()
        { return mIsUXInitialized; }
        public bool IsInit()
        { return mIsInitialized; }


        /// <summary>
        /// The possible types of WeMo devices that can be detected
        /// </summary>
        #endregion

        private IBaseEngine MyBaseEngine;

        public virtual bool Init()
        {
            if (mIsInitCalled) return mIsInitCalled;
            mIsInitCalled = true;

            if (string.IsNullOrEmpty(MyBaseThing.ID))
                MyBaseThing.ID = Guid.NewGuid().ToString();
            MyBaseThing.Value = "0";
            TheThing.SetSafePropertyBool(MyBaseThing, "IsActive", true);

            MyBaseThing.StatusLevel = 1;
            MyBaseThing.LastMessage = "MemoryTag Ready";
            mIsInitialized = true;
            return true;
        }

        public bool Delete()
        {

            mIsInitialized = false;
            // TODO Properly implement delete
            return true;
        }


        protected TheFormInfo MyStatusForm;
        protected TheDashPanelInfo SummaryForm;
        protected TheFieldInfo PropTable;
        public virtual bool CreateUX()
        {
            if (mIsUXInitCalled) return false;
            mIsUXInitCalled = true;

            var tFlds = TheNMIEngine.AddStandardForm(MyBaseThing, null,12,null,"Value");
            MyStatusForm = tFlds["Form"] as TheFormInfo;
            SummaryForm = tFlds["DashIcon"] as TheDashPanelInfo;
            SummaryForm.PropertyBag=new ThePropertyBag() { string.Format("Format={0}<br>{{0}} Properties", MyBaseThing.FriendlyName) };
            SummaryForm.RegisterPropertyChanged(sinkStatChanged);

            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.Number, 21, 0, 0x0, "###CDMyVThings.TheVThings#CountOfProperties598472#Count of Properties###", "Value", new nmiCtrlNumber() { ParentFld=1 });
            TheFieldInfo mSendbutton = TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.TileButton, 22, 2, 0, "###CDMyVThings.TheVThings#Reload598472#Reload###", false, "", null, new nmiCtrlTileButton() { ParentFld=1,NoTE=true, ClassName="cdeGoodActionButton" });
            mSendbutton.RegisterUXEvent(MyBaseThing, eUXEvents.OnClick, "", (pThing, pPara) =>
            {
                TheProcessMessage pMsg = pPara as TheProcessMessage;
                if (pMsg?.Message == null) return;
                sinkStatChanged(null, null);
                MyStatusForm.Reload(pMsg, true);
            });
            PropTable = TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.Table, 40, 0xA0, 0x80, "All Properties", "mypropertybag;0", new TheNMIBaseControl() { NoTE=true, ParentFld=1, TileWidth=12, TileHeight=16  });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.SingleEnded, 30, 0x0A, 0, "New Property Name", "ScratchName", new nmiCtrlSingleEnded() { ParentFld=1 });
            TheFieldInfo tBut = TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.TileButton, 35, 0x0A, 0, "Add Property", false, null, null, new nmiCtrlTileButton() { ParentFld=1, NoTE=true });
            tBut.RegisterUXEvent(MyBaseThing, eUXEvents.OnClick, "AddProp", (pThing, pObj) =>
            {
                TheProcessMessage pMsg = pObj as TheProcessMessage;
                if (pMsg?.Message == null) return;
                string[] parts = pMsg.Message.PLS.Split(':');
                TheThing tOrg = TheThingRegistry.GetThingByMID(MyBaseEngine.GetEngineName(), TheCommonUtils.CGuid(parts[2]));
                if (tOrg == null) return;

                string tNewPropName = TheThing.GetSafePropertyString(tOrg, "ScratchName");
                if (string.IsNullOrEmpty(tNewPropName))
                    TheCommCore.PublishToOriginator(pMsg.Message, new TSM(eEngineName.NMIService, "NMI_TOAST", "Please specify a new property name"));
                else
                {
                    if (tOrg.GetProperty(tNewPropName) != null)
                    {
                        TheCommCore.PublishToOriginator(pMsg.Message, new TSM(eEngineName.NMIService, "NMI_TOAST", "Property already exists"));
                    }
                    else
                    {
                        tOrg.DeclareNMIProperty(tNewPropName, ePropertyTypes.TString);
                        TheCommCore.PublishToOriginator(pMsg.Message, new TSM(eEngineName.NMIService, "NMI_TOAST", "Property Added"));
                        MyStatusForm.Reload(pMsg, false);
                    }
                }
                tOrg.RemoveProperty("ScratchName");
            });
            mIsUXInitialized = true;
            return true;
        }

        void sinkStatChanged(TheDashPanelInfo tDas, cdeP prop)
        {
            MyBaseThing.Value=MyBaseThing.PropertyCount.ToString();
            SummaryForm.SetUXProperty(Guid.Empty, string.Format("iValue={0}", MyBaseThing.Value));
        }

        public TheMemoryTag(TheThing pThing, IBaseEngine pEngine)
        {
            MyBaseThing = pThing ?? new TheThing();
            MyBaseEngine = pEngine;
            MyBaseThing.DeviceType = eVThings.eMemoryTag;
            MyBaseThing.EngineName = pEngine.GetEngineName();
            MyBaseThing.SetIThingObject(this);
        }
    }
}
