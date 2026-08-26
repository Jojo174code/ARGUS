using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Argus.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAgenticWorkflowRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "agentic_workflow_runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IncidentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CurrentStage = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StageResultsJson = table.Column<string>(type: "text", nullable: false),
                    BlockingMissingInformationJson = table.Column<string>(type: "text", nullable: true),
                    SummaryMetricsJson = table.Column<string>(type: "text", nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailureStage = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FailureMessage = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CoordinatorRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    InvestigatorRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResponseEducationRunId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agentic_workflow_runs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_agentic_workflow_runs_incidents_IncidentId",
                        column: x => x.IncidentId,
                        principalTable: "incidents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_agentic_workflow_runs_IncidentId_StartedAt",
                table: "agentic_workflow_runs",
                columns: new[] { "IncidentId", "StartedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agentic_workflow_runs");
        }
    }
}
