using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GammaRay.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddNetworkModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Networks",
                columns: table => new
                {
                    Identity = table.Column<string>(type: "TEXT", nullable: false),
                    UsedProfile = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Networks", x => x.Identity);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Networks");
        }
    }
}
