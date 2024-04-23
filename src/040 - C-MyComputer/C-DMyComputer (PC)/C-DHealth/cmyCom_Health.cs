// SPDX-FileCopyrightText: 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using cdeEnergyBase;
using CDMyComputer.ViewModels;
using nsCDEngine.BaseClasses;
using nsCDEngine.Communication;
using nsCDEngine.Engines;
using nsCDEngine.Engines.StorageService;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.ISM;
using nsCDEngine.ViewModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading;

namespace CDMyComputer
{
    public class ThePerfCounter
    {
        public PerformanceCounter PerfCounter;
        public string PropertyName;
    }

    public class ProcessCpuCounter
    {
        public static PerformanceCounter GetPerfCounterForProcessId(int processId, string processCounterName = "% Processor Time")
        {
            string instance = GetInstanceNameForProcessId(processId);
            if (string.IsNullOrEmpty(instance))
                return null;

            return new PerformanceCounter("Process", processCounterName, instance);
        }

        public static string GetInstanceNameForProcessId(int processId)
        {
            var process = Process.GetProcessById(processId);
            string processName = Path.GetFileNameWithoutExtension(process.ProcessName);

            PerformanceCounterCategory cat = new PerformanceCounterCategory("Process");
            string[] instances = cat.GetInstanceNames()
                .Where(inst => inst.StartsWith(processName))
                .ToArray();

            foreach (string instance in instances)
            {
                using (PerformanceCounter cnt = new PerformanceCounter("Process",
                    "ID Process", instance, true))
                {
                    int val = (int)cnt.RawValue;
                    if (val == processId)
                    {
                        return instance;
                    }
                }
            }
            return null;
        }
    }

    public class TheHealthMonitor
    {
        public TheHealthMonitor(int pHealthCycle, TheCDMyComputerEngine pEngineName, bool DisableCollection, bool DisableHistorian)
        {
            if (pEngineName != null)
            {
                MyEngineName = pEngineName.GetBaseEngine().GetEngineName();
                MyBaseEngine = pEngineName;
            }

            if (pHealthCycle > 0)
                HealthUpdateCycle = pHealthCycle;

            mDisableCollection = DisableCollection;
            mDisableHistorian = DisableHistorian;
            TheBaseEngine.WaitForStorageReadiness(StorageHasStarted, true);
            InitHealthCollection();
            TheQueuedSenderRegistry.RegisterHealthTimer(CheckOnHealthTimer);
        }

        public Action<TheServiceHealthData> eventNewHealthData;

        private readonly bool mDisableCollection;
        private readonly bool mDisableHistorian;
        private TheCPUInfo MyCPUInfoData = null;
        private TheServiceHealthData MyHealthData = null;
        private readonly TheCDMyComputerEngine MyBaseEngine = null;
        private readonly object lockCheckTimer = new object();
        ManagementObjectSearcher wmiObjectWin32;
        private readonly DiskInfo MyDiskInfo = new DiskInfo();

        private bool IsEnergyRegistered = false;
        private DateTimeOffset StationStart = DateTimeOffset.Now;
        internal int HealthUpdateCycle = 15;
        private readonly string MyEngineName = "ISMHEALTH";
        private bool AreCounterInit = false;
        private bool UseNicState = false;

        #region HealthInfoStore
        internal TheStorageMirror<TheThingStore> MyServiceHealthDataStore;

        private void StorageHasStarted(ICDEThing sender, object pReady)
        {
            if (pReady!=null)
            {
                if (MyServiceHealthDataStore == null && !mDisableHistorian)
                {
                    MyServiceHealthDataStore = new TheStorageMirror<TheThingStore>(TheCDEngines.MyIStorageService)
                    {
                        IsRAMStore = "RAM".Equals(pReady.ToString()),
                        IsCachePersistent = "RAM".Equals(pReady.ToString()) && !TheBaseAssets.MyServiceHostInfo.IsCloudService,
                        CacheTableName = "TheHealthHistory"
                    };
                    if (MyServiceHealthDataStore.IsRAMStore)
                    {
                        MyServiceHealthDataStore.SetRecordExpiration(86400, null);   //RAM stores for 1 day
                        MyServiceHealthDataStore.InitializeStore(true, false);
                    }
                    else
                    {
                        MyServiceHealthDataStore.SetRecordExpiration(604800, null); //Storage stores for 7 days
                        MyServiceHealthDataStore.CreateStore("C-DMyComputer: DeviceHealthData", "All health Data of a Device/Service", null, true, false);
                    }
                    TheBaseAssets.MySYSLOG.WriteToLog(8002, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("jcHealth", "Health Storage Started", eMsgLevel.l3_ImportantMessage));
                }
            }
        }
        #endregion

