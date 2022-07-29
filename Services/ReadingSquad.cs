using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using NodaTime;
using NodaTime.Extensions;
using Shisho.Models;
using Shisho.Utility;
using Shisho.Utility.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Shisho.Services;

[AutoDiscoverSingletonService]
public class ReadingSquad
{
    public class Config : PersistableConfig<Config>
    {
        public ulong? ChannelDiscordId { get => Get<ulong?>(); set => Set(value); }
        
        public ulong? RoleDiscordId { get => Get<ulong?>(); set => Set(value); }
        
        public string? TimeOfDay { get => Get<string>(); set => Set(value); }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public DayOfWeek? DayOfWeek { get => Get<DayOfWeek?>(); set => Set(value); }
        
        public string? SchedulingRelativeToTz { get => Get<string>(); set => Set(value); }

        public bool Enabled { get => Get<bool>(); set => Set(value); }

        public bool IsConfigured()
        {
            return
                !(ChannelDiscordId == null ||
                RoleDiscordId == null ||
                TimeOfDay == null ||
                DayOfWeek == null ||
                SchedulingRelativeToTz == null);
        }
    }

    private readonly SemaphoreSlim approveUserSemaphore = new(1, 1);
    public Task<bool> TryApproveUser(Instance instance, IGuildUser user, IUserMessage msg) => Task.Run(async () =>
    {
        try
        {
            await approveUserSemaphore.WaitAsync();
            var cfg = instance.ReadingSquadConfig;
            if (!cfg.Enabled)
            {
                logger.LogWarning("Report approval system is not enabled");
                return false;
            }

            var nextDeadline = instance.NextDeadline;
            if (!cfg.IsConfigured() || nextDeadline == null)
            {
                logger.LogError("Can't approve user; configuration missing or no upcoming deadline");
                return false;
            }

            try
            {
                var report = new ReadingReport
                {
                    MessageDiscordId = msg.Id,
                    ReportMessageInstant = msg.Timestamp.ToInstant(),
                    UserDiscordId = user.Id,
                    DeadlineKey = nextDeadline.Key,
                };

                if (nextDeadline.DeadlineInstant - report.ReportMessageInstant > Duration.FromDays(7))
                {
                    logger.LogWarning($"Can't approve a report from more than one week before the next deadline (message: {msg.Id})");
                    return false;
                }

                var alreadyReportedThisWeek = instance.Database.Select<ReadingReport>()
                    .Where(x => x.DeadlineKey == nextDeadline.Key)
                    .Any(x => x.UserDiscordId == report.UserDiscordId);

                if (alreadyReportedThisWeek)
                {
                    logger.LogTrace($"A report for user {report.UserDiscordId} has already been approved for the upcoming deadline");
                    return true;
                }

                instance.Database.Insert(report);
                await user.AddRoleAsync(cfg.RoleDiscordId!.Value, new RequestOptions { AuditLogReason = "Reading report approved" });
                logger.LogInformation($"Approved report (user: {user.Id}, message: {msg.Id}");
                return true;
            }
            catch (Exception e)
            {
                logger.LogError("Failed to approve user", e);
                return false;
            }
        }
        finally
        {
            approveUserSemaphore.Release();
        }
    });

    public Task<DataExport> ExportHistory(Config cfg) => Task.Run(async () =>
    {
        if (await discord.GetChannelAsync(cfg.ChannelDiscordId!.Value) is not ITextChannel reportChannel)
            throw new Exception("Error accessing reports channel; export failed");

        var history = new List<(ulong userId, ulong messageId, long unixSeconds)>();
        foreach (var message in await reportChannel.GetMessagesAsync(int.MaxValue).FlattenAsync())
        {
            if (message.Author.IsBot)
                continue;

            if (message.Reactions.Any(x => x.Key.Name == "❌"))
                continue;

            history.Add((message.Author.Id, message.Id, message.Timestamp.ToUnixTimeSeconds()));
        }

        if (history.Count == 0)
            throw new Exception($"Failed to export messages or {reportChannel.Mention} is empty");

        history.Sort((x, y) => x.unixSeconds.CompareTo(y.unixSeconds));

        var firstMessageInstant = Instant.FromUnixTimeSeconds(history.First().unixSeconds);
        var tz = timezoneProvider.Tzdb[cfg.SchedulingRelativeToTz!];
        var start = firstMessageInstant.InZone(tz).LocalDateTime.Date;
        var deadlines = GenerateDeadlineInstants(cfg, start).GetEnumerator();

        var items = new List<DataExport.Item>();
        var reports = new List<ReadingReport>();
        var nextDeadline = new ReadingDeadline();
        var usersThisWeek = new HashSet<ulong>();

        var next = () =>
        {
            deadlines.MoveNext();
            if (reports.Any())
            {
                items.Add(new DataExport.Item
                {
                    Deadline = nextDeadline,
                    Reports = reports,
                });

                reports = new List<ReadingReport>();
                usersThisWeek.Clear();
            }

            nextDeadline = new ReadingDeadline { DeadlineInstant = deadlines.Current };
        };

        next();
        foreach (var (usr, msg, ts) in history)
        {
            var instant = Instant.FromUnixTimeSeconds(ts);
            while (instant > nextDeadline.DeadlineInstant)
                next();

            if (usersThisWeek.Add(usr))
                reports.Add(new ReadingReport
                {
                    DeadlineKey = nextDeadline.Key,
                    MessageDiscordId = msg,
                    UserDiscordId = usr,
                    ReportMessageInstant = instant
                });
        }

        next();

        return new DataExport { Items = items };
    });

