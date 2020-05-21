// SPDX-FileCopyrightText: 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

﻿using System;
using nsCDEngine.ViewModels;
using nsCDEngine.Communication;
using nsCDEngine.BaseClasses;
using nsCDEngine.Engines;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.Engines.NMIService;

namespace CDMyLogger.ViewModel
{


    class TheGELF : TheLoggerBase
    {
        TheREST MyRest = new TheREST();

        public TheGELF(TheThing tBaseThing, ICDEPlugin pPluginBase):base (tBaseThing, pPluginBase)
        {
            MyBaseThing.DeviceType = eTheLoggerServiceTypes.GELF;
        }

        protected override void DoCreateUX(TheFormInfo pForm)
        {
            base.DoCreateUX(pForm);
            var tcheck = TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.SingleCheck, 140, 2, 0x80, "Write to Console", "ToConsole", new nmiCtrlSingleCheck { ParentFld=120 });
        }

        public override void OnNewEvent(TheProcessMessage timer, object unused)
        {
            TheEventLogEntry tEntry = timer?.Cookie as TheEventLogEntry;
            if (tEntry == null)
                return;
            if (!string.IsNullOrEmpty(MyBaseThing.Address) && tEntry?.Message?.TXT?.Contains(MyBaseThing.Address) == true) //Avoid recursive
                return;
            var t = new TheGELFLogEntry()
            {
                version = "1.1",
                host = TheBaseAssets.MyServiceHostInfo.GetPrimaryStationURL(false),
                level = (int)tEntry.Message?.LVL,
#if CDE_NET45 || CDE_NET4 || CDE_NET35
                timestamp = TheCommonUtils.CDbl($"{(tEntry.Message.TIM.Subtract(new DateTime(1970, 1, 1))).TotalSeconds}.{tEntry.Message.TIM.Millisecond}"),
#else
                timestamp = TheCommonUtils.CDbl($"{tEntry.Message.TIM.ToUnixTimeSeconds()}.{tEntry.Message.TIM.Millisecond}"),
#endif
                full_message = tEntry.Message?.PLS,
                short_message = tEntry.Message?.TXT,
                _log_id = tEntry.EventID,
                _serial_no = tEntry.Serial,
                _engine = tEntry.Message.ENG,
                _device_id = tEntry.Message.GetOriginator().ToString()
            };

            try
            {
                if (!string.IsNullOrEmpty(MyBaseThing.Address))
                {
                    var pData = new TheRequestData()
                    {
                        RequestUri = new Uri($"{MyBaseThing.Address}/gelf"),
                        ResponseMimeType = "application/json",
                        PostData = TheCommonUtils.CUTF8String2Array(TheCommonUtils.SerializeObjectToJSONString<TheGELFLogEntry>(t)),
                        DontCompress = true
                    };

                    MyRest.PostRESTAsync(pData, (okData) =>
                    {
                        MyBaseThing.StatusLevel = 1;
                    }, (errData) =>
                    {
                        MyBaseThing.LastMessage = $"Post to Log Error: {errData.ErrorDescription}";
                        MyBaseThing.StatusLevel = 2;
                    });
                }
            }
            catch (Exception e)
            {
                MyBaseThing.LastMessage = $"Cannot Post to Log Error: {e.Message}";
                MyBaseThing.StatusLevel = 2;
            }

            if (TheThing.GetSafePropertyBool(MyBaseThing, "ToConsole"))
                Console.WriteLine(TheCommonUtils.SerializeObjectToJSONString<TheGELFLogEntry>(t));
        }


    }
}
