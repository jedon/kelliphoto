using KelliPhoto.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KelliPhoto.Web.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260809170213_AddTagNameNormalized")]
    public partial class AddTagNameNormalized : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tags_Name",
                table: "Tags");

            migrationBuilder.AddColumn<string>(
                name: "NameNormalized",
                table: "Tags",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            // Backfill before unique index so existing rows are not all "".
            migrationBuilder.Sql(
                """
                UPDATE "Tags"
                SET "NameNormalized" = lower(btrim("Name"));
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Tags_NameNormalized",
                table: "Tags",
                column: "NameNormalized",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tags_NameNormalized",
                table: "Tags");

            migrationBuilder.DropColumn(
                name: "NameNormalized",
                table: "Tags");

            migrationBuilder.CreateIndex(
                name: "IX_Tags_Name",
                table: "Tags",
                column: "Name",
                unique: true);
        }
    }
}
