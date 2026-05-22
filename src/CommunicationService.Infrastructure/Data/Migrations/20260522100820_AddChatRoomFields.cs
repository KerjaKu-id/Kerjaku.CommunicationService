using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CommunicationService.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddChatRoomFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "chat_participants",
                newName: "ShadowUserId");

            migrationBuilder.RenameIndex(
                name: "IX_chat_participants_UserId",
                table: "chat_participants",
                newName: "IX_chat_participants_ShadowUserId");

            migrationBuilder.RenameIndex(
                name: "IX_chat_participants_ChatRoomId_UserId",
                table: "chat_participants",
                newName: "IX_chat_participants_ChatRoomId_ShadowUserId");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "messages",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "messages",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "messages",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OrderId",
                table: "chat_rooms",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RoomType",
                table: "chat_rooms",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "chat_rooms",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "chat_rooms",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAdmin",
                table: "chat_participants",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastReadAt",
                table: "chat_participants",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LeftAt",
                table: "chat_participants",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_chat_participants_users_shadow_ShadowUserId",
                table: "chat_participants",
                column: "ShadowUserId",
                principalTable: "users_shadow",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_messages_users_shadow_SenderId",
                table: "messages",
                column: "SenderId",
                principalTable: "users_shadow",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_chat_participants_users_shadow_ShadowUserId",
                table: "chat_participants");

            migrationBuilder.DropForeignKey(
                name: "FK_messages_users_shadow_SenderId",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "OrderId",
                table: "chat_rooms");

            migrationBuilder.DropColumn(
                name: "RoomType",
                table: "chat_rooms");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "chat_rooms");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "chat_rooms");

            migrationBuilder.DropColumn(
                name: "IsAdmin",
                table: "chat_participants");

            migrationBuilder.DropColumn(
                name: "LastReadAt",
                table: "chat_participants");

            migrationBuilder.DropColumn(
                name: "LeftAt",
                table: "chat_participants");

            migrationBuilder.RenameColumn(
                name: "ShadowUserId",
                table: "chat_participants",
                newName: "UserId");

            migrationBuilder.RenameIndex(
                name: "IX_chat_participants_ShadowUserId",
                table: "chat_participants",
                newName: "IX_chat_participants_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_chat_participants_ChatRoomId_ShadowUserId",
                table: "chat_participants",
                newName: "IX_chat_participants_ChatRoomId_UserId");
        }
    }
}
