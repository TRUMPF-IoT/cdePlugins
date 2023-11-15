// SPDX-FileCopyrightText: 2009-2023 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using nsCDEngine.Engines.NMIService;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.ViewModels;

namespace CDMyNetwork
{
    partial class MyNetworkServices 

    {
        TheDashPanelInfo RootDashPanel;
        TheDashboardInfo mMyDashboard;
        public override bool CreateUX()
        {
            if (mIsUXInitCalled) return false;
            mIsUXInitCalled = true;
            if (!MyBaseEngine.GetEngineState().IsService)
            {
                return true;
            }
            //NUI Definition for All clients
            // File Service Main Tile
            mMyDashboard = TheNMIEngine.AddDashboard(MyBaseThing, new TheDashboardInfo(MyBaseEngine, "0")
            {
                OnChangeName = "Value",
                PropertyBag =
                new nmiDashboard() { Category = "Devices", Format = "Network Status<br>Issues: {0}", LabelFormat = "Current Issues: {0}", Thumbnail="FA5:f6ff", ClassName = "cdeLiveTile cdeLiveTileBar", SideBarTitle = "Network Services" }
            });
            mMyDashboard.RegisterEvent2(eUXEvents.OnLoad, (obj, pMsg) =>
            {
                if (RootDashPanel == null)
                {
                    var tm = pMsg as TheProcessMessage;
                    TheDashPanelInfo pDash = tm?.Cookie as TheDashPanelInfo;
                    RootDashPanel = pDash;
                    sinkStatChanged(null);
                    ThePropertyBag.PropBagUpdateValue(pDash?.PropertyBag, "Foreground", "=", "white");// "background-image:url('GlasButton.png');color:white;background-color:gray");
                    ThePropertyBag.PropBagUpdateValue(pDash?.PropertyBag, "sStyle", "=", "color:white;");// "background-image:url('GlasButton.png');color:white;background-color:gray");
                    ScanAllServices();
                }
            });
            // File Service Form: single instance for multi-agents
            TheFormInfo tAllFileServers = TheNMIEngine.AddForm(new TheFormInfo(MyBaseEngine) { cdeMID = TheThing.GetSafeThingGuid(MyBaseThing, "NETS"), FormTitle = "All Network Services", AddButtonText = "Add a Service" });
            TheNMIEngine.AddFormToThingUX(MyBaseThing, tAllFileServers, "CMyTable", "<i class='fa faIcon fa-5x'>&#xf0ce;</i></br>Network Services", 1, 1, 0x0, TheNMIEngine.GetNodeForCategory(), null, new nmiDashboardTile() { });

            TheNMIEngine.AddCommonTableColumns(MyBaseThing, tAllFileServers, new eNetworkServiceTypes(), eNetworkServiceTypes.PingService);
            TheNMIEngine.AddSmartControl(MyBaseThing, tAllFileServers, eFieldType.Number, 21, 64, 0, "RTT", "Value", new nmiCtrlNumber { TileWidth = 1 });



            //Easy To add Wizard
            var tFlds = TheNMIEngine.AddNewWizard(MyBaseThing, tAllFileServers.cdeMID, "Add new Network Service", null, (item, client) =>
            {
                if (item != null)
                {
                    TheThing t = item;
                    TheThing.SetSafePropertyBool(t, "AutoConnect", true);
                    TheThing.SetSafePropertyBool(t, "AllowRTT", true);
                }
            });
            TheFormInfo tF = tFlds["Form"] as TheFormInfo;
            tAllFileServers.AddTemplateType = tF.cdeMID.ToString();
            TheNMIEngine.AddNewWizardPage(MyBaseThing, tF, 0, 1, 0, "Add new Service");
            TheNMIEngine.AddWizardControl(MyBaseThing, tF, eFieldType.SingleEnded,1, 1, 2, 0, "Name your Service", "FriendlyName");
            TheNMIEngine.AddWizardControl(MyBaseThing, tF, eFieldType.ComboBox,1, 2, 2, 0, "Service Type", "DeviceType", new nmiCtrlComboBox { Options = new eNetworkServiceTypes(), DefaultValue = eNetworkServiceTypes.PingService });
            TheNMIEngine.AddWizardControl(MyBaseThing, tF, eFieldType.SingleEnded,1, 3, 2, 0, "Address", "Address");
            TheNMIEngine.AddWizardControl(MyBaseThing, tF, eFieldType.SmartLabel, 1, 4, 0, 0, null, null, new nmiCtrlSmartLabel { NoTE=true,FontSize=32, Text= "Once you click finish the service will be created and started." });


            TheNMIEngine.AddAboutButton(MyBaseThing, true, "REFRESH_DASH", 0xc0);
            mIsUXInitialized = true;
            return true;
        }
    }
}
