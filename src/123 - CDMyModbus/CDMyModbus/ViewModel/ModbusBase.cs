using nsCDEngine.BaseClasses;
using nsCDEngine.Engines;
using nsCDEngine.Engines.NMIService;
using nsCDEngine.Engines.ThingService;
using System;
using System.Collections.Generic;
using System.Text;
using CU = nsCDEngine.BaseClasses.TheCommonUtils;
using NMI = nsCDEngine.Engines.NMIService.TheNMIEngine;
using TT = nsCDEngine.Engines.ThingService.TheThing;

namespace CDMyModbus.ViewModel
{
    public class ModbusBase : TheThingBase
    {

        [ConfigProperty]
        public Guid TargetThing
        {
            get { return TT.MemberGetSafePropertyGuid(MyBaseThing); }
            set { TT.MemberSetSafePropertyGuid(MyBaseThing, value); }
        }

        protected TheFieldInfo AddThingTarget(TheFormInfo pForm, int StartFld, int ParentFld=1)
        {
            return NMI.AddSmartControl(MyBaseThing, pForm, eFieldType.ThingPicker, StartFld, 2, 0, "Target Thing", nameof(TargetThing), new nmiCtrlThingPicker() { ParentFld=ParentFld   });
        }

        protected void PushProperties(Dictionary<string, object> dict, DateTimeOffset timestamp)
        {

            MyBaseThing.SetProperties(dict, timestamp);
            if (TargetThing != Guid.Empty)
            {
                var t = TheThingRegistry.GetThingByMID(TargetThing);
                if (t != null)
                    t.SetProperties(dict, timestamp);
                else
                    SetMessage("Target Thing not found", DateTimeOffset.Now, 0, eMsgLevel.l2_Warning);
            }
        }
    }
}
