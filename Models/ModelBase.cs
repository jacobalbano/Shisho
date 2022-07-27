namespace Shisho.Models;

public abstract record class ModelBase
{
    public Guid Key { get; init; } = Guid.NewGuid();
}
