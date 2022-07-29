using Discord.Interactions;
using Discord.WebSocket;
using Shisho.Utility;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Shisho.Modules;

[AutoDiscoverSingletonService, ForceInitialization]
public class CommandHandler
{
    private readonly InteractionService _commands;
    private readonly DiscordSocketClient _discord;
    private readonly ILogger<CommandHandler> logger;
    private readonly IServiceProvider _services;

    public CommandHandler(InteractionService commands, DiscordSocketClient discord, ILogger<CommandHandler> logger, IServiceProvider services)
    {
        _commands = commands;
        _discord = discord;
        this.logger = logger;
        _services = services;
    }

    public async Task Initialize()
    {
        try
        {
            await _commands.AddModulesAsync(Assembly.GetExecutingAssembly(), _services);
            _discord.InteractionCreated += InteractionCreated;
            _discord.ButtonExecuted += ButtonExecuted;
            _discord.Ready += Ready;
            _commands.SlashCommandExecuted += _commands_SlashCommandExecuted;
            _commands.AutocompleteHandlerExecuted += _commands_AutocompleteHandlerExecuted;
            _commands.InteractionExecuted += _commands_InteractionExecuted;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error initializing command handler");
            throw;
        }
    }

    private async Task _commands_InteractionExecuted(ICommandInfo arg1, Discord.IInteractionContext arg2, IResult arg3)
    {
        if (arg3 is PreconditionResult result)
        {
            await arg2.Interaction.RespondAsync(result.ErrorReason);
        }
    }

    private Task _commands_AutocompleteHandlerExecuted(IAutocompleteHandler arg1, Discord.IInteractionContext arg2, IResult arg3)
    {
        return Task.CompletedTask;
    }

    private Task _commands_SlashCommandExecuted(SlashCommandInfo arg1, Discord.IInteractionContext arg2, IResult arg3)
    {
        return Task.CompletedTask;
    }

    // Generic variants of interaction contexts can be used to create interaction specific modules, but you need to make sure that the destination command resides in a module
    // with the matching context type. See -> ComponentOnlyModule
    private async Task ButtonExecuted(SocketMessageComponent arg)
    {
        var ctx = new SocketInteractionContext<SocketMessageComponent>(_discord, arg);
        await _commands.ExecuteCommandAsync(ctx, _services);
    }

    private async Task Ready()
    {
        await RegisterCommands();
        _discord.Ready -= Ready;
    }

    private Task InteractionCreated(SocketInteraction arg)
    {
        try
        {
            var ctx = new SocketInteractionContext(_discord, arg);
            return _commands.ExecuteCommandAsync(ctx, _services);
        }
        catch (Exception)
        {
            throw;
        }
    }

    private async Task RegisterCommands()
    {
        try
        {
            foreach (var guild in _discord.Guilds)
                await _commands.RegisterCommandsToGuildAsync(guild.Id, deleteMissing: true);
        }
        catch (Exception)
        {
            throw;
        }
    }
}
