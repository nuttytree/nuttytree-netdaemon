using NetDaemon.HassModel.Entities;

namespace NuttyTree.NetDaemon.Infrastructure.Extensions;

public static class EntityExtensions
{
    public static ServiceTarget ToServiceTarget(this Entity entity)
        => new ServiceTarget { EntityIds = new[] { entity.EntityId } };
}
