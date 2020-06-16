// SPDX-FileCopyrightText: 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

﻿using System;
using System.Collections.Generic;
using nsCDEngine.Engines;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.ViewModels;
using nsCDEngine.Engines.NMIService;
using System.IO;
using nsCDEngine.BaseClasses;
using System.Threading.Tasks;
using System.Linq;
using System.Xml.Linq;
using System.Collections.Concurrent;
using System.Threading;

namespace CDMyLogger.ViewModel
{
    [DeviceType(
    DeviceType = eTheLoggerServiceTypes.IISLogger,
    Capabilities = new eThingCaps[] { eThingCaps.ConfigManagement },
    Description = "IIS Error Logger")]
    class TheIISLogger : TheLoggerBase
    {
        /// <summary>
        /// Path to the folder where the IIS failed request tracing log files are stored
        /// </summary>
        [ConfigProperty(cdeT = ePropertyTypes.TString, DefaultValue = "", Description = "Path to the IIS failed request trace logs folder", Generalize = true, Required = true)]
        public string IISFailedReqTraceFolderPath
        {
            get { return MyBaseThing.Address; }
            set { MyBaseThing.Address=value; }
        }
        /// <summary>
        /// Disables the write-through of IIS errors to the SYSLOG.  Allows for a quick deactivation if necessary.
        /// </summary>
        [ConfigProperty(cdeT = ePropertyTypes.TBoolean, DefaultValue = false, Description = "Disables the write-through of IIS errors to the SYSLOG", Required = true)]
        public bool DisableWriteToSYSLOG
        {
            get { return TheThing.MemberGetSafePropertyBool(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyBool(MyBaseThing, value); }
        }
        private FileSystemWatcher watcher;
        private int errorCount = 0;
        private int bcTimeOutSeconds = 5;
        private const int MAX_ERRORS_TO_PROCESS = 100;
        private BlockingCollection<string> queuedErrorMessages = new BlockingCollection<string>(MAX_ERRORS_TO_PROCESS);

        public TheIISLogger(TheThing tBaseThing, ICDEPlugin pPluginBase) : base(tBaseThing, pPluginBase)
        {
            MyBaseThing.DeviceType = eTheLoggerServiceTypes.IISLogger;
        }

        protected override void DoInit()
        {
            base.DoInit();
            StartFolderWatch();
            MyBaseThing.RegisterOnChange(nameof(IISFailedReqTraceFolderPath), sinkOnTraceLogDirectoryChanged);
            LogIISErrors();
        }

        private void sinkOnTraceLogDirectoryChanged(cdeP obj)
        {
            if (watcher != null)
                watcher.Path = IISFailedReqTraceFolderPath;
            else
                StartFolderWatch();
        }

        protected override void DoCreateUX(TheFormInfo pForm)
        {
            base.DoCreateUX(pForm);
            var AdressFld = TheNMIEngine.GetFieldByFldOrder(pForm, 124);
            AdressFld.Header = "Path to IIS Trace Logs Folder";
            //TheNMIEngine.AddSmartControl(MyBaseThing, pForm, eFieldType.SingleEnded, 200, 2, 0, "Path to IIS Trace Logs Folder", nameof(IISFailedReqTraceFolderPath), new nmiCtrlSingleEnded { TileWidth = 6, ParentFld = 120, HelpText = "e.g. C:\\inetpub\\logs\\FailedReqLogFiles\\W3SVC1" });
            TheNMIEngine.AddSmartControl(MyBaseThing, pForm, eFieldType.SingleCheck, 201, 2, 0, "Disable Write to SYSLOG", nameof(DisableWriteToSYSLOG), new nmiCtrlSingleCheck { ParentFld = 120, DefaultValue = "false" });
        }

        public override void OnNewEvent(TheProcessMessage timer, object unused)
        {
            base.OnNewEvent(timer, unused);
        }

        public override bool LogEvent(TheEventLogData pItem)
        {
            return base.LogEvent(pItem);
        }

        private void StartFolderWatch()
        {
            try
            {
                if (!Directory.Exists(IISFailedReqTraceFolderPath))
                    Directory.CreateDirectory(IISFailedReqTraceFolderPath);
                watcher = new FileSystemWatcher()
                {
                    Path = IISFailedReqTraceFolderPath,
                    NotifyFilter = NotifyFilters.LastWrite,
                    Filter = "*.xml",
                    EnableRaisingEvents = true,
                };
                watcher.Changed += new FileSystemEventHandler(sinkOnNewTraceFile); 
            } 
            catch(Exception e)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(26010, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseEngine.GetEngineName(), "Could not begin IIS failed request trace log folder watch: " + e.Message, eMsgLevel.l1_Error));
            }
        }

