using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BTBS420.RecruitmentSystem.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInterviewParticipantInfrastructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InterviewParticipants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InterviewId = table.Column<int>(type: "int", nullable: false),
                    ParticipantUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    AssignedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InterviewParticipants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InterviewParticipants_AspNetUsers_ParticipantUserId",
                        column: x => x.ParticipantUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InterviewParticipants_Interviews_InterviewId",
                        column: x => x.InterviewId,
                        principalTable: "Interviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InterviewParticipants_ParticipantUserId",
                table: "InterviewParticipants",
                column: "ParticipantUserId");

            migrationBuilder.CreateIndex(
                name: "UX_InterviewParticipants_InterviewId_ParticipantUserId",
                table: "InterviewParticipants",
                columns: new[] { "InterviewId", "ParticipantUserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InterviewParticipants");
        }
    }
}
