using KelliPhoto.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KelliPhoto.Web.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260525200000_AddFolderSortOrder")]
    public partial class AddFolderSortOrder : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "Folders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(
                """
                WITH ranked AS (
                    SELECT "Id",
                           ROW_NUMBER() OVER (
                               PARTITION BY "ParentId"
                               ORDER BY "Name"
                           ) - 1 AS rn
                    FROM "Folders"
                )
                UPDATE "Folders" AS f
                SET "SortOrder" = ranked.rn
                FROM ranked
                WHERE f."Id" = ranked."Id";
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "Folders");
        }
    }
}
