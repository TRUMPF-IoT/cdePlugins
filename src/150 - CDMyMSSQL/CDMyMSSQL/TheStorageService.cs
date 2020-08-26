// SPDX-FileCopyrightText: 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using nsCDEngine.BaseClasses;
using nsCDEngine.Communication;
using nsCDEngine.Engines;
using nsCDEngine.Engines.StorageService;
using nsCDEngine.Engines.StorageService.Model;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.Security;
using nsCDEngine.ViewModels;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading.Tasks;
// ReSharper disable UseNullPropagation

//ERROR Range 440-459

namespace CDMyMSSQLStorage
{
    internal partial class TheStorageService 
    {
        private TheSQLHelper MySqlHelperClass;
        private readonly SQLDefinition MySQLDefinitions = new SQLDefinition();
        private readonly List<string> UniqueIDs = new List<string>();
        private TheStorageMirror<StorageDefinition> storageMapCache;
        private long IDCounter = 0; 

        public bool InitEdgeStore()
        {
            string temp = TheBaseAssets.MySettings.GetSetting("150-SQLCredentialToken", MyBaseEngine.GetEngineID()); 
            if (!string.IsNullOrEmpty(temp))
            {
                TheBaseAssets.MySettings.SetSetting("150-SQLCredentialToken", temp, true,MyBaseEngine.GetEngineID());//in case the token comes from old AppParameter of Provisioning Service. Take it and own it
                string[] tSec = TheCommonUtils.cdeSplit(temp, ";:;", false, false);
                if (tSec != null && tSec.Length > 2)
                {
                    MySQLDefinitions.ServerName = tSec[0];
                    MySQLDefinitions.UserName = tSec[1];
                    MySQLDefinitions.Password = tSec[2];
                }
            }
            else
            {
                temp = TheCommonUtils.CStr(TheBaseAssets.MySettings.GetSetting("SQLServerName"));
                if (!string.IsNullOrEmpty(temp))
                    MySQLDefinitions.ServerName = temp;
                else
                    return false;   //If no SQLServer is specified - no StorageService!
                temp = TheCommonUtils.CStr(TheBaseAssets.MySettings.GetSetting("SQLUserName"));
                if (!string.IsNullOrEmpty(temp))
                    MySQLDefinitions.UserName = temp;
                else
                    MySQLDefinitions.UserName = "sa";   //fallback to sa
                temp = TheCommonUtils.CStr(TheBaseAssets.MySettings.GetSetting("SQLPassword"));
                if (!string.IsNullOrEmpty(temp))    //If no password we try to connect with sa/<empty> to the SQL Server ..if that fails no StorageService
                    MySQLDefinitions.Password = temp;
                TheBaseAssets.MySettings.SetSetting("150-SQLCredentialToken", MySQLDefinitions.ServerName + ";:;" + MySQLDefinitions.UserName + ";:;" + MySQLDefinitions.Password, true, MyBaseEngine.GetEngineID());
                TheBaseAssets.MySettings.DeleteAppSetting("SQLServerName");
                TheBaseAssets.MySettings.DeleteAppSetting("SQLUserName");
                TheBaseAssets.MySettings.DeleteAppSetting("SQLPassword");
            }
            temp = TheCommonUtils.CStr(TheBaseAssets.MySettings.GetSetting("SQLDatabaseName"));
            if (!string.IsNullOrEmpty(temp))
                MySQLDefinitions.DatabaseName = temp;
            else
            {
                bool OneStorePerMesh= TheCommonUtils.CBool(TheBaseAssets.MySettings.GetSetting("SQLOneStorePerMesh"));
                if (!OneStorePerMesh)
                    MySQLDefinitions.DatabaseName = $"{TheBaseAssets.MyServiceHostInfo.ApplicationName.Replace(' ', '_')}-{TheCommonUtils.cdeGuidToString(TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID)}";
                else
                    MySQLDefinitions.DatabaseName = $"{TheBaseAssets.MyServiceHostInfo.ApplicationName.Replace(' ', '_')}-{TheScopeManager.GetTokenFromScrambledScopeID()}";
            }
            temp = TheCommonUtils.CStr(TheBaseAssets.MySettings.GetSetting("ConnectionRetryWaitSeconds"));
            if (!string.IsNullOrEmpty(temp)) MySQLDefinitions.ConnectionRetryWaitSeconds = TheCommonUtils.CInt(temp);
            temp = TheCommonUtils.CStr(TheBaseAssets.MySettings.GetSetting("ConnectionRetries"));
            if (!string.IsNullOrEmpty(temp)) MySQLDefinitions.ConnectionRetries = TheCommonUtils.CInt(temp);

            MyStorageCaps.RedundancyFactor = eRedundancyFactor.RF_NOREDUNDANCY;
            temp = TheCommonUtils.CStr(TheBaseAssets.MySettings.GetSetting("ReqStoreRedundancy"));
            if (!string.IsNullOrEmpty(temp)) MyStorageCaps.RedundancyFactor = (eRedundancyFactor)TheCommonUtils.CInt(temp);

            temp = TheCommonUtils.CStr(TheBaseAssets.MySettings.GetSetting("ReqStoreSize"));
            if (!string.IsNullOrEmpty(temp)) MyStorageCaps.StorageCapacity = TheCommonUtils.CInt(temp);
            if (MyStorageCaps.StorageCapacity == 0) MyStorageCaps.StorageCapacity = 100;

            temp = TheCommonUtils.CStr(TheBaseAssets.MySettings.GetSetting("ReqStoreSpeed"));
            if (!string.IsNullOrEmpty(temp)) MyStorageCaps.StorageSpeed = TheCommonUtils.CInt(temp);
            MyStorageCaps.StorageSpeed = 1000;
            MyStorageCaps.ServerURL = TheBaseAssets.MyServiceHostInfo.GetPrimaryStationURL(false);
            if (MySQLDefinitions.ServerName.Equals("."))
                MySQLDefinitions.ServerName = Dns.GetHostName();
            if (MySqlHelperClass == null && !string.IsNullOrEmpty(MySQLDefinitions.ServerName) && !MySQLDefinitions.ServerName.Equals("NONE"))
                MySqlHelperClass = new TheSQLHelper(MySQLDefinitions.UserName, MySQLDefinitions.Password, MySQLDefinitions.ServerName, MySQLDefinitions.DatabaseName, MySQLDefinitions.ConnectionRetryWaitSeconds, MySQLDefinitions.ConnectionRetries);

            bool Success = InitializeStores();
            if (Success)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(442, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseEngine.GetEngineName(), "SQL Server connectivity establised - Storage Service ready", eMsgLevel.l3_ImportantMessage));
            }
            else
            {
                TheBaseAssets.MySYSLOG.WriteToLog(442, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseEngine.GetEngineName(), "SQL Server connectivity FAILED - EdgeStore disabled", eMsgLevel.l1_Error));
            }
            return Success;
        }

        private readonly object lockProcessStorageServiceCommands = new object();
        internal void ProcessStorageServiceCommands(TSM pCommand, Action<TSM> LocalCallBack, bool LocalCallBackOnly)
        {
            TheCommonUtils.cdeRunAsync("Storage-PSSC", true, o =>
            {
                if (MyBaseEngine.GetEngineState().IsSimulated || !TheBaseAssets.MasterSwitch) return;

                DateTimeOffset profStart = DateTimeOffset.Now;
                bool DoProfile = false;

                Guid tConnKey = Guid.Empty;
                string[] tCommand = pCommand.TXT.Split(':');
                string SenderGUID = ""; if (tCommand.Length > 1) SenderGUID = tCommand[1];
                string ProcessingResult = "";
                string tSQLCommand = "";
                string tScrambledScopeID = pCommand.SID; // TheBaseAssets.MyScopeManager.ScopeID; //NEWV4: Use Scrambled Scoped ID ALWAYS from incoming commmand - no longer RealScopeID
                if (string.IsNullOrEmpty(tScrambledScopeID))
                    tScrambledScopeID = TheScopeManager.GetScrambledScopeID();
                switch (tCommand[0])
                {
                    case "CDE_INITIALIZE":
                        if (MyBaseEngine.GetEngineState().IsService)
                        {
                            if (!MyBaseEngine.GetEngineState().IsEngineReady)
                                MyBaseEngine.SetEngineReadiness(true, null);
                            MyBaseEngine.ReplyInitialized(pCommand);
                        }
                        break;
                    case "CDE_GETEDGECAPS":
                        //TheStorageCaps tInquiry = TheCommonUtils.DeserializeJSONStringToObject<TheStorageCaps>(pCommand.PLS); //  TODO: parse request and answer it
                        tSQLCommand = TheCommonUtils.SerializeObjectToJSONString(MyStorageCaps);
                        TSM EdgeCapsMsg = new TSM(MyBaseEngine.GetEngineName(), "CDE_GETEDGECAPSRESULTS:" + SenderGUID, tSQLCommand);
                        if (!LocalCallBackOnly)
                        {
                            if (!string.IsNullOrEmpty(SenderGUID)) // !SenderGUID.Equals("CDE_GETEDGECAPSRESULTS")) //tPublishTo += TSM.AddTarget(pCommand); 
                                TheCommCore.PublishToOriginator(pCommand, EdgeCapsMsg);
                            else
                                TheCommCore.PublishCentral(MyBaseEngine.GetEngineName() + pCommand.AddScopeIDFromTSM(), EdgeCapsMsg);
                        }
                        LocalCallBack?.Invoke(EdgeCapsMsg);
                        DoProfile = true;
                        break;
                    case "CDE_EXECUTESQL":
                        if (!TheBaseAssets.MasterSwitch) return;
                        StorageGetRequest tGetRequestExec = TheCommonUtils.DeserializeJSONStringToObject<StorageGetRequest>(pCommand.PLS); // TheStorageUtilities.StorageGetRequestFromXml
                        if (string.IsNullOrEmpty(SenderGUID)) SenderGUID = tGetRequestExec.UID;
                        if (MySqlHelperClass == null)
                        {
                            ProcessingResult = "No SQL Server Available";
                            TheBaseAssets.MySYSLOG.WriteToLog(442, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseEngine.GetEngineName(), "SQL Server not ready, yet - still trying to initialize", eMsgLevel.l1_Error));
                        }
                        else
                        {
                            if (tGetRequestExec.SFI.Length == 0)
                            {
                                ProcessingResult = "No SQL Statement specified";
                                TheBaseAssets.MySYSLOG.WriteToLog(440, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseEngine.GetEngineName(), ProcessingResult, eMsgLevel.l1_Error));
                            }
                            else
                            {
                                if (tGetRequestExec.UID.Equals("<null>"))
                                {
                                    tSQLCommand = string.Format(tGetRequestExec.SFI, "[dbo].[cdeStorageMap]");
                                    ProcessingResult = TheCommonUtils.CStr(MySqlHelperClass.cdeGetScalarData(tSQLCommand, 0, true, true, ref tConnKey));
                                }
                                else
                                {
                                    long theRealStoreIDExec;
                                    if (storageMapCache != null)
                                    {
                                        StorageDefinition storeEntry = storageMapCache.MyMirrorCache.GetEntryByFunc((def) => def.DID.Equals(tGetRequestExec.DID) && def.UID.Equals(tGetRequestExec.UID.StartsWith(CommonTypeUIDs.cdePUniqueID) ? tCommand[2] : tGetRequestExec.UID));
                                        if (storeEntry != null)
                                            theRealStoreIDExec = TheCommonUtils.CLng(storeEntry.SID);
                                        else
                                            theRealStoreIDExec = 0;
                                    }
                                    else
                                        theRealStoreIDExec = TheCommonUtils.CLng(MySqlHelperClass.cdeGetScalarData($"Select top 1 ID from cdeStorageMap where NodeID='{tGetRequestExec.DID}' and UniqueID='{(tGetRequestExec.UID.StartsWith(CommonTypeUIDs.cdePUniqueID) ? tCommand[2] : tGetRequestExec.UID)}'", 2, false, true, ref tConnKey));
                                    if (theRealStoreIDExec == 0)
                                    {
                                        ProcessingResult = $"StorageTable not found of NodeID:({tGetRequestExec.DID}) and UID:({(tGetRequestExec.UID.StartsWith(CommonTypeUIDs.cdePUniqueID) ? tCommand[2] : tGetRequestExec.UID)}) for EXECUTESQL: " + tGetRequestExec.SFI;
                                        TheBaseAssets.MySYSLOG.WriteToLog(4420, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseEngine.GetEngineName(), ProcessingResult, eMsgLevel.l1_Error));
                                    }
                                    else
                                    {
                                        tSQLCommand = string.Format(tGetRequestExec.SFI, ("[dbo].[" + (tGetRequestExec.UID.StartsWith(CommonTypeUIDs.cdePUniqueID) ? "cdeProperties" : "cdeStore") + theRealStoreIDExec + "]"));
                                        if (!string.IsNullOrEmpty(tScrambledScopeID))
                                        {
                                            if (tSQLCommand.ToLower().Contains("where"))
                                                tSQLCommand += " and cdeSCOPEID='" + TheScopeManager.GetTokenFromScrambledScopeID(tScrambledScopeID) + "'";
                                            else
                                                tSQLCommand += " where cdeSCOPEID='" + TheScopeManager.GetTokenFromScrambledScopeID(tScrambledScopeID) + "'";
                                            if (!string.IsNullOrEmpty(tGetRequestExec.CFI))
                                                tSQLCommand += " and " + tGetRequestExec.CFI;
                                        }
                                        else
                                        {
                                            if (!string.IsNullOrEmpty(tGetRequestExec.CFI))
                                                tSQLCommand += " where " + tGetRequestExec.CFI;
                                        }
                                        ProcessingResult = TheCommonUtils.CStr(MySqlHelperClass.cdeGetScalarData(tSQLCommand, 0, true, true, ref tConnKey));
                                    }
                                }
                                MySqlHelperClass.cdeCloseConnection(ref tConnKey);
                            }
                        }
                        TheBaseAssets.MySYSLOG.WriteToLog(442, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseEngine.GetEngineName(), "SQLEXECUTED:"+ ProcessingResult, eMsgLevel.l3_ImportantMessage));
                        TSM ExecuteMsg = new TSM(MyBaseEngine.GetEngineName(), "SQLEXECUTED:" + SenderGUID, ProcessingResult);
                        if (!LocalCallBackOnly)
                        {
                            if (!SenderGUID.Equals(tGetRequestExec.UID)) // tPublishTo += TSM.AddTarget(pCommand);
                                TheCommCore.PublishToOriginator(pCommand, ExecuteMsg);
                            else
                                TheCommCore.PublishCentral(MyBaseEngine.GetEngineName() + pCommand.AddScopeIDFromTSM(), ExecuteMsg);
                        }
                        LocalCallBack?.Invoke(ExecuteMsg);
                        DoProfile = true;
                        break;
                    case "CDE_CREATENEWSTORE":
                    case "CDE_CREATESTORE":
                        if (!TheBaseAssets.MasterSwitch) return;
                        StorageDefinition tDefinition = TheCommonUtils.DeserializeJSONStringToObject<StorageDefinition>(pCommand.PLS);
                        if (string.IsNullOrEmpty(SenderGUID)) SenderGUID = tDefinition.UID;
                        if (MySqlHelperClass == null)
                        {
                            ProcessingResult = "No SQL Server Available";
                            TheBaseAssets.MySYSLOG.WriteToLog(442, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseEngine.GetEngineName(), "SQL Server not ready, yet - still trying to initialize", eMsgLevel.l1_Error));
                        }
                        else
                        {
                            lock (lockProcessStorageServiceCommands)
                            {
                                long StoreID;
                                if (storageMapCache != null)
                                {
                                    StorageDefinition storeEntry = storageMapCache.MyMirrorCache.GetEntryByFunc((def) => def.DID.Equals(tDefinition.DID) && def.UID.Equals(tDefinition.UID));
                                    if (storeEntry != null)
                                        StoreID = storeEntry.SID;
                                    else
                                        StoreID = 0;
                                }
                                else
                                    StoreID = TheCommonUtils.CLng(MySqlHelperClass.cdeGetScalarData($"Select top 1 ID from cdeStorageMap where NodeID='{tDefinition.DID}' and UniqueID='{tDefinition.UID}'", 2, false, true, ref tConnKey));
                                if (tCommand[0] == "CDE_CREATENEWSTORE" && MySqlHelperClass.DoesTableExist("cdeStore" + StoreID))
                                {
                                    MySqlHelperClass.cdeRunNonQuery("TRUNCATE TABLE [dbo].[cdeStore" + StoreID + "]", TheBaseAssets.MyServiceHostInfo.TO.StoreRequestTimeout, true, true, ref tConnKey);
                                }
                                else
                                {
                                    if ((StoreID == 0 || !MySqlHelperClass.DoesTableExist("cdeStore" + StoreID)) && !UniqueIDs.Contains(tDefinition.UID))
                                    {
                                        UniqueIDs.Add(tDefinition.UID);
                                        if (StoreID == 0)
                                        {
                                            if (!MySqlHelperClass.cdeRunNonQuery($"INSERT INTO cdeStorageMap (StoreDescription, UniqueID, DeviceName, ScopeToken, NodeID, AppID) VALUES ('{tDefinition.DES}', '{tDefinition.UID}', '{tDefinition.NAM}',{TheScopeManager.GetTokenFromScrambledScopeID(tScrambledScopeID)},'{tDefinition.DID}','{TheBaseAssets.MyServiceHostInfo.ApplicationName}');", TheBaseAssets.MyServiceHostInfo.TO.StoreRequestTimeout, true, true, ref tConnKey))
                                            {
                                                ProcessingResult = "Store (INSERT) Could not be created: " + tDefinition.NAM;
                                                TheBaseAssets.MySYSLOG.WriteToLog(444, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseEngine.GetEngineName(), ProcessingResult, eMsgLevel.l1_Error), true);
                                            }
                                            else
                                            {
                                                StoreID = TheCommonUtils.CLng(MySqlHelperClass.cdeGetScalarData("SELECT @@IDENTITY AS 'ID';", 0, true, true, ref tConnKey));

                                                // Add to the storageMap cache 
                                                tDefinition.SID = ++IDCounter;
                                                storageMapCache?.AddAnItem(tDefinition);
                                            }
                                        }
                                        else
                                            TheBaseAssets.MySYSLOG.WriteToLog(445, new TSM(MyBaseEngine.GetEngineName(), string.Format("StoreID={0} in cdeStorageMap but table does not exists...creating", StoreID), eMsgLevel.l2_Warning), true);
                                        if (StoreID == 0)
                                        {
                                            ProcessingResult = "Store (NO STOREID) Could not be created: " + tDefinition.NAM;
                                            TheBaseAssets.MySYSLOG.WriteToLog(446, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseEngine.GetEngineName(), ProcessingResult, eMsgLevel.l1_Error), true);
                                        }
                                        else
                                        {
                                            tSQLCommand = "CREATE TABLE [dbo].[cdeStore" + StoreID + "]([cdeMID] [uniqueidentifier] NOT NULL, [cdeCTIM] [datetimeoffset](7) NOT NULL,[cdeSCOPEID] [nvarchar](50) NULL,";
                                            int fldCnt = 0;
                                            for (int i = 0; i < tDefinition.CNT; i++)
                                            {
                                                if (!tDefinition.FNA[i].ToUpper().Equals("CDEMID") && !tDefinition.FNA[i].ToUpper().Equals("CDECTIM"))
                                                {
                                                    if (fldCnt > 0) tSQLCommand += ",";
                                                    tSQLCommand += "[" + tDefinition.FNA[i] + "] " + MySqlHelperClass.SQLTypeNames[tDefinition.FTY[i]] + " NULL";
                                                    fldCnt++;
                                                }
                                            }
                                            // Add extra columns for TheThingStore (used to more easily retrieve data for Grafana)
                                            if (tDefinition.UID.StartsWith(CommonTypeUIDs.thingStoreUniqueID)) 
                                            {
                                                tSQLCommand += ", [QValue] [float] NULL";

                                                fldCnt++;
                                            }
                                            tSQLCommand += ")"; // ON [PRIMARY]";
                                            MySqlHelperClass.cdeRunNonQuery(tSQLCommand, TheBaseAssets.MyServiceHostInfo.TO.StoreRequestTimeout, true, true, ref tConnKey);
                                            MySqlHelperClass.cdeRunNonQuery("ALTER TABLE [dbo].[cdeStore" + StoreID + "] ADD  CONSTRAINT [DF_cdeStore" + StoreID + "_cdeCTIM]  DEFAULT (SYSDATETIMEOFFSET()) FOR [cdeCTIM]", TheBaseAssets.MyServiceHostInfo.TO.StoreRequestTimeout, true, true, ref tConnKey);
                                            MySqlHelperClass.cdeRunNonQuery("ALTER TABLE [dbo].[cdeStore" + StoreID + "] ADD  CONSTRAINT [DF_cdeStore" + StoreID + "_cdeMID]  DEFAULT (newid()) FOR [cdeMID]", TheBaseAssets.MyServiceHostInfo.TO.StoreRequestTimeout, true, true, ref tConnKey);
                                            //MySqlHelperClass.cdeRunNonQuery("CREATE UNIQUE CLUSTERED INDEX [IDX_Store" + StoreID + "] ON [dbo].[cdeStore" + StoreID + "] ([cdeSCOPEID] asc, [cdeIDX] ASC) WITH (STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF)", TheBaseAssets.MyServiceHostInfo.TO.StoreRequestTimeout, true, true, ref tConnKey);
                                            if (tDefinition.UID.StartsWith(CommonTypeUIDs.thingStoreUniqueID))
                                                MySqlHelperClass.cdeRunNonQuery("CREATE CLUSTERED INDEX [CTIM_Store" + StoreID + "] ON [dbo].[cdeStore" + StoreID + "] ([cdeCTIM] ASC ) WITH (STATISTICS_NORECOMPUTE = OFF, DROP_EXISTING = OFF, ONLINE = OFF) ON [PRIMARY]", TheBaseAssets.MyServiceHostInfo.TO.StoreRequestTimeout, true, true, ref tConnKey);
                                            else
                                                MySqlHelperClass.cdeRunNonQuery("CREATE UNIQUE CLUSTERED INDEX [MID_Store" + StoreID + "] ON [dbo].[cdeStore" + StoreID + "] ([cdeMID] ASC ) WITH (STATISTICS_NORECOMPUTE = OFF, DROP_EXISTING = OFF, ONLINE = OFF) ON [PRIMARY]", TheBaseAssets.MyServiceHostInfo.TO.StoreRequestTimeout, true, true, ref tConnKey);
                                            if (tDefinition.DEF)
                                            {
                                                tSQLCommand = "insert into [dbo].[cdeStore" + StoreID + "] (";
                                                fldCnt = 0;
                                                for (int i = 0; i < tDefinition.CNT; i++)
                                                {
                                                    if (!tDefinition.FNA[i].ToUpper().Equals("CDEMID") && !tDefinition.FNA[i].ToUpper().Equals("CDECTIM") && tDefinition.FDE[i].Length > 0)
                                                    {
                                                        if (fldCnt > 0) tSQLCommand += ",";
                                                        tSQLCommand += tDefinition.FNA[i];
                                                        fldCnt++;
                                                    }
                                                }
                                                tSQLCommand += ") values (";
                                                fldCnt = 0;
                                                for (int i = 0; i < tDefinition.CNT; i++)
                                                {
                                                    if (!tDefinition.FNA[i].ToUpper().Equals("CDEMID") && !tDefinition.FNA[i].ToUpper().Equals("CDECTIM") && tDefinition.FDE[i].Length > 0)
                                                    {
                                                        if (fldCnt > 0) tSQLCommand += ",";
                                                        tSQLCommand += "'" + tDefinition.FDE[i] + "'";
                                                        fldCnt++;
                                                    }
                                                }
                                                tSQLCommand += ")";
                                            }
                                            TheBaseAssets.MySYSLOG.WriteToLog(447, new TSM(MyBaseEngine.GetEngineName(), "Creating table: [cdeStore" + StoreID + "]", eMsgLevel.l7_HostDebugMessage), true);
                                        }
                                    }
                                    else
                                    {
                                        ProcessingResult = "Table [cdeStore" + StoreID + "] exists...returning StoreID";
                                        TheBaseAssets.MySYSLOG.WriteToLog(448, new TSM(MyBaseEngine.GetEngineName(), ProcessingResult, eMsgLevel.l7_HostDebugMessage), true);
                                    }
                                }

                                Type PropertyBagElementType = null;
                                if (tDefinition.UID.StartsWith(CommonTypeUIDs.thingUniqueID))
                                    PropertyBagElementType = TheStorageUtilities.GetPropertyBagRecordType(typeof(TheThing));
             
                                if (PropertyBagElementType != null)
                                {
                                    // Now create table for all properties of the store
                                    if (StoreID != 0 && !MySqlHelperClass.DoesTableExist("cdeProperties" + StoreID))
                                    {
                                        string tSQLCreate = "CREATE TABLE [dbo].[cdeProperties" + StoreID + "]([cdePID] [int] IDENTITY(1,1) NOT NULL,[cdeMID] [uniqueidentifier] NOT NULL, [cdeCTIM] [datetimeoffset](7) NOT NULL,[cdeSCOPEID] [nvarchar](50) NULL,";
                                        string UniqueID;
                                        string serializedStore = TheStorageUtilities.SerializeCreateStore(PropertyBagElementType, null, $"cdeProperties{StoreID}", $"Stores the properties for each Thing", out UniqueID, null);
                                        StorageDefinition tDefinition2 = TheCommonUtils.DeserializeJSONStringToObject<StorageDefinition>(serializedStore);
                                        int fldCnt2 = 0;
                                        for (int i = 0; i < tDefinition2.CNT; i++)
                                        {
                                            if (!tDefinition2.FNA[i].ToUpper().Equals("CDEMID") && !tDefinition2.FNA[i].ToUpper().Equals("CDECTIM"))
                                            {
                                                if (fldCnt2 > 0) tSQLCreate += ",";
                                                tSQLCreate += "[" + tDefinition2.FNA[i] + "] " + MySqlHelperClass.SQLTypeNames[tDefinition2.FTY[i]] + " NULL";
                                                fldCnt2++;
                                            }
                                        }
                                        tSQLCreate += ")";
                                        MySqlHelperClass.cdeRunNonQuery(tSQLCreate, TheBaseAssets.MyServiceHostInfo.TO.StoreRequestTimeout, true, true, ref tConnKey);
                                    }
                                    else
                                    {
                                        TheBaseAssets.MySYSLOG.WriteToLog(448, new TSM(MyBaseEngine.GetEngineName(), "Table [cdeProperties" + StoreID + "] already exists", eMsgLevel.l7_HostDebugMessage), true);
                                    }
                                }
                                MySqlHelperClass.cdeCloseConnection(ref tConnKey);
                            }
                        }
                        TSM CreateStoreMsg = new TSM(MyBaseEngine.GetEngineName(), "STORECREATED:" + SenderGUID, tDefinition.UID);
                        //CreateStoreMsg.SetUnsubscribeAfterPublish(!SenderGUID.Equals(tDefinition.UID));
                        if (!LocalCallBackOnly)
                        {
                            if (!SenderGUID.Equals(tDefinition.UID))//tPublishTo += TSM.AddTarget(pCommand); 
                                TheCommCore.PublishToOriginator(pCommand, CreateStoreMsg);
                            else
                                TheCommCore.PublishCentral(MyBaseEngine.GetEngineName() + pCommand.AddScopeIDFromTSM(), CreateStoreMsg);
                        }
                        LocalCallBack?.Invoke(CreateStoreMsg);
                        DoProfile = true;
                        break;
                    case "CDE_EDGESTORE":
                        if (!TheBaseAssets.MasterSwitch) return;
                        TheDataRetrievalRecord tDataStoreGramm = TheCommonUtils.DeserializeJSONStringToObject<TheDataRetrievalRecord>(pCommand.PLS);
                        if (string.IsNullOrEmpty(SenderGUID)) SenderGUID = tDataStoreGramm.UID;
                        if (MySqlHelperClass == null)
                        {
                            ProcessingResult = "No SQL Server Available";
                            TheBaseAssets.MySYSLOG.WriteToLog(442, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseEngine.GetEngineName(), "SQL Server not ready, yet - still trying to initialize", eMsgLevel.l1_Error));
                        }
                        else
                        {
                            long theRealStoreID;
                            if (storageMapCache != null)
                            {
                                StorageDefinition storeEntry = storageMapCache.MyMirrorCache.GetEntryByFunc((def) => def.DID.Equals(tDataStoreGramm.DID) && def.UID.Equals(tDataStoreGramm.UID.StartsWith(CommonTypeUIDs.cdePUniqueID) ? tCommand[2] : tDataStoreGramm.UID));
                                if (storeEntry != null)
                                    theRealStoreID = TheCommonUtils.CLng(storeEntry.SID);
                                else
                                    theRealStoreID = 0;
                            }
                            else
                                theRealStoreID = TheCommonUtils.CLng(MySqlHelperClass.cdeGetScalarData($"Select top 1 ID from cdeStorageMap where NodeID='{tDataStoreGramm.DID}' and UniqueID='{(tDataStoreGramm.UID.StartsWith(CommonTypeUIDs.cdePUniqueID) ? tCommand[2] : tDataStoreGramm.UID)}'", 2, false, false, ref tConnKey));
                            if (theRealStoreID == 0)
                            {
                                if (tDataStoreGramm.CMD == eSCMD.CreateInsert)
                                {
                                }
                                ProcessingResult = "StorageTable not found for Storing: " + tDataStoreGramm.UID;
                                TheBaseAssets.MySYSLOG.WriteToLog(450, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseEngine.GetEngineName(), ProcessingResult, eMsgLevel.l1_Error, pCommand.PLS), true);
                            }
                            if (theRealStoreID != 0)
                            {
                                TheInsertQueueItem tItem = new TheInsertQueueItem
                                {
                                    DataStoreGramm = tDataStoreGramm,
                                    RealStoreID = theRealStoreID,
                                    LocalCallback = LocalCallBack,
                                    SScopeID = tScrambledScopeID,
                                    LocalCallbackOnly = LocalCallBackOnly
                                };
                                if (!SenderGUID.Equals(tDataStoreGramm.UID))
                                    tItem.DirectTarget = pCommand.GetOriginator();

                                if (tDataStoreGramm.UID.StartsWith(CommonTypeUIDs.thingStoreUniqueID))
                                {
                                    // Find any non-standard TFD properties (usually in the PB) that may not have table columns yet
                                    List<TFD> pbDefinitions = tDataStoreGramm.FLDs.Where(fld => !typeof(TheThingStore).GetProperties().Any(prop => prop.Name.Equals(fld.N))).ToList();
                                    string alterExpr = "";
                                    for (int i = 0; i < pbDefinitions.Count; i++)
                                    {
                                        if (!MySqlHelperClass.DoesColumnExist("cdeStore" + theRealStoreID, pbDefinitions[i].N))
                                        {
                                            if (alterExpr.Length>0) alterExpr += ", ";
                                            alterExpr += $"[{pbDefinitions[i].N}] {MySqlHelperClass.SQLTypeNames[TheStorageUtilities.GetTypeID(pbDefinitions[i].T)]}";
                                        }
                                    }
                                    if (!string.IsNullOrEmpty(alterExpr))
                                        MySqlHelperClass.cdeRunNonQuery($"ALTER TABLE [dbo].[cdeStore{theRealStoreID}] ADD {alterExpr}", false, false, ref tConnKey);
                                }
                                MySqlHelperClass.cdeUpdateInsertRecord(tItem);
                                break;
                            }
                            tSQLCommand = ProcessingResult;
                        }
                        TheDataRetrievalRecord tResultRecord = new TheDataRetrievalRecord { ERR = ProcessingResult };
                        TSM SendToClients = new TSM(MyBaseEngine.GetEngineName(), "DATAWASUPDATED:" + tDataStoreGramm.MID,TheCommonUtils.SerializeObjectToJSONString(tResultRecord));
                        SendToClients.SetNotToSendAgain(true);
                        if (!LocalCallBackOnly)
                        {
                            if (!SenderGUID.Equals(tDataStoreGramm.UID))
                            {
                                //tPublishTo += TSM.AddTarget(pCommand); 
                                TheCommCore.PublishToOriginator(pCommand, SendToClients);
                                TheCommCore.PublishCentral(tDataStoreGramm.UID + pCommand.AddScopeIDFromTSM(), SendToClients);
                            }
                        }
                        LocalCallBack?.Invoke(SendToClients);
                        DoProfile = true;
                        break;
                    case "CDE_EDGEGET":
                        if (!TheBaseAssets.MasterSwitch) return;
                        StorageGetRequest tGetRequest = TheCommonUtils.DeserializeJSONStringToObject<StorageGetRequest>(pCommand.PLS); // TheStorageUtilities.StorageGetRequestFromXml(pCommand.PLS);
                        if (string.IsNullOrEmpty(SenderGUID)) SenderGUID = tGetRequest.UID;
                        if (MySqlHelperClass == null)
                        {
                            ProcessingResult = "No SQL Server Available";
                            TheBaseAssets.MySYSLOG.WriteToLog(442, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseEngine.GetEngineName(), "SQL Server not ready, yet - still trying to initialize", eMsgLevel.l1_Error));
                        }
                        else
                        {
                            DataSet ds = null;
                            tSQLCommand = "select";
                            if (tGetRequest.TOP > 0)
                                tSQLCommand += " top " + tGetRequest.TOP.ToString();
                            bool IsRequestingPage = tGetRequest.PAG != 0 && tGetRequest.TOP > 0;
                            if (tGetRequest.UID.Equals("<null>"))
                            {
                                tSQLCommand += " * from cdeStorageMap";
                                if(!string.IsNullOrEmpty(tGetRequest.SFI))
                                {
                                    // Might want to call filter conversion commands here
                                    tSQLCommand += " where " + tGetRequest.SFI;
                                }
                            }
                            else
                            {
                                long theRealStoreID2;
                                if (storageMapCache != null)
                                {
                                    StorageDefinition storeEntry = storageMapCache.MyMirrorCache.GetEntryByFunc((def) => def.DID.Equals(tGetRequest.DID) && def.UID.Equals(tGetRequest.UID.StartsWith(CommonTypeUIDs.cdePUniqueID) ? tCommand[2] : tGetRequest.UID));
                                    if (storeEntry != null)
                                        theRealStoreID2 = TheCommonUtils.CLng(storeEntry.SID);
                                    else
                                        theRealStoreID2 = 0;
                                }
                                else
                                    theRealStoreID2 = TheCommonUtils.CLng(MySqlHelperClass.cdeGetScalarData($"Select top 1 ID from cdeStorageMap where NodeID='{tGetRequest.DID}' and UniqueID='{(tGetRequest.UID.StartsWith(CommonTypeUIDs.cdePUniqueID) ? tCommand[2] : tGetRequest.UID)}'", 2, false, true, ref tConnKey));
                                if (theRealStoreID2 == 0)
                                {
                                    ProcessingResult = "StorageTable not found for reading: " + tGetRequest.UID;
                                    TheBaseAssets.MySYSLOG.WriteToLog(452, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseEngine.GetEngineName(), ProcessingResult, eMsgLevel.l1_Error));
                                }
                                else
                                {
                                    string ColSelector = "*";
                                    if (!string.IsNullOrEmpty(tGetRequest.CFI))
                                        ColSelector = $"{tGetRequest.CFI},cdeCTIM,cdeMID,cdeN,cdePRI,cdeAVA,cdeEXP,cdeSCOPEID";

                                    if (IsRequestingPage)
                                    {
                                        string indexName = "cdeMID";
                                        if (tGetRequest.UID.StartsWith(CommonTypeUIDs.thingStoreUniqueID))
                                            indexName = "cdeCTIM";
                                        string tSqlCmd2 = "Select count(" + indexName + ") from [dbo].[" + (tGetRequest.UID.StartsWith(CommonTypeUIDs.cdePUniqueID) ? "cdeProperties" : "cdeStore") + theRealStoreID2  + "]";
                                        if (!string.IsNullOrEmpty(tScrambledScopeID))
                                            tSqlCmd2 += " where cdeSCOPEID='" + TheScopeManager.GetTokenFromScrambledScopeID(tScrambledScopeID) + "'";
                                        if (tGetRequest.SFI.Length > 0)
                                        {
                                            if (string.IsNullOrEmpty(tScrambledScopeID))
                                                tSqlCmd2 += " where " + tGetRequest.SFI;
                                            else
                                                tSqlCmd2 += " and (" + tGetRequest.SFI + ")";
                                        }
                                        long tTotalRows = TheCommonUtils.CLng(MySqlHelperClass.cdeGetScalarData(tSqlCmd2, 0, true, true, ref tConnKey));
                                        if (tGetRequest.PAG < 0)
                                            tGetRequest.PAG = (int)(tTotalRows / tGetRequest.TOP);
                                        else
                                        {
                                            if (tGetRequest.PAG * tGetRequest.TOP > tTotalRows)
                                            {
                                                ProcessingResult = string.Format("Requested Page {0} larger than available pages {1}", tGetRequest.PAG, (int)(tTotalRows / tGetRequest.TOP));
                                                TheBaseAssets.MySYSLOG.WriteToLog(453, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(MyBaseEngine.GetEngineName(), ProcessingResult, eMsgLevel.l1_Error));
                                            }
                                        }
                                        tSQLCommand += $" {ColSelector} from ( select {ColSelector},cdeROWID=ROW_Number() OVER (order by ";
                                        if (tGetRequest.SOR.Length > 0)
                                            tSQLCommand += tGetRequest.SOR + ")";
                                        else
                                            tSQLCommand += "cdeSCOPEID," + (tGetRequest.UID.StartsWith(CommonTypeUIDs.cdePUniqueID) ? "cdePID" : indexName) + ")";
                                    }
                                    else
                                    {
                                        tSQLCommand += $" {ColSelector}";
                                    }
                                    tSQLCommand += " from [dbo].[" + (tGetRequest.UID.StartsWith(CommonTypeUIDs.cdePUniqueID) ? "cdeProperties" : "cdeStore") + theRealStoreID2 + "]";
                                    if (!string.IsNullOrEmpty(tScrambledScopeID))
                                        tSQLCommand += " where cdeSCOPEID='" + TheScopeManager.GetTokenFromScrambledScopeID(tScrambledScopeID) + "'";
                                    if (tGetRequest.SFI.Length > 0)
                                    {
                                        // For charts: If a time based filter is used, use a special query to retrieve TOP # of records
                                        // stretched out over the entire dataset
                                        string[] timeFilters = new string[] { "LMI", "LHO", "LDA", "LWE", "LYE" };
                                        bool isTimeFilter = timeFilters.Contains(tGetRequest.SFI) || tGetRequest.SFI.StartsWith("LSP");
                                        List<SQLFilter> filters = TheStorageUtilities.CreateFilter(tGetRequest.SFI);
                                        string combinedFilter = ConvertFiltersToString(filters);
                                        if (tGetRequest.UID.StartsWith(CommonTypeUIDs.thingUniqueID))
                                            tGetRequest.SFI = ConvertSFIToPropertyBagQuery(combinedFilter, typeof(TheThing), theRealStoreID2);
                                        else
                                            tGetRequest.SFI = combinedFilter;
                                        if (isTimeFilter)
                                        {
                                            tSQLCommand = $"Select * from ( Select ROW_NUMBER() over (Partition by Chunk Order by cdeCTIM) as rank, * from ( Select NTILE({tGetRequest.TOP}) over (Order by cdeCTIM) as Chunk, {ColSelector}";
                                            tSQLCommand += $" from [dbo].[cdeStore{theRealStoreID2}] where {tGetRequest.SFI}";
                                            if (!string.IsNullOrEmpty(tScrambledScopeID))
                                                tSQLCommand += $" and cdeSCOPEID='{TheScopeManager.GetTokenFromScrambledScopeID(tScrambledScopeID)}'";
                                            tSQLCommand += ") T1 ) T2 where rank = 1";
                                        }
                                        else
                                        {
                                            if (string.IsNullOrEmpty(tScrambledScopeID) && !isTimeFilter)
                                                tSQLCommand += " where " + tGetRequest.SFI;
                                            else
                                                tSQLCommand += " and (" + tGetRequest.SFI + ")";
                                        }
                                    }
                                    if (IsRequestingPage)
                                        tSQLCommand += ") cdeTempTable where cdeTempTable.cdeROWID>=" + tGetRequest.PAG * tGetRequest.TOP;
                                    if (!string.IsNullOrEmpty(tGetRequest.GRP))
                                        tSQLCommand += TheStorageUtilities.ValidateGroupByClause(tGetRequest.GRP, tGetRequest.CFI, tGetRequest.UID, new string[] { "COUNT", "AVG", "SUM", "MIN", "MAX" });
                                }
                                MySqlHelperClass.cdeCloseConnection(ref tConnKey);
                            }
                            if (ProcessingResult.Length == 0)
                            {
                                if (tGetRequest.SOR.Length > 0 && !IsRequestingPage)
                                    tSQLCommand += " order by " + tGetRequest.SOR;
                                ds = MySqlHelperClass.cdeOpenDataSet(tSQLCommand, 0, TheBaseAssets.MyServiceHostInfo.TO.StoreRequestTimeout, false, false, ref tConnKey);
                                if (ds == null)
                                    ProcessingResult = tSQLCommand + " returned: NULL";
                                else
                                {
                                    if (ds.Tables[0].Rows.Count == 0)
                                        ProcessingResult = tSQLCommand + " returned: " + ds.Tables[0].Rows.Count + " records";
                                }
                            }
                            ProcessingResult = SeralizeDataSet(ref tGetRequest, ds, tSQLCommand, ProcessingResult);
                        }
                        //MySqlHelperClass.SSECloseConnection(ref tConnKey);
                        TSM EdgeGetMsg = new TSM(MyBaseEngine.GetEngineName(), "DATARETREIVED:" + SenderGUID, ProcessingResult);
                        EdgeGetMsg.SetNotToSendAgain(true);
                        if (!LocalCallBackOnly)
                        {
                            if (!SenderGUID.Equals(tGetRequest.UID)) //tPublishTo += TSM.AddTarget(pCommand);
                                TheCommCore.PublishToOriginator(pCommand, EdgeGetMsg);
                            else
                                TheCommCore.PublishCentral(MyBaseEngine.GetEngineName() + pCommand.AddScopeIDFromTSM(), EdgeGetMsg);
                        }
                        LocalCallBack?.Invoke(EdgeGetMsg);
                        DoProfile = true;
                        break;
                    case "CDE_GETSTOREBLOB":
                        string[] tBlobParts = SenderGUID.Split(';');
                        string fileToReturn =
                            TheCommonUtils.cdeFixupFileName(tBlobParts[0].Contains("?")
                                ? tBlobParts[0].Substring(0, tBlobParts[0].IndexOf("?", StringComparison.Ordinal))
                                : tBlobParts[0]);
                        byte[] tBlobBuffer = null;
                        if (File.Exists(fileToReturn))
                        {
                            DateTimeOffset fd = File.GetLastAccessTime(fileToReturn);
                            if (tBlobParts.Length > 1 && TheCommonUtils.CLng(tBlobParts[1]) - fd.DateTime.ToFileTimeUtc() >= 0)
                            {
                                ProcessingResult = string.Format("Request for Blob {0} not sent - file is up to date {1}", SenderGUID, fd);
                                TheBaseAssets.MySYSLOG.WriteToLog(454, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(MyBaseEngine.GetEngineName(), ProcessingResult, eMsgLevel.l6_Debug), true);
                                if (!LocalCallBackOnly) TheCommCore.PublishCentral("CDE_SYSTEMWIDE" + pCommand.AddScopeIDFromTSM(), new TSM(MyBaseEngine.GetEngineName(), "CDE_STOREBLOBREQUESTED:" + tBlobParts[0], tBlobParts[0].Substring(tBlobParts[0].Length - 3, 3) + ";" + ProcessingResult));
                                return;
                            }
                            TheBaseAssets.MySYSLOG.WriteToLog(454, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseEngine.GetEngineName(), string.Format("Sending Blob {0}  to {1} - FileDate: {2}", fileToReturn, SenderGUID, fd), eMsgLevel.l6_Debug), true);
                            Stream fileStream = new FileStream(fileToReturn, FileMode.Open, FileAccess.Read);
