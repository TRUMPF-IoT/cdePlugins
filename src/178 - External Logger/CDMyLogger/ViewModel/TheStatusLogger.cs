// SPDX-FileCopyrightText: 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

﻿using nsCDEngine.BaseClasses;
using nsCDEngine.Engines;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.ViewModels;
using System;
using nsCDEngine.Engines.NMIService;

namespace CDMyLogger.ViewModel
{
    class TheStatusLogger : TheLoggerBase
    {
        public TheStatusLogger(TheThing tBaseThing, ICDEPlugin pPluginBase):base (tBaseThing, pPluginBase)
        {
            MyBaseThing.DeviceType = eTheLoggerServiceTypes.StatusLogger;
        }

        public override void Connect(TheProcessMessage pMsg)
        {
            IsConnected = true;

            var tList = TheThingRegistry.GetBaseEngines(true);
            foreach (var tEng in tList)
            {
                tEng.GetBaseThing()?.RegisterStatusChanged(sinkSTChange);
                tEng.GetBaseThing()?.GetProperty("LastMessage", true).RegisterEvent(eThingEvents.PropertyChanged, sinkLMChange);
            }
            MyBaseThing.StatusLevel = 1;
            MyBaseThing.LastMessage = $"Connected to Logger at {DateTimeOffset.Now}";
        }
        public override void Disconnect(TheProcessMessage pMsg)
        {
            IsConnected = false;
            MyBaseThing.StatusLevel = 0;
            var tList = TheThingRegistry.GetBaseEngines(true);
            foreach (var tEng in tList)
            {
                tEng.GetBaseThing()?.UnregisterStatusChanged(sinkSTChange);
                tEng.GetBaseThing()?.GetProperty("LastMessage", true).UnregisterEvent(eThingEvents.PropertyChanged, sinkLMChange);
            }
            MyBaseThing.LastMessage = $"Disconnected from Logger at {DateTimeOffset.Now}";
        }

        protected override void DoCreateUX(TheFormInfo pForm)
        {
            base.DoCreateUX(pForm);
            pForm.DeleteByOrder(124);
        }

        void sinkSTChange(cdeP prop)
        {
            var t = TheThingRegistry.GetThingByMID(prop.cdeO);
            TheBaseAssets.MySYSLOG.WriteToLog(23010+TheCommonUtils.CInt(prop), new TSM(MyBaseEngine.GetEngineName(), $"Plugin {t?.EngineName} Status Level Changed to {prop}", eMsgLevel.l4_Message));
        }
        void sinkLMChange(cdeP prop)
        {
            var t = TheThingRegistry.GetThingByMID(prop.cdeO);
            TheBaseAssets.MySYSLOG.WriteToLog(23000, new TSM(MyBaseEngine.GetEngineName(), $"Plugin {t?.EngineName} Message: {prop}", eMsgLevel.l4_Message));
        }
    }
}
