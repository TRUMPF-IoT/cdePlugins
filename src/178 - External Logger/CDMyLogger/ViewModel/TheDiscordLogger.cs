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
        }

        protected override void DoCreateUX(TheFormInfo pForm)
        {
            base.DoCreateUX(pForm);
            MyStatusFormDashPanel.PropertyBag = new nmiDashboardTile { Thumbnail = "FA5Bf392" };

            var but=TheNMIEngine.AddSmartControl(MyBaseThing, pForm, eFieldType.TileButton, 136, 2, 0xf0, "Test Entry", null, new nmiCtrlTileButton() { NoTE=true, TileWidth = 3, ParentFld = 120, ClassName="cdeGoodActionButton" });
            but.RegisterUXEvent(MyBaseThing, eUXEvents.OnClick, "HRE", (sender, psmg) => {
                LogEvent(new TheEventLogData
                {
                    EventCategory = eLoggerCategory.ThingEvent,
                    EventLevel = eMsgLevel.l4_Message,
                    EventTime= DateTime.Now,
                    EventName="Test Event",
                    EventString="Clicked in the UX",
                    StationName=TheBaseAssets.MyServiceHostInfo.MyStationName
                }); 
            });
        }


        public override bool LogEvent(TheEventLogData pItem)
        {
            if (string.IsNullOrEmpty(MyBaseThing.Address) || pItem == null || !IsConnected)
                return false;
            switch (pItem.EventCategory)
            {
                default:
                case eLoggerCategory.NMIAudit:
                case eLoggerCategory.NodeConnect:
                    return false;
                case eLoggerCategory.UserEvent:
                case eLoggerCategory.ThingEvent:
                    break;
            }

            TheCommonUtils.cdeRunTaskAsync("webHook", async (_) =>
            {
                using (var client = new DiscordWebhookClient(MyBaseThing.Address))
                {
                    Discord.Color tCol = Discord.Color.Default;
                    switch (pItem.EventLevel)
                    {
                        case eMsgLevel.l4_Message:
                            tCol = Discord.Color.DarkGreen;
                            break;
                        case eMsgLevel.l1_Error:
                            tCol = Discord.Color.Red;
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
                    try
                    {
                        var embed = new EmbedBuilder
                        {
                            Description = pItem.EventName,
                            Title = pItem.EventString,
                            Color = tCol,
                            Timestamp = pItem.EventTime,
                        };

                        string tOrg = pItem.EventTrigger;
                        if (string.IsNullOrEmpty(tOrg))
                            tOrg = pItem.StationName;
                        else
                            tOrg = $"{tOrg} ({pItem.StationName})";
                        // Webhooks are able to send multiple embeds per message
                        // As such, your embeds must be passed as a collection.
                        if (pItem.EventData?.Length > 0)
                        {
                            using (var ms = new MemoryStream(pItem.EventData))
                            {
                                await client.SendFileAsync(stream: ms, filename: "Image.jpg", text: $"Node \"{pItem.StationName}\" had Event-Log entry for: {tOrg}", embeds: new[] { embed.Build() });
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
            return true;
        }
    }
}
