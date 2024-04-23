// SPDX-FileCopyrightText: 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using nsCDEngine.BaseClasses;
using nsCDEngine.Communication;
using System.Collections.Generic;
using System;
using nsCDEngine.ViewModels;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.Engines.StorageService;
using nsCDEngine.Engines.NMIService;
using nsCDEngine.Security;
using nsCDEngine.Engines;
// ReSharper disable UseNullPropagation

namespace CDMyWebRelay
{
    internal partial class TheRelayService: ThePluginBase
    {
        public class eWebAppTypes : TheDeviceTypeEnum
        {
            public const string TheWebApp = "Web Application";
        }

        #region ICDEPlugin
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pBase">The C-DEngine is creating a Host for this engine and hands it to the Plugin Service</param>
        public override void InitEngineAssets(IBaseEngine pBase)
        {
            base.InitEngineAssets(pBase);
            MyBaseEngine = pBase;
            MyBaseEngine.SetEngineID(new Guid("{A5FD8A57-C4B9-4EDB-8965-082D3F466E33}"));
            MyBaseEngine.SetFriendlyName("The Web Relay Service");
            MyBaseEngine.SetCDEMinVersion(6.104);

            MyBaseEngine.GetEngineState().IsAllowedUnscopedProcessing = TheBaseAssets.MyServiceHostInfo.IsCloudService;
            MyBaseEngine.SetPluginInfo("This service allows you relay web pages", 0, null, "toplogo-150.png", "C-Labs", "http://www.c-labs.com", new List<string> { "Service" });
        }
        #endregion

        public override bool Init()
        {
            if (mIsInitCalled) return false;
            mIsInitCalled = true;

            ReqBuffer = new TheMirrorCache<TheRequestData>(0);
            string t = TheCommonUtils.CStr(TheBaseAssets.MySettings.GetSetting("WRA_RequestTimeout"));
            if (!string.IsNullOrEmpty(t))
                RequestTimeout = TheCommonUtils.CInt(t);
            if (RequestTimeout < 15) RequestTimeout = 15;

            InitServices();

            TheCommCore.MyHttpService.RegisterHttpInterceptorB4("/", InterceptHttpRequest);
            TheScopeManager.RegisterScopeChanged(sinkScopeIDUpdate);
            MyBaseThing.RegisterEvent(eEngineEvents.IncomingMessage, HandleMessage);

            mIsInitialized = true;
            MyBaseEngine.ProcessInitialized();
            return true;
        }

        private void InitServices()
        {
            if (TheBaseAssets.MyServiceHostInfo.IsCloudService)
                return;
            List<TheThing> tDevList = TheThingRegistry.GetThingsOfEngine(MyBaseEngine.GetEngineName());
            if (tDevList.Count > 0)
            {
                foreach (TheThing tDev in tDevList)
                {
                    switch (tDev.DeviceType)
                    {
                        case eWebAppTypes.TheWebApp:
                            TheRelayAppInfo tApp = new TheRelayAppInfo(tDev, null, this);
                            if (string.IsNullOrEmpty(tApp.HomePage)) tApp.HomePage = "/";
                            if (!tApp.HomePage.StartsWith("/")) tApp.HomePage = "/" + tApp.HomePage;
                            tApp.SSID = TheScopeManager.GetScrambledScopeID();
                            TheThingRegistry.RegisterThing(tApp);
                            break;
                    }
                }
            }
        }

        public int RequestTimeout
        {
            get { return TheCommonUtils.CInt(TheThing.GetSafePropertyString(MyBaseThing, "RequestTimeout")); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, "RequestTimeout", value); }
        }

        TheDashboardInfo tDashGuid;

        public override bool CreateUX()
        {
            if (mIsUXInitCalled) return false;
            mIsUXInitCalled = true;

            tDashGuid = TheNMIEngine.AddDashboard(MyBaseThing, new TheDashboardInfo(MyBaseEngine, "") { PropertyBag = new ThePropertyBag() { "Category=Services", "Caption=Web-Relay", "Thumbnail=FA5:f382" }, FldOrder = 8500, cdeA = 0 });

            TheFormInfo tRelayForm = TheNMIEngine.CreateEngineForms(MyBaseThing, TheThing.GetSafeThingGuid(MyBaseThing, MyBaseEngine.GetFriendlyName()), "Admin Apps", null, 1, 1, 0x00C0, "Administration", "REFRESH_DASH");
            tRelayForm.GetFromFirstNodeOnly = true;
            tRelayForm.AddButtonText = "Add New Site";
            tRelayForm.AddACL = 128;
            tRelayForm.defDataSource = $"TheThing;:;0;:;True;:;EngineName={MyBaseEngine.GetEngineName()};DeviceType={eWebAppTypes.TheWebApp}";
            tRelayForm.AssociatedClassName = "CMyTable";
            TheNMIEngine.AddSmartControl(MyBaseThing, tRelayForm, eFieldType.SingleEnded, 40, 2, 0x80, "###Address###", "Address", new nmiCtrlSingleEnded { FldWidth = 6, TileWidth = 6, HelpText = "Leave blank to use Relay-Web-Server", Caption = "Target Url" });
            TheNMIEngine.AddSmartControl(MyBaseThing, tRelayForm, eFieldType.SingleEnded, 60, 2, 0x80, "Category", "Category", new nmiCtrlSingleEnded() { FldWidth = 2 });
            TheNMIEngine.AddSmartControl(MyBaseThing, tRelayForm, eFieldType.SingleEnded, 50, 2, 0x80, "HomePage", "HomePage", new nmiCtrlSingleEnded() { FldWidth = 2, DefaultValue = "/" });
            TheNMIEngine.AddSmartControl(MyBaseThing, tRelayForm, eFieldType.CheckField, 80, 2, 0x80, "ACL", "cdeA", new nmiCtrlCheckField { FldWidth = 8, DefaultValue = "0", Bits = 8 });
            TheNMIEngine.AddSmartControl(MyBaseThing, tRelayForm, eFieldType.SingleEnded, 90, 2, 0x80, "UserID", "SUID", new nmiCtrlSingleEnded() { FldWidth = 2 });
            TheNMIEngine.AddSmartControl(MyBaseThing, tRelayForm, eFieldType.Password, 100, 3, 0x80, "Password", "SPWD", new nmiCtrlPassword { FldWidth = 2 });
            TheNMIEngine.AddSmartControl(MyBaseThing, tRelayForm, eFieldType.SingleCheck, 110, 2, 0x80, "HTTP1.0", "IsHTTP10", new nmiCtrlSingleCheck { FldWidth = 1 }); 
            mIsUXInitialized = true;
            return true;
        }

        public override void HandleMessage(ICDEThing sender, object pIncoming)
        {
            if (!(pIncoming is TheProcessMessage pMsg)) return;
            string[] cmd = pMsg.Message.TXT.Split(':');
            switch (cmd[0])   //string 2 cases
            {
                case "CDE_INITIALIZED":
                    MyBaseEngine.SetInitialized(pMsg.Message);
                    break;
                //If the Service receives an "INITIALIZE" it fires up all its code and sends INITIALIZED back
                case "CDE_INITIALIZE":
                    if (MyBaseEngine.GetEngineState().IsService)
                    {
                        if (!MyBaseEngine.GetEngineState().IsEngineReady)
                            MyBaseEngine.SetEngineReadiness(true, null);
                    }
                    MyBaseEngine.ReplyInitialized(pMsg.Message);
                    break;
                case "REFRESH_DASH":
                    InitServices();
                    tDashGuid.Reload(pMsg, false);
                    break;
            }
            ProcessServiceMessage(pMsg);
        }
    }
}
