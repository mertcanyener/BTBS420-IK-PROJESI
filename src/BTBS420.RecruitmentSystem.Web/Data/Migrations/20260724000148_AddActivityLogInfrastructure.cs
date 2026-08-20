using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BTBS420.RecruitmentSystem.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddActivityLogInfrastructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActivityLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                    ActorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ActionCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TargetEntityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TargetEntityId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    JobPostingId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CandidateId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Summary = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLogs_ActionCode_OccurredAtUtc",
                table: "ActivityLogs",
                columns: new[] { "ActionCode", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLogs_ActorUserId_OccurredAtUtc",
                table: "ActivityLogs",
                columns: new[] { "ActorUserId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLogs_CandidateId_OccurredAtUtc",
                table: "ActivityLogs",
                columns: new[] { "CandidateId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLogs_JobPostingId_OccurredAtUtc",
                table: "ActivityLogs",
                columns: new[] { "JobPostingId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLogs_OccurredAtUtc",
                table: "ActivityLogs",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLogs_TargetEntityType_TargetEntityId_OccurredAtUtc",
                table: "ActivityLogs",
                columns: new[] { "TargetEntityType", "TargetEntityId", "OccurredAtUtc" });

            migrationBuilder.Sql(
                """
                CREATE TRIGGER [dbo].[TR_ActivityLogs_AppendOnly]
                ON [dbo].[ActivityLogs]
                INSTEAD OF UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    THROW 51023, 'ActivityLogs is append-only; UPDATE and DELETE are not allowed.', 1;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'[dbo].[TR_ActivityLogs_AppendOnly]', N'TR') IS NOT NULL
                BEGIN
                    DROP TRIGGER [dbo].[TR_ActivityLogs_AppendOnly];
                END;
                """);

            migrationBuilder.DropTable(
                name: "ActivityLogs");
        }
    }
}
