using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JIRAbot.Migrations
{
    /// <inheritdoc />
    public partial class AddNewColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Requests_RequestTypes_TypeId",
                table: "Requests");

            migrationBuilder.RenameColumn(
                name: "Сlient_id",
                table: "Groups",
                newName: "ClientId");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Groups",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Clients",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateIndex(
                name: "IX_Groups_ClientId",
                table: "Groups",
                column: "ClientId");

            migrationBuilder.AddForeignKey(
                name: "FK_Groups_Clients_ClientId",
                table: "Groups",
                column: "ClientId",
                principalTable: "Clients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Requests_RequestTypes_TypeId",
                table: "Requests",
                column: "TypeId",
                principalTable: "RequestTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Groups_Clients_ClientId",
                table: "Groups");

            migrationBuilder.DropForeignKey(
                name: "FK_Requests_RequestTypes_TypeId",
                table: "Requests");

            migrationBuilder.DropIndex(
                name: "IX_Groups_ClientId",
                table: "Groups");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Groups");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Clients");

            migrationBuilder.RenameColumn(
                name: "ClientId",
                table: "Groups",
                newName: "Сlient_id");

            migrationBuilder.AddForeignKey(
                name: "FK_Requests_RequestTypes_TypeId",
                table: "Requests",
                column: "TypeId",
                principalTable: "RequestTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
