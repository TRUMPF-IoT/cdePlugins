// SPDX-FileCopyrightText: 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

﻿using nsCDEngine.BaseClasses;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.ViewModels;
using System;

namespace CDMyComputer.ViewModels
{
    public class TheServiceHealthData : TheMetaDataBase, ICDEThing
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
            return MyBaseThing?.GetProperty(pName, DoCreate);
        }
        public cdeP SetProperty(string pName, object pValue)
        {
            return MyBaseThing?.SetProperty(pName, pValue);
        }
        public void RegisterEvent(string pName, Action<ICDEThing, object> pCallBack)
        {
            MyBaseThing?.RegisterEvent(pName, pCallBack);
        }
        public void UnregisterEvent(string pName, Action<ICDEThing, object> pCallBack)
        {
            MyBaseThing?.UnregisterEvent(pName, pCallBack);
        }
        public void FireEvent(string pEventName, ICDEThing sender, object pPara, bool FireAsync)
        {
            MyBaseThing?.FireEvent(pEventName, sender, pPara, FireAsync);
        }
        public bool HasRegisteredEvents(string pEventName)
        {
            if (MyBaseThing != null)
                return MyBaseThing.HasRegisteredEvents(pEventName);
            return false;
        }
        internal TheThing MyBaseThing ;

        protected bool mIsUXInitialized;
        protected bool mIsInitialized;
        public bool IsUXInit()
        { return mIsUXInitialized; }
        public bool IsInit()
        { return mIsInitialized; }


        public virtual bool Init()
        {
            mIsInitialized = true;
            return true;
        }

        public bool Delete()
        {
            mIsInitialized = false;
            // TODO Properly implement delete
            return true;
        }

        public virtual bool CreateUX()
        {
            mIsUXInitialized = true;
            return true;
        }

        public void HandleMessage(ICDEThing sender, object pIncoming)
        {
            //TheProcessMessage pMsg = pIncoming as TheProcessMessage;
            //if (pMsg == null) return;
        }

        #endregion

        public TheServiceHealthData()
        {
            MyBaseThing = new TheThing
            {
                DeviceType = "PC-Health",
                ID = "PCH@" + TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID,
                EngineName = "CDMyComputer.TheCDMyComputerEngine"
            };
            cdeMID = MyBaseThing.cdeMID;
            cdeA = MyBaseThing.cdeA;
        }


        public TheServiceHealthData(string pEngineName, string pURL)
        {
            MyBaseThing = TheThingRegistry.GetThingByProperty(pEngineName, Guid.Empty, "ID", "PCH@" + TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID) ?? new TheThing
                          {
                              DeviceType = "PC-Health",
                              ID = "PCH@" + TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID,
                              EngineName = pEngineName
                          };
            cdeMID = MyBaseThing.cdeMID;
            cdeA = MyBaseThing.cdeA;
            MyBaseThing.SetIThingObject(this);
            TheThingRegistry.RegisterThing(this);
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
        public string DeviceType
        {
            get { return MyBaseThing.DeviceType; }
            set { MyBaseThing.DeviceType = value; }
        }
        public DateTimeOffset LastUpdate
        {
            get { return MyBaseThing.LastUpdate; }
            set { MyBaseThing.LastUpdate = value; }
        }

        public string HostAddress
        {
            get { return TheThing.GetSafePropertyString(MyBaseThing, "HostAddress"); }
            set { TheThing.SetSafePropertyString(MyBaseThing, "HostAddress", value); }
        }
        public string HostVersion
        {
            get { return TheThing.GetSafePropertyString(MyBaseThing, "HostVersion"); }
            set { TheThing.SetSafePropertyString(MyBaseThing, "HostVersion", value); }
        }
        public double RAMAvailable
        {
            get { return TheThing.GetSafePropertyNumber(MyBaseThing, "RAMAvailable"); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, "RAMAvailable", value); }
        }
        public double PCUptime
        {
            get { return TheThing.GetSafePropertyNumber(MyBaseThing, "PCUptime"); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, "PCUptime", value); }
        }
        public double CPULoad
        {
            get { return TheThing.GetSafePropertyNumber(MyBaseThing, "CPULoad"); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, "CPULoad", value); }
        }
        public double CPUTemp
        {
            get { return TheThing.GetSafePropertyNumber(MyBaseThing, "CPUTemp"); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, "CPUTemp", value); }
        }
        public double CPUSpeed
        {
            get { return TheThing.GetSafePropertyNumber(MyBaseThing, "CPUSpeed"); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, "CPUSpeed", value); }
        }
        public string CoreTemps
        {
            get { return TheThing.GetSafePropertyString(MyBaseThing, "CoreTemps"); }
            set { TheThing.SetSafePropertyString(MyBaseThing, "CoreTemps", value); }
        }
        public string CoreSpeeds
        {
            get { return TheThing.GetSafePropertyString(MyBaseThing, "CoreSpeeds"); }
            set { TheThing.SetSafePropertyString(MyBaseThing, "CoreSpeeds", value); }
        }

        public double DiskSpaceFree
        {
            get { return TheThing.GetSafePropertyNumber(MyBaseThing, "DiskSpaceFree"); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, "DiskSpaceFree", value); }
        }

        public double DiskSpaceUsage
        {
            get { return TheThing.GetSafePropertyNumber(MyBaseThing, "DiskSpaceUsage"); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, "DiskSpaceUsage", value); }
        }

        public double cdeLoad
        {
            get { return TheThing.GetSafePropertyNumber(MyBaseThing, "cdeLoad"); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, "cdeLoad", value); }
        }
        public double cdeTemp
        {
            get { return TheThing.GetSafePropertyNumber(MyBaseThing, "cdeTemp"); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, "cdeTemp", value); }
        }
        public double cdeSpeed
        {
            get { return TheThing.GetSafePropertyNumber(MyBaseThing, "cdeSpeed"); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, "cdeSpeed", value); }
        }
        public double cdeUptime
        {
            get { return TheThing.GetSafePropertyNumber(MyBaseThing, "cdeUptime"); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, "cdeUptime", value); }
        }
        public int cdeHandles
        {
            get { return (int)TheThing.GetSafePropertyNumber(MyBaseThing, "cdeHandles"); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, "cdeHandles", value); }
        }
        public long cdeWorkingSetSize
        {
            get { return (long)TheThing.GetSafePropertyNumber(MyBaseThing, "cdeWorkingSetSize"); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, "cdeWorkingSetSize", value); }
        }
        public int cdeThreadCount
        {
            get { return (int)TheThing.GetSafePropertyNumber(MyBaseThing, "cdeThreadCount"); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, "cdeThreadCount", value); }
        }
        public double StationWatts
        {
            get { return TheThing.GetSafePropertyNumber(MyBaseThing, "StationWatts"); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, "StationWatts", value); }
        }
        public double StationAmps
        {
            get { return TheThing.GetSafePropertyNumber(MyBaseThing, "StationAmps"); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, "StationAmps", value); }
        }
        public double StationVolts
        {
            get { return TheThing.GetSafePropertyNumber(MyBaseThing, "StationVolts"); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, "StationVolts", value); }
        }
        public double StationSolar
        {
            get { return TheThing.GetSafePropertyNumber(MyBaseThing, "StationSolar"); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, "StationSolar", value); }
        }
        public int HealthIndex
        {
            get { return (int)TheThing.GetSafePropertyNumber(MyBaseThing, "HealthIndex"); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, "HealthIndex", value); }
        }

        public double QSenders
        {
            get { return TheThing.GetSafePropertyNumber(MyBaseThing, "QSenders"); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, "QSenders", value); }
        }
        public double QSReceivedTSM
        {
            get { return TheThing.GetSafePropertyNumber(MyBaseThing, "QSReceivedTSM"); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, "QSReceivedTSM", value); }
        }
        public double QSSendErrors
        {
            get { return TheThing.GetSafePropertyNumber(MyBaseThing, "QSSendErrors"); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, "QSSendErrors", value); }
        }
        public double QSInserted
        {
            get { return TheThing.GetSafePropertyNumber(MyBaseThing, "QSInserted"); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, "QSInserted", value); }
        }
        public double QSQueued
        {
            get { return TheThing.GetSafePropertyNumber(MyBaseThing, "QSQueued"); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, "QSQueued", value); }
        }
        public double QSRejected
        {
            get { return TheThing.GetSafePropertyNumber(MyBaseThing, "QSRejected"); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, "QSRejected", value); }
        }
        public double QSSent
        {
            get { return TheThing.GetSafePropertyNumber(MyBaseThing, "QSSent"); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, "QSSent", value); }
        }
        public double QKBReceived
        {
            get { return TheThing.GetSafePropertyNumber(MyBaseThing, "QKBReceived"); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, "QKBReceived", value); }
        }
        public double QKBSent
        {
            get { return TheThing.GetSafePropertyNumber(MyBaseThing, "QKBSent"); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, "QKBSent", value); }
        }
        public double QSLocalProcessed
        {
            get { return TheThing.GetSafePropertyNumber(MyBaseThing, "QSLocalProcessed"); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, "QSLocalProcessed", value); }
        }
        public double HTCallbacks
        {
            get { return TheThing.GetSafePropertyNumber(MyBaseThing, "HTCallbacks"); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, "HTCallbacks", value); }
        }

        public double CCTSMsRelayed
        {
            get { return TheThing.GetSafePropertyNumber(MyBaseThing, "CCTSMsRelayed"); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, "CCTSMsRelayed", value); }
        }
        public double CCTSMsReceived
        {
            get { return TheThing.GetSafePropertyNumber(MyBaseThing, "CCTSMsReceived"); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, "CCTSMsReceived", value); }
        }
        public double CCTSMsEvaluated
        {
            get { return TheThing.GetSafePropertyNumber(MyBaseThing, "CCTSMsEvaluated"); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, "CCTSMsEvaluated", value); }
        }

        public double EventTimeouts
        {
            get { return TheThing.GetSafePropertyNumber(MyBaseThing, "EventTimeouts"); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, "EventTimeouts", value); }
        }
        public double TotalEventTimeouts
        {
            get { return TheThing.GetSafePropertyNumber(MyBaseThing, "TotalEventTimeouts"); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, "TotalEventTimeouts", value); }
        }

        public double KPI1
        {
            get { return TheThing.GetSafePropertyNumber(MyBaseThing, "KPI1"); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, "KPI1", value); }
        }
        public double KPI2
        {
            get { return TheThing.GetSafePropertyNumber(MyBaseThing, "KPI2"); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, "KPI2", value); }
        }
        public double KPI3
        {
            get { return TheThing.GetSafePropertyNumber(MyBaseThing, "KPI3"); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, "KPI3", value); }
        }
        public double KPI4
        {
            get { return TheThing.GetSafePropertyNumber(MyBaseThing, "KPI4"); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, "KPI4", value); }
        }
        public double KPI5
        {
            get { return TheThing.GetSafePropertyNumber(MyBaseThing, "KPI5"); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, "KPI5", value); }
        }
        public double KPI6
        {
            get { return TheThing.GetSafePropertyNumber(MyBaseThing, "KPI6"); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, "KPI6", value); }
        }
        public double KPI7
        {
            get { return TheThing.GetSafePropertyNumber(MyBaseThing, "KPI7"); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, "KPI7", value); }
        }
        public double KPI8
        {
            get { return TheThing.GetSafePropertyNumber(MyBaseThing, "KPI8"); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, "KPI8", value); }
        }
        public double KPI9
        {
            get { return TheThing.GetSafePropertyNumber(MyBaseThing, "KPI9"); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, "KPI9", value); }
        }
        public double KPI10
        {
            get { return TheThing.GetSafePropertyNumber(MyBaseThing, "KPI10"); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, "KPI10", value); }
        }

        public double NetRead
        {
            get { return TheThing.MemberGetSafePropertyNumber(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyNumber(MyBaseThing, value); }
        }

        public double NetWrite
        {
            get { return TheThing.MemberGetSafePropertyNumber(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyNumber(MyBaseThing, value); }
        }

    }
}
