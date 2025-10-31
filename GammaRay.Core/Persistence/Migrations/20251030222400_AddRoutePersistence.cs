using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GammaRay.Core.Migrations
{
	/// <inheritdoc />
	public partial class AddRoutePersistence : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.CreateTable(
				name: "Routes",
				columns: table => new
				{
					SiteDomain = table.Column<string>(type: "TEXT", nullable: false),
					ProfileName = table.Column<string>(type: "TEXT", nullable: false),
					ValidUntil = table.Column<DateTime>(type: "TEXT", nullable: false),
					ConfigurationName = table.Column<string>(type: "TEXT", nullable: false)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_Routes", x => new { x.SiteDomain, x.ProfileName });
				});
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropTable(
				name: "Routes");
		}
	}
}
