// SPDX-FileCopyrightText: 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

﻿using nsCDEngine.BaseClasses;
using nsCDEngine.Communication;
using nsCDEngine.Engines;
using nsCDEngine.Engines.StorageService;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;

namespace CDMyLogger.ViewModel
{

    class TheRSSFeed : TheLoggerBase
    {
        public TheRSSFeed(TheThing tBaseThing, ICDEPlugin pPluginBase) : base(tBaseThing, pPluginBase)
        {
            MyBaseThing.DeviceType = eTheLoggerServiceTypes.RSSLogger;
        }

        public override void Connect(TheProcessMessage pMsg)
        {
            base.Connect(pMsg);
            if (TheCommCore.MyHttpService != null)
            {
                TheCommCore.MyHttpService.RegisterHttpInterceptorB4("/EVTLOG.RSS", InterceptRSSEvtRequest);
                TheCommCore.MyHttpService.RegisterHttpInterceptorB4("/SYSLOG.RSS", InterceptRSSRequest);
            }
        }

        public override void Disconnect(TheProcessMessage pMsg)
        {
            base.Disconnect(pMsg);
            if (TheCommCore.MyHttpService != null)
            {
                TheCommCore.MyHttpService.UnregisterHttpInterceptorB4("/EVTLOG.RSS");
                TheCommCore.MyHttpService.UnregisterHttpInterceptorB4("/SYSLOG.RSS");
            }
        }

        private void InterceptRSSRequest(TheRequestData pRequest)
        {
            if (pRequest == null) return;
            try
            {
                string Query = pRequest.RequestUri?.Query;
                if (Query != null && Query.StartsWith("?"))
                    Query = Query.Substring(1);
                Dictionary<string, string> tQ = TheCommonUtils.ParseQueryString(Query); //DIC-Allowed STRING
                CreateSysLogFeed(pRequest, 200);
            }
            catch
            {
                //ignored
            }
        }

        internal void InterceptRSSEvtRequest(TheRequestData pRequest)
        {
            if (pRequest == null) return;
            try
            {
                TheStorageMirror<TheEventLogData> tEventLog = TheCDEngines.GetStorageMirror("EventLog") as TheStorageMirror<TheEventLogData>;
                if (tEventLog!=null)
                    CreateEvtLogFeed(pRequest, tEventLog?.TheValues.OrderByDescending(s => s.cdeCTIM).ThenByDescending(s => s.cdeCTIM.Millisecond).ToList(), 200);
                else
                    TheRSSGenerator.CreateRSS(pRequest, new List<TheEventLogData>() { new TheEventLogData() { EventName = "No Eventlog on this Node" } }, 1);
            }
            catch
            {
                //ignored
            }
        }


        public static void CreateSysLogFeed(TheRequestData pRequest, int MaxCnt)
        {
            if (pRequest.RequestUri != null && !string.IsNullOrEmpty(pRequest.RequestUri.Query) && pRequest.RequestUri.Query.Length > 1)
            {
                var QParts = TheCommonUtils.ParseQueryString(pRequest.RequestUri.Query); //.Split('=');
                //if (QParts.ContainsKey("SID") && (!TheScopeManager.IsValidScopeID(TheScopeManager.GetScrambledScopeIDFromEasyID(QParts["SID"])) || QParts.ContainsKey("NID")))
                //{
                //    pRequest.SessionState.SScopeID = TheScopeManager.GetScrambledScopeIDFromEasyID(QParts["SID"]);
                //    Guid tTarget = Guid.Empty;
                //    if (QParts.ContainsKey("NID"))
                //        tTarget = TheCommonUtils.CGuid(QParts["NID"]);
                //    Communication.HttpService.TheHttpService.cdeStreamFile(pRequest, true, 10, tTarget);
                //    if (pRequest.ResponseBuffer == null)
                //        TheRSSGenerator.CreateRSS(pRequest, new List<TheEventLogData>() { new TheEventLogData() { EventName = "Relay did not answer" } }, 1);
                //    return;
                //}
            }
            TheStorageMirror<TheEventLogEntry> tEventLog = TheCDEngines.GetStorageMirror($"{typeof(TheEventLogEntry)}") as TheStorageMirror<TheEventLogEntry>;
            TheRSSGenerator.CreateRSS(pRequest, tEventLog.TheValues.OrderByDescending(s => s.cdeCTIM).ThenByDescending(s => s.cdeCTIM.Millisecond).ToList(), MaxCnt);
        }
      
