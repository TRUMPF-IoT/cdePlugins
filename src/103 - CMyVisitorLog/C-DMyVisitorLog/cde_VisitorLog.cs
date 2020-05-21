// SPDX-FileCopyrightText: 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

﻿using nsCDEngine.BaseClasses;
using nsCDEngine.Communication;
using nsCDEngine.Engines;
using nsCDEngine.Engines.StorageService;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.ViewModels;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
// ReSharper disable CompareOfFloatsByEqualityOperator

//ERROR Range 510-519

namespace CDMyVisitorLog
{

    public class Language
    {
        public string code { get; set; }
        public string name { get; set; }
        public string native { get; set; }
    }

    public class Location
    {
        public int geoname_id { get; set; }
        public string capital { get; set; }
        public List<Language> languages { get; set; }
        public string country_flag { get; set; }
        public string country_flag_emoji { get; set; }
        public string country_flag_emoji_unicode { get; set; }
        public string calling_code { get; set; }
        public bool is_eu { get; set; }
    }

    public class TheVisitorLogData : TheDataBase
    {
        public string ip { get; set; }
        public string type { get; set; }
        public string continent_code { get; set; }
        public string continent_name { get; set; }
        public string country_code { get; set; }
        public string country_name { get; set; }
        public string region_code { get; set; }
        public string region_name { get; set; }
        public string city { get; set; }
        public string zip { get; set; }
        public double latitude { get; set; }
        public double longitude { get; set; }
        public Location location { get; set; }

        public int Visits { get; set; }
        public DateTimeOffset LastVisit { get; set; }
        public string Description { get; set; }
        public string Referrer { get; set; }

        public TheVisitorLogData()
        {
            Visits = 1;
        }
    }

    /// <summary>
    /// Maintains a log of all visiting Nodes to this Node
    /// </summary>
    public class TheVisitorLog
    {
        /// <summary>
        /// this event is called when the C-DEngine was able to update the location of the current node
        /// You will NOT Be called on the UI Thread. Make sure you marshal corectly to the UI thread
        /// </summary>
        /// <summary>
        /// Contains the TheVisitorLogData of the hosting node
        /// </summary>
        public TheVisitorLogData MyLocation;
        /// <summary>
        /// StorageService - DataStore of all Visitors known to this node
        /// </summary>
        public TheStorageMirror<TheVisitorLogData> MyVisitorLogStore=null;


        #region INIT
        internal TheVisitorLog(ICDEThing pThing)
        {
            MyBaseThing = pThing;

            APIKey = TheBaseAssets.MySettings.GetSetting("VisitorLogAPIKey");
            if (!string.IsNullOrEmpty(APIKey))
                TheBaseEngine.WaitForStorageReadiness(StorageHasStarted, true);
        }

        string APIKey = null;
        readonly ICDEThing MyBaseThing;
        private void StorageHasStarted(ICDEThing sender, object pReady)
        {
            if (pReady !=null)
            {
                if (MyVisitorLogStore == null)
                {
                    MyVisitorLogStore = new TheStorageMirror<TheVisitorLogData>(TheCDEngines.MyIStorageService);
                    MyVisitorLogStore.RegisterEvent(eStoreEvents.StoreReady,StoreIsUp);
                }
                MyVisitorLogStore.IsRAMStore = TheCDEngines.MyIStorageService==null || TheCDEngines.MyIStorageService.GetBaseEngine().GetEngineState().IsSimulated;
                if (MyVisitorLogStore.IsRAMStore)
                {
                    MyVisitorLogStore.IsCachePersistent = true;
                    MyVisitorLogStore.CacheTableName = "VisitorLog";
                    MyVisitorLogStore.SetRecordExpiration(1000000,null);
                }
                MyVisitorLogStore.CreateStore(TheBaseAssets.MyServiceHostInfo.ApplicationName + ": The Visitor Log", "Logs all visitors to the Site " + TheBaseAssets.MyServiceHostInfo.ApplicationName, null,true, false);

            }
        }
        #endregion

        private bool mIsHostLogged ;
        private void StoreIsUp(StoreEventArgs ID)
        {
            if (!mIsHostLogged)
            {
                mIsHostLogged = true;
                TheVisitorLogData tLog = new TheVisitorLogData
                {
                    LastVisit = DateTimeOffset.Now,
                    Description = TheBaseAssets.MyServiceHostInfo.MyStationName
                };
                IPAddress myaddr = nsCDEngine.Discovery.TheNetworkInfo.GetIPAddress(true);
                if (myaddr == null)
                    nsCDEngine.Discovery.TheNetworkInfo.GetExternalIp(sinkRegisterHost, tLog);
                else
                {
                    TheREST.GetRESTAsync(new Uri($"http://api.ipstack.com/{myaddr}?access_key={APIKey}"), 0, sinkProcessLocation, tLog);
                }
            }
        }

        public List<TheVisitorLogData> GetAllVisitors()
        {
            var tcs = new TaskCompletionSource<List<TheVisitorLogData>>();  //REVIEW: Alle Callbacks brauchen AL
            MyVisitorLogStore?.GetRecords((resp)=>{
                tcs.SetResult(resp.MyRecords);
            }, true);
            return tcs.Task.Result;
        }

