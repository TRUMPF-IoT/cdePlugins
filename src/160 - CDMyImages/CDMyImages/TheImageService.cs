// SPDX-FileCopyrightText: 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0
using CDMyImages.ViewModel;
using nsCDEngine.BaseClasses;
using nsCDEngine.Communication;
using nsCDEngine.Engines;
using nsCDEngine.Engines.NMIService;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.ViewModels;
using System;
using System.Collections.Generic;

namespace CDMyImages
{
    public class eImageTypes : TheDeviceTypeEnum
    {
        public const string Bitmap = "Bitmap Image";
    }

    class TheImageService : ThePluginBase
    {
        public override void InitEngineAssets(IBaseEngine pBase)
        {
            base.InitEngineAssets(pBase);
            MyBaseEngine.SetFriendlyName("Image Service");
            MyBaseEngine.GetEngineState().IsAcceptingFilePush = true;
            MyBaseEngine.SetCDEMinVersion(6.104);
            MyBaseEngine.SetEngineID(new Guid("{795370F8-EF3A-4F1F-BBE0-F16053329B90}"));
            MyBaseEngine.SetPluginInfo("This service allows you to manage images", 0, null, "toplogo-150.png", "C-Labs", "http://www.c-labs.com", new List<string>() { "Digital-Thing" });
        }

        public override bool Init()
        {
            if (!mIsInitCalled)
            {
                mIsInitCalled = true;
                MyBaseThing.StatusLevel = 4;
                MyBaseThing.LastMessage = "Service has started";

                MyBaseThing.RegisterEvent(eEngineEvents.IncomingMessage, HandleMessage);
                MyBaseEngine.RegisterEvent(eEngineEvents.ThingDeleted, OnThingDeleted);
                MyBaseThing.RegisterEvent(eEngineEvents.FileReceived, sinkFileReceived);

                InitServices();
                MyBaseEngine.ProcessInitialized(); //Set the status of the Base Engine according to the status of the Things it manages
                mIsInitialized = true;
            }
            return false;
        }

        void sinkFileReceived(ICDEThing pThing, object pFileName)
        {
            TheProcessMessage pMsg = pFileName as TheProcessMessage;
            if (pMsg?.Message == null) return;

            var tb = new TheBitmapImage(null, this);
            TheThing.SetSafePropertyString(tb, "CurrentImage", pMsg.Message.TXT);
            tb.GetBaseThing().FriendlyName = pMsg.Message.TXT.Substring(pMsg.Message.TXT.IndexOf('\\')+1);
            TheThingRegistry.RegisterThing(tb);
            TheCommCore.PublishToOriginator(pMsg.Message, new TSM(eEngineName.NMIService, "NMI_TOAST", string.Format("###Image-File ({0}) received!###", pMsg.Message.TXT)));
        }

        public override bool CreateUX()
        {
            if (!mIsUXInitCalled)
            {
                mIsUXInitCalled = true;

                mMyDashboard = TheNMIEngine.AddDashboard(MyBaseThing, new TheDashboardInfo(MyBaseEngine, "###Image Service###") { PropertyBag = new nmiDashboard { Category="###Services###", Caption = "<i class='fa faIcon fa-5x'>&#xf03e;</i></br>###Image Service###" } });

                var tFlds= TheNMIEngine.CreateEngineForms(MyBaseThing, TheThing.GetSafeThingGuid(MyBaseThing, "MYNAME"), "###My Images###", null, 20, 0x0F, 0xF0, TheNMIEngine.GetNodeForCategory(), "REFFRESHME", false, new eImageTypes(),eImageTypes.Bitmap);
                TheFormInfo tForm = tFlds["Form"] as TheFormInfo;
                TheNMIEngine.AddSmartControl(MyBaseThing, tForm, eFieldType.Picture, 5, 0, 0, "###Current Image###", "CurrentImage", new nmiCtrlPicture { FldWidth = 1, TileHeight = 1 });

                var tMyForm2 = TheNMIEngine.AddStandardForm(MyBaseThing, "###New Image Uploader###", 6, "ImageUp", null, 0xC0);
                TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm2["Form"] as TheFormInfo, eFieldType.DropUploader, 3, 2, 0, "###Drop an image-file here###", null,
                    new nmiCtrlDropUploader { TileHeight = 6, ParentFld = 1, NoTE = true, TileWidth = 6, EngineName = MyBaseEngine.GetEngineName(), MaxFileSize = 5000000 });

                mIsUXInitialized = true;
            }
            return true;
        }

        TheDashboardInfo mMyDashboard;
        void InitServices()
        {
            List<TheThing> tDevList = TheThingRegistry.GetThingsOfEngine(MyBaseThing.EngineName); 
            if (tDevList.Count > 0)
            {
                foreach (TheThing tDev in tDevList)
                {
                    switch (tDev.DeviceType)
                    {
                        case eImageTypes.Bitmap:
                            CreateOrUpdateService<TheBitmapImage>(tDev, true);
                            break;
                    }
                }
            }
            MyBaseEngine.SetStatusLevel(-1); //Calculates the current statuslevel of the service/engine
        }

        T CreateOrUpdateService<T>(TheThing tDevice, bool bRegisterThing) where T : TheBitmapImage
        {
            T tServer;
            if (tDevice == null || !tDevice.HasLiveObject)
            {
                tServer = (T)Activator.CreateInstance(typeof(T), tDevice, this);
                if (bRegisterThing)
                    TheThingRegistry.RegisterThing((ICDEThing)tServer);
            }
            else
            {
                tServer = tDevice.GetObject() as T;
                if (tServer == null)
                    tServer = (T)Activator.CreateInstance(typeof(T), tDevice, this);
            }
            return tServer;
        }

        void OnThingDeleted(ICDEThing pEngine, object pDeletedThing)
        {
            if (pDeletedThing != null && pDeletedThing is ICDEThing)
            {
                ((ICDEThing)pDeletedThing).FireEvent(eEngineEvents.ShutdownEvent, pEngine, null, false);
            }
        }

        #region Message Handling
        public override void HandleMessage(ICDEThing sender, object pIncoming)
        {
            TheProcessMessage pMsg = pIncoming as TheProcessMessage;
            if (pMsg == null) return;

            string[] cmd = pMsg.Message.TXT.Split(':');
            switch (cmd[0])
            {
                case "CDE_INITIALIZED":
                    MyBaseEngine.SetInitialized(pMsg.Message);      //Sets the Service to "Ready". ProcessInitialized() internally contains a call to this Handler and allows for checks right before SetInitialized() is called. 
                    break;
                case "REFFRESHME":
                    InitServices();
                    mMyDashboard.Reload(pMsg, false);
                    break;
            }
        }
        #endregion
    }
}
