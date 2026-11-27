using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SmartPool.Infrastructure.Persistence.DbContext;

namespace SmartPool.Infrastructure.Migrations;

[DbContext(typeof(SmartPoolDbContext))]
[Migration("20261125120000_EnforceCaseInsensitiveUserEmail")]
public sealed class EnforceCaseInsensitiveUserEmail : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // CREATE UNIQUE INDEX is the final, race-safe check, even if a writer
        // commits after the inventory. Never select or merge conflicting users.
        migrationBuilder.Sql("""
            DO $$
            BEGIN
                IF EXISTS (SELECT 1 FROM smart_pool.users WHERE email IS NOT NULL
                           GROUP BY lower(btrim(email)) HAVING count(*) > 1) THEN
                    RAISE EXCEPTION 'Case-insensitive user email collisions require owner-approved resolution';
                END IF;
            END $$;
            CREATE UNIQUE INDEX users_email_lower_key ON smart_pool.users (lower(btrim(email)));
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("DROP INDEX smart_pool.users_email_lower_key;");
}
