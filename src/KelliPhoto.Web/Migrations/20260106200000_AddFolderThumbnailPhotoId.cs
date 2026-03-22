using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KelliPhoto.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddFolderThumbnailPhotoId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ThumbnailPhotoId",
                table: "Folders",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Folders_ThumbnailPhotoId",
                table: "Folders",
                column: "ThumbnailPhotoId");

            migrationBuilder.AddForeignKey(
                name: "FK_Folders_Photos_ThumbnailPhotoId",
                table: "Folders",
                column: "ThumbnailPhotoId",
                principalTable: "Photos",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Folders_Photos_ThumbnailPhotoId",
                table: "Folders");

            migrationBuilder.DropIndex(
                name: "IX_Folders_ThumbnailPhotoId",
                table: "Folders");

            migrationBuilder.DropColumn(
                name: "ThumbnailPhotoId",
                table: "Folders");
        }
    }
}
