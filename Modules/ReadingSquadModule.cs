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

namespace Shisho.Modules;

[RequireOwner]
[RequireContext(ContextType.Guild)]
[Group("readingsquad", "Reading Squad commands")]
public class ReadingSquadModule : InteractionModuleBase<SocketInteractionContext>
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

    [SlashCommand("simulate-reset", "Clear roles and post the reset message as if the deadline had passed")]
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

    [SlashCommand("export-history", "Export a file containing all report messages to date")]
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

    [SlashCommand("import-history", "Insert all historical messages, assigning them to deadlines according to the current configuration")]
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

    private async Task Discord_ReactionAdded(Cacheable<IUserMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction)
    {
        if (reaction.Channel is not ITextChannel tc)
            return;

        var instance = Instance.Get(tc.GuildId);
        if (channel.Id != instance.ReadingSquadConfig.ChannelDiscordId) return;
        if (reaction.Emote.Name != checkmark.Name) return;

        try
        {
            var msg = await message.GetOrDownloadAsync();
            if (msg.Author is not IGuildUser member)
            {
                if ((member = await tc.Guild.GetUserAsync(msg.Author.Id)) == null)
                {
                    logger.LogError($"Failed to approve report (user: {msg.Author.Id} is not in the server)");
                    return;
                }
            }

            if (await readingSquad.TryApproveUser(instance, member, msg))
                await msg.AddReactionAsync(checkmark);
        }
        catch
        {
            //  either we failed to get the message or the author blocked the bot
            //  in both cases, nothing more to do here
        }
    }

    public ReadingSquadModule(DiscordSocketClient discord, Orchestrator orchestrator, ReadingSquad readingSquad, ILogger<ReadingSquadModule> logger)
    {
        this.discord = discord;
        this.readingSquad = readingSquad;
        this.logger = logger;
        this.orchestrator = orchestrator;
    }

    public override void Construct(ModuleBuilder builder, InteractionService commandService)
    {
        base.Construct(builder, commandService);
        discord.ReactionAdded += Discord_ReactionAdded;
        orchestrator.OnTick += Orchestrator_OnTick;
    }

    private async Task Orchestrator_OnTick(object sender, EventArgs e)
    {
        foreach (var guild in discord.Guilds)
            await readingSquad.OnOrchestratorTick(Instance.Get(guild.Id));
    }

    private readonly DiscordSocketClient discord;
    private readonly ReadingSquad readingSquad;
    private readonly ILogger<ReadingSquadModule> logger;
    private readonly Orchestrator orchestrator;
    private static readonly IEmote checkmark = new Emoji("✅");
}