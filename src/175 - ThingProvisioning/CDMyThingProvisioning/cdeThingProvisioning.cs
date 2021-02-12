// SPDX-FileCopyrightText: 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

﻿using System;
using System.Collections.Generic;

using nsCDEngine.Engines;
using nsCDEngine.Engines.NMIService;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.Communication;
using nsCDEngine.ViewModels;
using nsCDEngine.BaseClasses;
using nsCDEngine.Engines.StorageService;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using System.Text.RegularExpressions;

namespace CDMyThingProvisioning
{
    class cdeThingProvisioning : ThePluginBase
    {
        // Initialization flags
        protected bool mIsInitStarted = false;
        protected bool mIsInitCompleted = false;
        protected bool mIsUXInitStarted = false;
        protected bool mIsUXInitCompleted = false;

        Guid guidEngineID = new Guid("{C0A23C9C-86A8-4A82-8C95-829E1D149583}");
        private readonly string strFriendlyName = "Thing Provisioning Service";

        #region ICDEPlugin - interface methods for service (engine)
        /// <summary>
        /// InitEngineAssets - The C-DEngine calls this initialization
        /// function as part of registering this service (engine)
        /// </summary>
        /// <param name="pBase">The C-DEngine creates a base engine object.
        /// This parameter is a reference to that base engine object.
        /// We keep a copy because it will be very useful to us.
        /// </param>
        public override void InitEngineAssets(IBaseEngine pBase)
        {
            base.InitEngineAssets(pBase);

            MyBaseEngine.SetEngineID(guidEngineID);          // Unique identifier for our service (engine)
            MyBaseEngine.SetFriendlyName(strFriendlyName);

            MyBaseEngine.SetCDEMinVersion(4.1111);
            MyBaseEngine.SetPluginInfo("This service lets you create and configure things.",       // Describe plugin for Plugin Store
                                       0,                       // pPrice - retail price (default = 0)
                                       null,                    // Custom home page - default = /ServiceID
                                       "toplogo-150.png",       // pIcon - custom icon.
                                       "C-Labs",                // pDeveloper - name of the plugin developer.
                                       "http://www.c-labs.com", // pDeveloperUrl - URL to developer home page.
                                       new List<string> { "Service" }); // pCategories - Search categories for service.
        }
        #endregion

        TheStorageMirror<ScriptSnapshot> MyScriptTableStorage;
        //List<ScriptSnapshot> MyScriptTableList;
        //Dictionary<string, Guid> SnapShotGuids;
        public override bool Init()
        {
            if (!mIsInitStarted)
            {
                mIsInitStarted = true;
                MyBaseThing.RegisterEvent(eEngineEvents.IncomingMessage, HandleMessage);
                mIsInitCompleted = true;
                MyBaseEngine.ProcessInitialized();
                MyBaseThing.SetStatus(4, "Waiting for other services before running scripts");
                TheBaseEngine.WaitForEnginesStarted(OnEnginesStarted);
                SetupStorageMirror();
            }
            return true;
        }

        private void OnEnginesStarted(ICDEThing arg1, object arg2)
        {
            RunScriptsAsync(null);
        }
#pragma warning disable CS0649
        private class SendMessage
        {
            public string MessageName;
            public TheMessageAddress Target;
            public object Parameters;
            public Dictionary<string, string> outputs; // output value path (JPath?) => variable name
            public int timeout;
        }
        private class TheScriptStep
        {
            public string Condition;
            public int? RetryCount;
            public bool? DontRetryOnEmptyResponse;
            public SendMessage Message;
            public string FriendlyName;

            public string GetName()
            {
                if (!string.IsNullOrEmpty(FriendlyName))
                {
                    return FriendlyName;
                }
                return Message?.MessageName;
            }
        }
        private class TheScript
        {
            public TheScriptStep[] Steps;

            public string Name { get; set; }
            public string[] DependsOn { get; set; }

            public string FileName { get; internal set; }
            public string ScriptRaw { get; internal set; }
            public override string ToString()
            {
                return $"{Name} {Steps?.Length}" + (DependsOn?.Length > 0 ? $"[{DependsOn.Aggregate((s1,s2) => s1 + "," + s2)}]" : "");
            }
        }
#pragma warning restore CS0649

