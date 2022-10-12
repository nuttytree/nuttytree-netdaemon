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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var dateTimeConverter = new ValueConverter<DateTime, DateTime>(
            v => v.ToUniversalTime(),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        var nullableDateTimeConverter = new ValueConverter<DateTime?, DateTime?>(
            v => v.HasValue ? v.Value.ToUniversalTime() : v,
            v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime))
                {
                    property.SetValueConverter(dateTimeConverter);
                }
                else if (property.ClrType == typeof(DateTime?))
                {
                    property.SetValueConverter(nullableDateTimeConverter);
                }
            }
        }

        modelBuilder.Entity<AppointmentEntity>(entity =>
        {
            entity.HasMany(e => e.Reminders)
                .WithOne(r => r.Appointment)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
