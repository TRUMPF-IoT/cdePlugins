// SPDX-FileCopyrightText: 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using nsCDEngine.BaseClasses;
using nsCDEngine.Communication;
using nsCDEngine.Engines;
using nsCDEngine.Engines.StorageService;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.Security;
using nsCDEngine.ViewModels;
using System;
using System.Collections.Generic;
using System.Threading;

namespace CDMyWebRelay
{
    internal partial class TheRelayService
    {
        private static TheMirrorCache<TheRequestData> ReqBuffer;    //DIC-Allowed

        private void sinkScopeIDUpdate(bool DoReq)
        {
            List<TheThing> tDevList = TheThingRegistry.GetThingsOfEngine(MyBaseEngine.GetEngineName());
            if (tDevList.Count > 0)
            {
                foreach (TheThing tDev in tDevList)
                {
                    if (tDev.DeviceType == eWebAppTypes.TheWebApp)
                    {
                        TheRelayAppInfo tApp = tDev.GetObject() as TheRelayAppInfo;
                        if (tApp != null && TheCommonUtils.IsUrlLocalhost(tApp.HostUrl)) // tApp.HostUrl.Equals(TheBaseAssets.MyServiceHostInfo.MyStation URL))
                        {
                            tApp.SSID = TheScopeManager.GetScrambledScopeID();
                            TheThingRegistry.UpdateThing(tDev, true);
                        }
                    }
                }
            }
        }

