using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KelliPhoto.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddFolderCoverPhotos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FolderCoverPhotos",
                columns: table => new
                {
                    FolderId = table.Column<int>(type: "integer", nullable: false),
                    PhotoId = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FolderCoverPhotos", x => new { x.FolderId, x.PhotoId });
                    table.ForeignKey(
                        name: "FK_FolderCoverPhotos_Folders_FolderId",
                        column: x => x.FolderId,
                        principalTable: "Folders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FolderCoverPhotos_Photos_PhotoId",
                        column: x => x.PhotoId,
                        principalTable: "Photos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FolderCoverPhotos_FolderId_SortOrder",
                table: "FolderCoverPhotos",
                columns: new[] { "FolderId", "SortOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FolderCoverPhotos_PhotoId",
                table: "FolderCoverPhotos",
                column: "PhotoId");

            migrationBuilder.Sql(
                """
                INSERT INTO "FolderCoverPhotos" ("FolderId", "PhotoId", "SortOrder")
                SELECT "Id", "ThumbnailPhotoId", 0
                FROM "Folders"
                WHERE "ThumbnailPhotoId" IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FolderCoverPhotos");
        }
    }
}
