using System;
using System.Collections.Generic;
using System.Linq;
using nsCDEngine.BaseClasses;
using nsCDEngine.Communication;
using nsCDEngine.Engines.ThingService;
using TheOPCUAClientAPI;
using CDMyOPCUAClient.Contracts;
using CDMyOPCUAServer.ViewModel;
using C_DEngine.Tests.TestCommon;
using NUnit.Framework;

namespace CDMyOPCUAClient.ViewModel.Tests
{
    [TestFixture]
    public class TheOPCUARemoteServerTests : TestHost
    {
        public TestContext TestContext { get; set; }

        [Test]
//#if DEBUG
        //[Ignore("ExcludeOfficialBuild")] // Sometimes hangs on build servers
//#endif

        public void MsgReadOpcTagTest2()
        {
            TestContext.Out.WriteLine($"About to run opc test");

            string hostName = new Uri(TheBaseAssets.MyServiceHostInfo.MyStationURL).Host;
            var result = MsgReadOpcTagTest(new List<string> { //windows-sef077c
                $"ns=2;s=3:{EscapePathSegment(hostName).Replace("/","%2F")}/OPCTestEng/OPCTestDT/{EscapePathSegment("OPCTestAddress")}?OpcProp02",
            },
            new List<object> { "0002" });

            //var result = MsgReadOpcTagTest(new List<string> { //windows-sef077c
            //    $"ns=2;s=3:{hostName}/CDMyTruLaserRobot.truLaserRobotService/TRUMPF Robot Connector/127.0.0.1?KrlProgramList",
            //});
            TestContext.Out.WriteLine($"opc test done");
        }

        string EscapePathSegment(string pathSegment)
        {
            return pathSegment.Replace("%", "%25").Replace("/", "%2F").Replace("?", "%3F");
        }

        internal MsgOPCUAReadTagsResponse MsgReadOpcTagTest()
        {
            return MsgReadOpcTagTest(new List<string> {
                //"ns=2;s=3:windows-sef077c/CDMyTruLaserRobot.truLaserRobotService/TRUMPF Robot Connector/127.0.0.1?KrlProgramList",
                "ns=4;s=33",
            });
        }
        internal MsgOPCUAReadTagsResponse MsgReadOpcTagTest(List<string> tags, List<object> expectedResultValues = null)
        {
            var response = myOPCClient.ReadTagsAsync(tags, new TimeSpan(0, 1, 0)).Result;
            Assert.IsNotNull(response);
            Assert.IsTrue(String.IsNullOrEmpty(response.Error), $"Error from Read Tags async: {response.Error}");
            Assert.IsNotNull(response.Results);
            Assert.IsTrue(response.Results.Count == tags.Count, $"Unexpected result count from Read OPC Tag message: {response.Results.Count}. Expected less than {tags.Count}");

            Assert.IsTrue(String.IsNullOrEmpty(response.Error), $"Error sending Read OPC Tag message: {response.Error}");
            if (expectedResultValues != null)
            {
                Assert.IsTrue(response.Results.Count == expectedResultValues.Count, $"Test error? expected result count does not match result from Read OPC Tag message: {response.Results.Count}. Expected {expectedResultValues.Count}");
            }

            int resultIndex = 0;
            foreach (var result in response.Results)
            {
                Assert.IsTrue(String.IsNullOrEmpty(result.Error), $"Result for node {tags[resultIndex]} returned error {result.Error}");
                if (expectedResultValues != null)
                {
                    var expectedResult = expectedResultValues[resultIndex];
                    Assert.IsTrue(TheCommonUtils.CStr(expectedResult) == TheCommonUtils.CStr(result.TagValue), $"Result for node {tags[resultIndex]} was {result.TagValue}. Expected: {expectedResult}");
                }
                else
                {
                    Assert.IsNotNull(result.TagValue);
                }
                resultIndex++;
            }
            return response;
        }

        static private TheOPCUAClient myOPCClient;
        static private TheThing myOPCServer;

        //static int activeHosts = 0;
        [SetUp]
        public void InitTests()
        {
            //if (System.Threading.Interlocked.Increment(ref activeHosts) != 1)
            //{
            //    return;
            //}
            if (myOPCServer != null)
            {
                return; // Workaround: cdEngine host and server shutdown not supported
            }
            StartHost();

            myOPCServer = StartOPCServer(true);
            Assert.IsNotNull(myOPCServer);

            // TODO Remove dependency on L49 VM in the Lab
            //myOPCClient = await ConnectOPCClientAsync("opc.tcp://10.1.10.135:4840");
            var opcServerAddress = "opc.tcp://localhost:4840/c-labs/DataAccessServer";
            myOPCClient = ConnectOPCClient(opcServerAddress);
            Assert.IsNotNull(myOPCClient, $"Unable to connect to OPC UA Server {opcServerAddress}");
        }

        static TheOPCUAClient ConnectOPCClient(string opcAddress)
        {
            var client = TheOPCUAClient.CreateAndInitAsync(myContentService, opcAddress, new OPCUAParameters
            {
                 AcceptInvalidCertificate = true,
                 AcceptUntrustedCertificate = true,
                 DisableDomainCheck = true,
                 DisableSecurity = true,
            }).Result;
            Assert.IsNotNull(client);
            var result = client.ConnectAsync().Result;
            Assert.IsTrue(String.IsNullOrEmpty(result), $"Error sending connect message to OPC UA Client plug-in: {result}");
            Assert.IsTrue(client.IsConnected, "OPC UA Client not connected after sending connect message");
            return client;
        }

