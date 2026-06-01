using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PromptTasks.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskNumbering : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TaskNumberPattern",
                table: "working_directories",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaskNumber",
                table: "prompts",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "daily_task_sequences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkingDirectoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    SequenceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CurrentValue = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_daily_task_sequences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_daily_task_sequences_working_directories_WorkingDirectoryId",
                        column: x => x.WorkingDirectoryId,
                        principalTable: "working_directories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_prompts_WorkingDirectoryId_TaskNumber",
                table: "prompts",
                columns: new[] { "WorkingDirectoryId", "TaskNumber" },
                unique: true,
                filter: "\"TaskNumber\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_daily_task_sequences_WorkingDirectoryId_SequenceDate",
                table: "daily_task_sequences",
                columns: new[] { "WorkingDirectoryId", "SequenceDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "daily_task_sequences");

            migrationBuilder.DropIndex(
                name: "IX_prompts_WorkingDirectoryId_TaskNumber",
                table: "prompts");

            migrationBuilder.DropColumn(
                name: "TaskNumberPattern",
                table: "working_directories");

            migrationBuilder.DropColumn(
                name: "TaskNumber",
                table: "prompts");
        }
    }
}
