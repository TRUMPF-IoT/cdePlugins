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
using System.Net.Mail;

namespace CDMyMessages.ViewModel
{
    public class TheSMSMessage : TheThingBase
    {
        public override cdeP SetProperty(string pName, object pValue)
        {
            if ("send" == pName.ToLower() || "sendSMS" == pName.ToLower())
                SendSMS(pValue.ToString());
            else
            {
                if (MyBaseThing != null)
                    return MyBaseThing.SetProperty(pName, pValue);
            }
            return null;
        }

        private IBaseEngine MyBaseEngine;

        public TheSMSMessage(TheThing tBaseThing, ICDEPlugin pPluginBase)
        {
            if (tBaseThing != null)
                MyBaseThing = tBaseThing;
            else
                MyBaseThing = new TheThing();
            MyBaseEngine = pPluginBase.GetBaseEngine();
            MyBaseThing.SetIThingObject(this);
        }

        public override bool Init()
        {
            if (!mIsInitialized)
            {
                mIsInitialized = true;
                MyBaseThing.RegisterEvent(eEngineEvents.IncomingMessage, HandleMessage);
            }
            return true;
        }

        public override bool CreateUX()
        {
            if (!mIsUXInitCalled)
            {
                mIsUXInitCalled = true;

                var tFlds = TheNMIEngine.AddStandardForm(MyBaseThing, null, 12);
                var tMyForm = tFlds["Form"] as TheFormInfo;
                (tFlds["DashIcon"] as TheDashPanelInfo).PropertyBag = new ThePropertyBag() { "Format={0}", "Thumbnail=FA5:f7cd" };

                TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.CollapsibleGroup, 1, 0, 0, "Message Header", false, null, null, ThePropertyBag.Create(new nmiCtrlCollapsibleGroup() { ParentFld=1, IsSmall = true, MaxTileWidth = 12 }));
                TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.SingleEnded, 2, 2, 0, "From Address", "FromAddress", new ThePropertyBag() { "TileHeight=1", "TileWidth=6", "ParentFld=1" });
                TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.SingleEnded, 3, 2, 0, "Enter a Recipient", "Recipient", new ThePropertyBag() { "TileHeight=1", "TileWidth=6", "ParentFld=1" });

                TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.SingleEnded, 4, 2, 0, "Subject", "SubjectText", new ThePropertyBag() { "TileHeight=1", "TileWidth=6", "ParentFld=1" });
                TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.TextArea, 5, 2, 0, "Enter a message", "MessageText", new ThePropertyBag() { "TileHeight=3", "TileWidth=6", "Rows=3", "ParentFld=1" });

                TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.CollapsibleGroup, 100, 2, 0, "Additional Settings...", null, new nmiCtrlCollapsibleGroup() { DoClose = true, IsSmall = true, MaxTileWidth = 12, ParentFld = 1 });
                TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.SingleEnded, 124, 2, 0xC0, "Username", "UserName", new ThePropertyBag() { "TileHeight=1", "TileWidth=6", "ParentFld=100" });
                TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.Password, 125, 3, 0xC0, "Password", "Password", new ThePropertyBag() { "TileHeight=1", "TileWidth=6", "ParentFld=100", "HideMTL=true" });
                TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.ComboBox, 27, 2, 0xC0, "Carrier", "Carrier", new nmiCtrlComboBox { ParentFld=100, Options=String.Format("{0};{1};{2};{3};{4};{5};{6};{7};{8};{9};{10};{11};{12};{13}", "", "AT&T", "Boost Mobile", "T-Mobile", "Virgin Mobile", "Cingular", "Sprint", "Verizon", "Nextel", "US Cellular", "Suncom", "Powertel", "Alltel", "Metro PCS") });

                TheFieldInfo mSendbutton = TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.TileButton, 32, 2, 0, "Send SMS", false, "", null, new nmiCtrlTileButton() { NoTE = true, ParentFld = 100, ClassName = "cdeGoodActionButton" });
                mSendbutton.RegisterUXEvent(MyBaseThing, eUXEvents.OnClick, "", (pThing, pPara) =>
                {
                    SendSMS(null);
                });
                TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.TextArea, 55, 0, 0, "Sent Result", "ResultText", new ThePropertyBag() { "TileHeight=2", "TileWidth=6", "Rows=2", "ParentFld=6" });
                MyBaseEngine.ProcessInitialized();
                mIsUXInitialized = true;
            }
            return true;
        }

        public string ResultText
        {
            get { return TheThing.GetSafePropertyString(MyBaseThing, "ResultText"); }
            set { TheThing.SetSafePropertyString(MyBaseThing, "ResultText", value); }
        }

        /*
         Boost Mobile: PhoneNumber@myboostmobile.com
T-Mobile: PhoneNumber@tmomail.net
Virgin Mobile: PhoneNumber@vmobl.com
Cingular: PhoneNumber@cingularme.com
Sprint Nextel: PhoneNumber@messaging.sprintpcs.com
Verizon: PhoneNumber@vtext.com
Nextel: PhoneNumber@messaging.nextel.com
US Cellular: PhoneNumber@email.uscc.net
SunCom: PhoneNumber@tms.suncom.com
Powertel: PhoneNumber@ptel.net
AT&T (Cingular): PhoneNumber@txt.att.net
Alltel: PhoneNumber@message.alltel.com
Metro PCS: PhoneNumber@MyMetroPcs.com
         */
        public string Recipient
        {
            get { return TheThing.GetSafePropertyString(MyBaseThing, "Recipient"); }
            set { TheThing.SetSafePropertyString(MyBaseThing, "Recipient", value); }
        }
        public string Credentials
        {
            get { return TheThing.GetSafePropertyString(MyBaseThing, "Credentials"); }
            set { TheThing.SetSafePropertyString(MyBaseThing, "Credentials", value); }
        }
        public string Carrier
        {
            get { return TheThing.GetSafePropertyString(MyBaseThing, "Carrier"); }
            set { TheThing.SetSafePropertyString(MyBaseThing, "Carrier", value); }
        }
        public string SubjectText
        {
            get { return TheThing.GetSafePropertyString(MyBaseThing, "SubjectText"); }
            set { TheThing.SetSafePropertyString(MyBaseThing, "SubjectText", value); }
        }
        public string MessageText
        {
            get { return TheThing.GetSafePropertyString(MyBaseThing, "MessageText"); }
            set { TheThing.SetSafePropertyString(MyBaseThing, "MessageText", value); }
        }
        public string UserName
        {
            get { return TheThing.GetSafePropertyString(MyBaseThing, "UserName"); }
            set { TheThing.SetSafePropertyString(MyBaseThing, "UserName", value); }
        }
        public string Password
        {
            get { return TheThing.GetSafePropertyString(MyBaseThing, "Password"); }
            set { TheThing.SetSafePropertyString(MyBaseThing, "Password", value); }
        }
        public string FromAddress
        {
            get { return TheThing.GetSafePropertyString(MyBaseThing, "FromAddress"); }
            set { TheThing.SetSafePropertyString(MyBaseThing, "FromAddress", value); }
        }
        public string ServerName
        {
            get { return TheThing.GetSafePropertyString(MyBaseThing, "ServerName"); }
            set { TheThing.SetSafePropertyString(MyBaseThing, "ServerName", value); }
        }
        public bool UseSsl
        {
            get { return TheThing.GetSafePropertyBool(MyBaseThing, "UseSsl"); }
            set { TheThing.SetSafePropertyBool(MyBaseThing, "UseSsl", value); }
        }
        public int Port
        {
            get { return TheCommonUtils.CInt(TheThing.GetSafePropertyString(MyBaseThing, "Port")); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, "Port", value); }
        }

        void SendSMS(string pText)
        {
            try
            {
                MailMessage mail = new MailMessage();
                SmtpClient SmtpServer = new SmtpClient(MyBaseThing.Address);

                mail.From = new MailAddress(FromAddress);
                string receipt = Recipient;
                string credentials = Credentials;
                if (TheCommonUtils.IsNullOrWhiteSpace(receipt))
                    receipt = "fr@c-labs.com";
                string carrier = Carrier;

                // Code to handle carrier
                //TODO: Set Server name and SSL/Server and Port per Carrier
                #region Handle Carrier
                switch (carrier.ToLower())
                {
                    case "t-mobile":
                        receipt += "@tmomail.net";
                        break;
                    case "at&t":
                        receipt += "@txt.att.net";
                        break;
                    case "sprint":
                        receipt += "@messaging.sprintpcs.com";
                        break;
                    case "verizon":
                        receipt += "@vtext.com";
                        break;
                    case "virgin mobile":
                        receipt += "@vmobl.com";
                        break;
                    case "metro pcs":
                        receipt += "@MyMetroPcs";
                        break;
                    case "alltel":
                        receipt += "@message.alltel.com";
                        break;
                    case "powertel":
                        receipt += "@ptel.net";
                        break;
                    case "suncom":
                        receipt += "@tms.suncom.com";
                        break;
                    case "nextel":
                        receipt += "@messaging.nextel.com";
                        break;
                    case "us cellular":
                        receipt += "@email.uscc.net";
                        break;
                    case "boost mobile":
                        receipt += "@myboostmobile.com";
                        break;
                    case "cingular":
                        receipt += "@cingularme.com";
                        break;
                }
                #endregion

                mail.To.Add(receipt);
                if (string.IsNullOrEmpty(SubjectText))
                    mail.Subject = TheBaseAssets.MyServiceHostInfo.ApplicationName + " Message";
                else
                    mail.Subject = SubjectText;
                if (!TheCommonUtils.IsNullOrWhiteSpace(pText))
                    mail.Body = pText;
                else
                    mail.Body = MessageText;
               
                if (credentials.Equals("Admin"))
                {
                    string AdminUsername = MyBaseEngine.GetBaseThing().GetProperty("AdminUsername", false).ToString();
                    string AdminServer = MyBaseEngine.GetBaseThing().GetProperty("AdminServer", false).ToString();
                    string AdminPassword = MyBaseEngine.GetBaseThing().GetProperty("AdminPassword", false).GetValue().ToString();
                    int AdminPort = int.Parse(MyBaseEngine.GetBaseThing().GetProperty("AdminPort", false).ToString());
                    SmtpServer.Credentials = new System.Net.NetworkCredential(AdminUsername, AdminPassword);
                    SmtpServer.Host = AdminServer;
                    SmtpServer.Port = AdminPort;
                    string AdminSsl = MyBaseEngine.GetBaseThing().GetProperty("AdminSsl", false).ToString();
                    if(AdminSsl.Equals("True"))
                    {
                        SmtpServer.EnableSsl = true;
                    }
                }
                else
                {
                    SmtpServer.Credentials = new System.Net.NetworkCredential(UserName, Password);
                    SmtpServer.Port = Port;
                    if (UseSsl)
                    {
                        SmtpServer.EnableSsl = true;
                    }
                }
                SmtpServer.Send(mail);
                ResultText = "SMS Sent to " + Recipient;

                TSM tTSM = new TSM(MyBaseEngine.GetEngineName(), "SET_LAST_MSG", string.Format("SMS: Subject:{2} Body:{3} From {0} at {1}", TheBaseAssets.MyServiceHostInfo.MyStationName, DateTimeOffset.Now, SubjectText, mail.Body));
                MyBaseEngine.ProcessMessage(new TheProcessMessage(tTSM));
                TheCommCore.PublishCentral(tTSM);

            }
            catch (Exception ex)
            {
                ResultText = ex.ToString();
            }
        }
    }
}