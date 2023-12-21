// SPDX-FileCopyrightText: 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using nsCDEngine.BaseClasses;
using nsCDEngine.Engines;
using nsCDEngine.Engines.StorageService;
using nsCDEngine.Engines.StorageService.Model;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.ViewModels;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using TheSensorTemplate;
// ReSharper disable UseNullPropagation

//ERROR Range: 430-

namespace CDMyMSSQLStorage
{
    internal partial class TheStorageService : ThePluginBase, IStorageService
    {
        #region ICDEPlugin Interfaces
        public override void InitEngineAssets(IBaseEngine pEngine)
        {
            base.InitEngineAssets(pEngine);
            MyBaseEngine.SetFriendlyName("MS-SQL Distributed Storage");
            MyBaseEngine.SetEngineID(new Guid("{0AAFF440-89B5-4B7E-B512-A1F0697711E7}"));
            MyBaseEngine.AddCapability(eThingCaps.DistributedStorage);
            MyBaseEngine.AddCapability(eThingCaps.MustBePresent);
            MyBaseEngine.GetEngineState().IsAllowedUnscopedProcessing = true;
            MyBaseEngine.SetPluginInfo("Distributed Storage Service for MS-SQL", 0, null, "toplogo-150.png", "C-Labs", "http://www.c-labs.com", new List<string>() { "IStorageService", "Service" });
            MyBaseEngine.AddManifestFiles(new List<string> { "System.Data.SqlClient.dll" });
        }
        #endregion

        #region IStorageService Interfaces

        public TheStorageCaps GetStorageCaps()
        {
            return MyStorageCaps;
        }

        /// <summary>
        /// Creates a new store in the Storage Service
        /// </summary>
        /// <param name="MyType">Type Definition of the class to be stored</param>
        /// <param name="pDefaults">a list of defaults for the class</param>
        /// <param name="pDeviceName">Name of the store</param>
        /// <param name="StoreDescription">Description of the store</param>
        /// <param name="ResetContent"></param>
        /// <param name="pCallBack">Callback to be called when store was created successfully</param>
        public void EdgeDataCreateStore(Type MyType, object pDefaults, string pDeviceName, string StoreDescription, bool ResetContent, Action<TSM> pCallBack, string pTableName)
        {
            string UniqueID;
            string serializedStore = TheStorageUtilities.SerializeCreateStore(MyType, pDefaults, pDeviceName, StoreDescription, out UniqueID, pTableName);

            if (!MyBaseEngine.GetEngineState().IsSimulated)
            {
                if (TheCDEngines.MyIStorageService != null)
                {
                    string post = "CDE_CREATESTORE";
                    if (ResetContent)
                        post = "CDE_CREATENEWSTORE";
                    if (pCallBack != null)
                    {
                        Guid ReqGUID = Guid.NewGuid();
                        MyTimedCallbacks.AddAnItem(new TheTimedCallback() { cdeMID = ReqGUID, MyCallback = pCallBack });
                        post += ":" + ReqGUID.ToString();
                    }
                    TSM tTSM = new TSM(MyBaseEngine.GetEngineName(), post, serializedStore);

                    if (TheCDEngines.MyIStorageService.GetBaseEngine().GetEngineState().IsService)
                        ProcessStorageServiceCommands(tTSM, pCallBack, true);
                    //else //Later for Remote Storage
                    //{
                    //    tTSM.SetToServiceOnly(true);
                    //    TheCommCore.PublishCentral(tTSM);
                    //}
                }
            }
            else
            {
                pCallBack?.Invoke(new TSM(MyBaseEngine.GetEngineName(), "UniqueID: " + UniqueID, UniqueID));
            }
        }

