namespace GrabadorReuniones.Core;

public sealed class TranscriptionResult
{
    public bool Success { get; set; }
    public string Text { get; set; } = "";
    public string? ErrorMessage { get; set; }
    public string Language { get; set; } = "es";
    public double DurationSeconds { get; set; }

    public static TranscriptionResult Succeeded(string text, double duration = 0, string language = "es") => new()
    {
        Success = true,
        Text = text,
        DurationSeconds = duration,
        Language = language
    };

    public static TranscriptionResult Failed(string error) => new()
    {
        Success = false,
        ErrorMessage = error
    };
}

public interface ITranscriptionService
{
    Task<TranscriptionResult> TranscribeAsync(string audioFilePath, CancellationToken cancellationToken = default);
}
