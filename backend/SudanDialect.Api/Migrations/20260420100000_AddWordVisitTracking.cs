using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SudanDialect.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddWordVisitTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "VisitCount",
                table: "words",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastVisitedAt",
                table: "words",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VisitCount",
                table: "words");

            migrationBuilder.DropColumn(
                name: "LastVisitedAt",
                table: "words");
        }
    }
}