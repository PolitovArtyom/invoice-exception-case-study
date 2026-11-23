using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvoiceDesk.Core.Storage.Migrations
{
    public partial class AddInvoiceImportClassification : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DuplicateAttempt",
                table: "Revisions",
                type: "TEXT",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "OriginalInvoice",
                table: "Revisions",
                type: "TEXT",
                nullable: true
            );
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "DuplicateAttempt", table: "Revisions");

            migrationBuilder.DropColumn(name: "OriginalInvoice", table: "Revisions");
        }
    }
}