        //    static TheBaseApplication MyBaseApplication;
        //    static TheThing myContentService;
        //    static public void StartHost()
        //    {
        //        TheScopeManager.SetApplicationID("/cVjzPfjlO;{@QMj:jWpW]HKKEmed[llSlNUAtoE`]G?"); //SDK Non-Commercial ID. FOR COMMERCIAL APP GET THIS ID FROM C-LABS!

        //        TheBaseAssets.MyServiceHostInfo = new TheServiceHostInfo(cdeHostType.Application)
        //        {
        //            ApplicationName = "My-Relay",                                   //Friendly Name of Application
        //            cdeMID = TheCommonUtils.CGuid("{FB74F44B-129B-4DB9-96CB-161305ED09F3}"),     //TODO: Give a Unique ID to this Host Service
        //            Title = "My-Relay (C) C-Labs 2013-2016",                   //Title of this Host Service
        //            ApplicationTitle = "My-Relay Portal",                           //Title visible in the NMI Portal 
        //            CurrentVersion = 1.0001,                                        //Version of this Service, increase this number when you publish a new version that the store displays the correct update icon
        //            DebugLevel = eDEBUG_LEVELS.OFF,                                 //Define a DebugLevel for the SystemLog output.
        //            SiteName = "http://cloud.c-labs.com",                           //Link to the main Cloud Node of this host. this is not required and for documentation only

        //            ISMMainExecutable = "OPCUAClientUnitTest",                        //Name of the executable (without .exe)
        //            IgnoreAdminCheck = true,                                             //if set to true, the host will not start if launched without admin priviledges. 

        //            LocalServiceRoute = "LOCALHOST",                                     //Will be replaced by the full DNS name of the host during startup.

        //            MyStationPort = 8716,                   //Port for REST access to this Host node. If your PC already uses port 80 for another webserver, change this port. We recommend using Port 8700 and higher for UPnP to work properly.
        //            MyStationWSPort = 8717,                 //Enables WebSockets on the station port. If UseRandomDeviceID is false, this Value cannot be changed here once the App runs for the first time. On Windows 8 and higher running under "Adminitrator" you can use the same port
        //        };

        //        #region Args Parsing
        //        Dictionary<string, string> ArgList = new Dictionary<string, string>();

        //        // TODO Get this from the text context?
        //        //for (int i = 0; i < args.Length; i++)
        //        //{
        //        //    string[] tArgs = args[i].Split('=');
        //        //    if (tArgs.Length == 2)
        //        //    {
        //        //        string key = tArgs[0].ToUpper();
        //        //        ArgList[key] = tArgs[1];
        //        //    }
        //        //}
        //        #endregion

        //        ArgList.Add("DontVerifyTrust", "True"); //NEW: 3.2 If this is NOT set, all plugins have to be signed with the same certificate as the host application or C-DEngine.DLL

        //        ArgList.Add("UseRandomDeviceID", "true");                       //ATTENTION: ONLY if you set this to false, some of these parameters will be stored on disk and loaded at a later time. "true" assigns a new node ID everytime the host starts and no configuration data will be cached on disk.
        //        ArgList.Add("ScopeUserLevel", "255");   //Set the Scope Access Level 
        //        ArgList.Add("AROLE", eEngineName.NMIService + ";" + eEngineName.ContentService);    //Make NMI and Content Service known to this host
        //        ArgList.Add("SROLE", eEngineName.NMIService + ";" + eEngineName.ContentService);    //Add NMI and Content Service as Service to run on this host. If you omit these entries, this host will become an end-node (not able to relay) and will try to find a proper relay node to talk to.
        //        string tScope = TheScopeManager.GenerateNewScopeID();                 //TIP: instead of creating a new random ID every time your host starts, you can put a breakpoint in the next line, record the ID and feed it in the "SetScopeIDFromEasyID". Or even set a fixed ScopeID here. (FOR TESTING ONLY!!)
        //        Console.WriteLine("Current Scope:" + tScope);
        //        TheScopeManager.SetScopeIDFromEasyID(tScope);                       //Set a ScopeID - the security context of this node. You can replace tScope with any random 8 characters or numbers
        //        MyBaseApplication = new TheBaseApplication();    //Create a new Base (C-DEngine IoT) Application
        //        Assert.IsTrue(MyBaseApplication.StartBaseApplication(null, ArgList));         //Start the C-DEngine Application. If a PluginService class is added DIRECTLY to the host project you can instantiate the Service here replacing the null with "new cdePluginService1()"
        //                                                                                      //If the Application fails to start, quit the app. StartBaseApplication returns very fast as all the C-DEngine code is running asynchronously
        //                                                                                      //MyBaseApplication.MyCommonDisco.RegisterUPnPUID("*", null);     //Only necessary if UPnP is used to find devices

        //        var started = TheBaseEngine.WaitForEnginesStartedAsync().Result;
        //        Assert.IsTrue(started);

        //        myContentService = TheThingRegistry.GetBaseEngineAsThing(eEngineName.ContentService);
        //        Assert.IsNotNull(myContentService);
        //    }

        //    [ClassCleanup]
        //    void ShutdownHost()
        //    { 
        //        MyBaseApplication?.Shutdown(true);
        //    }
        [TearDown]
        public void ShutdownHost()
        {
            //if (System.Threading.Interlocked.Decrement(ref activeHosts) <= 0)
            {
                StopHost();
            }
        }
    }
}