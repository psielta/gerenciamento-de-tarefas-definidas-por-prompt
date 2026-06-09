namespace PromptTasks.Application.Features.Ai.Commands.GenerateNoteMarkdown;

/// <summary>
/// Optional structured formats the note generator can target. The key is the
/// stable identifier sent by the frontend; the value is the extra system
/// instruction block that nudges Gemini toward that layout.
/// </summary>
public static class NoteFormats
{
    public static readonly IReadOnlyDictionary<string, string> Instructions =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["adr"] =
                "Formato ADR (Architecture Decision Record): use as secoes Contexto, "
              + "Decisao, Status, Consequencias e Alternativas consideradas.",
            ["checklist"] =
                "Formato checklist: produza uma lista de tarefas acionaveis usando "
              + "caixas de marcacao Markdown (`- [ ]`), agrupadas por etapa quando fizer sentido.",
            ["ata"] =
                "Formato ata de reuniao: inclua data/participantes (se informados), "
              + "pauta, decisoes tomadas e itens de acao com responsaveis.",
            ["resumo"] =
                "Formato resumo: condense o conteudo em pontos principais claros e "
              + "objetivos, com uma breve conclusao.",
            ["plano"] =
                "Formato plano de implementacao: inclua objetivo, etapas numeradas, "
              + "arquivos/areas afetadas, criterios de validacao e riscos.",
        };

    public static bool IsKnown(string? format) =>
        !string.IsNullOrWhiteSpace(format) && Instructions.ContainsKey(format);
}
