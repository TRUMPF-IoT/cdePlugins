// SPDX-FileCopyrightText: 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0
using nsCDEngine.BaseClasses;
using nsCDEngine.Communication;
using nsCDEngine.Engines;
using nsCDEngine.Engines.NMIService;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.ViewModels;
using System;
using System.IO;

namespace CDMyImages.ViewModel
{
    class TheBitmapImage : TheThingBase
    {
        private IBaseEngine MyBaseEngine;
        public TheBitmapImage()
        {
        }

        public TheBitmapImage(TheThing tBaseThing, ICDEPlugin pPluginBase)
        {
            MyBaseThing = tBaseThing ?? new TheThing();
            MyBaseEngine = pPluginBase.GetBaseEngine();
            MyBaseThing.EngineName = MyBaseEngine.GetEngineName();
            MyBaseThing.SetIThingObject(this);
            MyBaseThing.DeviceType = eImageTypes.Bitmap;
            TheThing.SetSafePropertyBool(MyBaseThing, "IsCamera", true);
        }

        public override bool Init()
        {
            if (!mIsInitCalled)
            {
                mIsInitCalled = true;
                MyBaseThing.RegisterEvent(eEngineEvents.FileReceived, sinkFileReceived);
                MyBaseThing.LastMessage = "Thing has started";
                MyBaseThing.StatusLevel = 1;
                mIsInitialized = true;
            }
            return true;
        }

        void sinkFileReceived(ICDEThing pThing, object pFileName)
        {
            TheProcessMessage pMsg = pFileName as TheProcessMessage;
            if (pMsg?.Message == null) return;

            var tIm= TheThing.GetSafePropertyString(MyBaseThing, "CurrentImage");
            if (!string.IsNullOrEmpty(tIm) && tIm!=pMsg.Message.TXT)
                File.Delete(TheCommonUtils.cdeFixupFileName(tIm));
            TheThing.SetSafePropertyString(MyBaseThing, "CurrentImage", pMsg.Message.TXT);
            TheCommCore.PublishToOriginator(pMsg.Message, new TSM(eEngineName.NMIService, "NMI_TOAST", string.Format("###Update to Image-File ({0}) received!###", pMsg.Message.TXT)));
        }

        public override bool CreateUX()
        {
            if (!mIsUXInitCalled)
            {
                mIsUXInitCalled = true;

                var tMyForm2 = TheNMIEngine.AddStandardForm(MyBaseThing, MyBaseThing.FriendlyName, 6, MyBaseThing.cdeMID.ToString(), null, 0xC0,"Images");
                (tMyForm2["DashIcon"] as TheDashPanelInfo)?.SetUXProperty(Guid.Empty, $"BackgroundImage={TheThing.GetSafePropertyString(MyBaseThing,"CurrentImage").Replace('\\','/')}");
                var tMyForm = tMyForm2["Form"] as TheFormInfo;
                tMyForm.RegisterEvent2(eUXEvents.OnShow, (sender, para) =>
                {
                    (tMyForm2["DashIcon"] as TheDashPanelInfo)?.SetUXProperty(Guid.Empty, $"BackgroundImage={TheThing.GetSafePropertyString(MyBaseThing, "CurrentImage").Replace('\\', '/')}");
                });
                TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.TileGroup, 10, 0, 0, null, null, new nmiCtrlPicture { ParentFld=1, TileWidth = 6 });
                TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.DropUploader, 20, 2, 0, "###Drop an image-file here###", "CurrentImage",
                    new nmiCtrlDropUploader { TileHeight = 6, ParentFld = 10, NoTE = true, TileWidth = 6, EngineName = MyBaseEngine.GetEngineName(), MaxFileSize = 500000 });
                TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.SingleEnded, 30, 2, 0, "###Image Name###", "FriendlyName", new nmiCtrlSingleEnded { ParentFld = 10 });
                mIsUXInitialized = true;
            }
            return true;
        }

        #region Message Handling
        public override void HandleMessage(ICDEThing sender, object pIncoming)
        {
            TheProcessMessage pMsg = pIncoming as TheProcessMessage;
            if (pMsg == null) return;

            string[] cmd = pMsg.Message.TXT.Split(':');
            switch (cmd[0])
            {
                default:
                    break;
            }
        }
        #endregion
    }
}