        private async void RunScriptsAsync(object _)
        {
            try
            {
                MyBaseThing.SetStatus(4, "Running scripts.");
                var variables = new TheThing(); // Using this as a property bag to hold variables that can be shared across the scripts -> Maybe use the script thing itself for this? Keeps an audit trail of the state of the script...

                var scriptDir = TheCommonUtils.cdeFixupFileName(@"ClientBin\scripts");
                var scriptFiles = Directory.EnumerateFiles(scriptDir, "*.cdescript").ToList();
                var scriptsRun = new List<string>();
                var pendingScripts = new List<string>();
                var scriptsToRun = new List<TheScript>();
                int pendingScriptCount;
                pendingScriptCount = pendingScripts.Count;
                foreach (var scriptFile in scriptFiles)
                {
                    try
                    {
                        TheScript script = LoadScript(scriptFile);
                        scriptsToRun.Add(script);
                    }
                    catch (Exception e)
                    {
                        string scriptName = scriptFile;
                        try
                        {
                            scriptName = Path.GetFileNameWithoutExtension(scriptFile);
                        }
                        catch { }
                        UpdateStorageList(scriptFile, $"Failed to read: {e.Message}", -1, null, null, false);
                        TheBaseAssets.MySYSLOG.WriteToLog(175000, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, "Error reading cde script file", eMsgLevel.l1_Error, e.ToString()));
                    }
                }
                foreach (var dependentScript in scriptsToRun.Where(script => script.DependsOn != null && script.DependsOn.Length > 0).ToList())
                {
                    foreach (var dependencyName in dependentScript.DependsOn)
                    {
                        var dependencyScriptIndex = scriptsToRun.FindIndex(script => script.Name == dependencyName);
                        if (dependencyScriptIndex < 0)
                        {
                            TheBaseAssets.MySYSLOG.WriteToLog(175007, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, "Unable to run script with dependencies", eMsgLevel.l1_Error, $"Dependent: '{dependentScript.Name}'. Dependency not found: '{dependencyName}'"));
                        }
                        else
                        {
                            var dependentScriptIndex = scriptsToRun.IndexOf(dependentScript);
                            if (dependentScriptIndex >= 0 && dependencyScriptIndex > dependentScriptIndex)
                            {
                                scriptsToRun.Insert(dependencyScriptIndex + 1, dependentScript);
                                scriptsToRun.RemoveAt(dependentScriptIndex);
                            }
                        }
                    }
                }

                // This assumes RunScriptsAsync is only called from Init() (= on gate restart)
                foreach (var oldScript in MyScriptTableStorage.TheValues)
                {
                    if (oldScript.ScriptStatus == "Not found")
                    {
                        MyScriptTableStorage.RemoveAnItem(oldScript, null);
                    }
                    else
                    {
                        oldScript.ScriptStatus = "Not found";
                    }
                }

                foreach (var script in scriptsToRun)
                {
                    int stepNumber = 1;
                    foreach (var step in script.Steps ?? new TheScriptStep[0])
                    {
                        UpdateStorageList(script.Name, "Pending", stepNumber, script, null, false);
                        stepNumber++;
                    }
                }

