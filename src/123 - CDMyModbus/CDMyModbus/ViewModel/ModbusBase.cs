using NModbusExt.Config;
using nsCDEngine.BaseClasses;
using nsCDEngine.Engines;
using nsCDEngine.Engines.NMIService;
using nsCDEngine.Engines.StorageService;
using nsCDEngine.Engines.ThingService;
using System;
using System.Collections.Generic;
using CU = nsCDEngine.BaseClasses.TheCommonUtils;
using NMI = nsCDEngine.Engines.NMIService.TheNMIEngine;
using TCC = nsCDEngine.Communication.TheCommCore;
using TT = nsCDEngine.Engines.ThingService.TheThing;

namespace CDMyModbus.ViewModel
{
    public class ModbusBase : TheThingBase
    {

        [ConfigProperty]
        public uint Interval
        {
            get { return (uint)TT.GetSafePropertyNumber(MyBaseThing, nameof(Interval)); }
            set { TT.SetSafePropertyNumber(MyBaseThing, nameof(Interval), value); }
        }

        public string TemplateName
        {
            get { return TT.MemberGetSafePropertyString(MyBaseThing); }
            set { TT.MemberSetSafePropertyString(MyBaseThing, value); }
        }


        protected TheStorageMirror<FieldMapping> MyModFieldStore;
        protected ICDEPlugin MyBaseEngine;

        protected List<TheFieldInfo> AddThingTarget(TheFormInfo pForm, int StartFld, int ParentFld = 1)
        {
            var lst = new List<TheFieldInfo>();
            lst.Add(NMI.AddSmartControl(MyBaseThing, pForm, eFieldType.ThingPicker, StartFld, 2, 0, "Parent Thing", nameof(MyBaseThing.Parent), new nmiCtrlThingPicker() { ParentFld = ParentFld }));
            NMI.AddSmartControl(MyBaseThing, pForm, eFieldType.SingleEnded, StartFld + 1, 2, 0, "Template Name", nameof(TemplateName), new nmiCtrlSingleEnded() { ParentFld = ParentFld, NoTE = true, TileWidth=5 });
            var exp = NMI.AddSmartControl(MyBaseThing, pForm, eFieldType.TileButton, StartFld + 2, 2, 0, "Export", null, new nmiCtrlTileButton() { ParentFld = ParentFld, NoTE = true, TileWidth=1 });
            lst.Add(exp);
            exp.RegisterUXEvent(MyBaseThing, eUXEvents.OnClick, "ExportTemplate", (thing, obj) =>
            {
                string testJSON = TheDeviceDescription.CreateDeviceTemplate(MyBaseThing, TemplateName, new Dictionary<string, TheStorageMirror<FieldMapping>> { { "FLDMAP_ID", MyModFieldStore } });
                TCC.PublishCentral(new TSM(eEngineName.NMIService, "NMI_TOAST", $"Template {TemplateName} exported"));
                //Move to Client Thing
                //var temp2 = CU.DeserializeJSONStringToObject<TheDeviceDescription>(testJSON);
                //temp2.Properties["FriendlyName"] = $"Owned by {MyBaseThing.FriendlyName}";
                //temp2.Properties["ID"] = Guid.NewGuid().ToString();
                //temp2.Properties["FLDMAP_ID"] = Guid.NewGuid().ToString();
                //temp2.Properties["Parent"] = $"{MyBaseThing.cdeMID}";
                //TheDeviceDescription.SendTemplateToEngine(this, "Modbus.ModbusService", temp2);
            });
            return lst;
        }

        // Fixes for CS0314, CS0310, IDE0060

        // Update the generic constraint for T in CreateDeviceTemplate to match TheStorageMirror<T> requirements.
        // Remove unused parameter 'pSubStore' (IDE0060) if not used in the method body.





        protected List<TT.TheSensorSubscriptionStatus> CreateModbusTags(TT.MsgSubscribeSensors subscribeRequest)
        {
            var subscriptionStatus = new List<TT.TheSensorSubscriptionStatus>();
            foreach (TT.TheSensorSubscription sub in subscribeRequest.SubscriptionRequests)
            {
                FieldMapping fld = new FieldMapping()
                {
                    PropertyName = sub.TargetProperty,
                    cdeMID = CU.CGuid(sub.SensorId)
                };
                if (fld.cdeMID == Guid.Empty)
                    fld.cdeMID = Guid.NewGuid();
                if (sub.ExtensionData != null)
                {
                    object sourceType;
                    if (sub.ExtensionData.TryGetValue(nameof(TT.TheSensorSourceInfo.SourceType), out sourceType))
                        fld.SourceType = CU.CStr(sourceType);
                    object offset;
                    if (sub.ExtensionData.TryGetValue("SourceOffset", out offset))
                        fld.SourceOffset = CU.CInt(offset);
                    object size;
                    if (sub.ExtensionData.TryGetValue("SourceSize", out size))
                        fld.SourceSize = CU.CInt(size);
                    object allowWrite;
                    if (sub.ExtensionData.TryGetValue("AllowWrite", out allowWrite))
                        fld.AllowWrite = CU.CBool(allowWrite);
                    object scaleFactor;
                    if (sub.ExtensionData.TryGetValue("ScaleFactor", out scaleFactor))
                        fld.ScaleFactor = CU.CInt(scaleFactor);
                    object connType;
                    if (sub.ExtensionData.TryGetValue("ConnectionType", out connType))
                        fld.ConnectionType = CU.CInt(connType);
                    MyModFieldStore.AddAnItem(fld);
                    subscriptionStatus.Add(CreateSubscriptionStatusFromFieldMapping(fld));
                }
                else
                {
                    subscriptionStatus.Add(new TheThing.TheSensorSubscriptionStatus
                    {
                        Error = "Missing source info",
                        Subscription = sub,
                    });
                }
            }

            return subscriptionStatus;
        }

        protected TT.TheSensorSubscriptionStatus CreateSubscriptionStatusFromFieldMapping(FieldMapping fld)
        {
            return new TT.TheSensorSubscriptionStatus
            {
                Subscription = new TT.TheSensorSubscription
                {
                    TargetProperty = fld.PropertyName,
                    SensorId = CU.CStr(fld.cdeMID),
                    SubscriptionId = fld.cdeMID,
                    ExtensionData = new Dictionary<string, object>
                    {
                        { nameof(FieldMapping.SourceType), fld.SourceType },
                        { nameof(FieldMapping.SourceOffset), fld.SourceOffset },
                        { nameof(FieldMapping.ScaleFactor), fld.ScaleFactor},
                        { nameof(FieldMapping.SourceSize), fld.SourceSize },
                        { nameof(FieldMapping.ConnectionType), fld.ConnectionType},
                        { nameof(FieldMapping.AllowWrite), fld.AllowWrite }
                    },
                    TargetThing = new TheThingReference(MyBaseThing.Parent != null ? TheThingRegistry.GetThingByMID(CU.CGuid(MyBaseThing.Parent)) : MyBaseThing),
                    SampleRate = (int?)this.Interval
                },
                Error = null,
            };
        }
    }
}
