#define USEENGINEATTRIBUTEINFO

// Copyright 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
// SPDX-FileCopyrightText: 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using CDMyVThings.ViewModel;
using nsCDEngine.BaseClasses;
using nsCDEngine.Communication;
using nsCDEngine.Engines;
using nsCDEngine.Engines.NMIService;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.ViewModels;
using System;
using System.Collections.Generic;
// ReSharper disable RedundantEmptyObjectOrCollectionInitializer

namespace CDMyVThings
{
    public class eVThings : TheDeviceTypeEnum
    {
        public const string eVTimer = "Timer";
        public const string eVCountdown = "Countdown";
        public const string eMemoryTag = "Memory Tag";
        public const string eNMIElement = "NMI Element";
        public const string eDataGenerator = "Data Generator";
        public const string eDataVerifier = "Data Verifier";
        public const string eVStateSensor = "Virtual State Sensor";
        public const string eSineWave = "Sine Wave";
        public const string eVirtualSensor = "Virtual Sensor";
        public const string eDataPlayback = "Data Playback";
    }

    [EngineAssetInfo(
        EngineName = nameof(CDMyVThings) + "." + nameof(TheVThings),
        EngineType = typeof(TheVThings),
        FriendlyName = "Virtual Things",
        LongDescription = longDescription,
        IconUrl = iconUrl,
        Developer = "C-Labs",
        DeveloperUrl = "http://www.c-labs.com",
        Categories = new string[] { "Digital-Thing" },
        IsService = true,
        EngineID = "{B9DBC881-0EA8-4FA6-98B8-2C646B6B848F}",
        AcceptsFilePush = true,
        Capabilities = new [] { eThingCaps.ConfigManagement, eThingCaps.SensorContainer },
        CDEMinVersion = 6.104
        )]
    [DeviceType(DeviceType = eVThings.eMemoryTag, Description = "Memory tag stores custom data", Capabilities = new eThingCaps[] { })]
    
    public partial class TheVThings : ThePluginBase
    {

        const string longDescription = "This plug-in hosts many different functions and other virtual devices";
        const string iconUrl = "<i class='fa faIcon cl-3x'>&#xf61f;</i>";
        public override void InitEngineAssets(IBaseEngine pBase)
        {
            MyBaseEngine = pBase;
            MyBaseEngine.RegisterCSS("/P066/css/SensorTileStyle.min.css", null, sinkRes);
        }

        void sinkRes(TheRequestData pReq)
        {
            MyBaseEngine.GetPluginResource(pReq);
        }

        public override bool Init()
        {
            if (mIsInitCalled) return false;
            mIsInitCalled = true;

            MyBaseThing.StatusLevel = 4;
            StartEngineServices();
            if (TheCommonUtils.CBool(TheBaseAssets.MySettings.GetSetting("VThing-SimTest")) == true)
            {
                var tDev = TheThingRegistry.GetThingByID(MyBaseEngine.GetEngineName(), "TESTSIM");
                if (tDev == null)
                {
                    TheVCountdown tCD = new TheVCountdown(tDev, MyBaseEngine);
                    tCD.SetProperty("AutoStart", true);
                    tCD.SetProperty("Value", 100);
                    tCD.GetBaseThing().FriendlyName = "Test Automation Sim";
                    tCD.GetBaseThing().ID = "TESTSIM";
                    TheThingRegistry.RegisterThing(tCD);
                }
                else
                    tDev.SetProperty("Value", 100);
            }
            MyBaseThing.RegisterEvent(eEngineEvents.IncomingMessage, HandleMessage);
            MyBaseEngine.RegisterEvent(eEngineEvents.ThingRegistered, OnThingRegistered);
            mIsInitialized = true;
            MyBaseEngine.ProcessInitialized();
            return true;
        }

        private void OnThingRegistered(ICDEThing arg1, object arg2)
        {
            StartEngineServices();
        }

        public void HandleMessage(TheProcessMessage pMsg)
        {
            HandleMessage(this, pMsg);
        }
        public override void HandleMessage(ICDEThing pThing, object oMsg)
        {
            if (!(oMsg is TheProcessMessage pMsg)) return;

            string[] cmd = pMsg.Message.TXT.Split(':');

            switch (cmd[0])
            {
                case "CDE_INITIALIZED":
                    MyBaseEngine.SetInitialized(pMsg.Message);
                    break;
                case "CDE_INITIALIZE":
                    if (MyBaseEngine.GetEngineState().IsService)
                    {
                        if (!MyBaseEngine.GetEngineState().IsEngineReady)
                            MyBaseEngine.SetEngineReadiness(true, null);
                    }
                    MyBaseEngine.ReplyInitialized(pMsg.Message);
                    break;
                case "REFRESH_DASH":
                    StartEngineServices();
                    tVThingsForm.Reload(pMsg, false);
                    MyDashboard.Reload(pMsg, false);
                    break;
            }
        }

        private void StartEngineServices()
        {
            List<TheThing> tDevList = TheThingRegistry.GetThingsOfEngine(MyBaseEngine.GetEngineName());
            if (tDevList.Count > 0)
            {
                foreach (TheThing tDev in tDevList)
                {
                    if (tDev.GetObject() == null)
                    {
                        TheNMILiveTag tW = null;
                        switch (TheThing.GetSafePropertyString(tDev, "DeviceType"))
                        {
                            case eVThings.eVTimer:
                                tW = new TheVTimer(tDev, MyBaseEngine) { IsActive = true };
                                TheThingRegistry.RegisterThing(tW);
                                break;
                            case eVThings.eMemoryTag:
                                TheMemoryTag tM = new TheMemoryTag(tDev, MyBaseEngine);
                                TheThingRegistry.RegisterThing(tM);
                                break;
                            case eVThings.eVCountdown:
                                TheVCountdown tCD = new TheVCountdown(tDev, MyBaseEngine);
                                TheThingRegistry.RegisterThing(tCD);
                                break;
                            case eVThings.eNMIElement:
                                TheNMIElement tNE = new TheNMIElement(tDev, MyBaseEngine);
                                TheThingRegistry.RegisterThing(tNE);
                                break;
                            case eVThings.eDataGenerator:
                                TheDataGenerator tGen = new TheDataGenerator(tDev, MyBaseEngine);
                                TheThingRegistry.RegisterThing(tGen);
                                break;
                            case eVThings.eDataVerifier:
                                TheDataVerifier tVer = new TheDataVerifier(tDev, MyBaseEngine);
                                TheThingRegistry.RegisterThing(tVer);
                                break;
                            case eVThings.eDataPlayback:
                                TheDataPlayback tPlay= new TheDataPlayback(tDev, MyBaseEngine);
                                TheThingRegistry.RegisterThing(tPlay);
                                break;
                            case eVThings.eVStateSensor:
                                var tVSens = new TheVStateSensor(tDev, MyBaseEngine);
                                TheThingRegistry.RegisterThing(tVSens);
                                break;
                            case eVThings.eSineWave:
                                var tSine = new TheSineWave(tDev, MyBaseEngine);
                                TheThingRegistry.RegisterThing(tSine);
                                break;
                            case eVThings.eVirtualSensor:
                                var tVS = new TheVirtualSensor(tDev, this);
                                TheThingRegistry.RegisterThing(tVS);
                                break;
                            default:
                                tW = new TheNMILiveTag(tDev) { IsActive = true };
                                TheThingRegistry.RegisterThing(tW);
                                break;
                        }
                    }
                }
            }
            MyBaseEngine.SetStatusLevel(-1);
        }
    }
}