        public static void CreateEvtLogFeed(TheRequestData pRequest,List<TheEventLogData> pLogData, int MaxCnt)
        {
            if (pRequest.RequestUri != null && !string.IsNullOrEmpty(pRequest.RequestUri.Query) && pRequest.RequestUri.Query.Length > 1)
            {
                var QParts = TheCommonUtils.ParseQueryString(pRequest.RequestUri.Query); //.Split('=');
                //if (QParts.ContainsKey("SID") && (!TheScopeManager.IsValidScopeID(TheScopeManager.GetScrambledScopeIDFromEasyID(QParts["SID"])) || QParts.ContainsKey("NID")))
                //{
                //    pRequest.SessionState.SScopeID = TheScopeManager.GetScrambledScopeIDFromEasyID(QParts["SID"]);
                //    Guid tTarget = Guid.Empty;
                //    if (QParts.ContainsKey("NID"))
                //        tTarget = TheCommonUtils.CGuid(QParts["NID"]);
                //    TheHttpService.cdeStreamFile(pRequest, true, 300, tTarget);
                //    if (pRequest.ResponseBuffer == null)
                //        TheRSSGenerator.CreateRSS(pRequest, new List<TheEventLogData>() { new TheEventLogData() { EventName = "Relay did not answer" } }, 1);
                //    return;
                //}
            }
            if (pLogData != null)
                TheRSSGenerator.CreateRSS(pRequest, pLogData, 200);
            else
                TheRSSGenerator.CreateRSS(pRequest, new List<TheEventLogData>() { new TheEventLogData() { EventName = "No Eventlog on this Node" } }, 1);
        }

    }
    internal class TheRSSGenerator
    {
        internal static void CreateRSS(TheRequestData pRequest, List<TheEventLogEntry> pLog, int MaxCnt)
        {
            try
            {
                using (MemoryStream tW = new MemoryStream())
                {
                    TheRSSGenerator gen = new TheRSSGenerator(tW)
                    {
                        Title = TheBaseAssets.MyServiceHostInfo.ApplicationTitle + " - System Log Feed",
                        Description = "Last Events of the System Log",
                        LastBuildDate = DateTimeOffset.Now,
                        Link = TheBaseAssets.MyServiceHostInfo.SiteName,
                        PubDate = DateTimeOffset.Now
                    };
                    gen.WriteStartDocument();
                    gen.WriteStartChannel();

                    int cnt = 0;
                    foreach (TheEventLogEntry tEntry in pLog)
                    {
                        gen.WriteItem(

                            tEntry.Message.TXT,
                            TheBaseAssets.MyServiceHostInfo.SiteName + "/" + TheBaseAssets.MyServiceHostInfo.PortalPage, //"url to the item page",
                            tEntry.Message.PLS, //  "the description of the item",
                            tEntry.Message.ENG, // "the author",
                            tEntry.Message.ENG, //"the category",
                            "", //"comments",
                            tEntry.cdeMID.ToString(), //"the guid",
                            tEntry.cdeCTIM, //    DateTimeOffset.Now,
                            tEntry.Message.ENG, //"the source",
                            "", //"enclosure URL",
                            "", //"enclosure length",
                            ""//"enclosure type"
                            );
                        if (MaxCnt > 0 && cnt > MaxCnt)
                            break;
                        cnt++;
                    }
                    gen.WriteEndChannel();
                    gen.WriteEndDocument();
                    gen.Close();
                    pRequest.ResponseBuffer = tW.ToArray();
                }
                pRequest.StatusCode = 200;
                pRequest.ResponseMimeType = "application/rss+xml";
                pRequest.DontCompress = true;
            }

            catch
            {
                // do something with the error
            }
        }
        internal static void CreateRSS(TheRequestData pRequest, List<TheEventLogData> pLog, int MaxCnt)
        {
            try
            {
                using (MemoryStream tW = new MemoryStream())
                {
                    TheRSSGenerator gen = new TheRSSGenerator(tW)
                    {
                        Title = TheBaseAssets.MyServiceHostInfo.ApplicationTitle + " - Event Feed",
                        Description = "All Events coming from the Rules Event Log",
                        LastBuildDate = DateTimeOffset.Now,
                        Link = TheBaseAssets.MyServiceHostInfo.SiteName,
                        PubDate = DateTimeOffset.Now
                    };
                    //gen.Category = "Home-Automation";

                    gen.WriteStartDocument();
                    gen.WriteStartChannel();

                    int cnt = 0;
                    foreach (TheEventLogData tEntry in pLog)
                    {
                        gen.WriteItem(

                            tEntry.EventName,
                            TheBaseAssets.MyServiceHostInfo.SiteName + "/" + TheBaseAssets.MyServiceHostInfo.PortalPage, //"url to the item page",
                            string.Format("{0} occured at {1} on stations {2}. {3}", tEntry.EventName, tEntry.EventTime, tEntry.StationName, tEntry.EventString), //  "the description of the item",
                            tEntry.StationName, // "the author",
                            TheThing.GetSafePropertyString(TheThingRegistry.GetThingByMID("*", TheCommonUtils.CGuid(tEntry.EventTrigger)), "FriendlyName"), // "the category",
                            "", //"comments",
                            tEntry.cdeMID.ToString(), //"the guid",
                            tEntry.EventTime, //    DateTimeOffset.Now,
                            tEntry.StationName, //"the source",
                            "", //"enclosure URL",
                            "", //"enclosure length",
                            ""//"enclosure type"
                            );
                        if (MaxCnt > 0 && cnt > MaxCnt)
                            break;
                        cnt++;
                    }
                    gen.WriteEndChannel();
                    gen.WriteEndDocument();
                    gen.Close();
                    pRequest.ResponseBuffer = tW.ToArray();
                }
                pRequest.StatusCode = 200;
                pRequest.ResponseMimeType = "application/rss+xml";
                pRequest.DontCompress = true;
            }

            catch
            {
                // do something with the error
            }
        }

