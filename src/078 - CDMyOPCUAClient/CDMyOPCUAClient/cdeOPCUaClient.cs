// SPDX-FileCopyrightText: 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0
using System;
using System.Collections.Generic;

using nsCDEngine.Engines;
using nsCDEngine.BaseClasses;
using nsCDEngine.Communication;
using nsCDEngine.ViewModels;
using nsCDEngine.Engines.NMIService;
using nsCDEngine.Engines.ThingService;
using CDMyOPCUAClient.ViewModel;
using Opc.Ua;
using System.Globalization;

namespace CDMyOPCUAClient
{
    public class eOPCDeviceTypes
    {
        public const string OPCRemoteServer = "OPC-UA Remote Server";
        public const string OPCLiveTag = "OPC-UA Tag";
        public const string OPCMethod = "OPC-UA Method";
        public const string OPCEvent = "OPC-UA Event";
    }
    [EngineAssetInfo(
        FriendlyName = "My OPCUA Client Service",
        IsService = true,
        Capabilities = new[] { eThingCaps.ProtocolTransformer, eThingCaps.SensorProvider, eThingCaps.ConfigManagement, eThingCaps.SensorContainer },
        EngineID = "{3C1D53AE-E932-4D11-B1F9-F12428DEC27C}",
        CDEMinVersion = 4.2050,
        LongDescription = "This Protocol Transformer plugin allows to create Things or Properties of Things from OPC UA Tags",
        IconUrl = "images/OPCLogo.png",
        Developer = "C-Labs and its licensors",
        DeveloperUrl = "http://www.c-labs.com",
        AcceptsFilePush = true,
        ManifestFiles = new []
        {
                "Opc.Ua.Core.dll",
                "Opc.Ua.Client.dll",
                "Opc.Ua.Configuration.dll"
        },
        Categories = ["Protocol"],
        Platforms = new cdePlatform[]
        {
            cdePlatform.NETSTD_V20,
        }
        )]
    partial class cdeOPCUaClient : ThePluginBase
    {
        #region ICDEPlugin
        /// <summary>
        /// This constructor is called by The C-DEngine during initialization in order to register this service
        /// </summary>
        /// <param name="pBase">The C-DEngine is creating a Host for this engine and hands it to the Plugin Service</param>
        public override void InitEngineAssets(IBaseEngine pBase)
        {
            base.InitEngineAssets(pBase);
        }
        public cdeOPCUaClient()
        {
        }
#endregion

#region Service Properties
        bool EnableTracing
        {
            get { return TheThing.GetSafePropertyBool(MyBaseThing, nameof(EnableTracing)); }
            set { TheThing.SetSafePropertyBool(MyBaseThing, nameof(EnableTracing), value); }
        }
        bool EnableTracingToLog
        {
            get { return TheThing.GetSafePropertyBool(MyBaseThing, nameof(EnableTracingToLog)); }
            set { TheThing.SetSafePropertyBool(MyBaseThing, nameof(EnableTracingToLog), value); }
        }
        int OPCTraceMask
        {
            get { return TheCommonUtils.CInt(TheThing.GetSafePropertyNumber(MyBaseThing, nameof(OPCTraceMask))); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, nameof(OPCTraceMask), value); }
        }
#endregion

        public override bool Init()
        {
            if (mIsInitCalled) return false;
            mIsInitCalled = true;

            MyBaseThing.StatusLevel = 4;
            MyBaseThing.RegisterEvent(eEngineEvents.IncomingMessage, HandleMessage);
            MyBaseEngine.RegisterEvent(eEngineEvents.ThingRegistered, sinkRegistered);
            MyBaseEngine.RegisterEvent(eEngineEvents.ThingDeleted, sinkRegistered);

            if (EnableTracing)
            {
                Opc.Ua.Utils.SetTraceOutput(Utils.TraceOutput.FileOnly);
                Opc.Ua.Utils.SetTraceLog("opcclient.log", false);
            }
            else
            {
                Opc.Ua.Utils.SetTraceOutput(Utils.TraceOutput.Off);
            }
            if (EnableTracingToLog)
            {
                Opc.Ua.Utils.Tracing.TraceEventHandler += OnOpcLibraryTrace;
            }
            TheCommonUtils.cdeRunAsync("Init OPC Servers", true, (a) =>
            {
                InitServers();
                mIsInitialized = true;
                FireEvent(eThingEvents.Initialized, this, true, true);
                MyBaseEngine.ProcessInitialized();
            });
            return false;
        }