        /// <summary>
        /// Stores the values of a class in the StorageServer
        /// </summary>
        /// <typeparam name="T">Type of the class to be stored</typeparam>
        /// <param name="pMyValue">Class with values to be stored</param>
        public void EdgeDataStoreOnly<T>(T pMyValue, string pTableName) 
        {
            Dictionary<string, T> ToWriteOnly = new Dictionary<string, T>();
            TheDataBase tDB = pMyValue as TheDataBase;
            if (tDB != null) tDB.cdeMID = Guid.NewGuid();
            ToWriteOnly.Add(Guid.Empty.ToString(), pMyValue);
            eSCMD Cmd = eSCMD.Insert;
            EdgeDataStore(ToWriteOnly, Cmd, "", null, pTableName);
        }

        /// <summary>
        /// Main call to store values in the StorageService
        /// </summary>
        /// <typeparam name="T">Type of class to be stored</typeparam>
        /// <param name="MyValue">Dictionary of Values of the class to be stored</param>
        /// <param name="pCMD">Command for the store process</param>
        /// <param name="pMagicID">MagicID to be used for back-publish</param>
        /// <param name="pCallBack">If set, and the call came from a local service, the sevices calls back without using the communication system</param>
        public void EdgeDataStore<T>(Dictionary<string, T> MyValue, eSCMD pCMD, string pMagicID, Action<TSM> pCallBack, string pTableName)
        {
            if (TheCDEngines.MyIStorageService != null)
            {
                string serializedXML = TheStorageUtilities.SerializeDataToStore(MyValue, pMagicID, pCMD, typeof(T).Equals(typeof(cdeP)) ? null : pTableName);
                string post = "CDE_EDGESTORE";
                post += ":";
                if (!string.IsNullOrEmpty(pMagicID) && pCallBack != null)
                {
                    MyTimedCallbacks.AddAnItem(new TheTimedCallback() { cdeMID = TheCommonUtils.CGuid(pMagicID), MyCallback = pCallBack });
                    post += pMagicID;
                }
                if(typeof(T).Equals(typeof(cdeP)))
                    post += ":" + TheStorageUtilities.GenerateUniqueIDFromType(typeof(TheThing), pTableName); 
                TSM tTSM = new TSM(MyBaseEngine.GetEngineName(), post, serializedXML);
                if (TheCDEngines.MyIStorageService.GetBaseEngine().GetEngineState().IsService)
                    ProcessStorageServiceCommands(tTSM, pCallBack, true);
                //else //Later for Remote Storage
                //{
                //    tTSM.SetToServiceOnly(true);
                //    TheCommCore.PublishCentral(tTSM);
                //}
            }
        }

        /// <summary>
        /// Requests data from the StorageService
        /// Do not use this call if your StorageMirror has a preset TableName
        /// </summary>
        /// <param name="MyClass">type of the class to be retreived</param>
        /// <param name="pColFilter">request results only for certain colums</param>
        /// <param name="pTopRows">TOP statement to reduce amount of returned rows</param>
        /// <param name="pPageNo">If >0 this number describes a page in the store to be retreived. If set to -1 the last page is retreived</param>
        /// <param name="pSQLFilter">A SQL Filter for the query</param>
        /// <param name="pSQLOrder">A ORDER BY setting for the query</param>
        /// <param name="pGrouping"></param>
        /// <param name="pMagicID">MagicID to be used for back-publish</param>
        /// <param name="pCallBack">If set, and the call came from a local service, the sevices calls back without using the communication system</param>
        /// <param name="LocalCallBackOnly">if Set, no Publish or requested data is executed, just local callback</param>
        public void RequestEdgeStorageData(Type MyClass, string pColFilter, int pTopRows, int pPageNo, string pSQLFilter, string pSQLOrder, string pGrouping, string pMagicID, Action<TSM> pCallBack, bool LocalCallBackOnly, string pTableName)
        {
            string classID = "";
            if (MyClass == null)
                classID = "<null>";
            else
                classID = TheStorageUtilities.GenerateUniqueIDFromType(MyClass, pTableName);  
            RequestEdgeStorageData(classID, pColFilter, pTopRows, pPageNo, pSQLFilter, pSQLOrder, pGrouping, pMagicID, pCallBack, LocalCallBackOnly);
        }

