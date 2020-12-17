// SPDX-FileCopyrightText: 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0
using nsCDEngine.BaseClasses;
using nsCDEngine.Engines;
using nsCDEngine.Engines.NMIService;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.ViewModels;
using Opc.Ua;
using System;
using System.Text;

namespace CDMyOPCUAClient.ViewModel
{
    [DeviceType(
        DeviceType = eOPCDeviceTypes.OPCLiveTag,
        Capabilities = new eThingCaps[] { eThingCaps.SensorContainer, eThingCaps.ConfigManagement },
        Description = "OPC UA Live Tag")]
    [ConfigProperty(NameOverride = nameof(TheThing.Address), cdeT = ePropertyTypes.TString, Required = true)]
    [ConfigProperty(NameOverride = nameof(TheThing.FriendlyName), cdeT = ePropertyTypes.TString)]
    [ConfigProperty(NameOverride = nameof(TheOPCUATagThing.ControlType), cdeT = ePropertyTypes.TString)]
    [ConfigProperty(NameOverride = nameof(TheOPCUATagThing.DontMonitor), cdeT = ePropertyTypes.TBoolean)]
    [ConfigProperty(NameOverride = nameof(TheOPCUATagThing.ServerID), cdeT = ePropertyTypes.TString)] // TODO: need to generalize/specialize this (Task 1334)
    [ConfigProperty(NameOverride = nameof(TheOPCUATagThing.ServerName), cdeT = ePropertyTypes.TString)]
    // TODO Do any of the NMI properties need to be declared as config (for proper import/export)?
    public class TheOPCUATagThing : TheNMILiveTag
    {
        [ConfigProperty()]
        internal int SampleRate
        {
            get { return TheCommonUtils.CInt(TheThing.GetSafePropertyString(MyBaseThing, "SampleRate")); }
            set
            {
                MyBaseThing.SetProperty("SampleRate", value.ToString(), ePropertyTypes.TNumber);
            }
        }

        private TheOPCTag m_Tag = null;

        public TheOPCUATagThing(TheThing pThing, TheOPCTag pTag)
            : base(pThing)
        {
            MyBaseThing.DeviceType = eOPCDeviceTypes.OPCLiveTag;
            Reset();
            MyBaseThing.RegisterEvent("OnInitialized", sinkInit);
            MyBaseThing.SetIThingObject(this);
        }

        public void Setup(TheOPCTag pTag)
        {
            m_Tag = pTag;
            IsActive = false;
            sinkInit(this, null);
        }

        public void Reset()
        {
            IsActive = false;
            if (m_Tag != null)
                m_Tag.Reset();
            MyBaseThing.LastMessage = "Tag reset";
            MyBaseThing.StatusLevel = 0;
        }

        public override bool DoInit()
        {
            if (MyBaseThing.StatusLevel != 1)
            {
                MyBaseThing.LastMessage = "Tag Ready";
                MyBaseThing.StatusLevel = 0;
            }
            cdeP Mon = MyBaseThing.GetProperty("DontMonitor",true );
            Mon.RegisterEvent(eThingEvents.PropertyChanged, sinkPChanged);
            MyBaseThing.RegisterEvent(eThingEvents.PropertyChanged, sinkUXUpdatedThing);
            MyBaseThing.RegisterEvent(eThingEvents.ValueChanged, sinkRulesUpdatedThing);
            return true;
        }

