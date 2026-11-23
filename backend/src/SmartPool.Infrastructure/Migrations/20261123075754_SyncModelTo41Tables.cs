using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartPool.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SyncModelTo41Tables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Model snapshot synchronization:
            // All physical schema changes (dropping the 7 legacy tables and creating staff_schedule_changes)
            // were already executed by SimplifyStaffPersistence (20261113115444).
            // This migration exists purely to synchronize SmartPoolDbContextModelSnapshot with the 41 tables
            // so EF tools and diagram generators correctly reflect the live database schema.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