        readonly XmlTextWriter writer;

        #region Private Members
        private string _title;
        private string _link;
        private string _description;
        private string _language = "en-gb";
        private string _copyright = "Copyright " + DateTimeOffset.Now.Year.ToString();
        private string _managingEditor;
        private string _webMaster;
        private DateTimeOffset _pubDate;
        private DateTimeOffset _lastBuildDate;
        private string _category;
        private string _generator = "C-DEngine RSS Generator";
        private string _docs = "http://blogs.law.harvard.edu/tech/rss";
        private string _rating;
        private string _ttl = "20";
        private string _imgNavigationUrl;
        private string _imgUrl;
        private string _imgTitle;
        private string _imgHeight;
        private string _imgWidth;
        private bool _isItemSummary;
        private int _maxCharacters = 300;
        #endregion

        #region Public Members
        /// 
        /// Required - The name of the channel. It's how people refer to your service. If you have an HTML website that contains the same information as your RSS file, the title of your channel should be the same as the title of your website.
        /// 
        public string Title
        {
            get { return _title; }
            set { _title = value; }
        }

        /// 
        /// Required - The URL to the HTML website corresponding to the channel.
        /// 
        public string Link
        {
            get { return _link; }
            set { _link = value; }
        }

        /// 
        /// Required - Phrase or sentence describing the channel.
        /// 
        public string Description
        {
            get { return _description; }
            set { _description = value; }
        }

        /// 
        /// The language the channel is written in.
        /// 
        public string Language
        {
            get { return _language; }
            set { _language = value; }
        }

        /// 
        /// Copyright notice for content in the channel.
        /// 
        public string Copyright
        {
            get { return _copyright; }
            set { _copyright = value; }
        }

        /// 
        /// Email address for person responsible for editorial content.
        /// 
        public string ManagingEditor
        {
            get { return _managingEditor; }
            set { _managingEditor = value; }
        }

        /// 
        /// Email address for person responsible for technical issues relating to channel.
        /// 
        public string WebMaster
        {
            get { return _webMaster; }
            set { _webMaster = value; }
        }

        /// 
        /// The publication date for the content in the channel. For example, the New York Times publishes on a daily basis, the publication date flips once every 24 hours. That's when the pubDate of the channel changes. 
        /// 
        public DateTimeOffset PubDate
        {
            get { return _pubDate; }
            set { _pubDate = value; }
        }

        /// 
        /// The last time the content of the channel changed.
        /// 
        public DateTimeOffset LastBuildDate
        {
            get { return _lastBuildDate; }
            set { _lastBuildDate = value; }
        }

        /// 
        /// Specify one or more categories that the channel belongs to.
        /// 
        public string Category
        {
            get { return _category; }
            set { _category = value; }
        }

        /// 
        /// A string indicating the program used to generate the channel.
        /// 
        public string Generator
        {
            get { return _generator; }
            set { _generator = value; }
        }

        /// 
        /// A URL that points to the documentation for the format used in the RSS file.
        /// 
        public string Docs
        {
            get { return _docs; }
            set { _docs = value; }
        }

