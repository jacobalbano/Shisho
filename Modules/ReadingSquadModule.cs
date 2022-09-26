using Discord;
using Discord.Interactions;
using Shisho.Models;
using Shisho.TypeConverters;
using NodaTime;
using System.Text;
using Shisho.Services;
using Discord.Interactions.Builders;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Diagnostics;
using Shisho.Utility;
using System.Reflection;

namespace Shisho.Modules;

[RequireContext(ContextType.Guild)]
[Group("readingsquad", "Reading squad commands")]
public class ReadingSquadModule : InteractionModuleBase<SocketInteractionContext>
{
    public class PublicModule : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("stats", "Show a user's participation stats", runMode: RunMode.Async)]
        public async Task Stats(IUser? user = null)
        {
            var instance = Context.GetInstance();
            var cfg = instance.ReadingSquadConfig;
            if (!cfg.IsConfigured())
            {
                await RespondAsync("Reading squad is not configured!");
                return;
            }

            await DeferAsync();
            var stats = readingSquad.GetUserParticipationStats(instance, user ??= Context.User);

            if (stats.TotalReports == 0)
            {
                await FollowupAsync("User has not posted a report yet");
                return;
            }

            var builder = new EmbedBuilder()
                .WithAuthor(new EmbedAuthorBuilder()
                    .WithName(user.Username)
                    .WithIconUrl(user.GetAvatarUrl()))
                .AddField("First report", $"<t:{stats.FirstReport.ReportMessageInstant.ToUnixTimeSeconds()}:d>")
                .AddField("Reports", stats.TotalReports)
                .AddField("Best streak", stats.BestStreak, inline: true)
                .AddField("Current streak", stats.CurrentStreak, inline: true)
                .AddField("Consistency", $"{stats.Consistency}%")
                ;

            if (stats.RoleExpires > SystemClock.Instance.GetCurrentInstant())
                builder.AddField("Role expires", $"<t:{stats.RoleExpires.ToUnixTimeSeconds()}>");

            await FollowupAsync(embed: builder.Build());
        }

#if DEBUG
        [SlashCommand("think", "just think for a while", runMode: RunMode.Async)]
        public async Task Think()
        {
            await DeferAsync();
            await Task.Delay(30_000);
            await FollowupAsync("Done thinking");
        }
