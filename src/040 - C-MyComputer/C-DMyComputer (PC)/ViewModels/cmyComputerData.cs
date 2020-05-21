// SPDX-FileCopyrightText: 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

﻿using System;
using nsCDEngine.ViewModels;
using nsCDEngine.BaseClasses;
using System.Collections.ObjectModel;
using System.ComponentModel;
using nsCDEngine.Engines.ThingService;

namespace CDMyComputer.ViewModels
{

    public class TheCPUInfo : TheDataBase, ICDEThing
    {
        #region ICDEThing Methods
        public void SetBaseThing(TheThing pThing)
        {
            MyBaseThing = pThing;
        }
        public TheThing GetBaseThing()
        {
            return MyBaseThing;
        }
        public cdeP GetProperty(string pName, bool DoCreate)
        {
            if (MyBaseThing != null)
                return MyBaseThing.GetProperty(pName, DoCreate);
            return null;
        }
        public cdeP SetProperty(string pName, object pValue)
        {
            if (MyBaseThing != null)
                return MyBaseThing.SetProperty(pName, pValue);
            return null;
        }
        public void RegisterEvent(string pName, Action<ICDEThing, object> pCallBack)
        {
            if (MyBaseThing != null)
                MyBaseThing.RegisterEvent(pName, pCallBack);
        }
        public void UnregisterEvent(string pName, Action<ICDEThing, object> pCallBack)
        {
            if (MyBaseThing != null)
                MyBaseThing.UnregisterEvent(pName, pCallBack);
        }
        public void FireEvent(string pEventName, ICDEThing sender, object pPara, bool FireAsync)
        {
            if (MyBaseThing != null)
                MyBaseThing.FireEvent(pEventName, sender, pPara, FireAsync);
        }
        public bool HasRegisteredEvents(string pEventName)
        {
            if (MyBaseThing != null)
                return MyBaseThing.HasRegisteredEvents(pEventName);
            return false;
        }
        public bool IsUXInit() { return mIsUXInit; }
        public bool IsInit() { return mIsInit; }

        protected TheThing MyBaseThing = null;
        protected bool mIsUXInit = false;
        protected bool mIsInit = false;

        public virtual bool Init()
        {
            mIsInit = true;
            return true;
        }

        public bool Delete()
        {
            mIsInit = false;
            // TODO Properly implement delete
            return true;
        }

        public virtual bool CreateUX()
        {
            mIsUXInit = true;
            return true;
        }
        public void HandleMessage(ICDEThing sender, object message)
        { }

        #endregion

        public TheCPUInfo(string pEngineName,string pURL)
        {
            MyBaseThing = TheThingRegistry.GetThingByProperty(pEngineName, Guid.Empty, "ID", "CPU@" + TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID.ToString());
            if (MyBaseThing == null)
            {
                MyBaseThing = new TheThing();
                MyBaseThing.DeviceType = "CPUInfo";
                MyBaseThing.ID = "CPU@" + TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID.ToString();
                MyBaseThing.EngineName = pEngineName;
            }
            cdeMID = MyBaseThing.cdeMID;
            MyBaseThing.SetIThingObject(this);
            TheThingRegistry.RegisterThing(this);
        }

        public string HostAddress
        {
            get { return TheThing.GetSafePropertyString(MyBaseThing, "HostUrl"); }
            set { TheThing.SetSafePropertyString(MyBaseThing, "HostUrl", value); }
        }
        public string DeviceType
        {
            get { return MyBaseThing.DeviceType; }
            set { MyBaseThing.DeviceType = value; }
        }
        public string FriendlyName
        {
            get { return MyBaseThing.FriendlyName; }
            set { MyBaseThing.FriendlyName = value; }
        }
        public string ID
        {
            get { return MyBaseThing.ID; }
            set { MyBaseThing.ID = value; }
        }
        public DateTimeOffset LastUpdate
        {
            get { return MyBaseThing.LastUpdate; }
            set { MyBaseThing.LastUpdate = value; }
        }
        public string Description
        {
            get { return TheThing.GetSafePropertyString(MyBaseThing, "Description"); }
            set { TheThing.SetSafePropertyString(MyBaseThing, "Description", value); }
        }
        public string Version
        {
            get { return TheThing.GetSafePropertyString(MyBaseThing, "Version"); }
            set { TheThing.SetSafePropertyString(MyBaseThing, "Version", value); }
        }
        public int Revision
        {
            get { return TheCommonUtils.CInt(TheThing.GetSafePropertyString(MyBaseThing, "Revision")); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, "Revision", value); }
        }
        public string Manufacturer
        {
            get { return TheThing.GetSafePropertyString(MyBaseThing, "Manufacturer"); }
            set { TheThing.SetSafePropertyString(MyBaseThing, "Manufacturer", value); }
        }
        public int AddressWidth
        {
            get { return TheCommonUtils.CInt(TheThing.GetSafePropertyString(MyBaseThing, "AddressWidth")); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, "AddressWidth", value); }
        }
        public int MaxClockSpeed
        {
            get { return TheCommonUtils.CInt(TheThing.GetSafePropertyString(MyBaseThing, "MaxClockSpeed")); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, "MaxClockSpeed", value); }
        }
        public int L2CacheSize
        {
            get { return TheCommonUtils.CInt(TheThing.GetSafePropertyString(MyBaseThing, "L2CacheSize")); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, "L2CacheSize", value); }
        }
        public int NumberOfCores
        {
            get { return TheCommonUtils.CInt(TheThing.GetSafePropertyString(MyBaseThing, "NumberOfCores")); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, "NumberOfCores", value); }
        }
        public int CodeOnCore
        {
            get { return TheCommonUtils.CInt(TheThing.GetSafePropertyString(MyBaseThing, "CodeOnCore")); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, "CodeOnCore", value); }
        }

        public double DiskSizeTotal
        {
            get { return TheThing.GetSafePropertyNumber(MyBaseThing, "DiskSizeTotal"); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, "DiskSizeTotal", value); }
        }
    }
}
