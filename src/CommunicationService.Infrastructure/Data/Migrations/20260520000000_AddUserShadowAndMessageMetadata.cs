using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CommunicationService.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserShadowAndMessageMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Metadata",
                table: "messages",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "eventstore_checkpoints",
                columns: table => new
                {
                    Name = table.Column<string>(type: "nvarchar(128)", nullable: false),
                    LastEventNumber = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eventstore_checkpoints", x => x.Name);
                });

            migrationBuilder.CreateTable(
                name: "users_shadow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(256)", nullable: true),
                    AvatarUrl = table.Column<string>(type: "nvarchar(1024)", nullable: true),
                    FirebaseUid = table.Column<string>(type: "nvarchar(128)", nullable: true),
                    Role = table.Column<string>(type: "nvarchar(64)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(64)", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users_shadow", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_users_shadow_Email",
                table: "users_shadow",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_users_shadow_FirebaseUid",
                table: "users_shadow",
                column: "FirebaseUid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "eventstore_checkpoints");

            migrationBuilder.DropTable(
                name: "users_shadow");

            migrationBuilder.DropColumn(
                name: "Metadata",
                table: "messages");
        }
    }
}
