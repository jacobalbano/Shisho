using Discord;
using Discord.Interactions;
using Discord.Interactions.Builders;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Shisho.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shisho.Modules
{
    public class ReactionHandlerModule : InteractionModuleBase<SocketInteractionContext>
    {
        private Task Discord_ReactionAdded(Cacheable<IUserMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction) => Task.Run(async () =>
        {
            if (reaction.User.GetValueOrDefault() is IGuildUser bot && bot.IsBot)
                return;

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
        });

        public override void Construct(ModuleBuilder builder, InteractionService commandService)
        {
            base.Construct(builder, commandService);
            discord.ReactionAdded += Discord_ReactionAdded;
        }

        public ReactionHandlerModule(DiscordSocketClient discord, Orchestrator orchestrator, ReadingSquad readingSquad, ILogger<ReactionHandlerModule> logger)
        {
            this.discord = discord;
            this.readingSquad = readingSquad;
            this.logger = logger;
        }

        private readonly DiscordSocketClient discord;
        private readonly ReadingSquad readingSquad;
        private readonly ILogger<ReactionHandlerModule> logger;
        private static readonly IEmote checkmark = new Emoji("✅");
    }
}
