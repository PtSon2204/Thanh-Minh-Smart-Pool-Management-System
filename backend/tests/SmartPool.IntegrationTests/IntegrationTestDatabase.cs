using DotNetEnv;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartPool.Infrastructure.Persistence.DbContext;

namespace SmartPool.IntegrationTests;

internal static class IntegrationTestDatabase
{
    private const string ConnectionVariable = "ConnectionStrings__DefaultConnection";
    private const string EnvironmentPathVariable = "SMARTPOOL_TEST_ENV_PATH";
    private const string BootstrapVariable = "SMARTPOOL_TEST_BOOTSTRAP";
    private static readonly SemaphoreSlim InitializationGate = new(1, 1);
    private static readonly object DataSourceGate = new();
    private static NpgsqlDataSource? dataSource;
    private static string? dataSourceConnectionString;
    private static bool initialized;

    public static async Task<DbContextOptions<SmartPoolDbContext>> CreateOptionsAsync()
    {
        var connectionString = LoadConnectionString();
        ValidateTarget(connectionString);

        var options = new DbContextOptionsBuilder<SmartPoolDbContext>()
            .UseNpgsql(GetDataSource(connectionString))
            .Options;

        if (string.Equals(Environment.GetEnvironmentVariable(BootstrapVariable), "true", StringComparison.OrdinalIgnoreCase))
        {
            await InitializeAsync(options);
        }

        return options;
    }

    private static NpgsqlDataSource GetDataSource(string connectionString)
    {
        lock (DataSourceGate)
        {
            if (dataSource is not null)
            {
                if (!string.Equals(dataSourceConnectionString, connectionString, StringComparison.Ordinal))
                    throw new InvalidOperationException("Integration tests cannot switch databases within one process.");
                return dataSource;
            }

            var builder = new NpgsqlDataSourceBuilder(connectionString);
            builder.EnableDynamicJson();
            dataSource = builder.Build();
            dataSourceConnectionString = connectionString;
            return dataSource;
        }
    }

    private static string LoadConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionVariable);
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            return connectionString;
        }

        var configuredPath = Environment.GetEnvironmentVariable(EnvironmentPathVariable);
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            if (!File.Exists(configuredPath))
            {
                throw new InvalidOperationException($"{EnvironmentPathVariable} does not identify an existing file.");
            }

            Env.Load(configuredPath);
        }
        else
        {
            var discoveredPath = FindEnvironmentFile()
                ?? throw new InvalidOperationException(
                    $"Set {ConnectionVariable}, set {EnvironmentPathVariable}, or provide src/SmartPool.API/.env.");
            Env.Load(discoveredPath);
        }

        return Environment.GetEnvironmentVariable(ConnectionVariable)
            ?? throw new InvalidOperationException($"{ConnectionVariable} is unavailable.");
    }

    private static string? FindEnvironmentFile()
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var directory = new DirectoryInfo(start);
            while (directory is not null)
            {
                var candidate = Path.Combine(directory.FullName, "src", "SmartPool.API", ".env");
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }
        }

        return null;
    }

    private static void ValidateTarget(string connectionString)
    {
        var connection = new NpgsqlConnectionStringBuilder(connectionString);
        var allowedDatabase = string.Equals(connection.Database, "smartpool_dev", StringComparison.Ordinal)
            || string.Equals(connection.Database, "smartpool_test", StringComparison.Ordinal);
        if (!string.Equals(connection.Host, "localhost", StringComparison.OrdinalIgnoreCase)
            || connection.Port != 5432
            || !allowedDatabase)
        {
            throw new InvalidOperationException(
                "Integration test database guard requires localhost:5432/smartpool_dev or localhost:5432/smartpool_test.");
        }
    }

    private static async Task InitializeAsync(DbContextOptions<SmartPoolDbContext> options)
    {
        await InitializationGate.WaitAsync();
        try
        {
            if (initialized)
            {
                return;
            }

            await using var context = new SmartPoolDbContext(options);
            await context.Database.ExecuteSqlRawAsync("""
                CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
                CREATE EXTENSION IF NOT EXISTS pgcrypto;
                """);
            await context.Database.EnsureCreatedAsync();
            await context.Database.ExecuteSqlRawAsync(
                "ALTER TABLE smart_pool.tickets ADD COLUMN IF NOT EXISTS remaining_entries integer NULL");

            foreach (var roleName in new[] { "ADMIN", "STAFF", "CUSTOMER" })
            {
                var roleId = Guid.NewGuid();
                await context.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO smart_pool.roles (id, name, permissions, is_deleted, created_at, updated_at)
                    SELECT {roleId}, {roleName}, '[]'::jsonb, false, now(), now()
                    WHERE NOT EXISTS (
                        SELECT 1 FROM smart_pool.roles
                        WHERE upper(name) = {roleName} AND is_deleted IS NOT TRUE
                    )
                    """);
            }

            var operatorId = Guid.NewGuid();
            await context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO smart_pool.users
                    (id, role_id, username, password_hash, email, phone, status, is_deleted, created_at, updated_at)
                SELECT {operatorId}, role.id, 'ci-test-operator', 'integration-test-only',
                    'ci-test-operator@example.test', '0900000000', 'Active', false, now(), now()
                FROM smart_pool.roles role
                WHERE upper(role.name) = 'ADMIN' AND role.is_deleted IS NOT TRUE
                    AND NOT EXISTS (SELECT 1 FROM smart_pool.users)
                LIMIT 1
                """);

            initialized = true;
        }
        finally
        {
            InitializationGate.Release();
        }
    }
}
