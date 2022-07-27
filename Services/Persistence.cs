using Discord.WebSocket;
using Shisho.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shisho.Services;

[AutoDiscoverSingletonService, ForceInitialization]
public class Persistence
{
    public Persistence(Orchestrator orchestrator)
    {
        orchestrator.OnTick += Orchestrator_OnTick;
    }

    private Task Orchestrator_OnTick(object? sender, EventArgs e)
    {
        return Task.Run(() =>
        {
            Instance.PersistAll();
        });
    }
}
