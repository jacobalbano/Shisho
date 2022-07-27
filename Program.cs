using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Microsoft.Extensions.Logging;
using Shisho.Utility;
using Shisho.Modules;

namespace Shisho;

public class Program
{
    static void Main(string[] args)
    {
        if (args.Any())
            Directory.SetCurrentDirectory(args[0]);

        RunAsync().GetAwaiter().GetResult();
    }

    static async Task RunAsync()
    {
        using var services = ConfigureServices();
        ForceInitializationAttribute.DiscoverAndInitialize(services);

        var client = services.GetRequiredService<DiscordSocketClient>();
        var handler = services.GetRequiredService<CommandHandler>();

        await handler.Initialize();
        await client.LoginAsync(TokenType.Bot, Instance.BotConfig.Token);
        await client.StartAsync();

        //  load everything upfront
        foreach (var guild in client.Guilds)
            Instance.Get(guild.Id);

        await Task.Delay(Timeout.Infinite);
    }

    static ServiceProvider ConfigureServices() =>
        new ServiceCollection()
        .DiscoverTaggedSingletons()
        .AddSingleton<DiscordSocketClient>()
        .AddSingleton(new DiscordSocketConfig { LogGatewayIntentWarnings = false })
        .AddSingleton<InteractionService>()
        .AddLogging(x => ConfigureLogging(x))
        .BuildServiceProvider();

    private static ILoggingBuilder ConfigureLogging(ILoggingBuilder x)
    {
        return x.AddSerilog(new LoggerConfiguration()
            .WriteTo.File("logs/shisho.log",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: null,
                shared: true
            )
            .WriteTo.Console()
            .CreateLogger());
    }
}
