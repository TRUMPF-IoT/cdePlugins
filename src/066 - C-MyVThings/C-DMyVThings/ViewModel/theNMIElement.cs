// SPDX-FileCopyrightText: 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

﻿/*********************************************************************
*
* Project Name: 066-CMyVThings
*
* Description: 
*
* Date of creation: 2015/09/03
*
* 
* Author: Chris Muench
*
* NOTES:
*        UX "pOrder" for this file is  100-299 and  1000-1999
*
**************************************************************************/
using System;
using nsCDEngine.BaseClasses;
using nsCDEngine.Engines;
using nsCDEngine.Engines.NMIService;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.ViewModels;
using System.Collections.Generic;
using nsCDEngine.Communication;

namespace CDMyVThings.ViewModel
{
    partial class TheNMIElement : TheNMILiveTag
    {
        bool ShowTimestamp
        {
            get { return TheThing.GetSafePropertyBool(MyBaseThing, nameof(ShowTimestamp)); }
            set { TheThing.SetSafePropertyBool(MyBaseThing, nameof(ShowTimestamp), value); }
        }

        bool ShowChangeTimestamp
        {
            get { return TheThing.GetSafePropertyBool(MyBaseThing, nameof(ShowChangeTimestamp)); }
            set { TheThing.SetSafePropertyBool(MyBaseThing, nameof(ShowChangeTimestamp), value); }
        }

        public DateTimeOffset ValueTimestamp
        {
            get { return TheThing.GetSafePropertyDate(MyBaseThing, nameof(ValueTimestamp)); }
            set { TheThing.SetSafePropertyDate(MyBaseThing, nameof(ValueTimestamp), value); }
        }

        public DateTimeOffset ValueChangeTimestamp
        {
            get { return TheThing.GetSafePropertyDate(MyBaseThing, nameof(ValueChangeTimestamp)); }
            set { TheThing.SetSafePropertyDate(MyBaseThing, nameof(ValueChangeTimestamp), value); }
        }

        public TheNMIElement(TheThing pThing, IBaseEngine pEngine)
       : base(pThing)
        {
            MyBaseThing = pThing ?? new TheThing();
            MyBaseEngine = pEngine;
            MyBaseThing.DeviceType = eVThings.eNMIElement;
            MyBaseThing.EngineName = pEngine.GetEngineName();
            MyBaseThing.SetIThingObject(this);

            TheBaseEngine.WaitForEnginesStarted(sinkUpdateControls);
        }


        private IBaseEngine MyBaseEngine;

        private void OnValueChanged(cdeP valueProp)
        {
            ValueChangeTimestamp = valueProp.cdeCTIM;
        }

        private void OnValueSet(cdeP valueProp)
        {
            ValueTimestamp = valueProp.cdeCTIM;
        }




    }
}