        #region Publish Info
        public void SendCPUInfo(Guid pOrg)
        {
            GetCPUInfo(TheBaseAssets.MyServiceHostInfo.GetPrimaryStationURL(false));
            TSM tMsg = new TSM(MyEngineName, "CPUINFO", TheCommonUtils.SerializeObjectToJSONString<TheCPUInfo>(MyCPUInfoData));
            tMsg.SetNoDuplicates(true);
            if (!TheBaseAssets.MyServiceHostInfo.IsCloudService) //Cloud would send unscoped Message going nowhere!
            {
                if (pOrg == Guid.Empty)
                    TheCommCore.PublishCentral(MyEngineName, tMsg);
                else
                    TheCommCore.PublishToNode(pOrg, tMsg);
            }
            TheBaseAssets.MySYSLOG.WriteToLog(8003, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : tMsg);
        }
        public void SendCPUInfo(TSM message)
        {
            Guid originator = message.GetOriginator();
            if (originator == Guid.Empty)
            {
                SendCPUInfo(originator);
            }
            else
            {
                GetCPUInfo(TheBaseAssets.MyServiceHostInfo.GetPrimaryStationURL(false));
                TSM tMsg = new TSM(MyEngineName, "CPUINFO", TheCommonUtils.SerializeObjectToJSONString<TheCPUInfo>(MyCPUInfoData));
                tMsg.SetNoDuplicates(true);
                TheCommCore.PublishToOriginator(message, tMsg, true);
                TheBaseAssets.MySYSLOG.WriteToLog(8003, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : tMsg);
            }
        }

        public void SendHealthInfo(Guid pOrg)
        {
            GetISMHealthData();
            TSM tMsg = new TSM(MyEngineName, "ISMHEALTH", TheCommonUtils.SerializeObjectToJSONString<TheServiceHealthData>(MyHealthData));
            if (!TheBaseAssets.MyServiceHostInfo.IsCloudService) //Cloud would send unscoped Message going nowhere!
            {
                tMsg.SetNoDuplicates(true);
                if (pOrg == Guid.Empty)
                    TheCommCore.PublishCentral(MyEngineName, tMsg);
                else
                    TheCommCore.PublishToNode(pOrg, tMsg);
            }
            eventNewHealthData?.Invoke(MyHealthData);
            TheBaseAssets.MySYSLOG.WriteToLog(8004, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : tMsg);
        }

        public void SendHealthInfo(TSM message)
        {
            Guid originator = message.GetOriginator();
            if (originator == Guid.Empty)
            {
                SendHealthInfo(originator);
            }
            else
            {
                GetISMHealthData();
                TSM tMsg = new TSM(MyEngineName, "ISMHEALTH", TheCommonUtils.SerializeObjectToJSONString<TheServiceHealthData>(MyHealthData));
                tMsg.SetNoDuplicates(true);
                TheCommCore.PublishToOriginator(message, tMsg, true);
                eventNewHealthData?.Invoke(MyHealthData);
                TheBaseAssets.MySYSLOG.WriteToLog(8004, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : tMsg);
            }
        }

        private void sinkEnergyFound(ICDEThing sender, object pIncoming)
        {
            TheProcessMessage pMsg = pIncoming as TheProcessMessage;
            if (pMsg == null) return;

            string[] cmd = pMsg.Message.TXT.Split(':');

            switch (cmd[0]) 
            {
                case eEnergyMessages.EnergyConsumerUpdate:
                    TheEnergyData tData = TheCommonUtils.DeserializeJSONStringToObject<TheEnergyData>(pMsg.Message.PLS);
                    if (tData != null)
                    {
                        LastStationWatts =tData.Watts;
                    }
                    break;
            }
        }
        #endregion

        private void InitHealthCollection()
        {
            if (MyHealthData != null) return;

            MyHealthData = new TheServiceHealthData(MyEngineName, System.Environment.MachineName);
            MyCPUInfoData = new TheCPUInfo(MyEngineName, System.Environment.MachineName);

           // PerformanceCounterCategory[] perfCats = PerformanceCounterCategory.GetCategories();
            
            if (mDisableCollection) return;
            SendCPUInfo(Guid.Empty);
            SendHealthInfo(Guid.Empty);

            try
            {
                MyCPULoadCounter = new PerformanceCounter
                {
                    CategoryName = "Processor Information",
                    CounterName = "% Processor Time",
                    InstanceName = "_Total"
                };
            }
            catch (Exception e)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(8006, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("jcHealth", "Cannot create CPU Counter", eMsgLevel.l1_Error, e.ToString()));
            }

