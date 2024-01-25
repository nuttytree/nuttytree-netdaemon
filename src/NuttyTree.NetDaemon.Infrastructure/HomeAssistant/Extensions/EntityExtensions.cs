using NetDaemon.HassModel.Entities;
using NuttyTree.NetDaemon.Infrastructure.HomeAssistant;

namespace NuttyTree.NetDaemon.Infrastructure.Extensions;

public static class EntityExtensions
{
    public static ServiceTarget ToServiceTarget(this Entity entity)
    {
        _ = entity ?? throw new ArgumentNullException(nameof(entity));

        return new ServiceTarget { EntityIds = new[] { entity.EntityId } };
    }

    public static void Increase(this CounterEntity entity, long increaseBy)
    {
        _ = entity ?? throw new ArgumentNullException(nameof(entity));

        entity.SetValue((entity.EntityState?.AsInt() ?? 0) + increaseBy);
    }
}
