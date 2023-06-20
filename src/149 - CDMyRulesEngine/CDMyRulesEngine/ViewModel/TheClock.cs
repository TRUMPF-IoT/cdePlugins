// SPDX-FileCopyrightText: 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0
using nsCDEngine.Communication;
using nsCDEngine.Engines;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.ViewModels;
using System;
using TT = nsCDEngine.Engines.ThingService.TheThing;

namespace CDMyRulesEngine.ViewModel
{
    [DeviceType(DeviceType = "System Clock", Description = "A System Clock", Capabilities = new eThingCaps[] { /* eThingCaps.ConfigManagement */ })]
    internal class TheClock : TheThingBase
    {
        public TheClock(TT tBaseThing, IBaseEngine pPluginBase)
        {
            MyBaseThing = tBaseThing ?? new TT();
            MyBaseThing.EngineName = pPluginBase.GetEngineName();
            MyBaseThing.SetIThingObject(this);
            MyBaseThing.DeviceType = "System Clock";
            MyBaseThing.ID = "SYSTEM_CLOCK";
            MyBaseThing.FriendlyName = "System Clock";
        }

        public override bool Init()
        {
            if (!mIsInitCalled)
            {
                mIsInitCalled = true;
                MyBaseThing.StatusLevel = 1;
                TheQueuedSenderRegistry.RegisterHealthTimer(sinkTimer);
                sinkTimer(0);
                mIsInitialized = true;
            }
            return true;
        }

        void sinkTimer(long tick)
        {
            if ((tick % 50) == 0)
            {
                DateTimeOffset now= DateTimeOffset.Now;
                SetProperty("TimeOfDay", now.TimeOfDay);
                SetProperty("Hour", now.Hour);
                SetProperty("Minute", now.Minute);
                SetProperty("Day", now.Day);
                SetProperty("DayOfWeek", now.DayOfWeek);
                SetProperty("DayOfYear", now.DayOfYear);
                SetProperty("HourMin", $"{now.Hour}.{now.Minute}");
            }
        }

        public override bool CreateUX()
        {
            mIsUXInitCalled = true;
            mIsUXInitialized = true;
            return true;
        }
    }
}
