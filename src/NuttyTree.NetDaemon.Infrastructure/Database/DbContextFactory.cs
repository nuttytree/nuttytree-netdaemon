using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace NuttyTree.NetDaemon.Infrastructure.Database;

public sealed class MessagingContextFactory : IDesignTimeDbContextFactory<NuttyDbContext>
{
    public NuttyDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<NuttyDbContext>();
        optionsBuilder.UseSqlServer(
            "server=(localdb)\\mssqllocaldb;database=NetDaemon;trusted_connection=yes",
            opt => opt.MigrationsAssembly(typeof(NuttyDbContext).GetTypeInfo().Assembly.GetName().Name));

        return new NuttyDbContext(optionsBuilder.Options);
    }
}