        private void OnOpcLibraryTrace(object sender, TraceEventArgs e)
        {
            try
            {
                var mask = OPCTraceMask;
                if (mask == 0 || ((e.TraceMask & mask) != 0))
                {
                    string output;
                    if (e.Arguments != null)
                    {
                        try
                        {
                            output = String.Format(CultureInfo.InvariantCulture, e.Format, e.Arguments);
                        }
                        catch
                        {
                            output = e.Format;
                        }
                    }
                    else
                    {
                        output = e.Format;
                    }
                    if (!string.IsNullOrEmpty(e.Message))
                    {
                        output = $"{e.Message}: {output}";
                    }
                    TheBaseAssets.MySYSLOG.WriteToLog(78008, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, $"OPC Trace: {e.TraceMask:X04} - {output}", e.Exception != null ? eMsgLevel.l1_Error : eMsgLevel.l6_Debug, e.Exception?.ToString()));
                }
            }
            catch (Exception ex)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(78008, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseThing.EngineName, $"OPC Client Library Trace: Error processing event", eMsgLevel.l1_Error, ex.ToString()));
            }
        }

        void sinkRegistered(ICDEThing pThing, object pPara)
        {
            TheThing tThing = pPara as TheThing;
            if (tThing != null && tThing.DeviceType == eOPCDeviceTypes.OPCRemoteServer)
                InitServers();
            mMyDashboard?.Reload(null, false);
        }

        void  InitServers()
        {
            List<TheThing> tDevList = TheThingRegistry.GetThingsOfEngine(MyBaseThing.EngineName); // TheThingRegistry.GetThingsByProperty("*", Guid.Empty, "DeviceType", eOPCDeviceTypes.OPCServer);
            if (tDevList.Count > 0)
            {
                foreach (TheThing tDev in tDevList)
                {
                    if (!tDev.HasLiveObject)
                    {
                        switch (tDev.DeviceType)
                        {
                            case eOPCDeviceTypes.OPCRemoteServer:
                                TheOPCUARemoteServer tS = new TheOPCUARemoteServer(tDev, this, null);
                                TheThingRegistry.RegisterThing(tS);
                                break;
                            case eOPCDeviceTypes.OPCLiveTag:
                                TheOPCUATagThing tag = new TheOPCUATagThing(tDev, null);
                                TheThingRegistry.RegisterThing(tag);
                                break;
                            case eOPCDeviceTypes.OPCMethod:
                                TheOPCUAMethodThing tMeth = new TheOPCUAMethodThing(tDev, MyBaseEngine.GetEngineName());
                                TheThingRegistry.RegisterThing(tMeth);
                                break;
                        }
                    }
                }
            }
            MyBaseEngine.SetStatusLevel(-1);
        }

        //private ApplicationConfiguration m_configuration;
        private void GetEndpoints()
        {
            try
            {
                // set a short timeout because this is happening in the drop down event.
                EndpointConfiguration configuration = EndpointConfiguration.Create();
                configuration.OperationTimeout = 20000;

                // Connect to the local discovery server and find the available servers.
                using (DiscoveryClient client = DiscoveryClient.Create(new Uri(Utils.Format("opc.tcp://{0}:4840", MyBaseThing.Address)), configuration))
                {
                    ApplicationDescriptionCollection servers = client.FindServers(null);

                    // populate the drop down list with the discovery URLs for the available servers.
                    for (int ii = 0; ii < servers.Count; ii++)
                    {
                        // don't show discovery servers.
                        if (servers[ii].ApplicationType == ApplicationType.DiscoveryServer)
                        {
                            continue;
                        }

                        for (int jj = 0; jj < servers[ii].DiscoveryUrls.Count; jj++)
                        {
                            string discoveryUrl = servers[ii].DiscoveryUrls[jj];

                            // Many servers will use the '/discovery' suffix for the discovery endpoint.
                            // The URL without this prefix should be the base URL for the server. 
                            if (discoveryUrl.EndsWith("/discovery"))
                            {
                                discoveryUrl = discoveryUrl.Substring(0, discoveryUrl.Length - "/discovery".Length);
                            }

                            TheThing tDev = TheThingRegistry.GetThingByFunc(MyBaseEngine.GetEngineName(),s=>s.GetProperty("Address",false).ToString()==discoveryUrl);
                            if (tDev==null)
                            {
                                TheOPCUARemoteServer tS = new TheOPCUARemoteServer(null, this, discoveryUrl);
                                TheThingRegistry.RegisterThing(tS);
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
            }
            finally
            {
            }
        }


        /// <summary>
        /// Handles Messages sent from a host sub-engine to its clients
        /// </summary>
        /// <param name="Command"></param>
        /// <param name="pMessage"></param>
        public override void HandleMessage(ICDEThing sender, object pIncoming)
        {
            TheProcessMessage pMsg = pIncoming as TheProcessMessage;
            if (pMsg == null) return;

            string[] cmd = pMsg.Message.TXT.Split(':');

            switch (cmd[0])
            {
                case "REFRESH_DASH":
                    InitServers();
                    mMyDashboard.Reload(pMsg, false);
                    break;

                case "CDE_INITIALIZED":
                        MyBaseEngine.SetInitialized(pMsg.Message);
                    break;
                case "CDE_INITIALIZE":
                    if (MyBaseEngine.GetEngineState().IsService)
                    {
                        if (!MyBaseEngine.GetEngineState().IsEngineReady)
                            MyBaseEngine.SetEngineReadiness(true, null);
                        MyBaseEngine.ReplyInitialized(pMsg.Message);
                    }
                    break;
                default:
                    TheThing tt = TheThingRegistry.GetThingByProperty(MyBaseEngine.GetEngineName(), Guid.Empty, "ID", pMsg.Message.PLS);
                    if (tt != null)
                        tt.HandleMessage(this, pMsg);
                    break;
            }
        }
    }
}
