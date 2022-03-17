// SPDX-FileCopyrightText: 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

﻿using nsCDEngine.BaseClasses;
using nsCDEngine.Engines.ThingService;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CDMyMeshSender.ViewModel
{
    class TheLatencyAggregator
    {

        public TimeSpan AckLatencyLatest
        {
            get { return new TimeSpan(0, 0, 0, 0, TheCommonUtils.CInt(TheThing.GetSafePropertyNumber(MyBaseThing, nameof(AckLatencyLatest)))); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, nameof(AckLatencyLatest), value.TotalMilliseconds, true); }
        }

        public TimeSpan AckLatencyAvg
        {
            get { return new TimeSpan(0, 0, 0, 0, TheCommonUtils.CInt(TheThing.GetSafePropertyNumber(MyBaseThing, nameof(AckLatencyAvg)))); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, nameof(AckLatencyAvg), value.TotalMilliseconds, true); }
        }
        public TimeSpan AckLatencyMin
        {
            get { return new TimeSpan(0, 0, 0, 0, TheCommonUtils.CInt(TheThing.GetSafePropertyNumber(MyBaseThing, nameof(AckLatencyMin)))); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, nameof(AckLatencyMin), value.TotalMilliseconds, true); }
        }
        public TimeSpan AckLatencyMax
        {
            get { return new TimeSpan(0, 0, 0, 0, TheCommonUtils.CInt(TheThing.GetSafePropertyNumber(MyBaseThing, nameof(AckLatencyMax)))); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, nameof(AckLatencyMax), value.TotalMilliseconds, true); }
        }

        public TheLatencyAggregator(TheThing baseThing)
        {
            MyBaseThing = baseThing;
        }
        TheThing MyBaseThing;
        class LatencyEntry
        {
            public DateTimeOffset Timestamp;
            public TimeSpan Latency;
        }

        ConcurrentQueue<LatencyEntry> _latencyHistoryQueue = new ConcurrentQueue<LatencyEntry>();
        List<LatencyEntry> _latencyHistory = new List<LatencyEntry>();
        double _sumOfLatencies = 0;
        TimeSpan _aggregationWindowLength = new TimeSpan(1, 0, 0);

        public void UpdateAckLatency(TimeSpan newLatency)
        {
            _latencyHistoryQueue.Enqueue(new LatencyEntry { Timestamp = DateTimeOffset.Now, Latency = newLatency });
            EnsureDequeue();
        }

        volatile Task _task = null;
        object dequeueTaskLock = new object();
        void EnsureDequeue()
        {
            // Lock-free in high throughput scenarios: race condition (caller saw _task != null when the dequeue had already ended) closed in Dequeue
            if (_task == null)
            {
                // Take a lock to ensure there is exaclty on dequeue task
                lock (dequeueTaskLock)
                {
                    if (_task == null)
                    {
                        _task = Task.Factory.StartNew(() =>
                        {
                            Dequeue();
                        });
                    }
                }
            }
        }

        void Dequeue()
        {
            Task taskRunning;
            lock (dequeueTaskLock)
            {
                taskRunning = _task;
            }
            try
            {
                if (taskRunning?.Id != Task.CurrentId)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(95316, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, $"Dequeue Task Id does not match the expected task's Id on startup: Current {Task.CurrentId}. Expected {taskRunning?.Id}", eMsgLevel.l4_Message));
                }

                bool bRetry = false;
                do
                {
                    while (_latencyHistoryQueue.TryDequeue(out var newLatencyEntry))
                    {
                        AckLatencyLatest = newLatencyEntry.Latency; // TODO: Throttle here?
                        try
                        {
                            lock (_latencyHistory)
                            {
                                checked
                                {
                                    var windowStartTime = DateTimeOffset.Now - _aggregationWindowLength;
                                    LatencyEntry oldestLatencyEntry;
                                    bool bUpdateMax = false;
                                    bool bUpdateMin = false;
                                    while (_latencyHistory.Count > 0 && (oldestLatencyEntry = _latencyHistory[0]).Timestamp < windowStartTime)
                                    {
                                        _sumOfLatencies -= oldestLatencyEntry.Latency.TotalMilliseconds;
                                        _latencyHistory.RemoveAt(0);
                                        if (oldestLatencyEntry.Latency >= AckLatencyMax)
                                        {
                                            bUpdateMax = true;
                                        }
                                        if (oldestLatencyEntry.Latency <= AckLatencyMin)
                                        {
                                            bUpdateMin = true;
                                        }
                                    }
                                    _sumOfLatencies += newLatencyEntry.Latency.TotalMilliseconds;
                                    if (_latencyHistory.Count == 0)
                                    {
                                        AckLatencyMax = newLatencyEntry.Latency;
                                        AckLatencyMin = newLatencyEntry.Latency;
                                        bUpdateMin = false;
                                        bUpdateMax = false;
                                    }
                                    else
                                    {
                                        if (newLatencyEntry.Latency > AckLatencyMax)
                                        {
                                            AckLatencyMax = newLatencyEntry.Latency;
                                            bUpdateMin = false;
                                        }
                                        if (newLatencyEntry.Latency < AckLatencyMin || AckLatencyMin == TimeSpan.Zero)
                                        {
                                            AckLatencyMin = newLatencyEntry.Latency;
                                            bUpdateMin = false;
                                        }

                                    }
                                    _latencyHistory.Add(newLatencyEntry);
                                    if (bUpdateMax && AckLatencyMax > newLatencyEntry.Latency)
                                    {
                                        AckLatencyMax = _latencyHistory.Select(le => le.Latency).Max(); // Enums are thread safe (they take a snapshot)
                                    }
                                    if (bUpdateMin && AckLatencyMin < newLatencyEntry.Latency)
                                    {
                                        AckLatencyMin = _latencyHistory.Select(le => le.Latency).Min();
                                    }
                                }
                                AckLatencyAvg = new TimeSpan(0, 0, 0, 0, (int)_sumOfLatencies / _latencyHistory.Count);
                            }
                        }
                        catch (OverflowException)
                        {
                        }
                    }
                    _task = null;
                } while (bRetry);
            }
            finally
            {
                lock (dequeueTaskLock)
                {
                    if (_task == taskRunning)
                    {
                        _task = null;
                    }
                }
                // Ensure that in race conditions where a caller saw _task != null and didn't retrigger a dequee task we still drain the enqueued item(s)
                if (_latencyHistoryQueue.Count > 0)
                {
                    EnsureDequeue();
                }
            }
        }

    }
}