        #region Http Relay Processing
        private void InterceptHttpRequest(TheRequestData pRequest)
        {
            if (pRequest == null || pRequest.SessionState == null) return;
            if (pRequest.cdeRealPage.StartsWith("/CDEWRA"))
            {
                pRequest.SessionState.ARApp = TheCommonUtils.CGuid(pRequest.cdeRealPage.Substring(7, Guid.Empty.ToString().Length));
            }
            InterceptHttpRequest(pRequest, pRequest.SessionState.ARApp, MyBaseEngine, RequestTimeout);
        }
        private void InterceptHttpRequest(TheRequestData pRequest, Guid MyApp, IBaseEngine MyBaseEngine, int pRequestTimeout) // TheRelayAppInfo MyApp)
        {
            if (MyApp == Guid.Empty)
                return;
            TheRequestData tOutBuffer = null;
            //NEW BY CM
            string tMagixc = Guid.NewGuid().ToString();
            ReqBuffer.AddOrUpdateItem(TheCommonUtils.CGuid(tMagixc), null, null);
            if (!MyBaseEngine.GetEngineState().IsService) // || string.IsNullOrEmpty(MyApp.TargetUrl) || !TheCommonUtils.IsLocalhost(MyApp.HostUrl)) //  !MyApp.HostUrl.Equals(TheBaseAssets.MyServiceHostInfo.MyStation URL))
            {
                TSM tTSM = new TSM(MyBaseEngine.GetEngineName(), "WEBRELAY_REQUEST") { PLB = pRequest.PostData };
                pRequest.PostData = null;
                pRequest.ResponseBuffer = null;
                if (!string.IsNullOrEmpty(pRequest.CookieString))
                    pRequest.CookieString += ";";
                else
                    pRequest.CookieString = "";
                pRequest.CookieString += tMagixc;
                //if (string.IsNullOrEmpty(MyApp.CloudUrl)) MyApp.CloudUrl = TheBaseAssets.MyServiceHostInfo.GetPrimaryStationURL(false);
                if (string.IsNullOrEmpty(TheBaseAssets.MyServiceHostInfo.RootDir))
                    pRequest.RequestUriString = pRequest.RequestUri.ToString();
                else
                {
                    pRequest.RequestUriString = pRequest.RequestUri.Scheme + "://" + pRequest.RequestUri.Host + ":" + pRequest.RequestUri.Port + pRequest.cdeRealPage;
                    if (!string.IsNullOrEmpty(pRequest.RequestUri.Query))
                        pRequest.RequestUriString += "?" + pRequest.RequestUri.Query;
                }
                TheBaseAssets.MySYSLOG.WriteToLog(400, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(MyBaseEngine.GetEngineName(), string.Format("Requesting Page:{0}", pRequest.RequestUriString), eMsgLevel.l6_Debug));

                tTSM.PLS = TheCommonUtils.SerializeObjectToJSONString(pRequest);
                tTSM.SID = pRequest.SessionState.GetSID(); //.SScopeID;
                TheCommCore.PublishCentral(tTSM); //  .PublishToNode(MyApp.HostUrl, pRequest.SessionState.SScopeID, tTSM);
            }
            else
            {
                TheBaseAssets.MySYSLOG.WriteToLog(400, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM(MyBaseEngine.GetEngineName(), string.Format("AppID:{1} Requesting Page:{0}", pRequest.cdeRealPage, MyApp)));
                ReadHttpPage(pRequest, MyApp, tMagixc, sinkResults);
            }
            int SyncFailCount = 0;
            ManualResetEvent MyMRE = new ManualResetEvent(false);
            do
            {
                try
                {
                    tOutBuffer = ReqBuffer.GetEntryByID(TheCommonUtils.CGuid(tMagixc));
                    if (tOutBuffer != null)
                    {
                        pRequest.ResponseBuffer = tOutBuffer.ResponseBuffer;
                        pRequest.ResponseMimeType = tOutBuffer.ResponseMimeType;
                        if (pRequest.SessionState.StateCookies == null)
                            pRequest.SessionState.StateCookies = new cdeConcurrentDictionary<string, string>();
                        if (tOutBuffer.SessionState != null && tOutBuffer.SessionState.StateCookies != null && tOutBuffer != pRequest)
                        {
                            foreach (KeyValuePair<String, String> kvp in tOutBuffer.SessionState.StateCookies.GetDynamicEnumerable())
                            {
                                string value;
                                if (!pRequest.SessionState.StateCookies.TryGetValue(kvp.Key, out value))
                                    pRequest.SessionState.StateCookies.TryAdd(kvp.Key, kvp.Value);
                                else
                                    pRequest.SessionState.StateCookies[kvp.Key] = kvp.Value;
                            }
                        }

                        if (!string.IsNullOrEmpty(tOutBuffer.ResponseBufferStr))
                            pRequest.ResponseBufferStr = tOutBuffer.ResponseBufferStr;
                        else
                        {
                            if (pRequest.ResponseMimeType.StartsWith("text/html") || pRequest.ResponseMimeType.Contains("javascript"))  //OK
                                pRequest.ResponseBufferStr = TheCommonUtils.CArray2UTF8String(tOutBuffer.ResponseBuffer);
                        }
                        string tReqUri = pRequest.RequestUri.Host;
                        if (pRequest.RequestUri.Port != 80)
                            tReqUri += ":" + pRequest.RequestUri.Port;
                        TheBaseAssets.MySYSLOG.WriteToLog(400, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(MyBaseEngine.GetEngineName(), string.Format("Got Response Page:{0}", tReqUri), eMsgLevel.l6_Debug));

                        if (!string.IsNullOrEmpty(pRequest.ResponseBufferStr) && (pRequest.ResponseMimeType.StartsWith("text/html") || pRequest.ResponseMimeType.Contains("javascript")) && pRequest.ResponseBufferStr.IndexOf(tReqUri, StringComparison.CurrentCultureIgnoreCase) >= 0)
                        {
                            if (pRequest.SessionState.ARApp != null && pRequest.SessionState.ARApp != Guid.Empty)
                            {
                                TheRelayAppInfo tMyApp = TheThingRegistry.GetThingObjectByMID(MyBaseEngine.GetEngineName(), pRequest.SessionState.ARApp) as TheRelayAppInfo;
                                //MyRelayApps.MyMirrorCache.GetEntryByFunc(s => s.cdeMID.Equals(pRequest.SessionState.ARApp));

                                if (tMyApp != null && tMyApp.CloudUrl != null)
                                {
                                    Uri tCloudUri = TheCommonUtils.CUri(tMyApp.CloudUrl, false);
                                    if (!string.IsNullOrEmpty(pRequest.NewLocation))
                                        pRequest.NewLocation = pRequest.NewLocation.Replace(pRequest.RequestUri.Scheme + "://" + tReqUri, tCloudUri.Scheme + "://" + tCloudUri.Host + ":" + tCloudUri.Port);
                                    TheBaseAssets.MySYSLOG.WriteToLog(400, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(MyBaseEngine.GetEngineName(), string.Format("Patching Uri from:{0} to:{1}", tReqUri, tCloudUri), eMsgLevel.l6_Debug));

                                    pRequest.ResponseBufferStr = pRequest.ResponseBufferStr.Replace(pRequest.RequestUri.Scheme + "://" + tReqUri, tCloudUri.Scheme + "://" + tCloudUri.Host + ":" + tCloudUri.Port);
                                    pRequest.ResponseBuffer = TheCommonUtils.CUTF8String2Array(pRequest.ResponseBufferStr);
                                }
                            }
                        }
                        break;
                    }
                    MyMRE.WaitOne(50);
                    SyncFailCount++; if (SyncFailCount > (pRequestTimeout * 20))
                    {
                        if (TheCommonUtils.IsMono())
                            TheBaseAssets.MySYSLOG.WriteToLog(400, new TSM(MyBaseEngine.GetEngineName(), string.Format("Requesting Page:{0} FAILED", pRequest.cdeRealPage), eMsgLevel.l1_Error));
                        else
                            TheBaseAssets.MySYSLOG.WriteToLog(400, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(MyBaseEngine.GetEngineName(), string.Format("Requesting Page:{0} FAILED", pRequest.cdeRealPage), eMsgLevel.l1_Error));
                        break;
                    }
                }
                catch (Exception ee)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(400, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseEngine.GetEngineName(), string.Format("Error during HttpIntercept for Page:{0}", pRequest.cdeRealPage), eMsgLevel.l1_Error, ee.ToString()));
                }
            } while (tOutBuffer == null);
            if (MyMRE != null)
                MyMRE = null; //.Dispose();
            if ((pRequest.ResponseBuffer == null && string.IsNullOrEmpty(pRequest.ResponseBufferStr)) || pRequest.StatusCode != 0)
            {
                if (pRequest.StatusCode == 0)
                    pRequest.StatusCode = 404;
            }
            else
            {
                pRequest.AllowStatePush = true;
                pRequest.StatusCode = 200;
            }
            ReqBuffer.RemoveAnItemByID(TheCommonUtils.CGuid(tMagixc), null);
        }

        private void ReadHttpPage(TheRequestData pRequest, Guid MyAppGuid, string tMagixc, Action<TheRequestData> pSinkProcessResponse) //TheRelayAppInfo MyApp, 
        {
            TheRelayAppInfo MyApp = TheThingRegistry.GetThingObjectByMID(MyBaseEngine.GetEngineName(), MyAppGuid) as TheRelayAppInfo; // MyRelayApps.MyMirrorCache.GetEntryByFunc(s => s.cdeMID.Equals(MyAppGuid));
            if (MyApp == null)
            {
                if (pRequest.cdeN.Equals(TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID))
                {
                    TSM tTSM = new TSM(MyBaseEngine.GetEngineName(), "WEBRELAY_REQUEST") { PLB = pRequest.PostData };
                    pRequest.PostData = null;
                    pRequest.ResponseBuffer = null;
                    if (!string.IsNullOrEmpty(pRequest.CookieString))
                        pRequest.CookieString += ";";
                    else
                        pRequest.CookieString = "";
                    pRequest.CookieString += tMagixc;
                    if (string.IsNullOrEmpty(TheBaseAssets.MyServiceHostInfo.RootDir))
                        pRequest.RequestUriString = pRequest.RequestUri.ToString();
                    else
                    {
                        pRequest.RequestUriString = pRequest.RequestUri.Scheme + "://" + pRequest.RequestUri.Host + ":" + pRequest.RequestUri.Port + pRequest.cdeRealPage;
                        if (!string.IsNullOrEmpty(pRequest.RequestUri.Query))
                            pRequest.RequestUriString += "?" + pRequest.RequestUri.Query;
                    }
                    TheBaseAssets.MySYSLOG.WriteToLog(400, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(MyBaseEngine.GetEngineName(), string.Format("Requesting Page:{0}", pRequest.RequestUriString), eMsgLevel.l6_Debug));

                    tTSM.PLS = TheCommonUtils.SerializeObjectToJSONString(pRequest);
                    tTSM.SID = pRequest.SessionState.GetSID();
                    TheCommCore.PublishCentral(tTSM); //  .PublishToNode(MyApp.HostUrl, pRequest.SessionState.SScopeID, tTSM);
                }
                return;
            }
            if (TheCommonUtils.IsUrlLocalhost(MyApp.GetBaseThing().Address))
            {
                TheCommonUtils.GetAnyFile(pRequest);
                if (tMagixc != null)
                    pRequest.CookieString = tMagixc;
                pSinkProcessResponse(pRequest);
            }
            else
            {
                int relPathIndex = pRequest.RequestUri.AbsolutePath.IndexOf('/', 1);
                string relPath = relPathIndex == -1 ? "" : pRequest.RequestUri.AbsolutePath.Substring(relPathIndex + 1) + pRequest.RequestUri.Query;
                var tUri = pRequest.RequestUri.AbsolutePath.StartsWith("/CDEWRA") ? new Uri(MyApp.GetBaseThing().Address + MyApp.HomePage + relPath) : new Uri(MyApp.GetBaseThing().Address.TrimEnd('/') + pRequest.RequestUri.AbsolutePath + pRequest.RequestUri.Query);
                if (!string.IsNullOrEmpty(tMagixc))
                {
                    if (!string.IsNullOrEmpty(pRequest.CookieString))
                        pRequest.CookieString += ";";
                    else
                        pRequest.CookieString = "";
                    pRequest.CookieString += tMagixc;
                }
                pRequest.RequestUri = tUri;
                pRequest.ErrorDescription = "";
                pRequest.RequestCookies = pRequest.SessionState.StateCookies;
                if (!string.IsNullOrEmpty(MyApp.SUID) && !string.IsNullOrEmpty(MyApp.SPWD))
                {
                    pRequest.UID = MyApp.SUID;
                    pRequest.PWD = MyApp.SPWD;
                }
                if (MyApp.IsHTTP10)
                    pRequest.HttpVersion = 1.0;
                if (string.IsNullOrEmpty(MyApp.CloudUrl)) MyApp.CloudUrl = TheBaseAssets.MyServiceHostInfo.GetPrimaryStationURL(false);
                pRequest.Header = null;
                if (pRequest.PostData != null && pRequest.PostData.Length > 0)      //MONO Relay might not work right!
                {
                    TheREST MyRest = new TheREST();
                    MyRest.PostRESTAsync(pRequest, pSinkProcessResponse, pSinkProcessResponse);
                }
                else
                    TheREST.GetRESTAsync(pRequest, pSinkProcessResponse, pSinkProcessResponse);
                TheBaseAssets.MySYSLOG.WriteToLog(400, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM(MyBaseEngine.GetEngineName(), string.Format("Requesting Page:{0} Sent", pRequest.RequestUri), eMsgLevel.l3_ImportantMessage));
            }
        }

        private void sinkProcessResponse(TheRequestData pRequest)
        {
            if (pRequest.StatusCode >= 400 && pRequest.ResponseBuffer == null && !string.IsNullOrEmpty(pRequest.ErrorDescription))
                pRequest.ResponseBuffer = TheCommonUtils.CUTF8String2Array(pRequest.ErrorDescription);
            if (pRequest.ResponseBuffer == null && string.IsNullOrEmpty(pRequest.ResponseBufferStr))
                pRequest.ResponseBuffer = TheCommonUtils.CUTF8String2Array("EMPTY");
            if (pRequest.ResponseMimeType.StartsWith("text/html") || pRequest.ResponseMimeType.Contains("javascript"))  //OK
                pRequest.ResponseBufferStr = TheCommonUtils.CArray2UTF8String(pRequest.ResponseBuffer);
            string tReqUri = pRequest.RequestUri.Host;
            if (pRequest.RequestUri.Port != 80)
                tReqUri += ":" + pRequest.RequestUri.Port;
            if (!string.IsNullOrEmpty(pRequest.ResponseBufferStr) && (pRequest.ResponseMimeType.StartsWith("text/html") || pRequest.ResponseMimeType.Contains("javascript")) && pRequest.ResponseBufferStr.IndexOf(tReqUri, StringComparison.CurrentCultureIgnoreCase) >= 0)
            {
                if (pRequest.SessionState.ARApp != Guid.Empty)
                {
                    TheRelayAppInfo tMyApp = TheThingRegistry.GetThingObjectByMID(MyBaseEngine.GetEngineName(), pRequest.SessionState.ARApp) as TheRelayAppInfo; //MyRelayApps.MyMirrorCache.GetEntryByFunc(s => s.cdeMID.Equals(pRequest.SessionState.ARApp));
                    if (tMyApp != null)
                    {
                        Uri tCloudUri = new Uri(pRequest.RequestUriString);
                        //pRequest.ResponseBufferStr = pRequest.ResponseBufferStr.Replace(pRequest.RequestUri.Host + ":" + pRequest.RequestUri.Port, new Uri(tMyApp.CloudUrl).Host + ":" + new Uri(tMyApp.CloudUrl).Port);
                        pRequest.ResponseBufferStr = pRequest.ResponseBufferStr.Replace(pRequest.RequestUri.Scheme + "://" + tReqUri, tCloudUri.Scheme + "://" + tCloudUri.Host + ":" + tCloudUri.Port);
                        pRequest.ResponseBuffer = TheCommonUtils.CUTF8String2Array(pRequest.ResponseBufferStr);
                    }
                }
            }
            TheBaseAssets.MySYSLOG.WriteToLog(400, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(MyBaseEngine.GetEngineName(), string.Format("Response Bytes:{1} For Page:{0} Sent", pRequest.cdeRealPage, pRequest.ResponseBuffer != null ? pRequest.ResponseBuffer.Length : 0), eMsgLevel.l3_ImportantMessage));
            if (pRequest.ResponseBuffer != null)
            {
                //TheCommonUtils.SleepOneEye(5000, 100);
                TSM message3 = new TSM(MyBaseEngine.GetEngineName(), "WEBRELAY_RESPONSE") { PLB = pRequest.ResponseBuffer };
                pRequest.ResponseBuffer = null;
                pRequest.ResponseBufferStr = null;
                pRequest.RequestUriString = pRequest.RequestUri.ToString();
                TSM tMSG = pRequest.CookieObject as TSM;
                pRequest.CookieObject = null;
                pRequest.PostData = null;
                message3.PLS = TheCommonUtils.SerializeObjectToJSONString(pRequest);
                TheCommCore.PublishToOriginator(tMSG, message3); 
            }
        }

        private void sinkResults(TheRequestData resBytes)
        {
            TheBaseAssets.MySYSLOG.WriteToLog(400, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM(MyBaseEngine.GetEngineName(), string.Format("Result Bytes:{1} with Cookie:{2} For Page:{0} Sent", resBytes.cdeRealPage, resBytes.ResponseBuffer != null ? resBytes.ResponseBuffer.Length : 0, resBytes.CookieString), eMsgLevel.l4_Message));
            if (!string.IsNullOrEmpty(resBytes.CookieString))
            {
                string[] tCookies = resBytes.CookieString.Split(';');
                if (ReqBuffer.ContainsID(TheCommonUtils.CGuid(tCookies[tCookies.Length - 1])))
                {
                    if (tCookies.Length > 1)
                    {
                        resBytes.CookieString = "";
                        for (int i = 0; i < tCookies.Length - 1; i++)
                        {
                            if (resBytes.CookieString.Length > 0) resBytes.CookieString += ";";
                            resBytes.CookieString += tCookies[i];
                        }
                    }
                    if (resBytes.ResponseBuffer == null && !string.IsNullOrEmpty(resBytes.ErrorDescription))
                        resBytes.ResponseBuffer = TheCommonUtils.CUTF8String2Array(resBytes.ErrorDescription);

                    //TODO: HackProcessing - Advanced Hack Table: Replace X with Y
                    //Console.WriteLine("SR:" + resBytes.cdeRealPage.ToLower());
                    /*
                    if (resBytes.cdeRealPage.ToLower().StartsWith("/js/default.js"))
                    {
                        string t = TheCommonUtils.CArray2UTF8String(resBytes.ResponseBuffer);
                        //var checkLocal = document.URL replace with return true; //TODO: Hack List
                        if (t.IndexOf("var checkLocal=document.URL;") >= 0)
                            t = t.Replace("var checkLocal=document.URL;", "return true;");
                        //if (t.IndexOf("return _.isObject(user)&&user.isLogin()") >= 0)
                        //  t = t.Replace("return _.isObject(user)&&user.isLogin()", "return true;");
                        t = t.Replace("window.location.hostname", "window.location.hostname+':'+window.location.port");
                        resBytes.ResponseBuffer = TheCommonUtils.CUTF8String2Array(t);
                    }*/

                    ReqBuffer.AddOrUpdateItem(TheCommonUtils.CGuid(tCookies[tCookies.Length - 1]), resBytes, null);
                }
                else
                    TheBaseAssets.MySYSLOG.WriteToLog(400, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(MyBaseEngine.GetEngineName(), string.Format("Result Bytes For Page:{0} NoLonger in ReqBuffer", resBytes.cdeRealPage), eMsgLevel.l2_Warning));
            }
        }
        #endregion

        /// <summary>
        /// If this is a service the SimplexProc event will be called when the C-DEngine receives a new event sent by a subscriber to this service
        /// </summary>
        /// <param name="pMsg">The Message to be Processed</param>
        private void ProcessServiceMessage(TheProcessMessage pMsg)
        {
            string[] cmd = pMsg.Message.TXT.Split(':');
            switch (cmd[0]) //string 2 cases
            {
                case "WEBRELAY_RESPONSE":
                    TheRequestData tState = TheCommonUtils.DeserializeJSONStringToObject<TheRequestData>(pMsg.Message.PLS);
                    if (pMsg.Message.PLB != null && pMsg.Message.PLB.Length > 0)
                        tState.ResponseBuffer = pMsg.Message.PLB;
                    tState.RequestUri = new Uri(tState.RequestUriString);
                    //tState.SessionState = new TheSessionState() { SScopeID = pMsg.SID };
                    sinkResults(tState);
                    break;
                case "WEBRELAY_REQUEST":
                    TheRequestData tData = TheCommonUtils.DeserializeJSONStringToObject<TheRequestData>(pMsg.Message.PLS);
                    if (tData != null)
                    {
                        tData.RequestUri = new Uri(tData.RequestUriString);
                        //tData.SessionState = new TheSessionState() { SScopeID = pMsg.SID };
                        if (pMsg.Message.PLB != null && pMsg.Message.PLB.Length > 0)
                            tData.PostData = pMsg.Message.PLB;
                        tData.CookieObject = pMsg.Message;
                        ReadHttpPage(tData, tData.SessionState.ARApp, null, sinkProcessResponse);
                        //InterceptHttpRequest(tData,tData.SessionState.ARApp); 
                    }
                    break;
                case "WEBRELAY_REQUESTWRA":
                    TSM tTSM = new TSM();
                    List<TheThing> webApps = TheThingRegistry.GetThingsOfEngine(MyBaseEngine.GetEngineName());
                    foreach(TheThing tApp in webApps)
                    {
                        if(tApp.Address.Equals(pMsg.Message.PLS))
                        {
                            tTSM.PLS = $"/CDEWRA{tApp.cdeMID}" + TheThing.GetSafePropertyString(tApp, "HomePage");
                            break;
                        }
                    }
                    if (!string.IsNullOrEmpty(tTSM.PLS))
                    {
                        string[] org = pMsg.Message.ORG.Split(':');
                        TheThing senderThing = TheThingRegistry.GetThingByMID(TheCommonUtils.CGuid(org[1]), true);
                        IBaseEngine senderEngine = TheThingRegistry.GetBaseEngine(senderThing, true);                      
                        tTSM.ENG = senderEngine.GetEngineName();
                        tTSM.TXT = "RESPONSEWRA"; 
                        TheCommCore.PublishToOriginator(pMsg.Message, tTSM, true);
                    }
                    break;
            }
        }
    }
}
