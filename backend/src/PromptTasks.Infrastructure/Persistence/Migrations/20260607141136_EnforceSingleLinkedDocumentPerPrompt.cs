using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PromptTasks.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnforceSingleLinkedDocumentPerPrompt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Backfill destrutivo: garante no maximo 1 plano por prompt ANTES de criar o indice unico.
            // Mantem o plano mais antigo por (CreatedAtUtc, Id) e remove os demais + seus historicos de versao.
            // ATENCAO: irreversivel. Faca backup (pg_dump) antes de aplicar; o Down() nao restaura os dados removidos.
            migrationBuilder.Sql(@"
DO $$
DECLARE removed text;
BEGIN
  SELECT string_agg(""Id""::text, ', ') INTO removed
  FROM linked_documents
  WHERE ""Id"" NOT IN (
    SELECT DISTINCT ON (""PromptId"") ""Id"" FROM linked_documents
    ORDER BY ""PromptId"", ""CreatedAtUtc"", ""Id"");
  IF removed IS NOT NULL THEN
    RAISE NOTICE 'EnforceSingleLinkedDocumentPerPrompt: removendo planos duplicados %', removed;
  END IF;
END $$;");

            migrationBuilder.Sql(@"
DELETE FROM linked_document_versions
WHERE ""LinkedDocumentId"" NOT IN (
  SELECT DISTINCT ON (""PromptId"") ""Id"" FROM linked_documents
  ORDER BY ""PromptId"", ""CreatedAtUtc"", ""Id"");");

            migrationBuilder.Sql(@"
DELETE FROM linked_documents
WHERE ""Id"" NOT IN (
  SELECT DISTINCT ON (""PromptId"") ""Id"" FROM linked_documents
  ORDER BY ""PromptId"", ""CreatedAtUtc"", ""Id"");");

            migrationBuilder.DropIndex(
                name: "IX_linked_documents_PromptId_AbsolutePathKey",
                table: "linked_documents");

            migrationBuilder.CreateIndex(
                name: "IX_linked_documents_PromptId",
                table: "linked_documents",
                column: "PromptId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Observacao: o backfill do Up() e irreversivel — os planos duplicados removidos nao sao restaurados aqui.
            migrationBuilder.DropIndex(
                name: "IX_linked_documents_PromptId",
                table: "linked_documents");

            migrationBuilder.CreateIndex(
                name: "IX_linked_documents_PromptId_AbsolutePathKey",
                table: "linked_documents",
                columns: new[] { "PromptId", "AbsolutePathKey" },
                unique: true);
        }
    }
}
