using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Evaluation.Migrations
{
    /// <inheritdoc />
    public partial class SubmissionReviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Disqualified",
                table: "Teams",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "SubmissionReviews",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    SubmissionId = table.Column<string>(type: "TEXT", nullable: false),
                    SourceHash = table.Column<string>(type: "TEXT", nullable: false),
                    JudgeId = table.Column<string>(type: "TEXT", nullable: false),
                    Decision = table.Column<string>(type: "TEXT", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", nullable: false),
                    FindingsJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubmissionReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubmissionReviews_Submissions_SubmissionId",
                        column: x => x.SubmissionId,
                        principalTable: "Submissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SubmissionReviews_SubmissionId",
                table: "SubmissionReviews",
                column: "SubmissionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SubmissionReviews");

            migrationBuilder.DropColumn(
                name: "Disqualified",
                table: "Teams");
        }
    }
}