        /// <summary>
        /// Requests data from the StorageService
        /// </summary>
        /// <param name="pUniqueID">Known UniqueID of the storage data</param>
        /// <param name="pColFilter">request results only for certain colums</param>
        /// <param name="pTopRows">TOP statement to reduce amount of returned rows</param>
        /// <param name="pPageNo">If >0 this number describes a page in the store to be retreived. If set to -1 the last page is retreived</param>
        /// <param name="pSQLFilter">A SQL Filter for the query</param>
        /// <param name="pSQLOrder">A ORDER BY setting for the query</param>
        /// <param name="pGrouping"></param>
        /// <param name="pMagicID">MagicID to be used for back-publish</param>
        /// <param name="pCallBack">If set, and the call came from a local service, the sevices calls back without using the communication system</param>
        /// <param name="LocalCallBackOnly">if Set, no Publish or requested data is executed, just local callback</param>
        public void RequestEdgeStorageData(string pUniqueID, string pColFilter, int pTopRows, int pPageNo, string pSQLFilter, string pSQLOrder, string pGrouping, string pMagicID, Action<TSM> pCallBack, bool LocalCallBackOnly)
        {
            // Grab the numerical part of the ID
            string tUniqueID = "";
            if (pUniqueID.StartsWith(CommonTypeUIDs.cdePUniqueID))
                tUniqueID = pUniqueID.Substring(0, pUniqueID.IndexOf('-'));
            else
                tUniqueID = pUniqueID;
            StorageGetRequest tRequest = new StorageGetRequest
            {
                SFI = pSQLFilter,
                SOR = pSQLOrder,
                CFI = pColFilter,
                UID = tUniqueID,
                TOP = pTopRows,
                MID = pMagicID,
                PAG = pPageNo,
                GRP = pGrouping,
                DID = TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID
            };
            string serializedXML = TheCommonUtils.SerializeObjectToJSONString(tRequest);
            string post = "";
            if (!MyBaseEngine.GetEngineState().IsSimulated)
            {
                if (TheCDEngines.MyIStorageService != null)
                {
                    post = "CDE_EDGEGET";
                    post += ":";
                    if (pCallBack != null)
                    {
                        Guid ReqGUID = Guid.NewGuid();
                        MyTimedCallbacks.AddAnItem(new TheTimedCallback() { cdeMID = ReqGUID, MyCallback = pCallBack });
                        post += ReqGUID.ToString();
                    }
                    if (pUniqueID.StartsWith(CommonTypeUIDs.cdePUniqueID))
                        post += ":" + TheStorageUtilities.GenerateUniqueIDFromType(typeof(TheThing), pUniqueID.Substring(pUniqueID.IndexOf('-') + 1));  // Grabs the CacheTableName part of the ID
                    TSM tTSM = new TSM(MyBaseEngine.GetEngineName(), post, serializedXML);
                    if (TheCDEngines.MyIStorageService.GetBaseEngine().GetEngineState().IsService)
                        ProcessStorageServiceCommands(tTSM, pCallBack, true);
                    //else //Later for Remote Storage
                    //{
                    //    tTSM.SetToServiceOnly(true);
                    //    TheCommCore.PublishCentral(tTSM);
                    //}
                }
            }
            else
                pCallBack(null);
        }