#endif


        public PublicModule(ReadingSquad readingSquad)
        {
            this.readingSquad = readingSquad;
        }

        private readonly ReadingSquad readingSquad;
    }

    [RequireOwner]
    public class AdminModule : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("configure", "Set schedule")]
        public async Task Configure(
        [Summary(description: "Channel to post updates in")] ITextChannel channel,
        [Summary(description: "Role to apply")] IRole role,
        [Summary(description: "Time of day for the reset, e.g. 8:00pm")] string time,
        [Summary(description: "Day on which to trigger the reset each week")] DayOfWeek day,
        [Summary(description: "Timezone to schedule the reset with"), Autocomplete(typeof(TimezoneAutoComplete))] string timezone
    )
        {
            var instance = Context.GetInstance();
            var cfg = instance.ReadingSquadConfig;
            cfg.ChannelDiscordId = channel.Id;
            cfg.RoleDiscordId = role.Id;
            cfg.TimeOfDay = time;
            cfg.DayOfWeek = day;
            cfg.SchedulingRelativeToTz = timezone;

            await readingSquad.EstablishNextDeadline(instance, cfg);
            await ShowConfig();
        }

        [SlashCommand("show-config", "Display current reading squad config")]
        public async Task ShowConfig()
        {
            var instance = Context.GetInstance();
            var cfg = instance.ReadingSquadConfig;
            if (!cfg.IsConfigured())
            {
                await RespondAsync("Use `/configure` to initialize configuration");
                return;
            }

            await RespondAsync(embed: readingSquad.GenerateConfigEmbed(cfg));
        }

        [SlashCommand("simulate-reset", "Clear roles and post the reset message as if the deadline had passed", runMode: RunMode.Async)]
        public async Task SimulateReset()
        {
            var instance = Context.GetInstance();
            var cfg = instance.ReadingSquadConfig;
            if (!cfg.IsConfigured())
            {
                await RespondAsync("Use `/configure` to initialize configuration");
                return;
            }

            await RespondAsync("Simulating reset");
            await readingSquad.HandleDeadline(instance, instance.NextDeadline!);
        }

        [SlashCommand("export-history", "Export a file containing all report messages to date", runMode: RunMode.Async)]
        public async Task ExportHistory()
        {
            var instance = Context.GetInstance();
            var cfg = instance.ReadingSquadConfig;
            if (!cfg.IsConfigured())
            {
                await RespondAsync("Use `/configure` to initialize configuration");
                return;
            }

            await RespondAsync("Generating export, please wait");

            try
            {
                var export = await readingSquad.ExportHistory(cfg);
                var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(export, new JsonSerializerOptions { WriteIndented = true }));
                using var stream = new MemoryStream(bytes);
                await Context.Channel.SendFileAsync(stream, "export.txt", $"{Context.User.Mention} Export completed");
            }
            catch (Exception e)
            {
                await ReplyAsync($"Error exporting: { e.Message }");
                return;
            }
        }

        [SlashCommand("import-history", "Insert all historical messages, assigning them to deadlines according to the current configuration", runMode: RunMode.Async)]
        public async Task ImportHistory()
        {
            var instance = Context.GetInstance();
            var cfg = instance.ReadingSquadConfig;
            if (!cfg.IsConfigured())
            {
                await RespondAsync("Use `/configure` to initialize configuration");
                return;
            }

            await RespondAsync("Performing initial export, please wait");

            try
            {
                var export = await readingSquad.ExportHistory(cfg);
                await ReplyAsync($"Export completed; beginning import");
                var nextDeadline = instance.NextDeadline;

                int totalCount = 0;
                foreach (var item in export.Items)
                {
                    if (nextDeadline != null && nextDeadline.DeadlineInstant - item.Deadline.DeadlineInstant < Duration.FromDays(7))
                    {
                        await ReplyAsync($"Skipping import for {item.Reports.Count} reports that would count towards the upcoming deadline; please make sure to approve them manually");
                        continue;
                    }
                    else
                    {
                        instance.Database.Insert(item.Deadline);
                        foreach (var report in item.Reports)
                        {
                            instance.Database.Insert(report);
                            totalCount++;
                        }
                    }
                }

                await ReplyAsync($"Imported {totalCount} reports over {export.Items.Count} deadline periods");
            }
            catch (Exception e)
            {
                await ReplyAsync($"Error exporting: { e.Message }");
                return;
            }
        }

        [SlashCommand("set-enabled", "Enable or disable report approval and deadline polling")]
        public async Task SetEnabled(bool enabled)
        {
            var instance = Context.GetInstance();
            var cfg = instance.ReadingSquadConfig;
            cfg.Enabled = enabled;

            await RespondAsync(enabled ? "Enabled" : "Disabled");
        }

        [SlashCommand("run-diagnostics", "Run various diagnostics to see how the bot can be expected to perform", runMode: RunMode.Async)]
        public async Task RunDiagnostics()
        {
            await DeferAsync();

            var builder = new EmbedBuilder();
            var timer = Stopwatch.StartNew();
            await Context.Guild.DownloadUsersAsync();
            timer.Stop();

            var proc = Process.GetCurrentProcess();
            var nativeMem = proc.PrivateMemorySize64;
            var gcMem = GC.GetTotalMemory(forceFullCollection: false);
            static string format(long l) => $"{BytesFormatter.ToSize(l, BytesFormatter.SizeUnits.MB)}mb";

            var gitHash = Assembly
                .GetEntryAssembly()?
                .GetCustomAttributes<AssemblyMetadataAttribute>()
                .FirstOrDefault(attr => attr.Key == "GitHash")?.Value;

            builder.AddField("Time to download users", Duration.FromMilliseconds(timer.ElapsedMilliseconds).ToString());
            builder.AddField("Native memory usage", format(nativeMem), inline: true);
            builder.AddField("GC memory usage", format(gcMem), inline: true);
            builder.AddField("Total memory usage", format(nativeMem + gcMem));

            if (gitHash != null)
                builder.AddField("Commit", gitHash);

            builder.WithFooter($"Bot uptime: {Duration.FromTimeSpan(DateTime.UtcNow - proc.StartTime.ToUniversalTime())}");

            await FollowupAsync(embed: builder.Build());
        }


        public AdminModule( ReadingSquad readingSquad)
        {
            this.readingSquad = readingSquad;
        }

        private readonly ReadingSquad readingSquad;
    }

    public ReadingSquadModule(DiscordSocketClient discord, Orchestrator orchestrator, ReadingSquad readingSquad)
    {
        this.discord = discord;
        this.readingSquad = readingSquad;
        this.orchestrator = orchestrator;
    }

    public override void Construct(ModuleBuilder builder, InteractionService commandService)
    {
        base.Construct(builder, commandService);
        orchestrator.OnTick += Orchestrator_OnTick;
    }

    private async Task Orchestrator_OnTick(object sender, EventArgs e)
    {
        foreach (var guild in discord.Guilds)
            await readingSquad.OnOrchestratorTick(Instance.Get(guild.Id));
    }

    private readonly DiscordSocketClient discord;
    private readonly ReadingSquad readingSquad;
    private readonly Orchestrator orchestrator;
}