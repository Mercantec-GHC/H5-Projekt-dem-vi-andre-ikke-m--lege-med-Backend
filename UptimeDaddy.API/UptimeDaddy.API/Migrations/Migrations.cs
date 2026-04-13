using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UptimeDaddy.API.Migrations
{
    public partial class AddWebsiteAndMeasurementIndexes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_websites_user_id",
                table: "websites",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_measurements_website_id_created_at",
                table: "measurements",
                columns: new[] { "website_id", "created_at" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_websites_user_id",
                table: "websites");

            migrationBuilder.DropIndex(
                name: "ix_measurements_website_id_created_at",
                table: "measurements");
        }
    }
}