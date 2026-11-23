using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvoiceDesk.Core.Storage.Migrations
{
    /// <inheritdoc />
    public partial class DropCorrectionConflictFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "HasCorrectionConflict", table: "Revisions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasCorrectionConflict",
                table: "Revisions",
                type: "INTEGER",
                nullable: false,
                defaultValue: false
            );
        }
    }
}
