namespace GrabadorReuniones.Core;

public sealed class MeetingContext
{
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset EndedAt { get; set; }
    public int DurationSec { get; set; }
    public string AudioPath { get; set; } = "";
    public string SuggestedTitle { get; set; } = "Reunión";
    public string MeetingApp { get; set; } = "";
}

public sealed class LlmAnalysisResult
{
    public bool Success { get; set; }
    public string MarkdownSummary { get; set; } = "";
    public string? ErrorMessage { get; set; }
    public string ModelUsed { get; set; } = "";

    public static LlmAnalysisResult Succeeded(string markdown, string model = "") => new()
    {
        Success = true,
        MarkdownSummary = markdown,
        ModelUsed = model
    };

    public static LlmAnalysisResult Failed(string error) => new()
    {
        Success = false,
        ErrorMessage = error
    };
}

public interface ILlmService
{
    Task<LlmAnalysisResult> AnalyzeMeetingAsync(
        string transcriptText,
        PromptPreset preset,
        MeetingContext context,
        CancellationToken cancellationToken = default);
}