        /// <summary>
        /// Executes a SQL query against the store of the MyClass
        /// </summary>
        /// <param name="MyClass">Type of the Class determining the store to run the query against</param>
        /// <param name="SQLExec">SQL statement to execute</param>
        /// <param name="pColFilter"></param>
        /// <param name="pMagicID">MagicID to be used for back-publish</param>
        /// <param name="pCallBack">If set, and the call came from a local service, the sevices calls back without using the communication system</param>
        public void EdgeStorageExecuteSql(Type MyClass, string SQLExec, string pColFilter, string pMagicID, Action<TSM> pCallBack, string pTableName)
        {
            if (!MyBaseEngine.GetEngineState().IsSimulated)
            {
                if (TheCDEngines.MyIStorageService != null)
                {
                    string tableName = pTableName;
                    string post = "CDE_EXECUTESQL";
                    post += ":";
                    if (pMagicID.Length > 0 && pCallBack != null)
                    {
                        MyTimedCallbacks.AddAnItem(new TheTimedCallback() { cdeMID = TheCommonUtils.CGuid(pMagicID), MyCallback = pCallBack });
                        post += pMagicID;
                    }
                    if (MyClass != null)
                    {
                        if (MyClass.Equals(typeof(cdeP)))
                        {
                            tableName = null;
                            post += ":" + TheStorageUtilities.GenerateUniqueIDFromType(typeof(TheThing), pTableName);
                        }
                    }
                    string serializedXML = TheStorageUtilities.SerializeDataToExecute(MyClass, pMagicID, SQLExec, pColFilter, tableName); 
                    TSM tTSM = new TSM(MyBaseEngine.GetEngineName(), post, serializedXML);
                    if (TheCDEngines.MyIStorageService.GetBaseEngine().GetEngineState().IsService)
                        ProcessStorageServiceCommands(tTSM, pCallBack, true);
                    //else //Later for Remote Storage
                    //{
                    //    tTSM.SetToServiceOnly(true);
                    //    TheCommCore.PublishCentral(tTSM);
                    //}
                }
            }
        }

        /// <summary>
        /// Updates the definition of a store in the Storage Service's storage map
        /// </summary>
        /// <param name="MyClass">Type Definition of the class to be stored</param>
        /// <param name="pDeviceName">New name of the store</param>
        /// <param name="StoreDescription">New description of the store</param>
        /// <param name="CallBack">If set, and the call came from a local service, the sevices calls back without using the communication system</param>
        /// <param name="pTableName">Cache table name of the store - used with Type to create a UniqueID</param>
        public void EdgeUpdateStore(Type MyClass, string pDeviceName, string StoreDescription, Action<TSM> CallBack, string pTableName)
        {
            string UniqueID;
            string serializedStore = TheStorageUtilities.SerializeCreateStore(MyClass, null, pDeviceName, StoreDescription, out UniqueID, pTableName);

            if (!MyBaseEngine.GetEngineState().IsSimulated)
            {
                if (TheCDEngines.MyIStorageService != null)
                {
                    string post = "CDE_UPDATESTORE";
                    if (CallBack != null)
                    {
                        Guid ReqGUID = Guid.NewGuid();
                        MyTimedCallbacks.AddAnItem(new TheTimedCallback() { cdeMID = ReqGUID, MyCallback = CallBack });
                        post += ":" + ReqGUID.ToString();
                    }
                    TSM tTSM = new TSM(MyBaseEngine.GetEngineName(), post, serializedStore);

                    if (TheCDEngines.MyIStorageService.GetBaseEngine().GetEngineState().IsService)
                        ProcessStorageServiceCommands(tTSM, CallBack, true);
                    //else //Later for Remote Storage
                    //{
                    //    tTSM.SetToServiceOnly(true);
                    //    TheCommCore.PublishCentral(tTSM);
                    //}
                }
            }
            else
            {
                CallBack?.Invoke(new TSM(MyBaseEngine.GetEngineName(), "UniqueID: " + UniqueID, UniqueID));
            }
        }

