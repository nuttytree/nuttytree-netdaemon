using NetDaemon.HassModel.Entities;
using NuttyTree.NetDaemon.Infrastructure.HomeAssistant;

namespace NuttyTree.NetDaemon.Infrastructure.Extensions;

public static class EntityExtensions
{
    public static ServiceTarget ToServiceTarget(this Entity entity)
        => new ServiceTarget { EntityIds = new[] { entity.EntityId } };

    public static void Increase(this CounterEntity target, long increaseBy)
        => target.SetValue(target.EntityState?.AsInt() ?? 0 + increaseBy);
}
