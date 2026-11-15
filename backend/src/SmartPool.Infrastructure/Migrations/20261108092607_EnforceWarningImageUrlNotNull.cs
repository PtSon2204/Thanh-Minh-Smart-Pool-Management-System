using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartPool.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnforceWarningImageUrlNotNull : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ModelSnapshot đã ghi image_url là NOT NULL, còn cột trong DB vẫn nullable
            // (migration 20261108091536 chỉ kịp thêm DEFAULT ''). Sửa bằng SQL tường minh:
            // lấp những giá trị NULL còn sót rồi siết cột thành NOT NULL.
            migrationBuilder.Sql(@"UPDATE smart_pool.warnings SET image_url = '' WHERE image_url IS NULL;");
            migrationBuilder.Sql(@"ALTER TABLE smart_pool.warnings ALTER COLUMN image_url SET NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"ALTER TABLE smart_pool.warnings ALTER COLUMN image_url DROP NOT NULL;");
        }
    }
}