        private void sinkOnNewTraceFile(object sender, FileSystemEventArgs e)
        {
            TheCDEKPIs.IncrementKPI(eKPINames.IISRejectedClientConnections);
            // Log any other necessary KPIs here
            if (!DisableWriteToSYSLOG)
            {
                var failedReqInfo = ReadIISFailedReqTrace(e.FullPath);
                string errorMsg = "IIS Failed Request: ";
                errorMsg += failedReqInfo.Verb != null ? $"{failedReqInfo.Verb} request " : "Request ";
                errorMsg += failedReqInfo.RequestURL != null ? $"on {failedReqInfo.RequestURL} " : "";
                errorMsg += failedReqInfo.ClientIP != null ? $"by {failedReqInfo.ClientIP} " : "";
                errorMsg += failedReqInfo.StatusCode != null ? $"returned code {failedReqInfo.StatusCode} " : "returned ";
                errorMsg += failedReqInfo.TimeTaken != null ? $"in {failedReqInfo.TimeTaken} ms " : "";
                errorMsg += failedReqInfo.ErrorCode != null ? $"- {failedReqInfo.ErrorCode}" : "";

                try
                {
                    // For now, just discard any messages if queue has hit its capacity
                    if (queuedErrorMessages.Count < queuedErrorMessages.BoundedCapacity)
                    {
                        TheCommonUtils.cdeRunTaskAsync("QueueIISError" + errorCount, (o) =>
                        {
                            queuedErrorMessages.TryAdd(errorMsg, bcTimeOutSeconds * 1000, TheBaseAssets.MasterSwitchCancelationToken);
                        });
                    }

                }
                catch (Exception) { }
            }
        }

        // Run loop on separate thread checking for new queued error messages
        private void LogIISErrors()
        {
            TheCommonUtils.cdeRunTaskAsync("LogIISErrors", (o) =>
            {
                using (CancellationTokenSource cancelTokenSource = CancellationTokenSource.CreateLinkedTokenSource(TheBaseAssets.MasterSwitchCancelationToken))
                {
                    while (queuedErrorMessages != null && !queuedErrorMessages.IsCompleted && TheBaseAssets.MasterSwitch)
                    {
                        string errorMsg = null;
                        try
                        {
                            queuedErrorMessages.TryTake(out errorMsg, bcTimeOutSeconds * 1000, cancelTokenSource.Token);
                        }
                        catch (Exception) { }
                        if (errorMsg != null)
                        {
                            TheBaseAssets.MySYSLOG.WriteToLog(26012, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseEngine.GetEngineName(), errorMsg, eMsgLevel.l1_Error));
                        }
                    }
                }
            }, null, true);
        }

        XNamespace msNameSpace = "http://schemas.microsoft.com/win/2004/08/events/event";
        XNamespace xmlns = "http://www.w3.org/2000/xmlns/";

        // "Parse" the .xml trace file with Xml.Linq methods
        private IISFailedReqInfo ReadIISFailedReqTrace(string filePath)
        {
            IISFailedReqInfo info = new IISFailedReqInfo();
            try
            {
                XNamespace frebNameSpace = "";
                XDocument traceLog = XDocument.Load(filePath);
                XElement failedReqInfo = traceLog.Element("failedRequest");
                if(failedReqInfo != null)
                {
                    info.StatusCode = failedReqInfo.Attribute("statusCode")?.Value;
                    info.RequestURL = failedReqInfo.Attribute("url")?.Value;
                    info.TimeTaken = failedReqInfo.Attribute("timeTaken")?.Value;
                    info.Verb = failedReqInfo.Attribute("verb")?.Value;
                    frebNameSpace = failedReqInfo.Attribute(xmlns + "freb")?.Value;
                }
                IEnumerable<XElement> events = traceLog.Descendants(msNameSpace + "Event");
                if (events != null)
                {
                    IEnumerable<XElement> description = events.Descendants(msNameSpace + "RenderingInfo")?.Descendants(frebNameSpace + "Description")?.Where(elem => elem.Attribute("Data")?.Value == "ErrorCode" && (elem.PreviousNode as XElement)?.Value == "BEGIN_REQUEST");
                    if(description.Count() > 0)
                        info.ErrorCode = description?.First()?.Value;

                    IEnumerable<XElement> localAddress = events.Descendants(msNameSpace + "EventData")?.Descendants(msNameSpace + "Data")?.Where(elem => elem.Attribute("Name")?.Value == "LocalAddress");
                    if(localAddress.Count() > 0)
                        info.ClientIP = localAddress?.First()?.Value;
                }
            }
            catch(Exception e)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(26011, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseEngine.GetEngineName(), "Could not read IIS failed request trace .xml document: " + e.Message, eMsgLevel.l1_Error));
            }
            return info;
        }

        public override bool Delete()
        {
            try
            {
                queuedErrorMessages.CompleteAdding();
            }
            catch (Exception) { }
            finally { queuedErrorMessages.Dispose(); }
            return true;
        }
    }

    class IISFailedReqInfo
    {
        public string StatusCode { get; set; }
        public string RequestURL { get; set; }
        public string TimeTaken { get; set; }
        public string Verb { get; set; }
        public string ErrorCode { get; set; }
        public string ClientIP { get; set; }
    }
}
