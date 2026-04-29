using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CommunicationService.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "chat_rooms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsTemporary = table.Column<bool>(type: "bit", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsExpired = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chat_rooms", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "chat_participants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChatRoomId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JoinedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chat_participants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_chat_participants_chat_rooms_ChatRoomId",
                        column: x => x.ChatRoomId,
                        principalTable: "chat_rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChatRoomId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SenderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", maxLength: 4096, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_messages_chat_rooms_ChatRoomId",
                        column: x => x.ChatRoomId,
                        principalTable: "chat_rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "message_status",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecipientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_message_status", x => x.Id);
                    table.ForeignKey(
                        name: "FK_message_status_messages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_chat_participants_ChatRoomId_UserId",
                table: "chat_participants",
                columns: new[] { "ChatRoomId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_chat_participants_UserId",
                table: "chat_participants",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_chat_rooms_ExpiresAt",
                table: "chat_rooms",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_chat_rooms_IsExpired",
                table: "chat_rooms",
                column: "IsExpired");

            migrationBuilder.CreateIndex(
                name: "IX_message_status_MessageId_RecipientId",
                table: "message_status",
                columns: new[] { "MessageId", "RecipientId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_message_status_RecipientId",
                table: "message_status",
                column: "RecipientId");

            migrationBuilder.CreateIndex(
                name: "IX_message_status_RecipientId_Status",
                table: "message_status",
                columns: new[] { "RecipientId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_messages_ChatRoomId_CreatedAt",
                table: "messages",
                columns: new[] { "ChatRoomId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_messages_SenderId",
                table: "messages",
                column: "SenderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "chat_participants");

            migrationBuilder.DropTable(
                name: "message_status");

            migrationBuilder.DropTable(
                name: "messages");

            migrationBuilder.DropTable(
                name: "chat_rooms");
        }
    }
}