                int index = 0;
                var scriptTasks = new List<Task>();
                foreach (var script in scriptsToRun)
                {
                    Task task = null;
                    if (script.DependsOn?.Length > 0)
                    {
                        if (index > 1)
                        {
                            task = scriptTasks[index - 1].ContinueWith(t => RunScriptAsync(script, variables));
                        }
                    }
                    if (task == null)
                    {
                        task = RunScriptAsync(script, variables);
                    }
                    scriptTasks.Add(task);
                    scriptsRun.Add(script.Name);
                }
                await TheCommonUtils.TaskWhenAll(scriptTasks);
                if (MyBaseThing.StatusLevel == 4)
                {
                    if (scriptTasks?.Count > 0)
                    {
                        MyBaseThing.SetStatus(1, $"All {scriptTasks?.Count} scripts applied.");
                    }
                    else
                    {
                        MyBaseThing.SetStatus(1, "No scripts found.");
                    }
                    TheBaseAssets.MySYSLOG.WriteToLog(175001, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(MyBaseThing.EngineName, "Scripts applied", eMsgLevel.l3_ImportantMessage, $"Number of scripts: {scriptTasks?.Count}"));
                }
            }
            catch (DirectoryNotFoundException)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(175001, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(MyBaseThing.EngineName, "Error finding or running script files", eMsgLevel.l3_ImportantMessage, "Script directory not found"));
                MyBaseThing.SetStatus(1, "No script directory found.");
            }
            catch (Exception e)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(175001, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, "Error finding or running script files", eMsgLevel.l1_Error, e.ToString()));
                MyBaseThing.SetStatus(3, $"Error while finding or running cdescript files: {e.Message}");
            }
        }

        private static TheScript LoadScript(string scriptFile)
        {
            var scriptText = File.ReadAllText(scriptFile);
            //var scriptText = File.ReadAllText(@"test.json");
            scriptText = Regex.Replace(scriptText, @"(?:jsonFragment\()(.*)(?:\))", (m) =>
            {
                return TheCommonUtils.GenerateFinalStr(m.Groups[1].Value);
            });
            var script = TheCommonUtils.DeserializeJSONStringToObject<TheScript>(scriptText);
            if (string.IsNullOrEmpty(script.Name))
            {
                script.Name = Path.GetFileNameWithoutExtension(scriptFile);
            }
            script.FileName = scriptFile;
            script.ScriptRaw = scriptText;
            return script;
        }

        private async Task RunScriptAsync(TheScript script, TheThing variables, int stepNumber = 1, bool replay = false)
        {
            TheThing variablesSnapshot;
            try
            {
                for (; stepNumber <= script.Steps.Length; stepNumber++)
                {

                    //Clone thing before step occurs
                    variablesSnapshot = new TheThing();
                    variables.CloneThingAndPropertyMetaData(variablesSnapshot, true);

                    var step = script.Steps[stepNumber - 1];

                    var existingSnapshot = MyScriptTableStorage.MyMirrorCache.GetEntryByFunc(snapshot => snapshot.ScriptName == script.Name && snapshot.ScriptStep == stepNumber);
                    if (existingSnapshot?.Disabled == true)
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(175002, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, "Finished script step: skipped step because it was disabled", eMsgLevel.l3_ImportantMessage, TheCommonUtils.SerializeObjectToJSONString(new Dictionary<String, object> {
                                { "Script", script.Name },
                                { "Step", stepNumber },
                                { "Message", step.Message.MessageName },
                                { "Target", step.Message.Target },
                            })));

                        UpdateStorageList(script.Name, "Disabled", stepNumber, script, variablesSnapshot, replay);
                        continue;
                    }

                    if (step.Condition != null)
                    {
                        var condition = TheCommonUtils.GenerateFinalStr(step.Condition, variables);
                        if (
                            (condition == "" || condition.ToLowerInvariant() == "false" || condition.Trim() == "0")
                            || (condition.StartsWith("!") && condition.Length >= 1 && (condition.Substring(1).ToLowerInvariant() == "true") || condition.Substring(1).Trim() == "1"))
                        {
                            TheBaseAssets.MySYSLOG.WriteToLog(175002, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, "Finished script step: skipped step due to condition not met", eMsgLevel.l3_ImportantMessage, TheCommonUtils.SerializeObjectToJSONString(new Dictionary<String, object> {
                                { "Script", script.Name },
                                { "Step", stepNumber },
                                { "Message", step.Message.MessageName },
                                { "Target", step.Message.Target },
                                { "Condition", step.Condition },
                                { "ConditionEvaluated", condition },
                            })));

                            UpdateStorageList(script.Name, "Condition Not Met", stepNumber, script, variablesSnapshot, replay);

                            continue;
                        }
                    }
                    var messageType = TheCommonUtils.GenerateFinalStr(step.Message.MessageName, variables);
                    var txtPayload = TheCommonUtils.GenerateFinalStr(step.Message.Parameters?.ToString(), variables);
                    {
                        var txtPayload2 = txtPayload?.Replace("\"\\\"", "");
                        var txtPayload3 = txtPayload2?.Replace("\\\"\"", "");
                        txtPayload = txtPayload3;
                    }

                    // TODO Need a simpler and more flexible way to specify thing address in the script JSON
                    var target = step.Message.Target;
                    if (target == null)
                    {
                        if (txtPayload.Contains("EngineName"))
                        {
                            var payloadDict = TheCommonUtils.DeserializeJSONStringToObject<Dictionary<string, object>>(txtPayload);
                            object engineNameInferred = null;
                            if (payloadDict?.TryGetValue("EngineName", out engineNameInferred) == true && !string.IsNullOrEmpty(engineNameInferred?.ToString()))
                            {
                                target = new TheMessageAddress {  EngineName = engineNameInferred.ToString() };
                            }
                        }
                    }
                    if (target.EngineName.StartsWith("%") || target.EngineName.StartsWith("{"))
                    {
                        target.EngineName = TheCommonUtils.GenerateFinalStr(target.EngineName, variables);
                        // TODO Clean this up: support a serialized TheMessageAddress in the engine name, so that an output variable can be fed into a method invocation
                        try
                        {
                            var newTarget = TheCommonUtils.DeserializeJSONStringToObject<TheMessageAddress>(target.EngineName);
                            if (newTarget != null)
                            {
                                target = newTarget;
                            }
                        }
                        catch
                        {
                            // parsing error: ignore, will result in other errors downstream
                        }
                    }

                    await TheThingRegistry.WaitForInitializeAsync(target);
                    bool bDoRetry;
                    int remainingRetryCount = step.RetryCount ?? 0;
                    do
                    {
                        existingSnapshot = MyScriptTableStorage.MyMirrorCache.GetEntryByFunc(snapshot => snapshot.ScriptName == script.Name && snapshot.ScriptStep == stepNumber);
                        if (existingSnapshot?.Disabled == true)
                        {
                            TheBaseAssets.MySYSLOG.WriteToLog(175002, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, "Finished script step: skipped step because it was disabled", eMsgLevel.l3_ImportantMessage, TheCommonUtils.SerializeObjectToJSONString(new Dictionary<String, object> {
                                { "Script", script.Name },
                                { "Step", stepNumber },
                                { "Message", step.Message.MessageName },
                                { "Target", step.Message.Target },
                            })));

                            UpdateStorageList(script.Name, "Disabled", stepNumber, script, variablesSnapshot, replay);
                            break;
                        }

                        bDoRetry = false;
                        var response = await TheCommRequestResponse.PublishRequestAsync(MyBaseThing, target, messageType, new TimeSpan(0, 0, 0, 0, step.Message.timeout), null, txtPayload, null);
                        if (!string.IsNullOrEmpty(response?.PLS))
                        {
                            var outputs = TheCommonUtils.DeserializeJSONStringToObject<Dictionary<string, object>>(response.PLS);
                            if (outputs != null)
                            {
                                if (step.Message.outputs != null)
                                {
                                    foreach (var output in step.Message.outputs)
                                    {
                                        if (output.Key == "*")
                                        {
                                            variables.SetProperty(output.Value, response.PLS);
                                        }
                                        else if (outputs.TryGetValue(output.Key, out var outputValue))
                                        {
                                            variables.SetProperty(output.Value, outputValue);
                                            if (output.Value.Contains("Error") && !string.IsNullOrEmpty(TheCommonUtils.CStr(outputValue)))
                                            {
                                                TheBaseAssets.MySYSLOG.WriteToLog(175004, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, "Error in script step: output reported error", eMsgLevel.l1_Error, TheCommonUtils.SerializeObjectToJSONString(new Dictionary<String, object> {
                                                    { "Script", script.Name },
                                                    { "Step", stepNumber },
                                                    { "Message", messageType },
                                                    { "Target", target },
                                                    { "PLS", txtPayload },
                                                    { "Response", response },
                                                    { "ResponsePLS", response?.PLS},
                                                })));

                                                UpdateStorageList(script.Name, $"Error {outputValue} in output", stepNumber, script, variablesSnapshot, replay);

                                                if (remainingRetryCount < 0 || remainingRetryCount > 0)
                                                {
                                                    remainingRetryCount--;
                                                    bDoRetry = true;
                                                }
                                                string retriesRemaining = bDoRetry ? (remainingRetryCount >= 0 ? $"{remainingRetryCount + 1}" : "infinite") : "none";
                                                MyBaseThing.SetStatus(3, $"Error in script '{script?.Name}', step {stepNumber}: output '{output.Value}' reported error {outputValue}. Retries remaining: {retriesRemaining}");
                                            }
                                        }
                                        else
                                        {
                                            // TODO provide access to sub-elements in the JSON
                                            //var outputParts = output.Key.Split('/');
                                            //dynamic currentNode = outputs;
                                            //foreach (var outputPart in outputParts)
                                            //{
                                            //    if (currentNode.TryGetValue(outputPart, out var nextNode))
                                            //    {
                                            //        currentNode = nextNode;
                                            //    }
                                            //}
                                        }
                                    }
                                }
                                TheBaseAssets.MySYSLOG.WriteToLog(175003, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, "Finished script step", eMsgLevel.l3_ImportantMessage, TheCommonUtils.SerializeObjectToJSONString(new Dictionary<String, object> {
                                    { "Script", script.Name },
                                    { "Step", stepNumber },
                                    { "Message", messageType },
                                    { "Target", target },
                                    { "PLS", txtPayload },
                                    { "ResponsePLS", response.PLS},
                                })));

                                UpdateStorageList(script.Name, "Finished", stepNumber, script, variablesSnapshot, replay);
                            }
                            else
                            {
                                TheBaseAssets.MySYSLOG.WriteToLog(175004, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, "Error in script step: no outputs found in response", eMsgLevel.l1_Error, TheCommonUtils.SerializeObjectToJSONString(new Dictionary<String, object> {
                                    { "Script", script.Name },
                                    { "Step", stepNumber },
                                    { "Message", messageType },
                                    { "Target", target },
                                    { "PLS", txtPayload },
                                    { "Response", response },
                                    { "ResponsePLS", response?.PLS},
                                })));

                                UpdateStorageList(script.Name, "Error: No Output", stepNumber, script, variablesSnapshot, replay);

                                if (step.DontRetryOnEmptyResponse != true && (remainingRetryCount < 0 || remainingRetryCount > 0))
                                {
                                    remainingRetryCount--;
                                    bDoRetry = true;
                                }
                                string retriesRemaining = bDoRetry ? (remainingRetryCount >= 0 ? $"{remainingRetryCount + 1}" : "infinite") : "none";
                                MyBaseThing.SetStatus(3, $"Error in script '{script?.Name}', step {stepNumber}: no outputs found in response. Retries remaining: {retriesRemaining}");
                            }
                        }
                        else
                        {
                            TheBaseAssets.MySYSLOG.WriteToLog(175005, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, "Error Script step: timeout", eMsgLevel.l1_Error, TheCommonUtils.SerializeObjectToJSONString(new Dictionary<String, object> {
                                { "Script", script.Name },
                                { "Step", stepNumber },
                                { "Message", messageType },
                                { "Target", target },
                                { "PLS", txtPayload },
                                { "Response", response },
                            })));

                            UpdateStorageList(script.Name, "Error: Timeout", stepNumber, script, variablesSnapshot, replay);

                            //Retries infinitely unless count is specified
                            if (remainingRetryCount < 0 || remainingRetryCount > 0)
                            {
                                remainingRetryCount--;
                                bDoRetry = true;
                            }
                            string retriesRemaining = bDoRetry ? (remainingRetryCount >= 0 ? $"{remainingRetryCount + 1}" : "infinite") : "none";
                            MyBaseThing.SetStatus(3, $"Error in script '{script?.Name}', step {stepNumber}: timeout. Retries remaining: {retriesRemaining}");
                        }
                        if (bDoRetry)
                        {
                            await TheCommonUtils.TaskDelayOneEye(30000, 100).ConfigureAwait(false);
                        }
                    } while (bDoRetry && TheBaseAssets.MasterSwitch);
                }
            }
            catch (Exception e)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(175006, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, "Error in script step", eMsgLevel.l1_Error, TheCommonUtils.SerializeObjectToJSONString(new Dictionary<String, object> {
                                { "Script", script.Name },
                                { "Exception", e.Message },
                            })));
                MyBaseThing.SetStatus(3, $"Error in script '{script?.Name}': {e.Message}");
                //Save variables instead of snapshot in case of error
                UpdateStorageList(script.Name, $"Error: {e.Message}", stepNumber, script, variables, replay);
            }
        }

        //Helper to update storage list
        private void UpdateStorageList(string name, string status, int step, TheScript script, TheThing context, bool replay)
        {
            ScriptSnapshot existingSnapshot = null;
            // No longer adding a new entry on replay because the list is now used to disable script/steps etc.
            existingSnapshot = MyScriptTableStorage.MyMirrorCache.GetEntryByFunc(snapshot => snapshot.ScriptName == name && snapshot.ScriptStep == step);
            var stepName = step > 0 && step <= script?.Steps.Length ? script?.Steps[step - 1]?.GetName() ?? "" : "";
            if (existingSnapshot == null)
            {
                var newSnapshot = new ScriptSnapshot
                {
                    ScriptName = name,
                    ScriptStatus = status,
                    StepName = stepName,
                    ScriptStep = step,
                    ContextScript = script,
                    FileName = script?.FileName,
                    ScriptRaw = script?.ScriptRaw,
                    Context = context,
                    LastUpdate = DateTimeOffset.Now,
                };
                MyScriptTableStorage.AddAnItem(newSnapshot);
            }
            else
            {
                existingSnapshot.ScriptName = name;
                existingSnapshot.ScriptStatus = status;
                existingSnapshot.StepName = stepName;
                existingSnapshot.ScriptStep = step;
                existingSnapshot.ContextScript = script;
                existingSnapshot.FileName = script?.FileName;
                existingSnapshot.ScriptRaw = script?.ScriptRaw;
                existingSnapshot.Context = context;
                existingSnapshot.LastUpdate = DateTimeOffset.Now;
                MyScriptTableStorage.UpdateItem(existingSnapshot);
            }
        }

        protected TheFormInfo MyScriptTable = null;
        public override bool CreateUX()
        {
            if (!mIsUXInitStarted)
            {
                mIsUXInitStarted = true;

                //NUI Definition for All clients
                    TheNMIEngine.AddDashboard(MyBaseThing, new TheDashboardInfo(MyBaseEngine, "Thing Provisioner")
                    { PropertyBag = new nmiDashboardTile { Caption = "Thing Provisioner", Category = "Services", Thumbnail = "FA5Sf110", } });

                if (TheCommonUtils.CBool(TheBaseAssets.MySettings.GetSetting("RedPill")))
                {
                    MyScriptTable = new TheFormInfo(TheThing.GetSafeThingGuid(MyBaseThing, "SCRIPT_TABLE"), eEngineName.NMIService, "Script Table", $"ScriptTableFields{MyBaseThing.ID}") { PropertyBag = new nmiCtrlFormView { TileWidth = 12, TileHeight = 10 } };
                    TheNMIEngine.AddFormToThingUX(MyBaseThing, MyScriptTable, "CMyTable", "Script Table", 1, 3, 0xF0, null, null, new ThePropertyBag() { "Visibility=true" });
                    TheNMIEngine.AddSmartControl(MyBaseThing, MyScriptTable, eFieldType.SingleCheck, 48, 2, 0, "Disabled", nameof(ScriptSnapshot.Disabled), new nmiCtrlSingleEnded() { TileWidth = 1, FldWidth = 1 });
                    TheNMIEngine.AddSmartControl(MyBaseThing, MyScriptTable, eFieldType.SingleEnded, 50, 0, 0, "Script Name", nameof(ScriptSnapshot.ScriptName), new nmiCtrlSingleEnded() { TileWidth = 2, FldWidth = 2 });
                    TheNMIEngine.AddSmartControl(MyBaseThing, MyScriptTable, eFieldType.Number, 55, 0, 0, "Script Step", nameof(ScriptSnapshot.ScriptStep), new nmiCtrlNumber() { TileWidth = 1, FldWidth = 1 });
                    TheNMIEngine.AddSmartControl(MyBaseThing, MyScriptTable, eFieldType.SingleEnded, 60, 0, 0, "Step Status", nameof(ScriptSnapshot.ScriptStatus), new nmiCtrlSingleEnded() { TileWidth = 2, FldWidth = 2 });
                    TheNMIEngine.AddSmartControl(MyBaseThing, MyScriptTable, eFieldType.SingleEnded, 65, 2, 0, "Step Name", nameof(ScriptSnapshot.StepName), new nmiCtrlSingleEnded() { TileWidth = 2, FldWidth = 2 });
                    TheNMIEngine.AddSmartControl(MyBaseThing, MyScriptTable, eFieldType.DateTime, 67, 0, 0, "Time", nameof(ScriptSnapshot.LastUpdate), new nmiCtrlDateTime() { TileWidth = 1, FldWidth = 1 });

                    TheNMIEngine.AddTableButtons(MyScriptTable);

                    if (TheCommonUtils.CBool(TheBaseAssets.MySettings.GetSetting("EnableDiagnostics")))
                    {
                        CreateScriptEditTemplate();

                        var button = TheNMIEngine.AddSmartControl(MyBaseThing, MyScriptTable, eFieldType.TileButton, 45, 2, 0, "Replay", null, new nmiCtrlTileButton() { TileWidth = 1, TileHeight = 1, FldWidth = 1, ClassName = "cdeGoodActionButton" });
                        button.RegisterUXEvent(MyBaseThing, eUXEvents.OnClick, "test", async (sender, pPara) =>
                        {
                            try
                            {
                                if (!(pPara is TheProcessMessage pMSG) || pMSG.Message == null) return;

                                string[] cmd = pMSG.Message.PLS.Split(':');
                                if (cmd.Length > 2)
                                {
                                    var tScript = MyScriptTableStorage.GetEntryByID(TheCommonUtils.CGuid(cmd[2]));

                                    if (null != tScript)
                                    {
                                        if (string.IsNullOrEmpty(tScript.ScriptName))
                                        {
                                            var script = LoadScript(tScript.FileName);
                                            await RunScriptAsync(script, tScript.Context, tScript.ScriptStep - 1, true);
                                        }
                                    //Rerun script step from snapshot.
                                    await RunScriptAsync(tScript.ContextScript, tScript.Context, tScript.ScriptStep - 1, true);
                                    }
                                }
                            }
                            catch (Exception e)
                            {
                                MyBaseThing.LastMessage = $"Error replaying: {e.Message}";
                            }
                        });
                    }
                }
                
                TheNMIEngine.AddAboutButton(MyBaseThing, false);
                mIsUXInitCompleted = true;
            }
            return true;
        }

        //Helper class to create storage mirror for script data. 
        private class ScriptSnapshot : TheMetaDataBase
        {
            public TheFieldInfo Button { get; set; }
            public bool Disabled { get; set; }
            public string ScriptName { get; set; }
            public string ScriptStatus { get; set; }
            public string StepName { get; set; }
            public int ScriptStep { get; set; }
            public TheThing Context { get; set; }
            public TheScript ContextScript { get; set; }
            public string FileName { get; set; }
            public string ScriptRaw { get; set; }
            public string ScriptError { get; set; }
            public DateTimeOffset LastUpdate { get; set; }
        }

        //Helper Function
        private void SetupStorageMirror()
        {
            MyScriptTableStorage = new TheStorageMirror<ScriptSnapshot>(TheCDEngines.MyIStorageService)
            {
                IsRAMStore = true,
                IsCachePersistent = true,
                IsStoreIntervalInSeconds = true,
                CacheTableName = $"ScriptTableFields{MyBaseThing.ID}"               
            };
            MyScriptTableStorage.RegisterEvent(eStoreEvents.StoreReady, SinkStoreReady);
            MyScriptTableStorage.InitializeStore(new TheStorageMirrorParameters { TrackInsertionOrder = true,
                                                                                  CanBeFlushed = true,
                                                                                  ResetContent = false});
        }

        private void SinkStoreReady(StoreEventArgs e)
        {
            //MyScriptTableStorage.FlushCache(false);
            FireEvent(eThingEvents.Initialized, this, true, true);
        }

        #region Message Handling
        /// <summary>
        /// Handles Messages sent from a host sub-engine to its clients
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="pIncoming"></param>
        public override void HandleMessage(ICDEThing sender, object pIncoming)
        {
            if (!(pIncoming is TheProcessMessage pMsg)) return;

            string[] cmd = pMsg.Message.TXT.Split(':');
            switch (cmd[0])
            {
                case "CDE_INITIALIZED":
                    MyBaseEngine.SetInitialized(pMsg.Message);
                    break;
                default:
                    break;
            }
        }
        #endregion

        TheFormInfo MSE;
        void CreateScriptEditTemplate()
        {
            MSE = new TheFormInfo(new Guid("{00000000-6AD1-45AE-BE61-96AF02329613}"), eEngineName.NMIService, "Script Editor", null) { DefaultView = eDefaultView.Form, IsNotAutoLoading=true, PropertyBag = new nmiCtrlFormTemplate { TableReference = $"{TheThing.GetSafeThingGuid(MyBaseThing, "SCRIPT_TABLE")}", TileWidth = 12 } };
            TheNMIEngine.AddFormToThingUX(MyBaseThing, MSE, "CMyForm", "Script Editor", 100, 3, 0xF0, null, null, new ThePropertyBag() { "Visibility=false" });

            TheNMIEngine.AddSmartControl(MyBaseThing, MSE, eFieldType.SingleEnded, 50, 0, 0, "Script Name", "ScriptName", new nmiCtrlSingleEnded() { TileWidth = 12 });
            TheNMIEngine.AddSmartControl(MyBaseThing, MSE, eFieldType.TextArea, 60, 2, 0, null, "ScriptRaw", new nmiCtrlTextArea() { NoTE = true, TileWidth = 12, TileHeight = 12 });
            TheNMIEngine.AddSmartControl(MyBaseThing, MSE, eFieldType.TextArea, 110, 0, 0, "Last Error", "ScriptError", new nmiCtrlTextArea() { TileWidth = 12, TileHeight = 3 });
            var but = TheNMIEngine.AddSmartControl(MyBaseThing, MSE, eFieldType.TileButton, 100, 2, 0, "Save Script", null, new nmiCtrlTileButton() { TileWidth = 6, NoTE=true, TileHeight = 1, ClassName = "cdeGoodActionButton" });
            but.RegisterUXEvent(MyBaseThing, eUXEvents.OnClick, "SaveScript", (sender, pPara) =>
            {
                if (!(pPara is TheProcessMessage pMSG) || pMSG.Message == null) return;
                var tP = pMSG.Message.PLS.Split(':');
                var t = MyScriptTableStorage.MyMirrorCache.GetEntryByID(TheCommonUtils.CGuid(tP[2]));
                try
                {
                    var Res = TheCommonUtils.DeserializeJSONStringToObject<TheScript>(t.ScriptRaw);
                    t.ScriptError = "ok";
                    MSE.Reload(pMSG, true);
                }
                catch (Exception ee)
                {
                    t.ScriptError = ee.ToString();
                    MSE.Reload(pMSG, true);
                    return;
                }
                File.WriteAllText(t.FileName, t.ScriptRaw);
                TheCommCore.PublishToOriginator(pMSG.Message, new TSM(eEngineName.NMIService, "NMI_TTS", TheThing.GetSafeThingGuid(MyBaseThing, "SCRIPT_TABLE").ToString()));  //This is the same as the TTS:.. from above...but with an additional roundtrip to the Relay
            });
            TheNMIEngine.AddSmartControl(MyBaseThing, MSE, eFieldType.TileButton, 101, 2, 0, "Cancel", null, new nmiCtrlTileButton() { TileWidth = 6, NoTE = true, TileHeight = 1, OnClick=$"TTS:{TheThing.GetSafeThingGuid(MyBaseThing, "SCRIPT_TABLE")}", ClassName = "cdeBadActionButton" });

            var tBut = TheNMIEngine.AddSmartControl(MyBaseThing, MyScriptTable, eFieldType.TileButton, 70, 2, 0, "Edit Script...", null, new nmiCtrlTileButton() { TileWidth = 1, TileHeight = 1, ClassName = "cdeTransitButton" , OnClick=$"TTS:{new Guid("{00000000-6AD1-45AE-BE61-96AF02329613}")}:<%cdeMID%>"  });
        }
    }
}
