namespace PromptTasks.Application.Features.Ai.Commands.GenerateMermaidDiagram;

/// <summary>
/// Optional Mermaid diagram kinds the generator can target. The key is the stable
/// identifier sent by the frontend; the value is a short hint appended to the
/// system instruction so Gemini picks the right Mermaid syntax.
/// </summary>
public static class MermaidDiagramKinds
{
    public static readonly IReadOnlyDictionary<string, string> Hints =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["flowchart"] = "Gere um flowchart (`flowchart TD` ou `flowchart LR`).",
            ["sequence"] = "Gere um sequence diagram (`sequenceDiagram`).",
            ["erd"] = "Gere um entity relationship diagram (`erDiagram`).",
            ["state"] = "Gere um state diagram (`stateDiagram-v2`).",
            ["class"] = "Gere um class diagram (`classDiagram`).",
            ["mindmap"] = "Gere um mindmap (`mindmap`).",
        };

    public static bool IsKnown(string? kind) =>
        !string.IsNullOrWhiteSpace(kind) && Hints.ContainsKey(kind);
}