        private void sinkInit(ICDEThing sender, object pPara)
        {
            if (SampleRate > 0 && SampleRate < 50)
                SampleRate = 50;
            if (string.IsNullOrEmpty(MyBaseThing.ID))
                MyBaseThing.ID = Guid.NewGuid().ToString();

            if (m_Tag != null)
            {
                if (string.IsNullOrEmpty(MyBaseThing.Address))
                {
                    MyBaseThing.Address = string.Format("{0}:;:{1}", m_Tag.Parent, m_Tag.NodeIdName);
                    FormTitle = "My OPC Tags";
                    TileWidth = 3;
                    TileHeight = 1;
                    Flags = 0;
                    FldOrder = 0;
                    SampleRate = m_Tag.SampleRate;
                }
                if (string.IsNullOrEmpty(MyBaseThing.FriendlyName))
                    MyBaseThing.FriendlyName = m_Tag.DisplayName;

                if (ControlType == "")
                {
                    ControlType = ((int)eFieldType.SingleEnded).ToString();
                }
                if (m_Tag.PropAttr != null)
                {
                    foreach (TheOPCProperties tpro in m_Tag.PropAttr)
                    {
                        if (tpro.BrowseName == "Value") continue;
                        cdeP pProp = MyBaseThing.GetProperty(tpro.BrowseName, false);
                        if (pProp == null)
                            MyBaseThing.SetProperty(tpro.BrowseName, tpro.Value.WrappedValue.ToString(), tpro.DataType);
                        else
                            pProp.Value = tpro.Value.WrappedValue.ToString();
                    }
                }
                m_Tag.MyBaseThing = this.MyBaseThing;
                if (m_Tag.MyOPCServer != null)
                {
                    ServerID = m_Tag.MyOPCServer.GetBaseThing().ID;
                    ServerName = m_Tag.MyOPCServer.GetBaseThing().FriendlyName;
                    m_Tag.MyOPCServer.GetBaseThing().RegisterEvent("DisconnectComplete", sinkDisconnected);
                    MyBaseThing.EngineName = m_Tag.MyOPCServer.GetBaseThing().EngineName;
                    var subscription = m_Tag.MyOPCServer.GetOrCreateSubscription(m_Tag.SampleRate);
                    if (subscription != null)
                    {
                        string error;
                        TheBaseAssets.MySYSLOG.WriteToLog(78102, TSM.L(eDEBUG_LEVELS.FULLVERBOSE) ? null : new TSM(MyBaseThing.EngineName, $"Tag init for tag {m_Tag}", eMsgLevel.l4_Message, ""));
                        m_Tag.MonitorTag(subscription, out error);
                        MyBaseThing.LastMessage = "Tag alive";
                        MyBaseThing.StatusLevel = 1;
                    }
                }
            }
        }

        protected void sinkDisconnected(ICDEThing pThing, object p)
        {
            Reset();
        }

        public override bool DoCreateUX()
        {
            TheThing.SetSafePropertyString(MyBaseThing, "StateSensorIcon", "/Images/iconToplogo.png");
            var res=base.DoCreateUX();

            var ValueFld=TheNMIEngine.GetFieldByFldOrder(base.MyStatusForm, 15);
            ValueFld.Type = eFieldType.SingleEnded;

            TheNMIEngine.DeleteFieldsByRange(base.MyStatusForm, 14, 14);

            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.DateTime, 14, 0, 0, "###Last Update###", "SourceTimeStamp", new nmiCtrlDateTime() { ParentFld = 10 });

            SummaryForm.PropertyBag = new nmiDashboardTile() { Category = "..", Caption="hhh" };
            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.SingleCheck, 523, 2, 128, "Array 2 Properties", "StoreArrayInProperties", new nmiCtrlDateTime() { ParentFld = 500 });

