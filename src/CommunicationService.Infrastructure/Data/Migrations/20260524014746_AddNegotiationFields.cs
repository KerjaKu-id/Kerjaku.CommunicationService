using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CommunicationService.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNegotiationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AgreedPrice",
                table: "chat_rooms",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsNegotiationActive",
                table: "chat_rooms",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "NegotiationStatus",
                table: "chat_rooms",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "None");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AgreedPrice",
                table: "chat_rooms");

            migrationBuilder.DropColumn(
                name: "IsNegotiationActive",
                table: "chat_rooms");

            migrationBuilder.DropColumn(
                name: "NegotiationStatus",
                table: "chat_rooms");
        }
    }
}
