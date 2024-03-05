using NetDaemon.HassModel.Entities;

namespace NuttyTree.NetDaemon.Infrastructure.Extensions;

public static class EntityStateExtensions
{
    public static int? AsInt(this EntityState? entityState)
        => int.TryParse(entityState?.State, out var value) ? value : null;

    public static TEnum? AsEnum<TEnum>(this EntityState? entityState)
        where TEnum : struct
            => Enum.TryParse<TEnum>(entityState?.State, true, out var value) ? value : null;
}
