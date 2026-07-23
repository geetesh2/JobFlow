using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnrichJobModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAtUtc",
                table: "Jobs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Jobs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ErrorMessage",
                table: "Jobs",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxRetries",
                table: "Jobs",
                type: "integer",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.AddColumn<DateTime>(
                name: "MetadataScheduledAtUtc",
                table: "Jobs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MetadataSource",
                table: "Jobs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MetadataTags",
                table: "Jobs",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Payload",
                table: "Jobs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "Jobs",
                type: "integer",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<int>(
                name: "ProgressPercentage",
                table: "Jobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RetryCount",
                table: "Jobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompletedAtUtc",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "ErrorMessage",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "MaxRetries",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "MetadataScheduledAtUtc",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "MetadataSource",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "MetadataTags",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "Payload",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "ProgressPercentage",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "RetryCount",
                table: "Jobs");
        }
    }
}
