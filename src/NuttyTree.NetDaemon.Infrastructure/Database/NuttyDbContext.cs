using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NuttyTree.NetDaemon.Infrastructure.Database.Entities;

namespace NuttyTree.NetDaemon.Infrastructure.Database;

public class NuttyDbContext : DbContext
{
    public NuttyDbContext(DbContextOptions<NuttyDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AppointmentEntity> Appointments { get; set; } = null!;

    public virtual DbSet<AppointmentReminderEntity> AppointmentReminders { get; set; } = null!;

    public virtual DbSet<ToDoListItemEntity> ToDoListItems { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppointmentEntity>(entity =>
        {
            entity.HasMany(e => e.Reminders)
                .WithOne(r => r.Appointment)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ToDoListItemEntity>(entity =>
        {
            entity.HasAlternateKey(e => e.Uid);
        });
    }
}
