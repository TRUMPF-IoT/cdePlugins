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
    public class TheEmailMessage: TheThingBase
    {
        public override cdeP SetProperty(string pName, object pValue)
        {
            if ("send" == pName.ToLower() || "sendmail" == pName.ToLower())
                SendEmail(pValue.ToString());
            else
            {
                if (MyBaseThing != null)
                    return MyBaseThing.SetProperty(pName, pValue);
            }
            return null;
        }

        private IBaseEngine MyBaseEngine;

        public TheEmailMessage(TheThing tBaseThing, ICDEPlugin pPluginBase)
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
            if (mIsInitCalled) return false;
            mIsInitCalled = true;
            MyBaseThing.RegisterEvent(eEngineEvents.IncomingMessage, HandleMessage);
            MyBaseThing.RegisterProperty("sendmail");
            MyBaseThing.StatusLevel = 1;

            MyBaseThing.DeclareSecureProperty(nameof(Password), ePropertyTypes.TString);

            mIsInitialized = true;
            return true;
        }

        public override bool CreateUX()
        {
            if (mIsUXInitCalled) return false;
            mIsUXInitCalled = true;

            var tFlds=TheNMIEngine.AddStandardForm(MyBaseThing,null,12);
            var tMyForm = tFlds["Form"] as TheFormInfo;
            (tFlds["DashIcon"] as TheDashPanelInfo).PropertyBag = new ThePropertyBag() { "Format={0}", "Thumbnail=FA5:f0e0" };

                TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.CollapsibleGroup, 10, 2, 0, "Message Header", false, null, null, new nmiCtrlCollapsibleGroup() { IsSmall=true, MaxTileWidth=12, ParentFld=1  });
                TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.SingleEnded, 20, 2, 0, "From Address", "FromAddress", new ThePropertyBag() { "TileHeight=1", "TileWidth=6", "ParentFld=10" });
                TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.SingleEnded, 30, 2, 0, "Enter a Recipient", "Recipient", new ThePropertyBag() { "TileHeight=1", "TileWidth=6", "ParentFld=10" });

                TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.SingleEnded, 40, 2, 0, "Subject", "SubjectText", new ThePropertyBag() { "TileHeight=1", "TileWidth=6", "ParentFld=10" });
                TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.TextArea, 50, 2, 0, "Enter a message", "MessageText", new ThePropertyBag() { "TileHeight=3", "TileWidth=6", "Rows=3", "ParentFld=10" });



                TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.CollapsibleGroup, 100, 2, 0, "Additional Settings...", null, new nmiCtrlCollapsibleGroup() { DoClose=true, IsSmall=true, MaxTileWidth=12, ParentFld=1 });
                //TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.SmartLabel, 20, 0, 0xC0, "Server Info", true, null, null, new ThePropertyBag() { "Style=font-size:20px;text-align: left;float:none;clear:left;width:100%", "ParentFld=6" });
                TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.SingleEnded, 121, (int)eFlag.IsReadWrite, 0xC0, "Servername", "Address", new ThePropertyBag() { "TileHeight=1", "TileWidth=6", "ParentFld=100" });
                TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.SingleCheck, 122, 2, 0xC0, "UseSsl", "UseSsl", new ThePropertyBag() { "TileHeight=1", "TileWidth=3", "ParentFld=100" });
                TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.Number, 123, 2, 0xC0, "Port", "Port", new ThePropertyBag() { "TileHeight=1", "TileWidth=3", "ParentFld=100", "DefaultValue=25" });
                TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.SingleEnded, 124, 2, 0xC0, "Username", "UserName", new ThePropertyBag() { "TileHeight=1", "TileWidth=6", "ParentFld=100" });
                TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.Password, 125, 3, 0xC0, "Password", "Password", new ThePropertyBag() { "TileHeight=1", "TileWidth=6", "ParentFld=100", "HideMTL=true" });

                TheFieldInfo mSendbutton = TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.TileButton, 132, 2, 0, "Send Email", false, "", null, new nmiCtrlTileButton() { NoTE=true, ParentFld=100, ClassName="cdeGoodActionButton" });
                mSendbutton.RegisterUXEvent(MyBaseThing, eUXEvents.OnClick, "", (pThing,pPara) =>
                {
                    SendEmail(null);
                });
                TheNMIEngine.AddSmartControl(MyBaseThing, tMyForm, eFieldType.TextArea, 155, 0, 0, "Sent Result", "ResultText", new ThePropertyBag() { "TileHeight=2", "TileWidth=6", "Rows=2", "ParentFld=100" });
            mIsUXInitialized = true;
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

        void SendEmail(string pText)
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
                mail.To.Add(receipt);
                if (string.IsNullOrEmpty(SubjectText))
                    mail.Subject = TheBaseAssets.MyServiceHostInfo.ApplicationName + " Message";
                else
                    mail.Subject = SubjectText;
                if (!TheCommonUtils.IsNullOrWhiteSpace(pText))
                    mail.Body = pText;
                else
                    mail.Body = MessageText;
                if(credentials.Equals("Admin"))
                {
                    string AdminUsername = MyBaseEngine.GetBaseThing().GetProperty("AdminUsername", false).ToString();
                    string AdminServer = MyBaseEngine.GetBaseThing().GetProperty("AdminServer", false).ToString();
                    string AdminPassword = MyBaseEngine.GetBaseThing().GetProperty("AdminPassword", false).GetValue().ToString();
                    int AdminPort = int.Parse(MyBaseEngine.GetBaseThing().GetProperty("AdminPort", false).ToString());
                    SmtpServer.Credentials = new System.Net.NetworkCredential(AdminUsername, AdminPassword);
                    SmtpServer.Host = AdminServer;
                    SmtpServer.Port = AdminPort;
                    string AdminSsl = MyBaseEngine.GetBaseThing().GetProperty("AdminSsl", false).ToString();
                    if (AdminSsl.Equals("True"))
                    {
                        SmtpServer.EnableSsl = true;
                    }
                }
                else
                {
                SmtpServer.Port = Port;
                SmtpServer.Credentials = new System.Net.NetworkCredential(UserName, Password);
                if (UseSsl)
                {
                    SmtpServer.EnableSsl = true;
                }
                }
                
                SmtpServer.Send(mail);
                ResultText = "Mail Sent to " + Recipient;

                TSM tTSM = new TSM(MyBaseEngine.GetEngineName(), "SET_LAST_MSG", string.Format("eMail: Subject:{2} Body:{3} From {0} at {1}", TheBaseAssets.MyServiceHostInfo.MyStationName, DateTimeOffset.Now, SubjectText,mail.Body));
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
