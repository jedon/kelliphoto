using KelliPhoto.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KelliPhoto.Web.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260809170600_AddPhotoExif")]
    public partial class AddPhotoExif : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PhotoExifs",
                columns: table => new
                {
                    PhotoId = table.Column<int>(type: "integer", nullable: false),
                    DateTaken = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CameraMake = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CameraModel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Lens = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    FocalLength = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Aperture = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ShutterSpeed = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Iso = table.Column<int>(type: "integer", nullable: true),
                    GpsLatitude = table.Column<double>(type: "double precision", nullable: true),
                    GpsLongitude = table.Column<double>(type: "double precision", nullable: true),
                    Artist = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Copyright = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ImageDescription = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ExtraJson = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhotoExifs", x => x.PhotoId);
                    table.ForeignKey(
                        name: "FK_PhotoExifs_Photos_PhotoId",
                        column: x => x.PhotoId,
                        principalTable: "Photos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PhotoExifs");
        }
    }
}
