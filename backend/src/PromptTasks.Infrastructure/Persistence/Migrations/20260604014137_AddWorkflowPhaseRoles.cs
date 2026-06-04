using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PromptTasks.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowPhaseRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Role",
                table: "workflow_template_phases",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CurrentPhaseIteration",
                table: "prompt_workflows",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "Role",
                table: "prompt_workflow_phases",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Role",
                table: "workflow_template_phases");

            migrationBuilder.DropColumn(
                name: "CurrentPhaseIteration",
                table: "prompt_workflows");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "prompt_workflow_phases");
        }
    }
}