            return res;
        }

        void sinkUXUpdatedThing(ICDEThing pThing, object pData)
        {
            cdeP tProp = pData as cdeP;
            if (tProp != null && m_Tag!=null)
            {
                if (!DontMonitor && tProp != null && tProp.HasChanged && tProp.Name == "Value")
                {
                    var value = tProp.GetValue();
                    if (value!=null && tProp.cdeT==4 && TheThing.GetSafePropertyBool(MyBaseThing, "StoreArrayInProperties"))
                    {
                        byte[] bytes = (byte[])value;
                        for (int ii = 0; ii < bytes.Length; ii++)
                            TheThing.SetSafePropertyNumber(MyBaseThing, $"Value{ii}", bytes[ii]);
                    }
                    if (PropertyOPCToString((ePropertyTypes)tProp.cdeT,value) != m_Tag.MyLastValue.ToString()) //CODE-REVIEW: Racing can happen here! Both functions can take too long and change during the ToString operation (so far only seen in debugger!)
                        m_Tag.WriteTag(new DataValue(new Variant(tProp.Value)));
                }
            }
        }


        void sinkPChanged(cdeP tProp)
        {
            if (m_Tag!=null && tProp != null && tProp.HasChanged && tProp.Name == "DontMonitor")
            {
                if (!TheCommonUtils.CBool(tProp.ToString()))
                {
                    m_Tag.SampleRate = TheCommonUtils.CInt(TheThing.GetSafePropertyNumber(MyBaseThing, "SampleRate"));
                    string error;
                    TheBaseAssets.MySYSLOG.WriteToLog(78102, TSM.L(eDEBUG_LEVELS.FULLVERBOSE) ? null : new TSM(MyBaseThing.EngineName, $"Monitoring tag due to DontMonitor property change {m_Tag}", eMsgLevel.l4_Message, ""));

                    var subscription = m_Tag.MyOPCServer.GetOrCreateSubscription(m_Tag.SampleRate);
                    if (subscription != null)
                    {
                        m_Tag.MonitorTag(subscription, out error);
                        // TODO Handle error
                    }
                }
                else
                    m_Tag.UnmonitorTag();
            }
        }

        //void sinkRulesUpdatedThing(ICDEThing pThing, object pData)
        //{
        //    if (m_Tag == null) return;
        //    cdeP tPro = pData as cdeP;
        //    if (tPro != null && tPro.HasChanged)
        //    {
        //        string tOPCPropVal = PropertyOPCToString(tPro);
        //        TheThing.SetSafePropertyString(MyBaseThing, "QValue", tOPCPropVal);
        //        if ((tOPCPropVal != m_Tag.MyLastValue.ToString()))  //CODE-REVIEW: Racing can happen here! Both functions can take too long and change during the ToString operation (so far only seen in debugger!)
        //        {
        //            m_Tag.WriteTag(new DataValue(new Variant(tPro.Value)));
        //            //System.Diagnostics.Debug.WriteLine(string.Format("CDE: WriteTag:{0} to ({1})", MyBaseThing.FriendlyName,tPro.ToString()));
        //        }
        //    }
        //}
        void sinkRulesUpdatedThing(ICDEThing pThing, object pData)
        {
            if (m_Tag == null) return;
            cdeP tPro = pData as cdeP;
            if (tPro != null && tPro.HasChanged)
            {
                var value = tPro.GetValue();
                string tOPCPropVal = PropertyOPCToString((ePropertyTypes)tPro.cdeT, value);
                TheThing.SetSafePropertyString(MyBaseThing, "QValue", tOPCPropVal);
                if ((tOPCPropVal != m_Tag.MyLastValue.ToString()))  //CODE-REVIEW: Racing can happen here! Both functions can take too long and change during the ToString operation (so far only seen in debugger!)
                {
                    m_Tag.WriteTag(new DataValue(new Variant(value)));
                    //System.Diagnostics.Debug.WriteLine(string.Format("CDE: WriteTag:{0} to ({1})", MyBaseThing.FriendlyName,tPro.ToString()));
                }
            }
        }

        string PropertyOPCToString(ePropertyTypes propcdeT, object prop)
        {
            if (propcdeT == ePropertyTypes.TBinary)
            {
                if (prop == null)
                    return "(null)";
                StringBuilder buffer = new StringBuilder();
                byte[] bytes = (byte[])prop;
                for (int ii = 0; ii < bytes.Length; ii++)
                {
                    buffer.AppendFormat(null, "{0:X2}", bytes[ii]);
                }
                return buffer.ToString();
            }
            else
            {
                if (prop == null)
                    return null;
            }
            return TheCommonUtils.CStr(prop);
        }
    }
}
