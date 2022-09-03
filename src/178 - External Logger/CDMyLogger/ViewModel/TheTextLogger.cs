// SPDX-FileCopyrightText: 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

﻿using nsCDEngine.BaseClasses;
using nsCDEngine.Communication;
using nsCDEngine.Engines;
using nsCDEngine.Engines.NMIService;
using nsCDEngine.Engines.StorageService;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.ISM;
using nsCDEngine.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;

namespace CDMyLogger.ViewModel
{
    [DeviceType(
    DeviceType = eTheLoggerServiceTypes.TextLogger,
    Capabilities = new eThingCaps[] { eThingCaps.ConfigManagement },
    Description = "Text Event Logger")]
    class TheTextLogger : TheLoggerBase
    {
        public TheTextLogger(TheThing tBaseThing, ICDEPlugin pPluginBase) : base(tBaseThing, pPluginBase)
        {
            MyBaseThing.DeviceType = eTheLoggerServiceTypes.TextLogger;
        }

        protected override void DoCreateUX(TheFormInfo pForm)
        {
            base.DoCreateUX(pForm);
            pForm.PropertyBag = new nmiCtrlFormView { MaxTileWidth = 12 };
            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.SingleCheck, 140, 2, 0x80, "Write to Console", "WriteToConsole", new nmiCtrlSingleCheck { ParentFld = 120, TileWidth = 3 });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.SingleCheck, 141, 2, 0x80, "Each Category in separate file", "EachCategoryInOwnFile", new nmiCtrlSingleCheck { ParentFld = 120, TileWidth = 3 });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.SingleCheck, 142, 2, 0x80, "Each day in a separate file", "EachDayInOwnFile", new nmiCtrlSingleCheck { ParentFld = 120, TileWidth = 3 });
            // add log Details (LogFileLocation, MaxLength, MaxFiles, EachCategoryInOwnFile)

            CreateLogTable(1000, 1);
        }

        public override bool LogEvent(TheEventLogData pItem)
        {
            if (!IsConnected)
                return false;
            return WriteLogToFile(pItem, true);
        }

        protected override void DoInit()
        {
            base.DoInit();
            if (string.IsNullOrEmpty(Address))
                Address = "EventLog";
            MyLogFilesTableName = $"NodeBackups{TheThing.GetSafeThingGuid(MyBaseThing, "NODELOGS")}";
            TheBaseEngine.WaitForStorageReadiness(OnStorageReady, true);
        }

        public override void Connect(TheProcessMessage pMsg)
        {
            if (IsConnected)
                return;
            IsConnected = true;
            MyBaseThing.StatusLevel = 1;
            MyBaseThing.LastMessage = $"Connected to Logger at {DateTimeOffset.Now}";

            mMaxLogFileSize = (int)TheThing.GetSafePropertyNumber(MyBaseThing, "MaxLogFileSize");
            mWriteToConsole = TheThing.GetSafePropertyBool(MyBaseThing, "WriteToConsole");
            mLogFileDate = TheThing.GetSafePropertyDate(MyBaseThing, "LogFileDate");
            if (string.IsNullOrEmpty(Address))
                Address = "Log";
            if (mLogFileDate==DateTimeOffset.MinValue || mLogFileDate.Day!=DateTimeOffset.Now.Day)
                LogFileDate = mLogFileDate = DateTimeOffset.Now;
            tTimeStamp = $"{mLogFileDate:yyyMMdd_HHmmss}";

            if (LogRemoteEvents)
                TheCDEngines.MyContentEngine.RegisterEvent(eEngineEvents.NewEventLogEntry, sinkLogMe);
        }

        private readonly object writeLock = new ();
        private DateTimeOffset mLogFileDate;
        private int mMaxLogFileSize = 0;
        private bool mWriteToConsole = false;
        string tLastLogFile;
        string tTimeStamp;
        string tBaseLogName;
        public string MyCurLog;

        private bool WriteLogToFile(TheEventLogData pLogItem, bool WaitForLock)
        {
            if (pLogItem == null || string.IsNullOrEmpty(pLogItem.EventCategory))
                return false;
            if (mWriteToConsole)
                Console.WriteLine($"{pLogItem.EventCategory} : {TheCommonUtils.GetDateTimeString(pLogItem.EventTime, -1)} : {pLogItem.EventName} : {pLogItem.EventString}");

            if (EachCategoryInOwnFile)
                tBaseLogName = $"{Address}\\{pLogItem.EventCategory}";
            else
                tBaseLogName = Address;
            if (EachDayInOwnFile && mLogFileDate.Day != DateTimeOffset.Now.Day)
            {
                LogFileDate = mLogFileDate = DateTimeOffset.Now;
                tTimeStamp = $"{mLogFileDate:yyyMMdd_HHmmss}";
            }
            // ReSharper disable once EmptyEmbeddedStatement
            if (WaitForLock) while (TheCommonUtils.cdeIsLocked(writeLock)) { TheCommonUtils.SleepOneEye(50, 50); }
            if (!TheCommonUtils.cdeIsLocked(writeLock))
            {
                lock (writeLock)
                {
                    MyCurLog = SetCurrentFileName();
                    var bLogFileExists = File.Exists(MyCurLog);
                    if (mMaxLogFileSize > 0 && bLogFileExists)
                    {
                        try
                        {
                            FileInfo f2 = new (MyCurLog);
                            if (f2.Length > mMaxLogFileSize * (1024 * 1024))
                            {
                                LogFileDate = mLogFileDate = DateTimeOffset.Now;
                                tTimeStamp = $"{mLogFileDate:yyyMMdd_HHmmss}";
                                MyCurLog = SetCurrentFileName();
                                bLogFileExists = File.Exists(MyCurLog);
                            }
                        }
                        catch
                        {
                            //ignored
                        }
                    }
                    try
                    {
                        using (StreamWriter fs = new (MyCurLog, bLogFileExists))
                        {
                            if (EachCategoryInOwnFile)
                                fs.WriteLine($"{pLogItem.EventTime} : {TheCommonUtils.GetDateTimeString(pLogItem.EventTime, -1)} : {pLogItem.EventString}");
                            else
                                fs.WriteLine($"{pLogItem.EventCategory} : {TheCommonUtils.GetDateTimeString(pLogItem.EventTime, -1)} : {pLogItem.EventName} : {pLogItem.EventString}");
                        }
                    }
                    catch
                    {
                        //ignored
                    }
                }
            }
            return true;
        }

        private string SetCurrentFileName()
        {
            var tCurrLogFile = $"{tBaseLogName}_{tTimeStamp}.txt";
            if (tCurrLogFile != tLastLogFile)
            {
                tLastLogFile = tCurrLogFile;
                tCurrLogFile = TheCommonUtils.cdeFixupFileName(tCurrLogFile);
                TheCommonUtils.CreateDirectories(tCurrLogFile);
            }
            return tCurrLogFile;
        }

        /// <summary>
        /// Path to the Log File
        /// </summary>
        [ConfigProperty(Generalize = false)]
        public int MaxLogFileSize
        {
            get { return (int)TheThing.GetSafePropertyNumber(MyBaseThing, "MaxLogFileSize"); }
            set
            {
                TheThing.SetSafePropertyNumber(MyBaseThing, "MaxLogFileSize", value);
            }
        }

        [ConfigProperty(Generalize = false)]
        public int MaxLogFiles
        {
            get { return (int)TheThing.GetSafePropertyNumber(MyBaseThing, "MaxLogFiles"); }
            set
            {
                TheThing.SetSafePropertyNumber(MyBaseThing, "MaxLogFiles", value);
            }
        }

        /// <summary>
        /// Path to the Log File
        /// </summary>
        public DateTimeOffset LogFileDate
        {
            get { return TheThing.GetSafePropertyDate(MyBaseThing, "LogFileDate"); }
            set
            {
                TheThing.SetSafePropertyDate(MyBaseThing, "LogFileDate", value);
                mLogFileDate = value;
            }
        }

        [ConfigProperty(Generalize = false)]
        public bool WriteToConsole
        {
            get { return TheThing.GetSafePropertyBool(MyBaseThing, "WriteToConsole"); }
            set { TheThing.SetSafePropertyBool(MyBaseThing, "WriteToConsole", value); }
        }

        [ConfigProperty(Generalize = false)]
        public bool EachCategoryInOwnFile
        {
            get { return TheThing.GetSafePropertyBool(MyBaseThing, "EachCategoryInOwnFile"); }
            set { TheThing.SetSafePropertyBool(MyBaseThing, "EachCategoryInOwnFile", value); }
        }

        [ConfigProperty(Generalize = false)]
        public bool EachDayInOwnFile
        {
            get { return TheThing.GetSafePropertyBool(MyBaseThing, "EachDayInOwnFile"); }
            set { TheThing.SetSafePropertyBool(MyBaseThing, "EachDayInOwnFile", value); }
        }

        #region LogFile Viewer

        private string MyLogFilesTableName = null;
        private TheStorageMirror<TheBackupDefinition> MyLogFiles = null;

        private void OnStorageReady(ICDEThing pThing, object para)
        {
            MyLogFiles = new TheStorageMirror<TheBackupDefinition>(TheCDEngines.MyIStorageService)
            {
                CacheTableName = MyLogFilesTableName,
                IsRAMStore = true,
                IsCachePersistent = false,
                CacheStoreInterval = 0
            };
            MyLogFiles.RegisterEvent(eStoreEvents.StoreReady, sinkStorageReady);
            MyLogFiles.RegisterEvent(eStoreEvents.DeleteRequested, sinkDeleteLog);
            MyLogFiles.RegisterEvent(eStoreEvents.ReloadRequested, sinkStorageReady);
            MyLogFiles.InitializeStore(false, false);
        }

        void sinkDeleteLog(StoreEventArgs pArgs)
        {
            Guid tPluginG = TheCommonUtils.CGuid(pArgs.Para);
            if (tPluginG == Guid.Empty)
                return;
            var tPlugin = MyLogFiles.GetEntryByID(tPluginG);
            if (tPlugin != null && File.Exists(tPlugin.FileName))
                File.Delete(tPlugin.FileName);
        }

        void sinkStorageReady(StoreEventArgs pArgs)
        {
            string FileToReturn1 = TheCommonUtils.cdeFixupFileName(Address);
            DirectoryInfo di = new (FileToReturn1);
            List<TheFileInfo> tList = new ();
            TheISMManager.ProcessDirectory(di, ref tList, "", ".TXT", false, true);
            MyLogFiles.MyMirrorCache.Clear(false);
            foreach (TheFileInfo t in tList)
                MyLogFiles.AddAnItem(new TheBackupDefinition() { FileName = t.FileName, Title = t.Name, BackupSize = t.FileSize, BackupTime = t.CreateTime });
        }

        private void CreateLogTable(int StartFld, int pParentFld)
        {
            TheFormInfo tBackup = TheNMIEngine.AddForm(new TheFormInfo(TheThing.GetSafeThingGuid(MyBaseThing, "BKUPFORM"), eEngineName.NMIService, "Log Files", MyLogFilesTableName) { GetFromServiceOnly = true, TileWidth = -1, OrderBy = "BackupTime desc", TileHeight = 4 });
            TheNMIEngine.AddFields(tBackup, new List<TheFieldInfo>
                {
                {  new TheFieldInfo() { FldOrder=11,DataItem="BackupTime",Flags=0,Type=eFieldType.DateTime,Header="Log Create Date", FldWidth=2 }},
                {  new TheFieldInfo() { FldOrder=12,DataItem="Title",Flags=0,Type=eFieldType.SingleEnded,Header="Log Name", FldWidth=7 }},
                {  new TheFieldInfo() { FldOrder=13,DataItem="BackupSize",Flags=0,Type=eFieldType.SingleEnded,Header="Log Size",FldWidth=2 }},
                {  new TheFieldInfo() { FldOrder=100,DataItem="CDE_DELETE",Flags=2,cdeA=0x80, Type=eFieldType.TileButton, TileWidth=1, TileHeight=1 }},
                });
            TheFieldInfo btnDownload = TheNMIEngine.AddSmartControl(MyBaseThing, tBackup, eFieldType.TileButton, 1, 2, 0x0, "<i class='fa fa-3x'>&#xf019;</i>", "", new nmiCtrlTileButton() { ClassName = "cdeTableButton", TileHeight = 1, TileWidth = 1 });
            btnDownload.RegisterUXEvent(MyBaseThing, eUXEvents.OnClick, "DOWNLOAD", (pThing, pObj) =>
            {
                TheProcessMessage pMsg = pObj as TheProcessMessage;
                if (pMsg?.Message == null) return;

                string[] pCmds = pMsg.Message.PLS.Split(':');
                if (pCmds.Length > 2)
                {
                    TheBackupDefinition tFile = MyLogFiles.GetEntryByID(TheCommonUtils.CGuid(pCmds[2]));
                    if (tFile != null)
                    {
                        TSM tFilePush = new (eEngineName.ContentService, string.Format("CDE_FILE:{0}.txt:text/text", tFile.Title));
                        try
                        {
                            using (FileStream fr = new (tFile.FileName, FileMode.Open))
                            {
                                using (BinaryReader br = new (fr))
                                {
                                    tFilePush.PLB = br.ReadBytes((int)fr.Length);
                                }
                            }
                        }
                        catch (Exception)
                        { 
                            //ignored
                        }
                        if (tFilePush.PLB == null)
                            TheCommCore.PublishToOriginator(pMsg.Message, new TSM(eEngineName.NMIService, "NMI_TOAST", "Log cannot be downloaded..."));
                        else
                        {
                            TheCommCore.PublishToOriginator(pMsg.Message, new TSM(eEngineName.NMIService, "NMI_TOAST", "Log is downloading. Please wait"));
                            tFilePush.SID = pMsg.Message.SID;
                            tFilePush.PLS = "bin";
                            TheCommCore.PublishToOriginator(pMsg.Message, tFilePush);
                        }
                    }
                }
            });

            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.CollapsibleGroup, StartFld, 2, 0x0, "Logs on Node...", null, new nmiCtrlCollapsibleGroup { TileWidth = 12, IsSmall = true, DoClose = true, ParentFld = pParentFld, AllowHorizontalExpand = true, MaxTileWidth = 12 });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.Table, StartFld + 1, 0, 0, null, tBackup.cdeMID.ToString(), new nmiCtrlTableView { TileWidth = -1, IsDivOnly = true, ParentFld = StartFld, NoTE = true, TileHeight = -1, MID = TheThing.GetSafeThingGuid(MyBaseThing, "LOGMID"), MainClassName = "cdeInFormTable" });
        }
        #endregion
    }
}
