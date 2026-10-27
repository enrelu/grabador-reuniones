using System.Text;
using System.Text.Json;

namespace GrabadorReuniones.Core;

public sealed class GeminiLlmService : ILlmService
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _model;

    public GeminiLlmService(HttpClient http, string apiKey, string model = "gemini-2.0-flash")
    {
        _http = http;
        _apiKey = apiKey;
        _model = string.IsNullOrWhiteSpace(model) ? "gemini-2.0-flash" : model;
    }

    public async Task<LlmAnalysisResult> AnalyzeMeetingAsync(
        string transcriptText,
        PromptPreset preset,
        MeetingContext context,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(transcriptText))
            return LlmAnalysisResult.Failed("La transcripción está vacía.");

        if (string.IsNullOrWhiteSpace(_apiKey))
            return LlmAnalysisResult.Failed("No se ha configurado la clave de API de Gemini.");

        try
        {
            var systemPrompt = RenderTemplate(preset.SystemPrompt, context);
            var userPrompt = RenderTemplate(preset.UserPromptTemplate, context)
                .Replace("{TRANSCRIPCION}", transcriptText);

            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent?key={_apiKey.Trim()}";

            var payload = new
            {
                system_instruction = new
                {
                    parts = new[]
                    {
                        new { text = systemPrompt }
                    }
                },
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = userPrompt }
                        }
                    }
                },
                generationConfig = new
                {
                    temperature = 0.3
                }
            };

            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _http.PostAsync(url, content, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorSnippet = body.Length > 250 ? body[..250] + "..." : body;
                return LlmAnalysisResult.Failed($"Error de API de Gemini ({response.StatusCode}): {errorSnippet}");
            }

            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("candidates", out var candidates) &&
                candidates.GetArrayLength() > 0 &&
                candidates[0].TryGetProperty("content", out var candContent) &&
                candContent.TryGetProperty("parts", out var parts) &&
                parts.GetArrayLength() > 0 &&
                parts[0].TryGetProperty("text", out var textProp))
            {
                var markdown = textProp.GetString() ?? "";
                return LlmAnalysisResult.Succeeded(markdown.Trim(), _model);
            }

            return LlmAnalysisResult.Failed("No se pudo extraer el análisis de la respuesta de Gemini.");
        }
        catch (Exception ex)
        {
            return LlmAnalysisResult.Failed($"Excepción en análisis con Gemini: {ex.Message}");
        }
    }

    private static string RenderTemplate(string template, MeetingContext context)
    {
        return template
            .Replace("{TITULO_SUGERIDO}", context.SuggestedTitle)
            .Replace("{CLIENTE_O_PROYECTO}", context.SuggestedTitle)
            .Replace("{TEMA_PRINCIPAL}", context.SuggestedTitle)
            .Replace("{TEMA}", context.SuggestedTitle)
            .Replace("{PARTICIPANTES}", context.MeetingApp.Length > 0 ? $"Vía {context.MeetingApp}" : "Participantes de la reunión")
            .Replace("{FECHA}", context.StartedAt.ToString("yyyy-MM-dd HH:mm"))
            .Replace("{DURACION}", $"{context.DurationSec / 60}m {context.DurationSec % 60}s");
    }
}