        /// <summary>
        /// Requests the capabilities of the Server
        /// </summary>
        /// <param name="pMagicID">MagicID to be used for back-publish</param>
        /// <param name="pCallBack">If set, and the call came from a local service, the sevices calls back without using the communication system</param>
        public void EdgeStorageServerRequestCaps(string pMagicID, Action<TSM> pCallBack)
        {
            if (TheCDEngines.MyIStorageService == null || TheCDEngines.MyIStorageService.GetBaseEngine().GetEngineState().IsService) return;
            string serializedXML = TheCommonUtils.SerializeObjectToJSONString(TheCDEngines.MyIStorageService.GetStorageCaps());
            string post = "CDE_GETEDGECAPS";
            if (pMagicID.Length > 0 && pCallBack != null)
            {
                MyTimedCallbacks.AddAnItem(new TheTimedCallback() { cdeMID = TheCommonUtils.CGuid(pMagicID), MyCallback = pCallBack });
                post += ":" + pMagicID;
            }
            TSM tTSM = new TSM(MyBaseEngine.GetEngineName(), post, serializedXML);
            if (TheCDEngines.MyIStorageService.GetBaseEngine().GetEngineState().IsService)
                ProcessStorageServiceCommands(tTSM, pCallBack, true);
            //else //Later for Remote Storage
            //{
            //    tTSM.SetToServiceOnly(true);
            //    TheCommCore.PublishCentral(tTSM);
            //}
        }
        #endregion
#if JCR_TESTSTORE        
        public TheStorageMirror<TheTestBase> MyTESTBASE = null;
#endif
        internal TheStorageCaps MyStorageCaps = new TheStorageCaps();
        internal TheStorageMirror<TheTimedCallback> MyTimedCallbacks;

        public override bool Init()
        {
            if (mIsInitCalled) return false; 
            mIsInitCalled = true;

            if (TheCDEngines.MyIStorageService == null)
            {
                MyBaseThing.StatusLevel = 3;
                MyBaseThing.LastMessage = "Failure during Storage Service Rampup"; //TODO: Add reason why
                return true;    //We return true as in complete Init but no processing anymore
            }
            MyBaseThing.RegisterEvent(eEngineEvents.IncomingMessage, HandleMessage);
            TheBaseEngine.WaitForStorageReadiness(sinkStorageStationIsReadyFired, true);
            mIsInitialized = true;
            MyBaseEngine.SetInitialized(null);
            return true;
        }

        public override void HandleMessage(ICDEThing sender, object pIncoming)
        {
            if (TheCDEngines.MyIStorageService == null) return; //No processing if Storage is not active

            TheProcessMessage pMsg = pIncoming as TheProcessMessage;
            if (pMsg == null) return;
            if (!mIsInitialized)
            {
                //InitializeStores(); //V4.106: we dont allow this anymore during runtime - the store is either initialized during startup or never available
                return;
            }
            if (MyBaseEngine.GetEngineState().IsService)
            {
                var tCmd = pMsg.Message.TXT.Split(':');
                switch (tCmd[0])
                {
                    case "NEW_SENSOR_VALUE":
                        TheSensorValue tData = TheCommonUtils.DeserializeJSONStringToObject<TheSensorValue>(pMsg.Message.PLS);
                        if (tData != null && MySensorStore != null)
                        {
                            //TODO: tCmd[1] has the "ChartToken" that allows to put the value into different tables/charts. For now just one table for all charts
                            MySensorStore.AddAnItem(tData);
                        }
                        break;
                    default:
                        ProcessStorageServiceCommands(pMsg.Message, pMsg.LocalCallback, false);
                        break;
                }
                return;
            }

            //CM: Next section is about Remote Storage Services. This will come in a later step
            string[] reqGuid = pMsg.Message.TXT.Split(':');
            TheTimedCallback tCall = null;
            if (reqGuid.Length > 1 && (tCall = MyTimedCallbacks.MyMirrorCache.GetEntryByID(TheCommonUtils.CGuid(reqGuid[1]))) != null)
            {
                tCall.MyCallback?.Invoke(pMsg.Message);
                MyTimedCallbacks.RemoveAnItem(tCall, null);
            }
            else
            {
                object tStorageMirror = TheCDEngines.GetStorageMirror(pMsg.Topic); //, out tStorageMirror);  
                if (tStorageMirror != null)
                {
                    Type magicType = tStorageMirror.GetType();
                    MethodInfo magicMethod = magicType.GetTypeInfo().GetDeclaredMethod("sinkProcessServiceMessages");
                    if (magicMethod != null)
                        magicMethod.Invoke(tStorageMirror, new object[] { pMsg.Message }); //object magicValue = 
                }
            }
        }