#if !CDE_NET35
                            using (MemoryStream ms = new MemoryStream())
                            {
                                fileStream.CopyTo(ms);
                                tBlobBuffer = ms.ToArray();
                            }
#else
                    byte[] buffer = new byte[64000];
                    using (MemoryStream ms = new MemoryStream())
                    {
                        int read;
                        while ((read = fileStream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            ms.Write(buffer, 0, read);
                        }
                        tBlobBuffer=ms.ToArray();
                    }
#endif
                            ProcessingResult = string.Format("Sending Blob {0} Bytes={1}", fileToReturn, tBlobBuffer.Length);
                            TheBaseAssets.MySYSLOG.WriteToLog(454, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(MyBaseEngine.GetEngineName(), ProcessingResult, eMsgLevel.l6_Debug), true);
                            fileStream.Close();
                            ProcessingResult = TheCommonUtils.GetDateTimeString(fd);
                        }
                        else
                        {
                            ProcessingResult = string.Format("Error requesting Blob {0} - File does not exist", fileToReturn);
                            TheBaseAssets.MySYSLOG.WriteToLog(454, new TSM(MyBaseEngine.GetEngineName(), ProcessingResult, eMsgLevel.l1_Error), true);
                        }
                        TSM BlobGetRequest = new TSM(MyBaseEngine.GetEngineName(), "CDE_STOREBLOBREQUESTED:" + tBlobParts[0], tBlobParts[0].Substring(tBlobParts[0].Length - 3, 3) + ";" + ProcessingResult) {PLB = tBlobBuffer};
                        BlobGetRequest.SetNotToSendAgain(true);
                        if (!LocalCallBackOnly)
                        {
                            //tPublishTo="CDE_SYSTEMWIDE" + TheCommonUtils.AddScopeIDFromTSM(pCommand);   //TODO: Check if we can use this for all storage telegrams!
                            //tPublishTo += TSM.AddTarget(pCommand);
                            TheCommCore.PublishToOriginator(pCommand, BlobGetRequest);
                        }
                        LocalCallBack?.Invoke(BlobGetRequest);

                        break;
                    case "CDE_UPDATESTORE":
                        if (!TheBaseAssets.MasterSwitch) return;
                        StorageDefinition tUpdateDefinition = TheCommonUtils.DeserializeJSONStringToObject<StorageDefinition>(pCommand.PLS);
                        if (string.IsNullOrEmpty(SenderGUID)) SenderGUID = tUpdateDefinition.UID;
                        if (MySqlHelperClass == null)
                        {
                            ProcessingResult = "No SQL Server Available";
                            TheBaseAssets.MySYSLOG.WriteToLog(455, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseEngine.GetEngineName(), "SQL Server not ready, yet - still trying to initialize", eMsgLevel.l1_Error));
                        }
                        else
                        {
                            lock (lockProcessStorageServiceCommands)
                            {
                                if(MySqlHelperClass.DoesTableExist("cdeStorageMap"))
                                {
                                    string SQLCommand = "UPDATE [dbo].[cdeStorageMap] SET ";
                                    if (tUpdateDefinition.NAM != null)
                                        SQLCommand += "DeviceName = '" + tUpdateDefinition.NAM + "'";
                                    if(tUpdateDefinition.DES != null)
                                    {
                                        if (tUpdateDefinition.NAM != null)
                                            SQLCommand += ", ";
                                        SQLCommand += "StoreDescription = '" + tUpdateDefinition.DES + "'";
                                    }
                                    // If other columns can be updated add them here
                                    // Note: If UID is empty or null, the update will be applied to every row
                                    if (!string.IsNullOrEmpty(tUpdateDefinition.UID))
                                    {
                                        SQLCommand += "WHERE UniqueID = '" + tUpdateDefinition.UID + "' AND NodeID = '" + tUpdateDefinition.DID + "'";
                                    }
                                    MySqlHelperClass.cdeRunNonQuery(SQLCommand, TheBaseAssets.MyServiceHostInfo.TO.StoreRequestTimeout, false, false, ref tConnKey);
                                    if(storageMapCache != null)
                                    {
                                        StorageDefinition storeEntry = storageMapCache.MyMirrorCache.GetEntryByFunc((def) => def.DID.Equals(tUpdateDefinition.DID) && def.UID.Equals(tUpdateDefinition.UID));
                                        if (storeEntry != null) {
                                            if (tUpdateDefinition.NAM != null)
                                                storeEntry.NAM = tUpdateDefinition.NAM;
                                            if (tUpdateDefinition.DES != null)
                                                storeEntry.DES = tUpdateDefinition.DES;
                                            storageMapCache.UpdateItem(storeEntry);
                                        }
                                    }
                                }
                                else
                                {
                                    TheBaseAssets.MySYSLOG.WriteToLog(456, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseEngine.GetEngineName(), "SQL Storage Map table does not exist", eMsgLevel.l1_Error));
                                }
                            }
                        }
                        TSM UpdateStoreMsg = new TSM(MyBaseEngine.GetEngineName(), "STOREUPDATED:" + SenderGUID, tUpdateDefinition.UID);
                        if (!LocalCallBackOnly)
                        {
                            if (!SenderGUID.Equals(tUpdateDefinition.UID))
                                TheCommCore.PublishToOriginator(pCommand, UpdateStoreMsg);
                            else
                                TheCommCore.PublishCentral(MyBaseEngine.GetEngineName() + pCommand.AddScopeIDFromTSM(), UpdateStoreMsg);
                        }
                        LocalCallBack?.Invoke(UpdateStoreMsg);
                        DoProfile = true;
                        break;
                }

                if (DoProfile && TheBaseAssets.MyServiceHostInfo.DebugLevel > eDEBUG_LEVELS.ESSENTIALS)
                {
                    int profTime = DateTimeOffset.Now.Subtract(profStart).Milliseconds;
                    eMsgLevel tLev = eMsgLevel.l7_HostDebugMessage;
                    if (profTime > 100) tLev = eMsgLevel.l2_Warning;
                    if (profTime > 900) tLev = eMsgLevel.l1_Error;
                    TheBaseAssets.MySYSLOG.WriteToLog(454, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(MyBaseEngine.GetEngineName(), string.Format("Profiling {1} Time = {0} ms", profTime, pCommand.TXT), tLev, tSQLCommand), true);
                }
            });
        }

        private string SeralizeDataSet(ref StorageGetRequest tGetRequest, DataSet ds, string sql,string AddError)
        {
            TheDataRetrievalRecord tSendArray = new TheDataRetrievalRecord
            {
                UID = tGetRequest.UID,
                MID = tGetRequest.MID,
                SFI = tGetRequest.SFI,
                SOR = tGetRequest.SOR,
                CFI = tGetRequest.CFI,
                PAG = tGetRequest.PAG,
                GRP = tGetRequest.GRP,
                DID = tGetRequest.DID
            };
            if (ds == null)
                tSendArray.ERR = "Error Retreiving the DataSet " + AddError;
            else
            {
                tSendArray.ERR = "";
                tSendArray.CMD = eSCMD.Read;
                if (ds.Tables.Count==0 || ds.Tables[0].Rows.Count == 0)
                {
                    tSendArray.ERR = tGetRequest.SFI + ":No Records Found:" + sql;
                }
                else
                {
                    for (int j = 0; j < ds.Tables[0].Columns.Count; j++)
                    {
                        if (ds.Tables[0].Columns[j].ColumnName.Equals("cdeSCOPEID")) continue;
                        TFD tFieldDesc = new TFD
                        {
                            N = ds.Tables[0].Columns[j].ColumnName,
                            T = ds.Tables[0].Columns[j].DataType.Name,
                            C = j
                        };
                        tSendArray.FLDs.Add(tFieldDesc);
                    }
                    for (int i = 0; i < ds.Tables[0].Rows.Count; i++)
                    {
                        List<string> tRec = new List<string>();
                        for (int j = 0; j < ds.Tables[0].Rows[i].ItemArray.Length; j++)
                        {
                            if (ds.Tables[0].Columns[j].ColumnName.Equals("cdeSCOPEID")) continue;
                            string tVal = "<null>";
                            if (ds.Tables[0].Rows[i][j] != null)
                            {
                                switch (ds.Tables[0].Columns[j].DataType.Name)
                                {
                                    case "Byte[]":
                                        if (!(ds.Tables[0].Rows[i][j] is DBNull))
                                            tVal = Convert.ToBase64String((byte[])ds.Tables[0].Rows[i][j]);
                                        break;
                                    case "Char[]":
                                        tVal=new string((Char[])ds.Tables[0].Rows[i][j]);
                                        break;
                                    case "DateTimeOffset":
                                        tVal = String.Format("{0:yyyy-MM-dd HH:mm:ss.fff}", ds.Tables[0].Rows[i][j]);
                                        break;
                                    default:
                                        tVal = ds.Tables[0].Rows[i][j].ToString();
                                        break;
                                }
                            }
                            tRec.Add(string.Format("{0:000}{1}", j,tVal));
                        }
                        tSendArray.RECs.Add(tRec);
                    }
                }
            }
            string res = TheCommonUtils.SerializeObjectToJSONString(tSendArray);
            return res;
        }

        private bool CreateStorageDAT()
        {
            if (MySqlHelperClass == null) return true;
            bool retVal = true;
            //if (!MySqlHelperClass.DoesTableExist("cdeStorageMap"))    //With Multiple Nodes this table will exists multiple times
            {
                Guid tConnKey = Guid.Empty;
                if (!MySQLDefinitions.ServerName.Equals("AZURE"))
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(8888, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null: new TSM(MyBaseEngine.GetEngineName(), "Trying to locate SQL Server...", eMsgLevel.l3_ImportantMessage));
                    retVal = MySqlHelperClass.cdeRunNonQuery($"USE [{MySQLDefinitions.DatabaseName}]", false, false, ref tConnKey);
                    if (!retVal)
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(8888, new TSM(MyBaseEngine.GetEngineName(), "SQL Server found but DB not created, yet. Creating Database...", eMsgLevel.l3_ImportantMessage));
                        string CusConn = MySqlHelperClass.MyConnectionString.Substring(0, MySqlHelperClass.MyConnectionString.IndexOf(";Database=", StringComparison.Ordinal));
                        retVal = MySqlHelperClass.cdeRunNonQuery(CusConn, "USE [Master]", 0, false, true, ref tConnKey);
                        retVal = MySqlHelperClass.cdeRunNonQuery(CusConn, $"create Database [{MySQLDefinitions.DatabaseName}]", 0, true, false, ref tConnKey);
                        if (!retVal)
                            TheBaseAssets.MySYSLOG.WriteToLog(8888, new TSM(MyBaseEngine.GetEngineName(), "SQL Server cloud not create the Database. StorageService will terminate", eMsgLevel.l1_Error));
                    }
                    if (retVal)
                    {
                        tConnKey = Guid.Empty;
                        int retry = 0;
                        do
                        {
                            retVal = MySqlHelperClass.cdeRunNonQuery($"USE [{MySQLDefinitions.DatabaseName}]", false, true, ref tConnKey);
                            if (!retVal)
                            {
                                retry++;
                                TheCommonUtils.SleepOneEye(5000, 100);
                            }
                        } while (retry < 4 && !retVal); //CM: Retries are required because database might not be ready yet
                    }
                }
                if (retVal)
                {
                    if (!MySqlHelperClass.DoesTableExist("cdeStorageMap"))
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(8888, new TSM(MyBaseEngine.GetEngineName(), "StorageMap not created, yet. Creating StorageMap...", eMsgLevel.l3_ImportantMessage));
                        retVal = MySqlHelperClass.cdeRunNonQuery("SET ANSI_NULLS ON", true, true, ref tConnKey);
                        retVal = MySqlHelperClass.cdeRunNonQuery("SET QUOTED_IDENTIFIER ON", true, true, ref tConnKey);
                        retVal = MySqlHelperClass.cdeRunNonQuery("CREATE TABLE [dbo].[cdeStorageMap](" +
                            "[ID] [int] IDENTITY(1,1) NOT NULL," +
                            "[StoreDescription] [nvarchar](255) NULL," +
                            "[UniqueID] [nvarchar](255) NOT NULL," +
                            "[DeviceName] [nvarchar](100) NULL," +
                            "[CreationDate] [datetimeoffset] NULL," +
                            "[NodeID] [uniqueidentifier] NULL," +
                            "[ScopeToken] [nvarchar](50) NULL," +
                            "[AppID] [char](50) NOT NULL" +
                            ") ", true, true, ref tConnKey); //ON [PRIMARY]
                        retVal = MySqlHelperClass.cdeRunNonQuery("ALTER TABLE [dbo].[cdeStorageMap] ADD  CONSTRAINT [DF_cdeStorageMap_UniqueID]  DEFAULT (newid()) FOR [UniqueID]", true, true, ref tConnKey);
                        if (!MySqlHelperClass.cdeRunNonQuery("ALTER TABLE [dbo].[cdeStorageMap] ADD  CONSTRAINT [DF_cdeStorageMap_CreationDate]  DEFAULT (SYSDATETIMEOFFSET()) FOR [CreationDate]", true, false, ref tConnKey))
                            TheBaseAssets.MySYSLOG.WriteToLog(455, new TSM(MyBaseEngine.GetEngineName(), "cdeStorageMap could not be created", eMsgLevel.l1_Error));
                        retVal = MySqlHelperClass.cdeRunNonQuery("CREATE UNIQUE CLUSTERED INDEX [IDX_cdeStorageMap_UniqueID] ON [dbo].[cdeStorageMap] ([AppID] ASC, [ScopeToken] ASC, [NodeID] ASC, [UniqueID] ASC) WITH (STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF)", false, false, ref tConnKey);
                    }
                }
                // Cache the storageMap for later use - reduce SQL Server roundtrips
                if (!CreateStorageMapCache())
                    storageMapCache = null;
                else
                {
                    IDCounter = TheCommonUtils.CLng(MySqlHelperClass.cdeGetScalarData("SELECT MAX(ID) FROM [dbo].[cdeStorageMap]", 0, false, true, ref tConnKey));
                    DataSet ds = MySqlHelperClass.cdeOpenDataSet("SELECT * FROM [dbo].[cdeStorageMap]", 0, TheBaseAssets.MyServiceHostInfo.TO.StoreRequestTimeout, true, false, ref tConnKey);
                    FillStorageMapCache(ds, ref tConnKey);
                }
            }
            return retVal;
        }

        private string ConvertFiltersToString(List<SQLFilter> pFilter)
        {
            string tSqlFilterString = "";
            foreach (var f in pFilter)
            {
                string tJoin = "";
                if (tSqlFilterString.Length > 0)
                    tJoin = " and ";
                string fValue = f.Value.ToString();
                string fPropertyName = f.PropertyName;
                string op = ">";
                switch (f.Operation)
                {
                    case FilterOp.GreaterThan: op = ">"; break;
                    case FilterOp.Equals: op = "="; break;
                    case FilterOp.LessThan: op = "<"; break;
                    case FilterOp.GreaterThanOrEqual: op = ">="; break;
                    case FilterOp.LessThanOrEqual: op = "<="; break;
                    case FilterOp.StartsWith:
                        op = "LIKE";
                        fValue += "%";
                        break;
                    case FilterOp.EndsWith:
                        op = "LIKE";
                        fValue = "%" + fValue;
                        break;
                    case FilterOp.Contains:
                        op = "LIKE";
                        fValue = "%" + fValue + "%";
                        break;
                    default:
                        continue; 
                }
                if (f.Value is DateTimeOffset)  //maybe other types need checking too?
                {
                    //tSqlFilterString += $"CONVERT(time, {f.PropertyName}) {op} '{TheCommonUtils.CDate(f.Value).ToString("HH:mm:ss")}' and ";
                    //tSqlFilterString += $"CONVERT(date, {f.PropertyName}) {op}{(op.Contains('=') ? "" : "=")} '{TheCommonUtils.CDate(f.Value).ToString("d")}'";
                    //tSqlFilterString += $"CONVERT(datetimeoffset, {f.PropertyName}) {op} '{TheCommonUtils.CDate(f.Value).ToString()}'";
                    tSqlFilterString += $"SWITCHOFFSET({f.PropertyName}, '+00:00') {op} SWITCHOFFSET('{TheCommonUtils.CDate(f.Value).ToString()}', '+00:00')";
                    continue;
                } 
                else if(TheStorageUtilities.IsNumeric(f.Value))
                {
                    tSqlFilterString += $"{tJoin}{fPropertyName} {op} {fValue}"; 
                    continue;
                }
                else
                    tSqlFilterString += $"{tJoin}{fPropertyName} {op} '{fValue}'";
            }
            return tSqlFilterString;
        }

        private string ConvertSFIToPropertyBagQuery(string SQLFilter, Type myType, long storeID)
        {
            string NewFilter = string.Copy(SQLFilter);
            if (!string.IsNullOrEmpty(SQLFilter) && myType == typeof(TheThing))
            {
                NewFilter = "";
                List<FieldInfo> FieldsInfoArray = myType.GetFields().Where(field => !Attribute.IsDefined(field, typeof(IgnoreDataMemberAttribute))).OrderBy(x => x.Name).ToList();
                List<PropertyInfo> PropInfoArray = myType.GetProperties().Where(prop => !Attribute.IsDefined(prop, typeof(IgnoreDataMemberAttribute)) && prop.Name != "MyPropertyBag").OrderBy(x => x.Name).ToList();
                string PropertyFilter = "";
                string ThingFilter = "";
                string[] SCSplit = TheCommonUtils.cdeSplit(SQLFilter, " and ", false, true); 
                for (int i = 0; i < SCSplit.Length; i++)
                {
                    bool FoundThingProperty = false;
                    int EqIndex = SCSplit[i].IndexOf('=');
                    foreach (PropertyInfo PInfo in PropInfoArray)
                    {
                        if (PInfo.Name.Equals(SCSplit[i].Substring(0, EqIndex).Trim()))
                        {
                            FoundThingProperty = true;
                            break;
                        }
                    } 
                    if (!FoundThingProperty)
                    {
                        foreach (FieldInfo FInfo in FieldsInfoArray)
                        {
                            if (FInfo.Name.Equals(SCSplit[i].Substring(0, EqIndex).Trim()))
                            {
                                FoundThingProperty = true;
                                break;
                            }
                        }
                    }
                    if (FoundThingProperty)
                    {
                        ThingFilter += SCSplit[i];
                        if (i < SCSplit.Length - 1)
                            ThingFilter += " and ";
                    }
                    else
                    {
                        PropertyFilter += "(Name='" + SCSplit[i].Substring(0, EqIndex).Trim() + "' and Value=" + SCSplit[i].Substring(EqIndex + 1).Trim() + ")";
                        if (i < SCSplit.Length - 1)
                            PropertyFilter += " or ";
                    }
                }
                if (!string.IsNullOrEmpty(ThingFilter))
                    NewFilter += ThingFilter;
                if (!string.IsNullOrEmpty(PropertyFilter))
                {
                    if (!string.IsNullOrEmpty(ThingFilter))
                        NewFilter += " and ";
                    NewFilter += "cdeMID in (select cdeO from [dbo].[cdeProperties" + storeID + "] where " + PropertyFilter + ")";
                }
            }
            return NewFilter;
        }

        private bool CreateStorageMapCache()
        {
            storageMapCache = new TheStorageMirror<StorageDefinition>(TheCDEngines.MyIStorageService)
            {
                CacheTableName = "TheStorageMapCache",
                IsRAMStore = true,
                IsCached = true, 
                IsCachePersistent = false 
            };
            TheStorageMirrorParameters tStoreParams = new TheStorageMirrorParameters()
            {
                FriendlyName = "cdeTheStorageMapCache",
                Description = "Stores records of created stores",
                ResetContent = TheBaseAssets.MyServiceHostInfo.IsNewDevice,
                LoadSync = true
            };
            Task<bool> tRes = storageMapCache.CreateStoreAsync(tStoreParams, null);
            tRes.Wait();
            return tRes.Result;
        }

        private void FillStorageMapCache(DataSet ds, ref Guid pConnKey)
        {
            if (ds != null)
            {
                string[] defaultRows = new string[] { "cdeMID", "cdeCTIM", "cdeSCOPEID" };
                foreach (DataRow row in ds.Tables["CDEDS"].Rows)
                {
                    long storeID = TheCommonUtils.CLng(row["ID"]);
                    //int columnCount = TheCommonUtils.CInt(MySqlHelperClass.cdeGetScalarData($"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='cdeStore{storeID}'", 0, false, true, ref pConnKey));
                    DataSet columnTypes = MySqlHelperClass.cdeOpenDataSet($"SELECT DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='cdeStore{storeID}'", 0, TheBaseAssets.MyServiceHostInfo.TO.StoreRequestTimeout, true, false, ref pConnKey);
                    if (columnTypes == null || columnTypes.Tables["CDEDS"].Rows.Count < defaultRows.Length) //Crashed if a table did not exist although in the storage map....this happens only if a crash/shutdown occured between writing to the storagemap table and creating the table...or creating the table failed.
                    {
                        //TODO: Delete the entry for the cdeStore{StoreID} from the storageMap Table
                        continue;
                    }
                    int columnCount = columnTypes.Tables["CDEDS"].Rows.Count - defaultRows.Length;  
                    int[] fieldTypeIDS = new int[columnCount];
                    int index = 0;
                    foreach (DataRow dataTypeRow in columnTypes.Tables["CDEDS"].Rows)
                    {
                        string columnName = TheCommonUtils.CStr(dataTypeRow[2]);
                        if (!defaultRows.Contains(columnName))
                        {
                            string dataType = TheCommonUtils.CStr(dataTypeRow[0]);
                            string length = TheCommonUtils.CStr(dataTypeRow[1]);
                            if ((dataType.Equals("nvarchar") || dataType.Equals("varbinary")) && length.Equals("-1"))
                                length = "max";
                            string columnDefinition = $"[{dataType}]{(string.IsNullOrEmpty(length) ? "" : "(" + length + ")")}";
                            fieldTypeIDS[index++] = MySqlHelperClass.GetTypeIDFromSQLType(dataType, length);
                        }
                    }
                    StorageDefinition def = new StorageDefinition()
                    {
                        DES = TheCommonUtils.CStr(row["StoreDescription"]),
                        UID = TheCommonUtils.CStr(row["UniqueID"]),
                        NAM = TheCommonUtils.CStr(row["DeviceName"]),
                        CNT = columnCount,
                        FTY = fieldTypeIDS,
                        FDE = null, // Can't find this from SQL Server
                        DEF = false, // Can't find this from SQL Server
                        DID = TheCommonUtils.CGuid(row["NodeID"]),
                        SID = storeID
                    };
                    storageMapCache.AddAnItem(def);
                }
            }
        }
    }
}