        /// 
        /// The PICS rating for the channel.
        /// 
        public string Rating
        {
            get { return _rating; }
            set { _rating = value; }
        }

        /// 
        /// ttl stands for time to live. It's a number of minutes that indicates how long a channel can be cached before refreshing from the source. 
        /// 
        public string Ttl
        {
            get { return _ttl; }
            set { _ttl = value; }
        }

        /// 
        /// is the URL of the site, when the channel is rendered, the image is a link to the site. (Note, in practice the image 
        public string ImgNavigationUrl
        {
            get { return _imgNavigationUrl; }
            set { _imgNavigationUrl = value; }
        }

        /// 
        /// The URL of a GIF, JPEG or PNG image that represents the channel
        /// 
        public string ImgUrl
        {
            get { return _imgUrl; }
            set { _imgUrl = value; }
        }

        /// 
        /// Describes the image, it's used in the ALT attribute of the HTML  tag when the channel is rendered in HTML. 
        /// 
        public string ImgTitle
        {
            get { return _imgTitle; }
            set { _imgTitle = value; }
        }

        /// 
        /// The height of the image
        /// 
        public string ImgHeight
        {
            get { return _imgHeight; }
            set { _imgHeight = value; }
        }

        /// 
        /// The width of the image
        /// 
        public string ImgWidth
        {
            get { return _imgWidth; }
            set { _imgWidth = value; }
        }

        /// 
        /// Indicates whether to show the full Item description or a summary
        /// 
        public bool IsItemSummary
        {
            get { return _isItemSummary; }
            set { _isItemSummary = value; }
        }

        /// 
        /// Indicates the amount of characters to display in the Item description
        /// 
        public int MaxCharacters
        {
            get { return _maxCharacters; }
            set { _maxCharacters = value; }
        }

        #endregion

        #region Constructors

        public TheRSSGenerator(Stream stream, Encoding encoding)
        {
            writer = new XmlTextWriter(stream, encoding) { Formatting = Formatting.Indented };
        }

        public TheRSSGenerator(MemoryStream w)
        {
            //XmlWriterSettings settings = new XmlWriterSettings();
            //settings.Indent = true;
            //settings.Encoding = Encoding.UTF8;
            writer = new XmlTextWriter(w, Encoding.UTF8) { Formatting = Formatting.Indented };
        }

        #endregion

        #region Methods
        /// 
        /// Writes the beginning of the RSS document
        /// 
        public void WriteStartDocument()
        {
            //            //
            //            

            //settings.NewLineOnAttributes = true;
            writer.WriteStartDocument();
            //string PItext = "type='text/xsl' href='styles/rss.xsl'";
            //writer.WriteProcessingInstruction("xml-stylesheet", PItext);

            //string PItext2 = "type='text/css' href='styles/rss.css'";
            //writer.WriteProcessingInstruction("xml-stylesheet", PItext2);


            writer.WriteStartElement("rss");
            writer.WriteAttributeString("version", "2.0");
        }

        /// 
        /// Writes the end of the RSS document
        /// 
        public void WriteEndDocument()
        {
            writer.WriteEndElement(); //rss
            writer.WriteEndDocument();
        }

        /// 
        /// Closes this stream and the underlying stream
        /// 
        public void Close()
        {
            writer.Flush();
            writer.Close();
        }

