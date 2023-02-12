// SPDX-FileCopyrightText: 2009-2023 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0
using nsCDEngine.BaseClasses;
using nsCDEngine.Engines;
using nsCDEngine.Engines.NMIService;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.ViewModels;
using System;
using System.Collections.Generic;
using System.Text;

namespace CDMyLogger.ViewModel
{
    [DeviceType(DeviceType = eTheLoggerServiceTypes.ConsoleLogger,Capabilities = new eThingCaps[] { eThingCaps.ConfigManagement },
                Description = "Console Event Logger")]
    internal class TheConsoleLogger : TheLoggerBase
    {
        public TheConsoleLogger(TheThing tBaseThing, ICDEPlugin pPluginBase) : base(tBaseThing, pPluginBase)
        {
            MyBaseThing.DeviceType = eTheLoggerServiceTypes.ConsoleLogger;
        }

        protected override void DoCreateUX(TheFormInfo pForm)
        {
            pForm.DeleteByOrder(124);
        }

        public override bool LogEvent(TheEventLogData pItem)
        {
            if (!IsConnected)
                return false;
            Console.WriteLine($"{pItem.EventCategory} : {TheCommonUtils.GetDateTimeString(pItem.EventTime, -1)} : {pItem.EventName} : {pItem.EventString}");
            return true;
        }
    }
}
