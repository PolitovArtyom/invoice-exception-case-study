using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvoiceDesk.Core.Storage.Migrations
{
    /// <inheritdoc />
    public partial class AddHumanResolution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Acceptance",
                table: "Revisions",
                type: "TEXT",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "Corrections",
                table: "Revisions",
                type: "TEXT",
                nullable: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Acceptance", table: "Revisions");

            migrationBuilder.DropColumn(name: "Corrections", table: "Revisions");
        }
    }
}
