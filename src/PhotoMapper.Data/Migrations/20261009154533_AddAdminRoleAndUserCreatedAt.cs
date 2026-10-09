using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhotoMapper.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminRoleAndUserCreatedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.InsertData(
                table: "AspNetRoles",
                columns: new[] { "Id", "ConcurrencyStamp", "Name", "NormalizedName" },
                values: new object[] { "6f0f3c84-1b6e-4c7f-9d3a-2a5b9a7c1e01", "6f0f3c84-1b6e-4c7f-9d3a-2a5b9a7c1e01", "Admin", "ADMIN" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "6f0f3c84-1b6e-4c7f-9d3a-2a5b9a7c1e01");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "AspNetUsers");
        }
    }
}