            MyCounter = new List<ThePerfCounter>();
            UseNicState = true;

            //try
            //{
            //    var readBytesSec = new PerformanceCounter("Process", "IO Read Bytes/sec", Process.GetCurrentProcess().ProcessName);
            //    if (readBytesSec != null)
            //        MyCounter.Add(new ThePerfCounter { PerfCounter = readBytesSec, PropertyName = "NetRead" });
            //    var writeByteSec = new PerformanceCounter("Process", "IO Write Bytes/sec", Process.GetCurrentProcess().ProcessName);
            //    if (writeByteSec != null)
            //        MyCounter.Add(new ThePerfCounter { PerfCounter = readBytesSec, PropertyName = "NetWrite" });
            //}
            //catch (Exception e)
            //{
            //    UseNicState = true;
            //    TheBaseAssets.MySYSLOG.WriteToLog(8006, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("jcHealth", "Cannot Create Net Counter - fallback to NIC state", eMsgLevel.l1_Error, e.ToString()));
            //}

            try
            {
                wmiObjectWin32 = new ManagementObjectSearcher("select * from Win32_OperatingSystem");
            }
            catch(Exception e)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(8007, new TSM("jcHealth", "wmiObject-Warning (Not Supported on XP)", eMsgLevel.l2_Warning, e.ToString()));
            }
            StartProcessCPUMeasure();

