using Discord.WebSocket;
using Shisho.Models;
using Shisho.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shisho;

[AutoDiscoverSingletonService, ForceInitialization]
public class Orchestrator
{
    public event AsyncEventHandler OnTick = (_, _) => Task.CompletedTask;

    private void OnTickInterval(object? state)
    {
        var handler = OnTick;
        if (handler != null)
        {
            Task.Run(async () =>
            {
                await Task.WhenAll(handler.GetInvocationList()
                    .Cast<AsyncEventHandler>()
                    .Select(x => x.Invoke(this, EventArgs.Empty))
                );

                SetNextTick();
            });
        }
        else
        {
            SetNextTick();
        }
    }

    private void SetNextTick()
    {
        tick.Change(Instance.BotConfig.TickMilliseconds, Timeout.Infinite);
    }

    public Orchestrator(DiscordSocketClient discord)
    {
        this.discord = discord;
        discord.Ready += Discord_Ready;
        tick = new Timer(OnTickInterval, null, Timeout.Infinite, Timeout.Infinite);
    }

    private Task Discord_Ready()
    {
        SetNextTick();
        discord.Ready -= Discord_Ready;
        return Task.CompletedTask;
    }

    public delegate Task AsyncEventHandler(object sender, EventArgs e);
    private readonly Timer tick;
    private readonly DiscordSocketClient discord;
}
