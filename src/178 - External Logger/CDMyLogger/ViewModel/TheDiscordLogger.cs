using Discord;
using Discord.Webhook;
using nsCDEngine.BaseClasses;
using nsCDEngine.Engines;
using nsCDEngine.Engines.NMIService;
using nsCDEngine.Engines.ThingService;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace CDMyLogger.ViewModel
{
    internal class TheDiscordLogger : TheLoggerBase
    {
        public TheDiscordLogger(TheThing tBaseThing, ICDEPlugin pPluginBase) : base(tBaseThing, pPluginBase)
        {
            MyBaseThing.DeviceType = eTheLoggerServiceTypes.DiscordLogger;
        }
        protected override void DoInit()
        {
            if (AutoConnect)
                IsConnected = true;
            TheCDEngines.MyContentEngine.RegisterEvent(eEngineEvents.NewEventLogEntry, sinkLogMe);
        }

        protected override void DoCreateUX(TheFormInfo pForm)
        {
            base.DoCreateUX(pForm);
            MyStatusFormDashPanel.PropertyBag = new nmiDashboardTile { Thumbnail = "FA5Bf392" };
        }

        void sinkLogMe(ICDEThing sender, object para)
        {
            var pData = para as TheEventLogData;
            if (string.IsNullOrEmpty(MyBaseThing.Address) || pData==null || !IsConnected)
                return;
            switch (pData.EventCategory)
            {
                default:
                case eLoggerCategory.NMIAudit:
                case eLoggerCategory.NodeConnect:
                    return;
                case eLoggerCategory.UserEvent:
                case eLoggerCategory.ThingEvent:
                    break;
            }

            TheCommonUtils.cdeRunTaskAsync("webHook", async (_) =>
            {
                using (var client = new DiscordWebhookClient(MyBaseThing.Address))  
                {
                    Discord.Color tCol = Discord.Color.Default;
                    switch (pData.EventLevel)
                    {
                        case eMsgLevel.l4_Message:
                            tCol = Discord.Color.DarkGreen;
                            break;
                        case eMsgLevel.l1_Error:
                            tCol=Discord.Color.Red;
                            break;
                        case eMsgLevel.l2_Warning:
                            tCol = Discord.Color.LightOrange;
                            break;
                        case eMsgLevel.l6_Debug:
                            tCol = Discord.Color.LightGrey;
                            break;
                        case eMsgLevel.l7_HostDebugMessage:
                            tCol = Discord.Color.DarkerGrey;
                            break;
                        case eMsgLevel.l3_ImportantMessage:
                            tCol = Discord.Color.Green;
                            break;
                    }
                    var embed = new EmbedBuilder
                    {
                        Description = pData.EventName,
                        Title = pData.EventString,
                        Color = tCol,
                        Timestamp = pData.EventTime,
                    };

                    string tOrg = pData.EventTrigger;
                    if (string.IsNullOrEmpty(tOrg))
                        tOrg = pData.StationName;
                    // Webhooks are able to send multiple embeds per message
                    // As such, your embeds must be passed as a collection.
                    try
                    {
                        if (pData.EventData?.Length > 0)
                        {
                            using (var ms = new MemoryStream(pData.EventData))
                            {
                                await client.SendFileAsync(stream: ms, filename: "Image.jpg", text: $"Node \"{pData.StationName}\" had Event-Log entry for: {tOrg}", embeds: new[] { embed.Build() });
                            }
                        }
                        else
                            await client.SendMessageAsync(text: $"Event Log Entry for: {tOrg}", embeds: new[] { embed.Build() });
                    }
                    catch (Exception e)
                    {
                        SetMessage($"Error: {e}", DateTimeOffset.Now, 178001, eMsgLevel.l1_Error);
                    }
                }
            });
        }
    }
}
