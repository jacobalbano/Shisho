using Discord.Interactions;
using Shisho.Models;
using Shisho.Modules;
using Shisho.Services;
using Shisho.Utility;
using Shisho.Utility.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;

namespace Shisho;

public class Instance
{
    public ulong Id { get; init; }

    public static ConfigFile BotConfig { get; } = ConfigFile.Prepare();

    public ReadingSquad.Config ReadingSquadConfig { get; } = new();

    public IEnumerable<ReadingReport> ReportsThisWeek => Database.Select<ReadingReport>()
        .Where(x => x.DeadlineKey == NextDeadline.Key);

    public IEnumerable<ReadingDeadline> Deadlines => Database.Select<ReadingDeadline>();

    public ReadingDeadline? NextDeadline => Deadlines
        .OrderBy(x => x.DeadlineInstant)
        .LastOrDefault();

    public Database Database { get; } = new("ReadingSquad");

    public static Instance Get(ulong id)
    {
        if (Instances.TryGetValue(id, out var instance))
            return instance;

        return Establish(id);
    }

    public static void PersistAll()
    {
        foreach (var (id, instance) in Instances)
            instance.Persist(id.ToString());
    }

    #region implementation
    private Instance() { }

    private static Instance Establish(ulong id)
    {
        if (Directory.Exists(id.ToString()))
            return Load(id);

        Directory.CreateDirectory(id.ToString());
        return Instances[id] = new Instance { Id = id };
    }
    
    private static Instance Load(ulong id)
    {
        var result = new Instance { Id = id };
        result.LoadPersistentData(id.ToString());
        Instances.Add(id, result);
        return result;
    }

    public void Persist(string toDirectory)
    {
        foreach (var getChild in Persistables)
            getChild(this).Persist(toDirectory);
    }

    public void LoadPersistentData(string fromDirectory)
    {
        foreach (var getChild in Persistables)
            getChild(this).LoadPersistentData(fromDirectory);
    }

    private static readonly Dictionary<ulong, Instance> Instances = new();
    private static readonly IReadOnlyList<GetPersistable> Persistables = DiscoverPersistables();
    private static IReadOnlyList<GetPersistable> DiscoverPersistables()
    {
        return typeof(Instance)
            .GetProperties(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
            .Where(x => typeof(IPersistable).IsAssignableFrom(x.PropertyType))
            .Select(x =>
            {
                var param = Expression.Parameter(typeof(Instance));
                return Expression.Lambda<GetPersistable>(
                    Expression.TypeAs(Expression.Property(param, x), typeof(IPersistable)),
                    param
                ).Compile();
            }).ToList();
    }

    private delegate IPersistable GetPersistable(Instance instance);
    #endregion
}

public static class InstanceExt
{
    public static Instance GetInstance(this SocketInteractionContext context)
    {
        return Instance.Get(context.Guild.Id);
    }
}
