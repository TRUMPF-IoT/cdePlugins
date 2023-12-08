// SPDX-FileCopyrightText: 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

﻿using nsCDEngine.BaseClasses;
using nsCDEngine.Engines;
using nsCDEngine.Engines.NMIService;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using nsTheCSVParser;
using System.IO;
using nsTheEventConverters;
using System.Diagnostics;
using System.Globalization;
using nsCDEngine.Communication;

namespace CDMyVThings.ViewModel
{
    [DeviceType(DeviceType = eVThings.eDataPlayback, Description = "Data Player reads recorded thing updates and plays them back.", Capabilities = new[] { eThingCaps.ConfigManagement })]
    public class TheDataPlayback: TheThingBase
    {
        // KPIs/Status
        public bool IsActive
        {
            get { return TheThing.MemberGetSafePropertyBool(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyBool(MyBaseThing, value); }
        }
        public bool IsStarted
        {
            get { return TheThing.MemberGetSafePropertyBool(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyBool(MyBaseThing, value); }
        }

        public double Gen_Stats_PropertyCounter
        {
            get { return TheThing.MemberGetSafePropertyNumber(MyBaseThing); }
            set
            {
                TheThing.MemberSetSafePropertyNumber(MyBaseThing, value);
                MyBaseThing.Value = TheCommonUtils.CStr(value);
            }
        }
        public double Gen_Stats_PropertiesPerSecond
        {
            get { return TheThing.MemberGetSafePropertyNumber(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyNumber(MyBaseThing, value); }
        }
        public DateTimeOffset Gen_StatsLastUpdateTime
        {
            get { return TheThing.MemberGetSafePropertyDate(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyDate(MyBaseThing, value); }
        }

        public int ItemCount
        {
            get { return (int)TheThing.MemberGetSafePropertyNumber(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyNumber(MyBaseThing, value); }
        }

        public TimeSpan LoadDuration
        {
            get { return TheCommonUtils.CTimeSpan(TheThing.MemberGetSafeProperty(MyBaseThing)); }
            set { TheThing.MemberSetSafePropertyString(MyBaseThing, TheCommonUtils.CStr(value)); }
        }

        public TimeSpan PlaybackDuration
        {
            get { return TheCommonUtils.CTimeSpan(TheThing.MemberGetSafeProperty(MyBaseThing)); }
            set { TheThing.MemberSetSafePropertyString(MyBaseThing, TheCommonUtils.CStr(value)); }
        }

        // Config properties
        [ConfigProperty(DefaultValue = false)]
        public bool AutoStart
        {
            get { return TheThing.MemberGetSafePropertyBool(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyBool(MyBaseThing, value); }
        }

        [ConfigProperty(DefaultValue = false)]
        public bool IsDisabled
        {
            get { return TheThing.MemberGetSafePropertyBool(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyBool(MyBaseThing, value); }
        }

        [ConfigProperty(DefaultValue = "meshsenderdata.log")]
        public string InputFileName
        {
            get { return TheThing.MemberGetSafePropertyString(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyString(MyBaseThing, value); }
        }

        [ConfigProperty(DefaultValue = 1)]
        public double PlaybackSpeedFactor
        {
            get { return TheThing.MemberGetSafePropertyNumber(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyNumber(MyBaseThing, value); }
        }

        [ConfigProperty(DefaultValue = 1)]
        public double ParallelPlaybackCount
        {
            get { return TheThing.MemberGetSafePropertyNumber(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyNumber(MyBaseThing, value); }
        }
        [ConfigProperty(DefaultValue = false)]
        public bool RestartPlayback
        {
            get { return TheThing.MemberGetSafePropertyBool(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyBool(MyBaseThing, value); }
        }

        [ConfigProperty(DefaultValue = 1)]
        public int ReplayCount
        {
            get { return (int)TheThing.MemberGetSafePropertyNumber(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyNumber(MyBaseThing, value); }
        }

        [ConfigProperty(DefaultValue = false)]
        public bool AdjustTimestamps
        {
            get { return TheThing.MemberGetSafePropertyBool(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyBool(MyBaseThing, value); }
        }

        [ConfigProperty(DefaultValue = 0, Units = "ms")]
        public int PlaybackItemDelay
        {
            get { return (int) TheThing.MemberGetSafePropertyNumber(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyNumber(MyBaseThing, value); }
        }
        [ConfigProperty(DefaultValue = 0, Units = "ms")]
        public int MaxItemDelay
        {
            get { return (int)TheThing.MemberGetSafePropertyNumber(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyNumber(MyBaseThing, value); }
        }

        [ConfigProperty(DefaultValue = 60000, Units = "ms")]
        public int AutoStartDelay
        {
            get { return (int)TheThing.MemberGetSafePropertyNumber(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyNumber(MyBaseThing, value); }
        }
        [ConfigProperty(DefaultValue = "")]
        public string PlaybackEngineName
        {
            get { return TheThing.MemberGetSafePropertyString(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyString(MyBaseThing, value); }
        }
        [ConfigProperty(DefaultValue = "")]
        public string PlaybackDeviceType
        {
            get { return TheThing.MemberGetSafePropertyString(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyString(MyBaseThing, value); }
        }

        List<Task> playbackTasks = new List<Task>();
        CancellationTokenSource playbackCancel;
        private async Task StopPlaybackAsync(string message)
        {
            MyBaseThing.StatusLevel = 6;
            MyBaseThing.LastMessage = "Playback stopping";
            var ts = playbackCancel;
            playbackCancel = null;
            ts?.Cancel();

            await TheCommonUtils.TaskWhenAll(playbackTasks);
            playbackTasks.Clear();
            _kpiTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            OnKpiUpdate(null);
            IsStarted = false;
            MyBaseThing.StatusLevel = 0;
            MyBaseThing.LastMessage = string.IsNullOrEmpty(message) ? "Playback stopped" : message;
        }

        Timer _kpiTimer;
        TimeSpan _kpiInterval = new TimeSpan(0, 0, 2);
        DateTimeOffset _lastKpiUpdate = DateTimeOffset.MinValue;
        private void OnKpiUpdate(object notUsed)
        {
            try
            {
                var propsSinceLastCheck = Interlocked.Exchange(ref _propertyCounter, 0);
                Gen_Stats_PropertyCounter += propsSinceLastCheck;
                var now = DateTimeOffset.Now;
                if (_lastKpiUpdate != DateTimeOffset.MinValue)
                {
                    Gen_Stats_PropertiesPerSecond = propsSinceLastCheck / (now - _lastKpiUpdate).TotalSeconds;
                }
                _lastKpiUpdate = now;
                Gen_StatsLastUpdateTime = _lastSendTime;
                sinkStatChanged(null, null);
            }
            catch { }
        }

        bool _isStarting;
        readonly object startLock = new object();
        Stopwatch sw;
        private async Task StartPlaybackAsync(bool bFromAutoStart)
        {
            try
            {
                lock (startLock)
                {
                    if (_isStarting || IsStarted || playbackCancel?.IsCancellationRequested == false || playbackTasks?.Count > 0)
                    {
                        MyBaseThing.LastMessage = "Playback already started";
                        return;
                    }

                    _isStarting = true;
                }
            }
            catch { }
            try
            {  
                if (_kpiTimer == null)
                {
                    _kpiTimer = new Timer(OnKpiUpdate, null, 0, (int) _kpiInterval.TotalMilliseconds);
                }
                else
                {
                    _kpiTimer.Change(0, (int) _kpiInterval.TotalMilliseconds);
                }

                sw = new Stopwatch();
                sw.Start();
                MyBaseThing.LastMessage = "Loading data...";
                var thingUpdates = await LoadThingUpdatesAsync(InputFileName);
                ItemCount = thingUpdates.Count();
                LoadDuration = sw.Elapsed;
                MyBaseThing.LastMessage = $"Data loaded: {ItemCount} items in {LoadDuration}. Starting playback...";

                sw.Restart();
                var propCountBefore = Gen_Stats_PropertyCounter;

                playbackTasks.Clear();
                playbackCancel?.Cancel();
                playbackCancel = new CancellationTokenSource();
                var cancelCombined = CancellationTokenSource.CreateLinkedTokenSource(playbackCancel.Token, TheBaseAssets.MasterSwitchCancelationToken);
                var startupDelayRange = new TimeSpan(0, 0, 0);

                var tThingWithAllProperties = new TheThing
                {
                    FriendlyName = "ignored",
                    ID = "ignored",
                    EngineName = PlaybackEngineName,
                    DeviceType = PlaybackDeviceType,
                };
                _ = new CDMyMeshManager.Contracts.MsgReportTestStatus
                {
                    NodeId = TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID,
                    PercentCompleted = 0,
                    SuccessRate = 0,
                    Status = CDMyMeshManager.Contracts.eTestStatus.Running,
                    TestRunId = TheCommonUtils.CGuid(TheBaseAssets.MySettings.GetSetting("TestRunID")),
                    Timestamp = DateTimeOffset.Now,
                    ResultDetails = new Dictionary<string, object>
                             {
                                {"Message", "Starting playback "},
                             },
                }.Publish().Result;
                if (bFromAutoStart && AutoStartDelay > 0)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(700, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(MyBaseThing.EngineName, $"Playback gathering all properties from the data file.", eMsgLevel.l6_Debug));
                    await PlaybackLoop(tThingWithAllProperties, cancelCombined.Token, thingUpdates, startupDelayRange, bFromAutoStart);
                    TheBaseAssets.MySYSLOG.WriteToLog(700, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(MyBaseThing.EngineName, $"Playback gathered all properties from the data file. Found {tThingWithAllProperties.MyPropertyBag.Count} properties.", eMsgLevel.l6_Debug));
                }
                var allProperties = tThingWithAllProperties.GetAllProperties(10).Where(p => p.Name != nameof(TheThing.ID) && p.Name != nameof(TheThing.FriendlyName) && p.Name != nameof(TheThing.EngineName) && p.Name != nameof(TheThing.DeviceType));
                _ = new CDMyMeshManager.Contracts.MsgReportTestStatus
                {
                    NodeId = TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID,
                    PercentCompleted = 0,
                    SuccessRate = 0,
                    Status = CDMyMeshManager.Contracts.eTestStatus.Running,
                    TestRunId = TheCommonUtils.CGuid(TheBaseAssets.MySettings.GetSetting("TestRunID")),
                    Timestamp = DateTimeOffset.Now,
                    ResultDetails = new Dictionary<string, object>
                             {
                                {"Message", "Starting playback loops"},
                                {"UniquePropertyCount", allProperties.Count() },
                                {"NumberThings", ParallelPlaybackCount },
                             },
                }.Publish().Result;

                _ = RunPlaybackAsync(allProperties, thingUpdates, startupDelayRange, bFromAutoStart, propCountBefore);
            }
            catch (Exception e)
            {
                IsStarted = false;
                MyBaseThing.StatusLevel = 2;
                MyBaseThing.LastMessage = $"Error starting playback: {e.Message}";
            }
            finally
            {
                lock (startLock)
                {
                    _isStarting = false;
                }
            }
        }

        class MeshSenderDataLog
        {
            public DateTimeOffset TimePublished { get; set; }
            public TheThingStore PLS { get; set; }
        }

        private static Task<IEnumerable<object>> LoadThingUpdatesAsync(string fileName)
        {
            if (fileName.ToLowerInvariant() != "meshsenderdata.log") // We allow this filename so one can use the immediate local output from the mesh sender, all others are sanitized and must be effectively uploaded via the file uploader
            {
                fileName = TheCommonUtils.cdeFixupFileName(fileName);
            }
            var fileContent = File.ReadAllText(fileName, Encoding.UTF8);
            if (!fileContent.StartsWith("["))
            {
                fileContent = "[" + fileContent + "]";
            }
            var logEntries = TheCommonUtils.DeserializeJSONStringToObject<List<Dictionary<string,object>>>(fileContent);
            return TheCommonUtils.TaskFromResult(logEntries.Where(e => e.ContainsKey("PLS")).Select(e => e["PLS"]));
        }

        private async Task RunPlaybackAsync(IEnumerable<cdeP> allProperties, IEnumerable<object> thingUpdates, TimeSpan startupDelayRange, bool bFromAutoStart, double propCountBefore)
        { 
            for(int j = 1; j <= ReplayCount; j++)
            {
                playbackCancel?.Cancel();
                playbackCancel = new CancellationTokenSource();
                var cancelCombined = CancellationTokenSource.CreateLinkedTokenSource(playbackCancel.Token, TheBaseAssets.MasterSwitchCancelationToken);
                IsStarted = true;
                MyBaseThing.StatusLevel = 1;
                MyBaseThing.LastMessage = $"Playback started: {ItemCount} items. {ParallelPlaybackCount} things.";
                for (int i = 1; i <= ParallelPlaybackCount; i++)
                {
                    TheThing tThingOverride = null;
                    if (!string.IsNullOrEmpty(PlaybackEngineName))
                    {
                        if (TheThingRegistry.GetBaseEngine(PlaybackEngineName) == null)
                        {
                            TheCDEngines.RegisterNewMiniRelay(PlaybackEngineName);
                        }

                        var thingName = $"{MyBaseThing.FriendlyName}{i:D6}";
                        tThingOverride = TheThingRegistry.GetThingByID(PlaybackEngineName, thingName, true);
                        if (tThingOverride == null)
                        {
                            tThingOverride = new TheThing
                            {
                                FriendlyName = thingName,
                                ID = thingName,
                                EngineName = PlaybackEngineName,
                                DeviceType = PlaybackDeviceType,
                            };

                            foreach (var prop in allProperties)
                            {
                                tThingOverride.SetProperty(prop.Name, prop.Value, prop.cdeT);
                            }
                            TheThingRegistry.RegisterThing(tThingOverride);
                        }
                        // This only works if the plug-in is actually installed, not with mini relay
                        //var createThingInfo = new TheThingRegistry.MsgCreateThingRequestV1
                        //{
                        //    EngineName = PlaybackEngineName,
                        //    DeviceType = PlaybackDeviceType,
                        //    InstanceId = thingName,
                        //    FriendlyName = thingName,
                        //    CreateIfNotExist = true,
                        //    DoNotModifyIfExists = true,
                        //    OwnerAddress = MyBaseThing,
                        //    Properties = new Dictionary<string, object> { { "ID", thingName } },
                        //};
                        //tThingOverride = await TheThingRegistry.CreateOwnedThingAsync(createThingInfo, new TimeSpan(0, 1, 0));
                        if (tThingOverride == null)
                        {
                            MyBaseThing.LastMessage = "Error creating playback thing";
                            break;
                        }
                    }
                    var playbackTask = TheCommonUtils.cdeRunTaskChainAsync($"Playback{i}", o => PlaybackLoop(tThingOverride, cancelCombined.Token, thingUpdates, startupDelayRange, bFromAutoStart), true);
                    playbackTasks.Add(playbackTask);
                }

                _ = await TheCommonUtils.TaskWhenAll(playbackTasks).ContinueWith(async (t) =>
                {
                    try
                    {
                        PlaybackDuration = sw.Elapsed;
                        sw.Stop();
                        OnKpiUpdate(null);
                        var propCount = Gen_Stats_PropertyCounter - propCountBefore;
                        var message = $"Playback done. {propCount} props in {PlaybackDuration}. {propCount / PlaybackDuration.TotalSeconds} props/s. {ItemCount * ParallelPlaybackCount / PlaybackDuration.TotalSeconds} items/s.";
                        //MyBaseThing.LastMessage = message;
                        TheBaseAssets.MySYSLOG.WriteToLog(700, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(MyBaseThing.EngineName, message, eMsgLevel.l6_Debug));
                        await StopPlaybackAsync(message);
                        _ = new CDMyMeshManager.Contracts.MsgReportTestStatus
                        {
                            NodeId = TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID,
                            PercentCompleted = (j * 1.0 / ReplayCount) * 100,
                            SuccessRate = 100,
                            Status = j == ReplayCount ? CDMyMeshManager.Contracts.eTestStatus.Success : CDMyMeshManager.Contracts.eTestStatus.Running,
                            TestRunId = TheCommonUtils.CGuid(TheBaseAssets.MySettings.GetSetting("TestRunID")),
                            Timestamp = DateTimeOffset.Now,
                            ResultDetails = new Dictionary<string, object>
                             {
                                {"Message", message },
                                {"PropertyCount", propCount },
                                {"DurationInSeconds", PlaybackDuration.TotalSeconds }
                             },
                        }.Publish().Result;

                    }
                    catch { }
                }, TaskContinuationOptions.OnlyOnRanToCompletion);
            }
        }

        private async Task PlaybackLoop(TheThing tThingOverride, CancellationToken cancelToken, IEnumerable<object> updatesToPlay, TimeSpan startupDelayRange, bool bFromAutoStart)
        {
            var lastItemTime = DateTimeOffset.MaxValue;
            var previousUpdateTime = DateTimeOffset.Now;
            if (bFromAutoStart && AutoStartDelay > 0 && tThingOverride.FriendlyName != "ignored") // hack to delay only for real things
            {
                TheBaseAssets.MySYSLOG.WriteToLog(700, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(MyBaseThing.EngineName, $"Playback loop for {tThingOverride?.FriendlyName} holding for {AutoStartDelay} ms.", eMsgLevel.l6_Debug));
                await TheCommonUtils.TaskDelayOneEye(AutoStartDelay, 100);
            }
            TheBaseAssets.MySYSLOG.WriteToLog(700, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(MyBaseThing.EngineName, $"Playback loop started for {tThingOverride?.FriendlyName}", eMsgLevel.l6_Debug));

            if (startupDelayRange > TimeSpan.Zero)
            {
                var startupDelayInMs = (uint) startupDelayRange.TotalMilliseconds;
                var randomDelay = TheCommonUtils.GetRandomUInt(0, startupDelayInMs);
                await TheCommonUtils.TaskDelayOneEye((int) randomDelay, 100, cancelToken).ConfigureAwait(false);
            }

            var eventconverter = TheEventConverters.GetEventConverter("JSON Things", true) as JSonThingEventConverter;

            // These get set in the callback from ProcessEventData so we can process them afterwards
            List<Tuple<TheThing,TheThingStore>> updatesProcessed = new List<Tuple<TheThing, TheThingStore>>();
            eventconverter.ApplyUpdateCallback = (thing, update) => 
            {
                updatesProcessed.Add(Tuple.Create(thing, update));
                return false;
            };
            eventconverter.eventDecoder = (o => o.ToString());

            do
            {
                foreach (var updateObj in updatesToPlay)
                {
                    updatesProcessed.Clear();
                    eventconverter.ProcessEventData(tThingOverride, updateObj, DateTimeOffset.Now);
                    if (IsDisabled)
                    {
                        //How can a cacnel token be set?
                        break;
                    }
                    if (cancelToken.IsCancellationRequested)
                    {
                        break;
                    }
                    foreach (var updateAndThing in updatesProcessed)
                    {
                        TimeSpan timeToWait = TimeSpan.Zero;

                        var tThingProcessed = updateAndThing.Item1;
                        var tThingUpdateProcessed = updateAndThing.Item2;
                        if (PlaybackSpeedFactor > 0)
                        {
                            var now = DateTimeOffset.Now;
                            var timeSinceLastUpdate = now - previousUpdateTime;
                            var timeToNextItem = tThingUpdateProcessed.cdeCTIM - lastItemTime;
                            if (timeToNextItem > TimeSpan.Zero && timeSinceLastUpdate < timeToNextItem)
                            {
                                timeToWait = timeToNextItem - timeSinceLastUpdate;
                                if (timeToWait > TimeSpan.Zero)
                                {
                                    if (PlaybackSpeedFactor != 1)
                                    {
                                        timeToWait = new TimeSpan(0, 0, 0, 0, (int)(timeToWait.TotalMilliseconds / PlaybackSpeedFactor));
                                    }
                                    if (MaxItemDelay > 0 && timeToWait.TotalMilliseconds > MaxItemDelay)
                                    {
                                        timeToWait = new TimeSpan(0, 0, 0, 0, MaxItemDelay);
                                    }
                                }
                                else
                                {
                                    // falling behind!
                                }
                            }
                        }
                        else if (PlaybackItemDelay > 0)
                        {
                            timeToWait = new TimeSpan(0, 0, 0, 0, PlaybackItemDelay);
                        }

                        if (timeToWait > TimeSpan.Zero)
                        {
                            await TheCommonUtils.TaskDelayOneEye((int)timeToWait.TotalMilliseconds, 100, cancelToken).ConfigureAwait(false);
                        }

                        if (cancelToken.IsCancellationRequested)
                        {
                            break;
                        }

                        lastItemTime = tThingUpdateProcessed.cdeCTIM;
                        {
                            var now = DateTimeOffset.Now;

                            previousUpdateTime = now;

                            var timeToSend = AdjustTimestamps ? now : tThingUpdateProcessed.cdeCTIM;
                            if (tThingOverride != null)
                            {
                                if (tThingUpdateProcessed.PB.ContainsKey("FriendlyName"))
                                {
                                    tThingUpdateProcessed.PB["FriendlyName"] = tThingOverride.FriendlyName;
                                }
                                if (tThingUpdateProcessed.PB.ContainsKey("ID"))
                                {
                                    tThingUpdateProcessed.PB["ID"] = tThingOverride.FriendlyName;
                                }
                                if (tThingUpdateProcessed.PB.ContainsKey("EngineName"))
                                {
                                    tThingUpdateProcessed.PB["EngineName"] = tThingOverride.EngineName;
                                }
                                if (tThingUpdateProcessed.PB.ContainsKey("DeviceType"))
                                {
                                    tThingUpdateProcessed.PB["DeviceType"] = tThingOverride.DeviceType;
                                }
                                if (tThingUpdateProcessed.PB.ContainsKey("DeviceType"))
                                {
                                    tThingUpdateProcessed.PB["DeviceType"] = tThingOverride.DeviceType;
                                }
                            }
                            tThingProcessed.SetProperties(tThingUpdateProcessed.PB, timeToSend);
                            Interlocked.Add(ref _propertyCounter, tThingUpdateProcessed.PB.Count);
                            _lastSendTime = timeToSend;
                        }
                    }
                }
            } while (RestartPlayback && !cancelToken.IsCancellationRequested && !IsDisabled);
            TheBaseAssets.MySYSLOG.WriteToLog(700, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(MyBaseThing.EngineName, $"Playback loop stopped for {tThingOverride?.FriendlyName}", eMsgLevel.l6_Debug));
        }
        long _propertyCounter;
        DateTimeOffset _lastSendTime;

        public override bool Init()
        {
            if (mIsInitCalled) return false;
            mIsInitCalled = true;
            IsStarted = false;
            if (string.IsNullOrEmpty(MyBaseThing.ID))
            {
                MyBaseThing.ID = Guid.NewGuid().ToString();
            }

            if (MyBaseThing.GetProperty(nameof(PlaybackSpeedFactor), false) == null)
            {
                PlaybackSpeedFactor = 1;
            }
            if (MyBaseThing.GetProperty(nameof(InputFileName), false) == null)
            {
                InputFileName = "meshsenderdata.log";
            }
            if (MyBaseThing.GetProperty(nameof(ParallelPlaybackCount), false) == null)
            {
                ParallelPlaybackCount = 1;
            }

            MyBaseThing.RegisterOnChange(nameof(ParallelPlaybackCount), (o) => {
                if (IsStarted)
                {
                    StopPlaybackAsync("Changed number of things").ContinueWith(t => StartPlaybackAsync(false));
                }
            });

            MyBaseThing.Value = "0";
            IsActive = true;
            MyBaseThing.StatusLevel = 0;
            if (AutoStart)
            {
                TheCommonUtils.cdeRunAsync("DataPLayerAutostart", true, async o =>
                {
                    //if (AutoStartDelay > 0)
                    //{
                    //    await TheCommonUtils.TaskDelayOneEye(AutoStartDelay, 100);
                    //}
                    await StartPlaybackAsync(true);
                });
            }

            MyBaseThing.RegisterEvent(eEngineEvents.FileReceived, sinkFileReceived);

            mIsInitialized = true;
            return true;
        }

        protected TheFormInfo MyStatusForm;
        protected TheDashPanelInfo SummaryForm;
        protected TheFieldInfo CountBar;
        protected TheFieldInfo PropTable;
        public override bool CreateUX()
        {
            if (mIsUXInitCalled) return false;
            mIsUXInitCalled = true;

            var tFlds = TheNMIEngine.AddStandardForm(MyBaseThing, null, 12, null, "Value");
            MyStatusForm = tFlds["Form"] as TheFormInfo;
            SummaryForm = tFlds["DashIcon"] as TheDashPanelInfo;
            SummaryForm.PropertyBag = new ThePropertyBag() { string.Format("Format={0}<br>{{0}} Properties", MyBaseThing.FriendlyName), "TileWidth=2" };
            SummaryForm.RegisterPropertyChanged(sinkStatChanged);

            var ts = TheNMIEngine.AddStatusBlock(MyBaseThing, MyStatusForm, 10);
            ts["Group"].SetParent(1);

            var tc = TheNMIEngine.AddStartingBlock(MyBaseThing, MyStatusForm, 200, async (pMsg, DoStart) =>
            {
                if (DoStart)
                {
                    await StartPlaybackAsync(false);
                }
                else
                {
                    await StopPlaybackAsync(null);
                }
            });
            tc["Group"].SetParent(49);
            //ts["Group"].Header = "Settings...";

            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.CollapsibleGroup, 49, 2, 0x0, "###CDMyVThings.TheVThings#Settings#Settings...###", null, new nmiCtrlCollapsibleGroup() { ParentFld = 1, DoClose=true, IsSmall=true });


            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.Number, 50, 2, 0x0, "###CDMyVThings.TheVThings#PlaybackSpeedFactor#Speed Factor###", nameof(PlaybackSpeedFactor), new nmiCtrlNumber() { DefaultValue = "1", TileWidth = 3, ParentFld = 49 });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.Number, 60, 2, 0x0, "###CDMyVThings.TheVThings#PlaybackMaxItemDelay#Maximum Delay###", nameof(MaxItemDelay), new nmiCtrlNumber() { TileWidth = 3, ParentFld = 49 });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.Number, 70, 2, 0x0, "###CDMyVThings.TheVThings#PlaybackItemDelay#Item Delay###", nameof(PlaybackItemDelay), new nmiCtrlNumber() { TileWidth = 3, ParentFld = 49 });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.Number, 75, 2, 0x0, "###CDMyVThings.TheVThings#AutoStartDelay#Auto Start Delay###", nameof(AutoStartDelay), new nmiCtrlSingleCheck() { TileWidth = 3, ParentFld = 49 });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.SingleCheck, 80, 2, 0x0, "###CDMyVThings.TheVThings#PlaybackRestart#Restart###", nameof(RestartPlayback), new nmiCtrlSingleCheck() { TileWidth = 3, ParentFld = 49 });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.SingleCheck, 85, 2, 0x0, "###CDMyVThings.TheVThings#PlaybackAdjustTimestamps#Adjust times###", nameof(AdjustTimestamps), new nmiCtrlSingleCheck() { TileWidth = 3, ParentFld = 49 });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.Number, 87, 2, 0x0, "###CDMyVThings.TheVThings#ReplayCount#Replay Count###", nameof(ReplayCount), new nmiCtrlNumber() { TileWidth = 3, ParentFld = 49, DefaultValue = "1" });

            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.SingleEnded, 90, 2, 0x0, "###CDMyVThings.TheVThings#PlaybackInputFileName#Input File###", nameof(InputFileName), new nmiCtrlSingleEnded() { ParentFld = 49 });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.Number, 86, 2, 0x0, "###CDMyVThings.TheVThings#PlaybackNumberThings#Number Things###", nameof(ParallelPlaybackCount), new nmiCtrlNumber() { TileWidth = 3, ParentFld = 49 });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.ThingPicker, 110, 2, 0x0, "###CDMyVThings.TheVThings#PlaybackEngineName#Engine Name###", nameof(PlaybackEngineName), new nmiCtrlThingPicker() { IncludeEngines=true, Filter="DeviceType=IBaseEngine", ParentFld = 49 });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.DeviceTypePicker, 115, 2, 0x0, "###CDMyVThings.TheVThings#PlaybackDeviceType#Device Type###", nameof(PlaybackDeviceType), new nmiCtrlDeviceTypePicker() { ParentFld = 49 });


            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.CollapsibleGroup, 120, 2, 0x0, "###CDMyVThings.TheVThings#Settings#Upload file...###", null, new nmiCtrlCollapsibleGroup() { ParentFld = 49, DoClose = true, IsSmall = true });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.DropUploader,125, 2, 0x0, "###CDMyVThings.TheVThings#PlaybackInputFileDropper#Drop Input File here###", null,
                new nmiCtrlDropUploader { TileHeight = 6, NoTE = true, TileWidth = 6, EngineName = MyBaseEngine.GetEngineName(), MaxFileSize = 100000000, ParentFld=120 });


            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.CollapsibleGroup, 20, 2, 0x0, "###CDMyVThings.TheVThings#KPIs#KPIs...###", null, new nmiCtrlCollapsibleGroup() { ParentFld = 1, TileWidth=6, IsSmall = true });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.Number, 135, 0, 0x0, "###CDMyVThings.TheVThings#PropertiesperSecond546134#Properties per Second###", nameof(Gen_Stats_PropertiesPerSecond), new nmiCtrlNumber() { ParentFld = 20 });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.Number, 140, 0, 0x0, "###CDMyVThings.TheVThings#Propertycount904939#Property count###", nameof(Gen_Stats_PropertyCounter), new nmiCtrlNumber() { ParentFld = 20 });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.DateTime, 150, 0, 0x0, "###CDMyVThings.TheVThings#PlaybackUpdateTime#Update time###", nameof(Gen_StatsLastUpdateTime), new nmiCtrlDateTime() { ParentFld = 20 });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.TimeSpan, 160, 0, 0x0, "###CDMyVThings.TheVThings#PlaybackLoadDuration#Load duration###", nameof(LoadDuration), new nmiCtrlDateTime() { ParentFld = 20 });
            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.TimeSpan, 170, 0, 0x0, "###CDMyVThings.TheVThings#PlaybackDuration#Playback duration###", nameof(PlaybackDuration), new nmiCtrlDateTime() { ParentFld = 20 });


            TheFieldInfo mResetCounterButton = TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.TileButton, 180, 2, 0, "###CDMyVThings.TheVThings#ResetCounter741318#Reset Counter###", false, "", null, new nmiCtrlTileButton() { ParentFld = 130, ClassName = "cdeGoodActionButton" });
            mResetCounterButton.RegisterUXEvent(MyBaseThing, eUXEvents.OnClick, "", (pThing, pPara) =>
            {
                if (!(pPara is TheProcessMessage pMsg) || pMsg.Message == null) return;
                this.Gen_Stats_PropertyCounter = 0;
                Interlocked.Exchange(ref _propertyCounter, 0);
                sinkStatChanged(null, null);
            });

            mIsUXInitialized = true;
            return true;
        }

        void sinkStatChanged(TheDashPanelInfo tDas, cdeP prop)
        {
            MyBaseThing.Value = Gen_Stats_PropertyCounter.ToString();
            SummaryForm?.SetUXProperty(Guid.Empty, string.Format("iValue={0}", MyBaseThing.Value));
        }

        void sinkFileReceived(ICDEThing pThing, object pFileName)
        {
            TheProcessMessage pMsg = pFileName as TheProcessMessage;
            if (pMsg?.Message == null) return;

            InputFileName = TheCommonUtils.cdeFixupFileName(pMsg.Message.TXT);
            TheCommCore.PublishToOriginator(pMsg.Message, new TSM(eEngineName.NMIService, "NMI_TOAST", $"###Input data file ({pMsg.Message.TXT}) received!###"));
        }

        IBaseEngine MyBaseEngine;
        public TheDataPlayback(TheThing pThing, IBaseEngine pEngine)
        {
            if (pThing != null)
                MyBaseThing = pThing;
            else
                MyBaseThing = new TheThing();
            MyBaseEngine = pEngine;
            MyBaseThing.DeviceType = eVThings.eDataPlayback;
            MyBaseThing.EngineName = pEngine.GetEngineName();
            MyBaseThing.SetIThingObject(this);
        }
    }
}
