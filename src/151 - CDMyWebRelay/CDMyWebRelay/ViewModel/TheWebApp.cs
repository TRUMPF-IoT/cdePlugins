// SPDX-FileCopyrightText: 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using System;
using nsCDEngine.BaseClasses;
using nsCDEngine.Engines;
using nsCDEngine.Engines.NMIService;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.ViewModels;
using static CDMyWebRelay.TheRelayService;
// ReSharper disable UseNullPropagation

namespace CDMyWebRelay
{
    public class TheRelayAppInfo : TheThingBase
    {
        public TheRelayAppInfo(TheThing pBaseThing,string pName, ICDEPlugin pBase)
        {
            if (pBaseThing == null)
            {
                pBaseThing = new TheThing
                {
                    DeviceType = eWebAppTypes.TheWebApp,
                    EngineName = pBase.GetBaseEngine().GetEngineName()
                };
            }
            HostUrl = TheBaseAssets.MyServiceHostInfo.GetPrimaryStationURL(false);
            MyBaseThing = pBaseThing;
            if (!string.IsNullOrEmpty(pName))
                MyBaseThing.FriendlyName = pName;
            MyBaseThing.SetIThingObject(this);
        }

        public string DeviceType
        {
            get { return MyBaseThing.DeviceType; }
            set { MyBaseThing.DeviceType = value; } 
        }
        public string FriendlyName 
        { 
            get { return MyBaseThing.FriendlyName; } 
            set { MyBaseThing.FriendlyName=value; } 
        }

        public string CloudUrl
        {
            get { return TheThing.GetSafePropertyString(MyBaseThing, "CloudUrl"); }
            set { TheThing.SetSafePropertyString(MyBaseThing, "CloudUrl", value); }
        }
        public string Category
        {
            get { return TheThing.GetSafePropertyString(MyBaseThing, "Category"); }
            set { TheThing.SetSafePropertyString(MyBaseThing, "Category", value); }
        }
        public string HomePage
        {
            get { return TheThing.GetSafePropertyString(MyBaseThing, "HomePage"); }
            set { TheThing.SetSafePropertyString(MyBaseThing, "HomePage", value); }
        }
        public string SUID
        {
            get { return TheThing.GetSafePropertyString(MyBaseThing, "SUID"); }
            set { TheThing.SetSafePropertyString(MyBaseThing, "SUID", value); }
        }
        public string SPWD
        {
            get { return TheThing.GetSafePropertyString(MyBaseThing, "SPWD"); }
            set { TheThing.SetSafePropertyString(MyBaseThing, "SPWD", value); }
        }

        public bool IsHTTP10
        {
            get { return TheThing.GetSafePropertyBool(MyBaseThing, "IsHTTP10"); }
            set { TheThing.SetSafePropertyBool(MyBaseThing, "IsHTTP10", value); }
        }

        internal string SSID { get; set; }
        internal string HostUrl { get; set; }

        public override bool Init()
        {
            mIsInitialized = true;
            return true;
        }

        public override bool CreateUX()
        {
            if (!mIsUXInitialized)
            {
                mIsUXInitialized = true;
                TheFormInfo tMyLiveForm = new TheFormInfo(MyBaseThing) //, eEngineName.NMIService, "BTMid:" + MyBaseThing.cdeMID.ToString(), null)
                { DefaultView = eDefaultView.IFrame, PropertyBag = new nmiCtrlIFrameView { TileWidth = 18, TileHeight = 11,Source=$"/CDEWRA{MyBaseThing.cdeMID}", OnIFrameLoaded = "NOWN:IFRA" } }; 
                tMyLiveForm.RegisterEvent2(eUXEvents.OnLoad, (pMsg, Para) => {
                    //TheNMIEngine.SetUXProperty(pMsg.Message.GetOriginator(), tMyLiveForm.cdeMID, $"Source={HostUrl}/CDEWRA{MyBaseThing.cdeMID}");
                });
                TheNMIEngine.AddFormToThingUX(MyBaseThing, tMyLiveForm, "CMyForm", $"WebApp: {MyBaseThing.FriendlyName}", 1, 3, 0, Category, null, new ThePropertyBag() { });
                MyBaseThing.RegisterEvent($"OnLoaded:{tMyLiveForm.cdeMID}:IFRA", (sender, obj) =>
                {
                    if (!(obj is TheProcessMessage t))
                        return;
                    //TheNMIEngine.SetUXProperty(t.Message.GetOriginator(), tMyLiveForm.cdeMID, "Source=http://www.c-labs.com");
                });
            }
            return true;
        }
    }
}
