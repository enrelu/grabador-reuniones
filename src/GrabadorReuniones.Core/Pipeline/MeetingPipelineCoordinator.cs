namespace GrabadorReuniones.Core;

public sealed class MeetingPipelineResult
{
    public bool Success { get; set; }
    public string AudioPath { get; set; } = "";
    public string TranscriptPath { get; set; } = "";
    public string SummaryPath { get; set; } = "";
    public string TranscriptText { get; set; } = "";
    public string SummaryMarkdown { get; set; } = "";
    public PromptPreset PresetUsed { get; set; } = new();
    public string? ErrorMessage { get; set; }
}

public sealed class MeetingPipelineCoordinator
{
    private readonly AppSettings _settings;
    private readonly PromptPresetStore _presets;
    private readonly HttpClient _http;

    public event Action<string>? ProgressChanged;

    public MeetingPipelineCoordinator(AppSettings settings, PromptPresetStore presets, HttpClient http)
    {
        _settings = settings;
        _presets = presets;
        _http = http;
    }

    public async Task<MeetingPipelineResult> ProcessRecordingAsync(
        string audioFilePath,
        DateTimeOffset startedAt,
        DateTimeOffset endedAt,
        string? presetId = null,
        string? detectedApp = null,
        CancellationToken cancellationToken = default)
    {
        var result = new MeetingPipelineResult
        {
            AudioPath = audioFilePath
        };

        try
        {
            var durationSec = Math.Max(0, (int)Math.Round((endedAt - startedAt).TotalSeconds));
            var context = new MeetingContext
            {
                StartedAt = startedAt,
                EndedAt = endedAt,
                DurationSec = durationSec,
                AudioPath = audioFilePath,
                MeetingApp = detectedApp ?? "",
                SuggestedTitle = Path.GetFileNameWithoutExtension(audioFilePath)
            };

            var preset = _presets.GetPresetOrDefault(presetId ?? _settings.ActivePromptPresetId);
            result.PresetUsed = preset;

            // 1. Transcripción
            ProgressChanged?.Invoke("Transcribiendo audio de la reunión...");
            var transcriptionService = TranscriptionServiceFactory.Create(_settings, _http);
            var transcription = await transcriptionService.TranscribeAsync(audioFilePath, cancellationToken);

            if (!transcription.Success)
            {
                result.Success = false;
                result.ErrorMessage = $"Error en transcripción: {transcription.ErrorMessage}";
                return result;
            }

            result.TranscriptText = transcription.Text;
            var transcriptPath = Path.ChangeExtension(audioFilePath, ".transcripcion.txt");
            await File.WriteAllTextAsync(transcriptPath, transcription.Text, cancellationToken);
            result.TranscriptPath = transcriptPath;

            // 2. Análisis LLM con Prompt Maestro
            ProgressChanged?.Invoke($"Analizando reunión con plantilla '{preset.Name}'...");
            var llmService = LlmServiceFactory.Create(_settings, _http);
            var llmResult = await llmService.AnalyzeMeetingAsync(transcription.Text, preset, context, cancellationToken);

            if (!llmResult.Success)
            {
                result.Success = false;
                result.ErrorMessage = $"Transcripción completada, pero falló el análisis IA: {llmResult.ErrorMessage}";
                return result;
            }

            result.SummaryMarkdown = llmResult.MarkdownSummary;
            var summaryPath = Path.ChangeExtension(audioFilePath, ".resumen.md");
            await File.WriteAllTextAsync(summaryPath, llmResult.MarkdownSummary, cancellationToken);
            result.SummaryPath = summaryPath;

            result.Success = true;
            ProgressChanged?.Invoke("¡Reunión procesada y notas listas!");
            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = $"Error en el procesamiento: {ex.Message}";
            return result;
        }
    }

    public async Task<LlmAnalysisResult> ReanalyzeAsync(
        string transcriptText,
        PromptPreset newPreset,
        MeetingContext context,
        string summaryFilePath,
        CancellationToken cancellationToken = default)
    {
        ProgressChanged?.Invoke($"Re-analizando con plantilla '{newPreset.Name}'...");
        var llmService = LlmServiceFactory.Create(_settings, _http);
        var llmResult = await llmService.AnalyzeMeetingAsync(transcriptText, newPreset, context, cancellationToken);

        if (llmResult.Success && !string.IsNullOrWhiteSpace(summaryFilePath))
        {
            await File.WriteAllTextAsync(summaryFilePath, llmResult.MarkdownSummary, cancellationToken);
        }

        return llmResult;
    }
}
