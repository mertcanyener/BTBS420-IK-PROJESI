using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BTBS420.RecruitmentSystem.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddJobApplicationStatusTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "JobApplications",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "new");

            migrationBuilder.AddColumn<DateTime>(
                name: "WithdrawnAtUtc",
                table: "JobApplications",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                table: "JobApplications");

            migrationBuilder.DropColumn(
                name: "WithdrawnAtUtc",
                table: "JobApplications");
        }
    }
}