        /// 
        /// Writes the beginning of a channel in the RSS document
        /// 
        public void WriteStartChannel()
        {
            try
            {
                writer.WriteStartElement("channel");

                writer.WriteElementString("title", _title);
                writer.WriteElementString("link", _link);
                writer.WriteElementString("description", _description);

                if (!String.IsNullOrEmpty(_language))
                    writer.WriteElementString("language", _language);

                if (!String.IsNullOrEmpty(_copyright))
                    writer.WriteElementString("copyright", _copyright);

                if (!String.IsNullOrEmpty(_managingEditor))
                    writer.WriteElementString("managingEditor", _managingEditor);

                if (!String.IsNullOrEmpty(_webMaster))
                    writer.WriteElementString("webMaster", _webMaster);

                if (_pubDate != null && _pubDate != DateTimeOffset.MinValue && _pubDate != DateTimeOffset.MaxValue)
                    writer.WriteElementString("pubDate", _pubDate.ToString("r"));

                if (_lastBuildDate != null && _lastBuildDate != DateTimeOffset.MinValue && _lastBuildDate != DateTimeOffset.MaxValue)
                    writer.WriteElementString("lastBuildDate", _lastBuildDate.ToString("r"));

                if (!String.IsNullOrEmpty(_category))
                    writer.WriteElementString("category", _category);

                if (!String.IsNullOrEmpty(_generator))
                    writer.WriteElementString("generator", _generator);

                if (!String.IsNullOrEmpty(_docs))
                    writer.WriteElementString("docs", _docs);

                if (!String.IsNullOrEmpty(_rating))
                    writer.WriteElementString("rating", _rating);

                if (!String.IsNullOrEmpty(_ttl))
                    writer.WriteElementString("ttl", _ttl);

                if (!String.IsNullOrEmpty(_imgUrl))
                {
                    writer.WriteStartElement("image");
                    writer.WriteElementString("url", _imgUrl);

                    if (!String.IsNullOrEmpty(_imgNavigationUrl))
                        writer.WriteElementString("link", _imgNavigationUrl);

                    if (!String.IsNullOrEmpty(_imgTitle))
                        writer.WriteElementString("title", _imgTitle);

                    if (!String.IsNullOrEmpty(_imgWidth))
                        writer.WriteElementString("width", _imgWidth);

                    if (!String.IsNullOrEmpty(_imgHeight))
                        writer.WriteElementString("height", _imgHeight);

                    writer.WriteEndElement();
                }


            }
            catch
            {
                //ignored
            }

        }

        /// 
        /// Writes the end of a channel in the RSS document
        /// 
        public void WriteEndChannel()
        {
            writer.WriteEndElement(); //channel
        }

        /// 
        /// Writes an RSS Feed Item
        /// 
        /// The title of the item.
        /// The URL of the item
        /// The item synopsis.
        /// Email address of the author of the item.
        /// Includes the item in one or more categories
        /// URL of a page for comments relating to the item.
        /// A string that uniquely identifies the item.
        /// Indicates when the item was published.
        /// The URL of the RSS channel that the item came from.
        /// The URL of where the enclosure is located
        /// The length of the enclosure (how big it is in bytes).
        /// The standard MIME type of the enclosure.
        public void WriteItem(string title, string link, string description, string author, string category,
            string comments, string guid, DateTimeOffset pubDate, string source, string encUrl, string encLength, string encType)
        {
            try
            {
                writer.WriteStartElement("item");
                writer.WriteElementString("title", title);
                writer.WriteElementString("link", link);
                writer.WriteRaw("");

                if (!String.IsNullOrEmpty(author))
                    writer.WriteElementString("author", author);

                if (!String.IsNullOrEmpty(category))
                    writer.WriteElementString("category", category);

                if (!String.IsNullOrEmpty(comments))
                    writer.WriteElementString("comments", comments);

                if (!String.IsNullOrEmpty(description))
                    writer.WriteElementString("description", description);

                if (!String.IsNullOrEmpty(guid))
                {
                    //writer.WriteElementString("guid", guid);
                    writer.WriteStartElement("guid");
                    writer.WriteAttributeString("isPermaLink", "false");
                    writer.WriteString(guid);
                    writer.WriteEndElement();
                }

                if (pubDate != null && pubDate != DateTimeOffset.MinValue && pubDate != DateTimeOffset.MaxValue)
                    writer.WriteElementString("pubDate", pubDate.ToUniversalTime().ToString("r"));

                if (!String.IsNullOrEmpty(source))
                    writer.WriteElementString("source", source);

                if (!String.IsNullOrEmpty(encUrl) && !String.IsNullOrEmpty(encLength) && !String.IsNullOrEmpty(encType))
                {
                    writer.WriteStartElement("enclosure");
                    writer.WriteAttributeString("url", encUrl);
                    writer.WriteAttributeString("length", encLength);
                    writer.WriteAttributeString("type", encType);
                    writer.WriteEndElement();
                }

                writer.WriteEndElement();
            }
            catch
            {
                //ignored
            }
        }

        ///// 
        ///// Trims the description if necessary
        ///// 
        ///// 
        ///// 
        //private string GetDescription(string description)
        //{
        //    if (_isItemSummary)
        //    {
        //        if (description == "")
        //        {
        //            return "";
        //        }
        //        else
        //        {
        //            if (description.Length > _maxCharacters)
        //            {
        //                return description.ToString().Substring(0, _maxCharacters) + " ...";
        //            }
        //            else
        //                return description;
        //        }
        //    }
        //    else
        //        return description;
        //}

        #endregion
    }
}
