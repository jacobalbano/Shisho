using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Shisho.Utility.Persistence;

public abstract class PersistableConfig<TSelf> : IPersistable
    where TSelf : PersistableConfig<TSelf>
{
    public void LoadPersistentData(string fromDirectory)
    {
        var path = MakePath(fromDirectory);
        if (!File.Exists(path)) return;

        var backingFields = JsonSerializer.Deserialize<TSelf>(File.ReadAllText(path));
        foreach (var accessor in accessors)
            accessor.SetValue(this, accessor.GetValue(backingFields!));

        dirty = false;
    }

    public void Persist(string toDirectory)
    {
        if (!dirty) return;
        File.WriteAllText(MakePath(toDirectory), JsonSerializer.Serialize(this, typeof(TSelf)));
        dirty = false;
    }

    private string MakePath(string dir)
    {
        var t = GetType();
        if (t.DeclaringType != null)
            return Path.Combine(dir, $"{t.DeclaringType.Name}.{t.Name}.json");
        else return Path.Combine(dir, $"{t.Name}.json");
    }

    protected T? Get<T>([CallerMemberName] string propertyName = "")
    {
        if (values.TryGetValue(propertyName, out var value))
            return (T)value;

        return default;
    }

    protected void Set<T>(T? value, [CallerMemberName] string propertyName = "")
    {
        values[propertyName] = value!;
        dirty = true;
    }

    private bool dirty;
    private readonly Dictionary<string, object> values = new();
    private static readonly IReadOnlyList<IFastAccessor<object>> accessors = typeof(TSelf)
        .GetProperties()
        .Select(x => new FastAccessor<object>(x))
        .ToList();
}
