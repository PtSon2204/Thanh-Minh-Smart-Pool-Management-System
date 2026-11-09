using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartPool.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SyncSafetyMonitoringModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {            // Schema giam sat an toan da duoc tao boi migration 20261102181921_AddSafetyMonitoring.
            // Cac migration cua nhanh main (20261103 - 20261105) da ghi de ModelSnapshot nen EF
            // tuong cac cot nay chua ton tai. Migration nay chi dong bo lai snapshot voi
            // model hien tai; DB da khop nen khong phat sinh thay doi vat ly.
}

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {            // Schema giam sat an toan da duoc tao boi migration 20261102181921_AddSafetyMonitoring.
            // Cac migration cua nhanh main (20261103 - 20261105) da ghi de ModelSnapshot nen EF
            // tuong cac cot nay chua ton tai. Migration nay chi dong bo lai snapshot voi
            // model hien tai; DB da khop nen khong phat sinh thay doi vat ly.
}
    }
}
