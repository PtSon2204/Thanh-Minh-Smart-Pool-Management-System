using DotNetEnv;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SmartPool.Infrastructure.Persistence.DbContext;

namespace SmartPool.API;

public sealed class SmartPoolDesignTimeDbContextFactory : IDesignTimeDbContextFactory<SmartPoolDbContext>
{
    public SmartPoolDbContext CreateDbContext(string[] args)
    {
        var envPath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
        if (!File.Exists(envPath)) envPath = Path.Combine(AppContext.BaseDirectory, ".env");
        if (File.Exists(envPath)) Env.Load(envPath);

        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();
        var connection = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required for EF tooling.");
        var options = new DbContextOptionsBuilder<SmartPoolDbContext>()
            .UseNpgsql(connection, postgres => postgres.MigrationsAssembly(typeof(SmartPoolDbContext).Assembly.FullName))
            .Options;
        return new SmartPoolDbContext(options);
    }
}
