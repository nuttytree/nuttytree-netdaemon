using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace NuttyTree.NetDaemon.Infrastructure.Database;

public sealed class MessagingContextFactory : IDesignTimeDbContextFactory<NuttyDbContext>
{
    public NuttyDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<NuttyDbContext>();
        optionsBuilder.UseSqlite(
            "Data Source=./Data/netdaemon.db",
            opt => opt.MigrationsAssembly(typeof(NuttyDbContext).GetTypeInfo().Assembly.GetName().Name));

        return new NuttyDbContext(optionsBuilder.Options);
    }
}
