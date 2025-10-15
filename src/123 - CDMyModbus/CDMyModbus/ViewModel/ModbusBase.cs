using Modbus;
using NModbusExt.Config;
using nsCDEngine.BaseClasses;
using nsCDEngine.Engines;
using nsCDEngine.Engines.NMIService;
using nsCDEngine.Engines.StorageService;
using nsCDEngine.Engines.ThingService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CU = nsCDEngine.BaseClasses.TheCommonUtils;
using NMI = nsCDEngine.Engines.NMIService.TheNMIEngine;
using TT = nsCDEngine.Engines.ThingService.TheThing;

namespace CDMyModbus.ViewModel
{
    public class ModbusTemplate
    {
        public string Owner { get; set; }
        public Dictionary<string, object> Properties { get; set; }
        public List<TT.TheSensorSubscriptionStatus> Tags { get; set; }
    }
    public class ModbusBase : TheThingBase
    {

        [ConfigProperty]
        public Guid TargetThing
        {
            get { return TT.MemberGetSafePropertyGuid(MyBaseThing); }
            set { TT.MemberSetSafePropertyGuid(MyBaseThing, value); }
        }

        [ConfigProperty]
        public uint Interval
        {
            get { return (uint)TT.GetSafePropertyNumber(MyBaseThing, nameof(Interval)); }
            set { TT.SetSafePropertyNumber(MyBaseThing, nameof(Interval), value); }
        }

        protected TheStorageMirror<FieldMapping> MyModFieldStore;
        protected ICDEPlugin MyBaseEngine;

        protected List<TheFieldInfo> AddThingTarget(TheFormInfo pForm, int StartFld, int ParentFld = 1)
        {
            var lst = new List<TheFieldInfo>();
            lst.Add(NMI.AddSmartControl(MyBaseThing, pForm, eFieldType.ThingPicker, StartFld, 2, 0, "Target Thing", nameof(TargetThing), new nmiCtrlThingPicker() { ParentFld = ParentFld }));
            var exp = NMI.AddSmartControl(MyBaseThing, pForm, eFieldType.TileButton, StartFld + 1, 2, 0, "Export Template", null, new nmiCtrlTileButton() { ParentFld = ParentFld, NoTE = true });
            lst.Add(exp);
            exp.RegisterUXEvent(MyBaseThing, eUXEvents.OnClick, "ExportTemplate", (thing, obj) =>
            {
                var templ = new DeviceDescription { };
                var l = MyBaseThing.GetAllProperties();
                templ.Properties = l.ToDictionary(prop => prop.Name, prop => prop.Value);
                templ.Mapping = new DeviceTypeMapping { FieldList = MyModFieldStore.TheValues.ToList() };
                string testJSON = CU.SerializeObjectToJSONString(templ);

                var temp2 = CU.DeserializeJSONStringToObject<DeviceDescription>(testJSON);
                var tt = TheThingRegistry.GetThingByProperty(MyBaseThing.EngineName, Guid.Empty, "Owner", $"{MyBaseThing.cdeMID}");
                if (tt == null || tt.DeviceType != eModbusType.ModbusTCPDevice) //support for RTU?
                {
                    temp2.Properties["FriendlyName"] = $"Owned by {MyBaseThing.FriendlyName}";
                    temp2.Properties["ID"] = Guid.NewGuid().ToString();
                    temp2.Properties["Owner"] = $"{MyBaseThing.cdeMID}";
                    var pm = new ModbusTCPDevice(tt, MyBaseEngine, temp2);
                    TheThingRegistry.RegisterThing(pm);
                }
            });
            return lst;
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
                    TargetThing = new TheThingReference(TargetThing != null ? TheThingRegistry.GetThingByMID(TargetThing) : MyBaseThing),
                    SampleRate = (int?)this.Interval
                },
                Error = null,
            };
        }
    }
}