        private void sinkRegisterHost(IPAddress pIP, object pData)
        {
            TheVisitorLogData tData = pData as TheVisitorLogData;
            if (tData != null)
            {
                if (pIP == null)
                    pIP = nsCDEngine.Discovery.TheNetworkInfo.GetIPAddress(false);
                tData.ip = pIP.ToString();
                TheBaseAssets.MySYSLOG.WriteToLog(511, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("VisitorLog", "GetExternalIp returned: " + pIP.ToString(), eMsgLevel.l4_Message));
                TheREST.GetRESTAsync(new Uri($"http://api.ipstack.com/{pIP}?access_key={APIKey}"), 0, sinkProcessLocation, tData);
            }
        }
        internal void sinkProcessLocation(TheRequestData pLocationState)
        {
            if (pLocationState.ResponseBuffer == null) return;
            TheVisitorLogData tlog = pLocationState.CookieObject as TheVisitorLogData;
            if (tlog != null) MyLocation = tlog;
            if (MyLocation == null) MyLocation = new TheVisitorLogData();

            string pLocation = TheCommonUtils.CArray2UTF8String(pLocationState.ResponseBuffer);

            if (!string.IsNullOrEmpty(pLocation))
            {
                try
                {
                    TheVisitorLogData tIP = TheCommonUtils.DeserializeJSONStringToObject<TheVisitorLogData>(pLocation);
                    if (tIP != null)
                    {
                        MyLocation.ip = tIP.ip;
                        MyLocation.latitude = tIP.latitude;
                        MyLocation.longitude = tIP.longitude;
                        MyLocation.region_code = tIP.region_code;
                        MyLocation.region_name = tIP.region_name;
                        MyLocation.zip = tIP.zip;
                        MyBaseThing?.FireEvent("NewVisitorLogged",MyBaseThing,null,true);
                        //TheREST.GetRESTAsync(new Uri(string.Format("http://a4544535456.api.wxbug.net/getLiveWeatherRSS.aspx?ACode=a4544535456&lat={0}&long={1}&UnitType=1&OutputType=1", MyLocation.Latitude, MyLocation.Longitude)), 0, MyWeather.sinkProcessWeather, null);
                    }
                }
                catch (Exception ee)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(512, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("VisitorLog", "Error processing Location ", eMsgLevel.l1_Error, ee.ToString() + ":::" + pLocation));
                }
            }
            MyVisitorLogStore?.UpdateItem(MyLocation);
        }



        private void GetIP(TheStorageMirror<TheVisitorLogData>.StoreResponse pRec)
        {
            lock (mVisitorList.MyLock)
            {
                TheVisitorLogData tLog = null;
                if (pRec == null || pRec.HasErrors || pRec.MyRecords.Count == 0)
                {
                    if (pRec != null)
                    {
                        mVisitorList.TryGetValue(pRec.SQLFilter, out tLog);
                        if (tLog != null)
                        {
                            TheREST.GetRESTAsync(new Uri($"http://api.ipstack.com/{tLog.ip}?access_key={APIKey}"), 0, sinkProcessLocation, tLog);
                            mVisitorList.RemoveNoCare(pRec.SQLFilter);
                        }
                    }
                }
                else
                {
                    tLog = pRec.MyRecords[0];
                    tLog.Visits++;
                    tLog.LastVisit = DateTimeOffset.Now;
                    if (tLog.latitude == 0 && tLog.longitude == 0)
                        TheREST.GetRESTAsync(new Uri($"http://api.ipstack.com/{tLog.ip}?access_key={APIKey}"), 0, sinkProcessLocation, tLog);
                    else
                        MyVisitorLogStore.UpdateItem(MyLocation);
                }
            }
        }

        private readonly cdeConcurrentDictionary<string, TheVisitorLogData> mVisitorList = new cdeConcurrentDictionary<string, TheVisitorLogData>();    //DIC-Allowed - has cleanup STRING
        readonly object mVisitorListLock = new object();

        public bool LogVisitor(TheVisitorLogData pData)
        {
            MyVisitorLogStore?.AddAnItem(pData);
            return MyVisitorLogStore!=null;
        }

        /// <summary>
        /// Log a new Visitor
        /// </summary>
        /// <param name="IP">IP address of the Visitor</param>
        /// <param name="pReferer">A cookie (i.e. URL Referer) of the visitor stored with the Log Entry</param>
        /// <returns></returns>
        public Guid LogVisitor(string IP, string pReferer)
        {
            if (mIsHostLogged)
            {
                TheVisitorLogData tLog = new TheVisitorLogData();
                string tFilter = "";
                lock (mVisitorListLock)
                {
                    tLog.ip = IP;
                    tLog.LastVisit = DateTimeOffset.Now;
                    tLog.Referrer = pReferer;
                    tLog.Description = "VISITOR";
                    tFilter = "ip='" + IP + "'";
                    if (mVisitorList.ContainsKey(tFilter) && DateTimeOffset.Now.Subtract(mVisitorList[tFilter].LastVisit).TotalMinutes < 10)
                        return Guid.Empty;
                    if (mVisitorList.ContainsKey(tFilter))
                        mVisitorList[tFilter] = tLog;
                    else
                        mVisitorList.TryAdd(tFilter, tLog);
                }
                MyVisitorLogStore.GetRecords(tFilter, 1, GetIP, false);
                return tLog.cdeMID;
            }
            return Guid.Empty;
        }
    }
}