    public async Task OnOrchestratorTick(Instance instance)
    {
        if (!instance.ReadingSquadConfig.Enabled)
            return;

        var nextDeadline = instance.NextDeadline;
        if (nextDeadline == null)
            return;

        var now = SystemClock.Instance.GetCurrentInstant();
        if (now > nextDeadline.DeadlineInstant)
            await HandleDeadline(instance, nextDeadline);
    }

    public Task HandleDeadline(Instance instance, ReadingDeadline deadline) => Task.Run(async () =>
    {
        var cfg = instance.ReadingSquadConfig;
        if (!cfg.IsConfigured())
            return;

        try
        {
            var reports = instance.Database
                .Select<ReadingReport>()
                .ToList();

            var thisWeekReports = reports
                .Where(x => x.DeadlineKey == deadline.Key)
                .ToList();

            var mostReports = reports
                .Where(x => x.DeadlineKey != deadline?.Key)
                .GroupBy(x => x.DeadlineKey)
                .Select(x => x.Count())
                .Max();

            var messages = new List<string> { $"Anyone who has posted a report since the previous deadline will keep the <@&{cfg.RoleDiscordId!.Value}> role for another week.\n" };

            if (thisWeekReports.Count > 0)
            {
                messages.Add("New reports this week: ");

                var sb = new StringBuilder();
                foreach (var report in thisWeekReports)
                    sb.Append($"<@{report.UserDiscordId}>　");

                messages.Add(sb.ToString());

                if (thisWeekReports.Count > mostReports)
                    messages.Add($"{thisWeekReports.Count} is our new record for reports in a single week! 🎉 (up from {mostReports})");
            }

            var embed = new EmbedBuilder()
                .WithColor(new Color(0x0CCDD3))
                .WithTitle($"It is now <t:{deadline.DeadlineInstant.ToUnixTimeSeconds()}>, and a new week has begun.")
                .WithFooter("Make sure to post another report before the next deadline!\n")
                .WithDescription(string.Join("\n", messages));

            if (await discord.GetChannelAsync(cfg.ChannelDiscordId!.Value) is not ITextChannel channel)
                throw new Exception("Failed to get report channel");

            await channel.SendMessageAsync(embed: embed.Build());
            await ClearLapsedUsers(instance, cfg, thisWeekReports);
            await EstablishNextDeadline(instance, cfg);
        }
        catch (Exception e)
        {
            logger.LogError("Error handling deadline timer", e);
            throw;
        }
    });

    public Embed GenerateConfigEmbed(Config cfg)
    {
        return new EmbedBuilder()
            .WithColor(coolBlue)
            .WithTitle("Reading squad configuration")
            .AddField("Channel", $"<#{cfg.ChannelDiscordId}>")
            .AddField("Role", $"<@&{cfg.RoleDiscordId}>")
            .AddField("Day", $"{cfg.DayOfWeek}", inline: true)
            .AddField("Time", $"{cfg.TimeOfDay}", inline: true)
            .AddField("TZ", $"{cfg.SchedulingRelativeToTz}", inline: true)
            .AddField("Enabled", $"{cfg.Enabled}")
            .WithDescription($"Next deadline is <t:{GenerateDeadlineInstants(cfg).First().ToUnixTimeSeconds()}>")
            .Build();
    }

    private async Task ClearLapsedUsers(Instance instance, Config cfg, IEnumerable<ReadingReport> reportsThisWeek)
    {
        var guild = discord.GetGuild(instance.Id);
        var role = guild.GetRole(cfg.RoleDiscordId!.Value);
        if (role == null)
        {
            logger.LogError("Configured role is missing");
            return;
        }

        var approvedMembers = reportsThisWeek
            .Select(x => x.UserDiscordId)
            .ToHashSet();

        await guild.DownloadUsersAsync();

        foreach (var member in role.Members.Where(x => !approvedMembers.Contains(x.Id)))
        {
            logger.LogInformation($"Report expired (user: {member.Id})");

            try
            {
                await member.RemoveRoleAsync(role, new RequestOptions { AuditLogReason = "Reading report expired" });
            }
            catch (Exception e)
            {
                logger.LogError("Failed to remove role", e);
            }
        }
    }

    public async Task EstablishNextDeadline(Instance instance, Config cfg)
    {
        var next = GenerateDeadlineInstants(cfg)
            .First();

        instance.Database.Insert(new ReadingDeadline { DeadlineInstant = next });
    }

    private IEnumerable<Instant> GenerateDeadlineInstants(Config cfg, LocalDate? start = null)
    {
        var tz = timezoneProvider.Tzdb[cfg.SchedulingRelativeToTz!];
        var clock = SystemClock.Instance.InZone(tz);

        var isoDay = cfg.DayOfWeek!.Value.ToIsoDayOfWeek();
        var getNextDay = DateAdjusters.NextOrSame(isoDay);
        var time = TimeOnly.Parse(cfg.TimeOfDay!).ToLocalTime();

        var nextDay = start ?? clock.GetCurrentDate();
        while (true)
        {
            var testDay = getNextDay(nextDay);
            nextDay = testDay.PlusDays(1);

            var result = (testDay + time)
                .InZoneLeniently(tz)
                .ToInstant();

            if (result >= clock.GetCurrentInstant() || start != null)
                yield return result;
        }
    }

    public ReadingSquad(DiscordSocketClient discord, TimezoneProvider timezoneProvider, ILogger<ReadingSquad> logger)
    {
        this.discord = discord;
        this.timezoneProvider = timezoneProvider;
        this.logger = logger;
    }

    private readonly DiscordSocketClient discord;
    private readonly TimezoneProvider timezoneProvider;
    private readonly ILogger<ReadingSquad> logger;
    private readonly Color coolBlue = new(0x0CCDD3);
}