            AreCounterInit = true;
        }

        private void UpdateNetworkInterface()
        {
            try
            {
                var nicArr = NetworkInterface.GetAllNetworkInterfaces();
                long tR = 0;
                long tW = 0;
                for (int i = 0; i < nicArr.Length; i++)
                {
                    NetworkInterface nic = nicArr[i];
                    IPv4InterfaceStatistics interfaceStats = nic.GetIPv4Statistics();
                    tR += interfaceStats.BytesReceived;
                    tW += interfaceStats.BytesSent;
                }
                MyHealthData.NetRead = tR;
                MyHealthData.NetWrite = tW;
            }
            catch (Exception e)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(8006, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("jcHealth", "Cannot read NIC Counter", eMsgLevel.l1_Error, e.ToString()));
            }
        }

        List<ThePerfCounter> MyCounter;

        internal void CheckOnHealthTimer(long tTimerVal)
        {
            if (mDisableCollection) return;
            if ((tTimerVal % HealthUpdateCycle) != 0) return;
            if (TheCommonUtils.cdeIsLocked(lockCheckTimer)) return;
            lock (lockCheckTimer)
            {
                if (!AreCounterInit)
                    return;
                TheBaseAssets.MySYSLOG.WriteToLog(8005, TSM.L(eDEBUG_LEVELS.FULLVERBOSE) ? null : new TSM("jcHealth", "Enter CheckOnHealthTimer", eMsgLevel.l7_HostDebugMessage));
                TheDiagnostics.SetThreadName("C-MyComputer-HealthTimer", true);
                if (!IsEnergyRegistered)
                {
                    IsEnergyRegistered = true;
                    TheCDEngines.RegisterNewMiniRelay("EnergyMessages");
                    TheThingRegistry.GetBaseEngine("EnergyMessages")?.RegisterEvent(eEngineEvents.IncomingMessage, sinkEnergyFound);
                }
                SendHealthInfo(Guid.Empty);

                if (MyServiceHealthDataStore != null)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(8006, TSM.L(eDEBUG_LEVELS.FULLVERBOSE) ? null : new TSM("jcHealth", "Storing HealthInfo", eMsgLevel.l7_HostDebugMessage, MyHealthData.cdeUptime.ToString()));
                    if (!MyServiceHealthDataStore.IsReady)
                    {

                        MyServiceHealthDataStore.InitializeStore(false);
                        TheBaseAssets.MySYSLOG.WriteToLog(8006, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("jcHealth", "Storing HealthInfo- Initializing store", eMsgLevel.l4_Message));
                    }
                    MyServiceHealthDataStore.AddAnItem(TheThingStore.CloneFromTheThing(MyHealthData, true));
                }

                TheBaseAssets.MySYSLOG.WriteToLog(8006, TSM.L(eDEBUG_LEVELS.FULLVERBOSE) ? null : new TSM("jcHealth", "Leave CheckOnHealthTimer", eMsgLevel.l7_HostDebugMessage));
            }
        }


        private double LastStationWatts = 0;
        private readonly object GetHealthLock = new object();
        private void GetISMHealthData()
        {
            if (TheCommonUtils.cdeIsLocked(GetHealthLock) || !AreCounterInit) return;
            lock (GetHealthLock)
            {
                MyHealthData.StationWatts = LastStationWatts;
                MyHealthData.QSenders = TheCDEKPIs.QSenders;
                MyHealthData.QSReceivedTSM = TheCDEKPIs.QSReceivedTSM;
                MyHealthData.QSSendErrors = TheCDEKPIs.QSSendErrors;
                MyHealthData.QSInserted = TheCDEKPIs.QSInserted;
                MyHealthData.QSQueued = TheCDEKPIs.QSQueued;
                MyHealthData.QSRejected = TheCDEKPIs.QSRejected;
                MyHealthData.QSSent = TheCDEKPIs.QSSent;
                MyHealthData.QKBSent = Math.Floor(TheCDEKPIs.QKBSent > 0 ? TheCDEKPIs.QKBSent / 1024 : 0);
                MyHealthData.QKBReceived = Math.Floor(TheCDEKPIs.QKBReceived > 0 ? TheCDEKPIs.QKBReceived / 1024 : 0);
                MyHealthData.QSLocalProcessed = TheCDEKPIs.QSLocalProcessed;

                MyHealthData.CCTSMsRelayed = TheCDEKPIs.CCTSMsRelayed;
                MyHealthData.CCTSMsReceived = TheCDEKPIs.CCTSMsReceived;
                MyHealthData.CCTSMsEvaluated = TheCDEKPIs.CCTSMsEvaluated;
                MyHealthData.EventTimeouts = TheCDEKPIs.EventTimeouts;
                MyHealthData.TotalEventTimeouts = TheCDEKPIs.TotalEventTimeouts;
                MyHealthData.KPI1 = TheCDEKPIs.KPI1;
                MyHealthData.KPI2 = TheCDEKPIs.KPI2;
                MyHealthData.KPI3 = TheCDEKPIs.KPI3;
                MyHealthData.KPI4 = TheCDEKPIs.KPI4;
                MyHealthData.KPI5 = TheCDEKPIs.KPI5;
                MyHealthData.KPI6 = TheCDEKPIs.KPI6;
                MyHealthData.KPI7 = TheCDEKPIs.KPI7;
                MyHealthData.KPI8 = TheCDEKPIs.KPI8;
                MyHealthData.KPI9 = TheCDEKPIs.KPI9;
                MyHealthData.KPI10 = TheCDEKPIs.KPI10;
                MyHealthData.HTCallbacks = TheCDEKPIs.HTCallbacks;
                TheCDEKPIs.Reset();

                MyHealthData.HostAddress = System.Environment.MachineName;
                MyHealthData.FriendlyName = TheBaseAssets.MyServiceHostInfo.GetPrimaryStationURL(false);
                MyHealthData.HostVersion = TheBaseAssets.MyServiceHostInfo.Version;
                MyHealthData.LastUpdate = DateTimeOffset.Now;
                DiskInfo di = GetDiskInfo();
                MyHealthData.DiskSpaceFree = di.TotalFreeSpace;
                if (di.TotalSize > 0 && di.TotalFreeSpace <= di.TotalSize)
                    MyHealthData.DiskSpaceUsage = ((di.TotalSize - di.TotalFreeSpace) / di.TotalSize) * 100;
                else
                    MyHealthData.DiskSpaceUsage = 0;

                try
                {
                    if (wmiObjectWin32 != null)
                    {
                        var tRed = wmiObjectWin32.Get().Cast<ManagementObject>();
                        var memoryValues = tRed.Select(mo => new
                        {
                            FreePhysicalMemory = TheCommonUtils.CDbl(mo["FreePhysicalMemory"].ToString()),
                            TotalVisibleMemorySize = TheCommonUtils.CDbl(mo["TotalVisibleMemorySize"].ToString())
                        }).FirstOrDefault();

                        if (memoryValues != null)
                        {
                            MyHealthData.RAMAvailable = memoryValues.FreePhysicalMemory;
                            //var percent = ((memoryValues.TotalVisibleMemorySize - memoryValues.FreePhysicalMemory) / memoryValues.TotalVisibleMemorySize) * 100;
                        }
                    }
                }
                catch(Exception e)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(8007, new TSM("jcHealth", "wmiObject-Warning (Not Supported on XP)", eMsgLevel.l2_Warning, e.ToString()));
                }

                try
                {
                    MeasureProcessCPU();
                    MyHealthData.cdeLoad = cpuUsageSinceLastMeasure; // Can also use cpuUsageSinceStart, depending on purpose of metric
                }
                catch(Exception e)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(8007, new TSM("jcHealth", "Could not get CPU usage for process", eMsgLevel.l2_Warning, e.ToString()));
                }

                try
                {
                    if (MyCPULoadCounter != null)
                    {
                        MyHealthData.CPULoad = TheCommonUtils.CDbl(MyCPULoadCounter.NextValue()).cdeTruncate(2);
                    }
                }
                catch (UnauthorizedAccessException e)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(8007, new TSM("jcHealth", "PerfCounter-Warning (Requires admin privileges)", eMsgLevel.l2_Warning, e.ToString()));

                    MyCPULoadCounter?.Dispose();
                    MyCPULoadCounter = null; // avoid further retries
                }
                catch (Exception e)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(8007, new TSM("jcHealth", "PerfCounter-Warning (Not Supported on XP)", eMsgLevel.l2_Warning, e.ToString()));

                    MyCPULoadCounter?.Dispose();
                    MyCPULoadCounter = null; // avoid further retries
                }

                if (UseNicState)
                    UpdateNetworkInterface();
                else
                {
                    if (MyCounter?.Count > 0)
                    {
                        foreach (var t in MyCounter)
                        {
                            try
                            {
                                MyHealthData.SetProperty(t.PropertyName, t.PerfCounter.NextValue());
                            }
                            catch (Exception)
                            {
                                TheBaseAssets.MySYSLOG.WriteToLog(8007, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("jcHealth", $"PerfCounter: {t.PropertyName} could not be read", eMsgLevel.l2_Warning));
                            }
                        }
                    }
                }

                try
                {
                    MyHealthData.cdeCTIM = DateTimeOffset.Now;
                    MyHealthData.LastUpdate = DateTimeOffset.Now;
                    using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("select CreationDate from Win32_Process where Name='System'"))
                    {
                        foreach (ManagementObject share in searcher.Get())
                        {
                            foreach (PropertyData pData in share.Properties)
                            {
                                if (pData.Name.Equals("CreationDate"))
                                {
                                    DateTime dc = ManagementDateTimeConverter.ToDateTime(TheCommonUtils.CStr(pData.Value));
                                    //dc = dc.AddTicks(-TimeZone.CurrentTimeZone.GetUtcOffset(DateTime.Now).Ticks).ToLocalTime();
                                    TimeSpan ts = DateTime.Now.Subtract(dc);
                                    MyHealthData.PCUptime = ts.TotalMinutes;
                                }
                            }
                        }
                    }


                    /// cde Process Specific Information
                    int nProcessID = Process.GetCurrentProcess().Id;
                    using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("select CreationDate,WorkingSetSize,HandleCount,ThreadCount from Win32_Process where ProcessID='" + nProcessID + "'"))
                    {
                        foreach (ManagementObject share in searcher.Get())
                        {
                            foreach (PropertyData PC in share.Properties)
                            {
                                string objName = PC.Name;
                                string objValue = "";
                                if (PC.Value != null && PC.Value.ToString() != "")
                                {
                                    switch (PC.Value.GetType().ToString())
                                    {
                                        case "System.String[]":
                                            string[] str = (string[])PC.Value;
                                            foreach (string st in str)
                                                objValue += st + " ";
                                            break;
                                        case "System.UInt16[]":
                                            ushort[] shortData = (ushort[])PC.Value;
                                            foreach (ushort st in shortData)
                                                objValue += st.ToString() + " ";
                                            break;
                                        default:
                                            objValue = PC.Value.ToString();
                                            break;
                                    }
                                }
                                switch (objName)
                                {
                                    case "CreationDate":
                                        DateTime dc = ManagementDateTimeConverter.ToDateTime(PC.Value.ToString());
                                        //dc=dc.AddTicks(-TimeZone.CurrentTimeZone.GetUtcOffset(DateTime.Now).Ticks).ToLocalTime();
                                        TimeSpan ts = DateTime.Now.Subtract(dc);
                                        MyHealthData.cdeUptime = ts.TotalMinutes;
                                        break;
                                    case "WorkingSetSize":
                                        MyHealthData.cdeWorkingSetSize = TheCommonUtils.CLng(objValue) / 1024;
                                        break;
                                    case "HandleCount":
                                        MyHealthData.cdeHandles = TheCommonUtils.CInt(objValue);
                                        break;
                                    case "ThreadCount":
                                        MyHealthData.cdeThreadCount = TheCommonUtils.CInt(objValue);
                                        break;
                                }
                            }
                            break;
                        }
                    }
                    //if (!WasRunBefore)
                    UpdateDiagInfo(MyHealthData);

                }
                catch (Exception e)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(8008, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("jcHealth", "Ring0-Warning", eMsgLevel.l2_Warning, e.ToString()));
                }
            }
        }

        private double cpuUsageSinceStart;
        private double cpuUsageSinceLastMeasure;

        TimeSpan startCPUTime;
        TimeSpan oldCPUTime;
        DateTime lastMonitorTime;
        DateTime startMonitorTime;

        private void StartProcessCPUMeasure()
        {
            Process currProcess = Process.GetCurrentProcess();
            if(currProcess != null)
                startCPUTime = currProcess.TotalProcessorTime; 
            oldCPUTime = new TimeSpan(0);
            lastMonitorTime = DateTime.UtcNow;
            startMonitorTime = DateTime.UtcNow;
        }
        private void MeasureProcessCPU()
        {
            if (startCPUTime == TimeSpan.Zero || startMonitorTime == DateTime.MinValue)
                StartProcessCPUMeasure();
            TimeSpan newCPUTime = Process.GetCurrentProcess().TotalProcessorTime - startCPUTime;
            cpuUsageSinceLastMeasure = ((newCPUTime - oldCPUTime).TotalSeconds / (Environment.ProcessorCount * DateTime.UtcNow.Subtract(lastMonitorTime).TotalSeconds)) * 100;
            cpuUsageSinceStart = (newCPUTime.TotalSeconds / (Environment.ProcessorCount * DateTime.UtcNow.Subtract(startMonitorTime).TotalSeconds)) * 100;
            lastMonitorTime = DateTime.UtcNow;
            oldCPUTime = newCPUTime;
        }

        readonly object UpdateDiagInfoLock = new object();
        internal void UpdateDiagInfo(TheServiceHealthData pMyHealthData)
        {
            if (pMyHealthData == null || TheCommonUtils.cdeIsLocked(UpdateDiagInfoLock)) return;
            lock (UpdateDiagInfoLock)
            {
                //uint s;
                //GetCurrentCoreInfo(out t, out s);
                //pMyHealthData.cdeTemp = t;
                //pMyHealthData.cdeSpeed = s;

                double tTemp = 0;
                long tSpeed = 0;
                pMyHealthData.CoreTemps = "";
                pMyHealthData.CoreSpeeds = "";
                if (MyCPUInfoData.NumberOfCores == 0) MyCPUInfoData.NumberOfCores = 1;
                for (int i = 0; i < MyCPUInfoData.NumberOfCores; i++)
                {
                    Thread thread = new Thread(new ParameterizedThreadStart(GetCoreInfo))
                    {
                        Name = "GetCoreInfo" + (i + 1).ToString(),
                        IsBackground = true
                    };
                    thread.Start(i);
                    thread.Join();
                    thread = null;
                    tTemp += MyCoreTemps[i];
                    tSpeed += MyCoreSpeeds[i];
                    if (i > 0) pMyHealthData.CoreSpeeds += ",";
                    pMyHealthData.CoreSpeeds += MyCoreSpeeds[i].ToString();
                    if (i > 0) pMyHealthData.CoreTemps += ",";
                    pMyHealthData.CoreTemps += MyCoreTemps[i].ToString();
                }
                if (MyCPUInfoData.NumberOfCores > 0)
                {
                    pMyHealthData.CPUTemp = tTemp / MyCPUInfoData.NumberOfCores;
                    pMyHealthData.CPUSpeed = tSpeed / MyCPUInfoData.NumberOfCores;
                }
                else
                {
                    pMyHealthData.CPUTemp = tTemp;
                    pMyHealthData.CPUSpeed = tSpeed;
                }
            }
        }

        //private Ols MyOls = null;
        private double[] MyCoreTemps;
        private uint[] MyCoreSpeeds;
        private void GetCPUInfo(string URL)
        {

            MyCPUInfoData.HostAddress = System.Environment.MachineName;
            MyCPUInfoData.DiskSizeTotal = GetDiskInfo().TotalSize;
            MyCPUInfoData.LastUpdate = DateTimeOffset.Now;
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("select Name,Description,Version,Manufacturer,Revision,AddressWidth,MaxClockSpeed,L2CacheSize,NumberOfCores from Win32_Processor"))
                {
                    try
                    {
                        foreach (ManagementObject share in searcher.Get())
                        {
                            foreach (PropertyData PC in share.Properties)
                            {
                                string objName = PC.Name;
                                string objValue = "";
                                if (PC.Value != null && PC.Value.ToString() != "")
                                {
                                    switch (PC.Value.GetType().ToString())
                                    {
                                        case "System.String[]":
                                            string[] str = (string[])PC.Value;
                                            foreach (string st in str)
                                                objValue += st + " ";
                                            break;
                                        case "System.UInt16[]":
                                            ushort[] shortData = (ushort[])PC.Value;
                                            foreach (ushort st in shortData)
                                                objValue += st.ToString() + " ";
                                            break;
                                        default:
                                            objValue = PC.Value.ToString();
                                            break;
                                    }
                                }
                                switch (objName)
                                {
                                    case "Name":
                                        MyCPUInfoData.FriendlyName = objValue;
                                        break;
                                    case "Description":
                                        MyCPUInfoData.Description = objValue;
                                        break;
                                    case "Version":
                                        MyCPUInfoData.Version = objValue;
                                        break;
                                    case "Manufacturer":
                                        MyCPUInfoData.Manufacturer = objValue;
                                        break;
                                    case "Revision":
                                        MyCPUInfoData.Revision = TheCommonUtils.CInt(objValue);
                                        break;
                                    case "AddressWidth":
                                        MyCPUInfoData.AddressWidth = TheCommonUtils.CInt(objValue);
                                        break;
                                    case "MaxClockSpeed":
                                        MyCPUInfoData.MaxClockSpeed = TheCommonUtils.CInt(objValue);
                                        break;
                                    case "L2CacheSize":
                                        MyCPUInfoData.L2CacheSize = TheCommonUtils.CInt(objValue);
                                        break;
                                    case "NumberOfCores":
                                        MyCPUInfoData.NumberOfCores = TheCommonUtils.CInt(objValue);
                                        break;
                                }
                            }
                            break;
                        }

                    }
                    catch (Exception exp)
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(8009, new TSM("jcHealth", "CPUInfoGather-Warning", eMsgLevel.l2_Warning, exp.ToString()));
                    }
                }
            }
            catch (Exception)
            {
                MyCPUInfoData.Description = "MOS not supported";
            }
            MyCoreTemps = new double[MyCPUInfoData.NumberOfCores];
            MyCoreSpeeds = new uint[MyCPUInfoData.NumberOfCores];
        }

        private void GetCoreInfo(object pCores)
        {
            int core = (int)pCores;
            MyCoreTemps[core] = 0;
            MyCoreSpeeds[core] = 0;
            int utid = GetCurrentThreadId();
            ProcessThreadCollection tCol = Process.GetCurrentProcess().Threads;
            //if (MyOls != null)
            {
                try
                {
                    foreach (ProcessThread pt in tCol)
                    {
                        if (utid == pt.Id) // && MyOls != null)
                        {
                            pt.ProcessorAffinity = (IntPtr)(1<<core); // Set affinity for this thread to CPU #1
                            Thread.BeginCriticalRegion();
                            Thread.BeginThreadAffinity();
                            //SetThreadAffinityMask(new IntPtr(GetCurrentThreadId()), new IntPtr(1 << core));
                            //HiPerfStopWatch.SleepInNs(5000, 0);
                            uint eax = TheMSRs.MSR_IA32_THERM_STATUS;
                            //Ring0.Rdmsr(TheMSRs.MSR_IA32_THERM_STATUS, out eax, out edx);
                            //MyOls.Rdmsr(TheMSRs.MSR_IA32_THERM_STATUS, ref eax, ref edx);
                            uint temp1 = 100 - ((eax & 0x00ff0000) >> 16);
                            MyCoreTemps[core] = temp1;

                            eax = TheMSRs.MSR_IA32_APERF;
                            //Ring0.Rdmsr(TheMSRs.MSR_IA32_APERF, out eax, out edx);
                            //MyOls.Rdmsr(TheMSRs.MSR_IA32_APERF, ref eax, ref edx);
                            MyCoreSpeeds[core] = eax;
                            Thread.EndCriticalRegion();
                            Thread.EndThreadAffinity();
                        }
                    }
                }
                catch 
                {
                    MyCoreTemps[core] = 0;
                    MyCoreSpeeds[core] = 0;
                }
            }
        }

        private DiskInfo GetDiskInfo()
        {
            try
            {
                long totalDiskSize = 0;
                long totalFreeDiskSpace = 0;
                DriveInfo[] allDrives = DriveInfo.GetDrives();
                foreach (DriveInfo drive in allDrives)
                {
                    if (drive.IsReady && drive.DriveType == DriveType.Fixed)
                    {
                        totalDiskSize += drive.TotalSize;
                        totalFreeDiskSpace += drive.TotalFreeSpace;
                    }
                }
                MyDiskInfo.TotalSize = totalDiskSize;
                MyDiskInfo.TotalFreeSpace = totalFreeDiskSpace;
            }
            catch (Exception e)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(8010, new TSM("jcHealth", "GetDiskInfo-Warning", eMsgLevel.l2_Warning, e.ToString()));
            }
            // If accessing a drive fails, returns last calculated version of DiskInfo
            return MyDiskInfo;
        }

        private static PerformanceCounter MyCPULoadCounter;

        [DllImport("kernel32")]
        static extern int GetCurrentThreadId();
        [DllImport("kernel32.dll")]
        static extern IntPtr SetThreadAffinityMask(IntPtr hThread, IntPtr dwThreadAffinityMask);

        public void Shutdown()
        {
        }

        public class DiskInfo
        {
            public double TotalSize { get; set; }
            public double TotalFreeSpace { get; set; }
        }

        public class TheTemperature
        {
            public double CurrentValue { get; set; }
            public string InstanceName { get; set; }
            public static List<TheTemperature> Temperatures
            {
                get
                {
                    List<TheTemperature> result = new List<TheTemperature>();
                    ManagementObjectSearcher searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT * FROM MSAcpi_ThermalZoneTemperature");
                    if (searcher != null)
                    {
                        foreach (ManagementObject obj in searcher.Get())
                        {
                            Double temp = Convert.ToDouble(obj["CurrentTemperature"].ToString());
                            temp = (temp - 2732) / 10.0; //Celsius
                            result.Add(new TheTemperature { CurrentValue = temp, InstanceName = obj["InstanceName"].ToString() });
                        }
                    }
                    return result;
                }
            }
        }

    }

    internal sealed class HiPerfStopWatch
    {
        [System.Runtime.InteropServices.DllImport("winmm.dll")]
        internal static extern uint timeBeginPeriod(uint period);
        [System.Runtime.InteropServices.DllImport("winmm.dll")]
        internal static extern uint timeEndPeriod(uint period);


        [System.Runtime.InteropServices.DllImport("Kernel32.dll")]
        private static extern bool QueryPerformanceCounter(
            out long lpPerformanceCount);

        [System.Runtime.InteropServices.DllImport("Kernel32.dll")]
        private static extern bool QueryPerformanceFrequency(
            out long lpFrequency);

        private long startTime, stopTime;
        private readonly long freq;
        // Constructor

        public HiPerfStopWatch()
        {
            startTime = 0;
            stopTime = 0;

            if (QueryPerformanceFrequency(out freq) == false)
            {
                // high-performance counter not supported

                throw new System.ComponentModel.Win32Exception();
            }
        }

        // Start the timer

        public void Start()
        {
            // lets do the waiting threads there work

            Thread.Sleep(0);

            QueryPerformanceCounter(out startTime);
        }

        // Stop the timer

        public void Stop()
        {
            QueryPerformanceCounter(out stopTime);
        }

        // Returns the duration of the timer (in seconds)

        public double Duration
        {
            get
            {
                return (double)(stopTime - startTime) / (double)freq;
            }
        }

        private static readonly HiPerfStopWatch gW1 = new HiPerfStopWatch();
        public static long SleepInNs1(int ms)
        {
            gW1.Stop();
            do
            {
                gW1.Stop();
            } while (gW1.startTime + (ms * 1000) > gW1.stopTime);
            return gW1.stopTime - gW1.startTime;
        }
        private static readonly HiPerfStopWatch[] gW = new HiPerfStopWatch[6];
        private static readonly long[] gL = new long[6];
        public static long SleepInNs(int ms, int pNo)
        {
            if (gW[pNo] == null)
            {
                gW[pNo] = new HiPerfStopWatch();
                gW[pNo].Start();
            }

            gW[pNo].Stop();
            gL[pNo] = gW[pNo].stopTime;
            do
            {
                gW[pNo].Stop();
            } while (gL[pNo] + (ms * 10000) > gW[pNo].stopTime);
            return gW[pNo].stopTime - gL[pNo];
        }
    }
}