        /// <summary>
        /// Returns false if stores could not be initalized - IStorageService failed and will be shutdown
        /// </summary>
        /// <returns></returns>
        private bool InitializeStores()
        {
            bool bInitialized = CreateStorageDAT();
            if (!bInitialized) return false;
            bool Success = true;
#if JCR_TESTSTORE
                if (MyTESTBASE == null)
                {
                    MyTESTBASE = new TheStorageMirror<TheTestBase>(this);
                    MyTESTBASE.IsRAMStore = MyBaseEngine.GetEngineState().IsSimulated;
                    if (MyBaseEngine.GetEngineState().IsService)
                        MyTESTBASE.CreateStore(TheBaseAssets.MyServiceHostInfo.ApplicationName + ": Test Store", "Test Push and other test messages", null);
                    else
                        MyTESTBASE.InitializeStore();
                }
#endif
            if (MyBaseEngine.GetEngineState().IsService && TheBaseAssets.MyServiceHostInfo.StoreLoggedMessages)
                EdgeDataCreateStore(typeof(TSM), null, TheBaseAssets.MyServiceHostInfo.ApplicationName + ": SystemMessageLog", "Log of all Application System Messages", false, null, null);

            if (MyTimedCallbacks == null)
            {
                MyTimedCallbacks = new TheStorageMirror<TheTimedCallback>(TheCDEngines.MyIStorageService) { IsRAMStore = true };
                MyTimedCallbacks.InitializeStore(true);
                MyTimedCallbacks.SetRecordExpiration(TheBaseAssets.MyServiceHostInfo.TO.ReceivedChunkTimeout, null);
            }
            if (TheCommonUtils.CBool(TheBaseAssets.MySettings.GetSetting("UseStorageForThingRegistry")))
                Success=CreateThingRegistryStore();
            return Success;
        }

        private bool CreateThingRegistryStore()
        {
            TheStorageMirror<TheThing> thingRegistryMirror = new TheStorageMirror<TheThing>(TheCDEngines.MyIStorageService)
            {
                CacheTableName = "TheThingRegistry",
                IsRAMStore = false,
                IsCached = true, //V4.106: This is required until ALL ThingRegistry calls do not go against StorageMirrorCache
                IsCachePersistent = false // New in V4: All nodes can load ThingRegistry - some can store  TheBaseAssets.MyServiceHostInfo.cdeNodeType == cdeNodeType.Relay,
            };
            TheStorageMirrorParameters tStoreParams = new TheStorageMirrorParameters()
            {
                FriendlyName= "cdeTheThingRegistry",
                Description= "Stores all C-DEngine Things",
                ResetContent = TheBaseAssets.MyServiceHostInfo.IsNewDevice,
                LoadSync = true
            };
            Task<bool> tRes = thingRegistryMirror.CreateStoreAsync(tStoreParams, null);
            tRes.Wait();
            return tRes.Result;
        }

        public TheStorageMirror<TheSensorValue> MySensorStore = null;
        private void sinkStorageStationIsReadyFired(ICDEThing sender, object pReady)
        {
            if (pReady != null)
            {
                if (MySensorStore == null)
                {
                    MySensorStore = new TheStorageMirror<TheSensorValue>(TheCDEngines.MyIStorageService);
                    if (MyBaseEngine.GetEngineState().IsSimulated || "RAM".Equals(pReady.ToString()))
                    {
                        MySensorStore.IsRAMStore = true;
                        MySensorStore.IsCachePersistent = true;
                        MySensorStore.SetRecordExpiration(10000, null);
                    }
                    else
                        MySensorStore.IsRAMStore = false;
                    if (!MySensorStore.IsRAMStore && MyBaseEngine.GetEngineState().IsService)
                        MySensorStore.CreateStore("Sensor Data: Sensor Data Store", "Data from sensors", null, true, false);
                    else
                        MySensorStore.InitializeStore(true, false);
                }
            }
        }
    }
}
