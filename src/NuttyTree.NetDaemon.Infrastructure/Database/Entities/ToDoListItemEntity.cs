using System.ComponentModel.DataAnnotations;

namespace NuttyTree.NetDaemon.Infrastructure.Database.Entities;

public sealed class ToDoListItemEntity
{
    public int Id { get; set; }

    public Guid Uid { get; set; }

    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public int MinutesEarned { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public Guid? ReviewUid { get; set; }
}
