using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace GrabadorReuniones.Core;

public sealed class OpenAiCompatibleLlmService : ILlmService
{
    private readonly HttpClient _http;
    private readonly string _endpoint;
    private readonly string _apiKey;
    private readonly string _model;

    public OpenAiCompatibleLlmService(HttpClient http, string endpoint, string apiKey, string model)
    {
        _http = http;
        _endpoint = endpoint;
        _apiKey = apiKey;
        _model = model;
    }

    public async Task<LlmAnalysisResult> AnalyzeMeetingAsync(
        string transcriptText,
        PromptPreset preset,
        MeetingContext context,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(transcriptText))
            return LlmAnalysisResult.Failed("La transcripción está vacía.");

        if (string.IsNullOrWhiteSpace(_apiKey) && !_endpoint.Contains("localhost") && !_endpoint.Contains("127.0.0.1"))
            return LlmAnalysisResult.Failed("No se ha configurado la clave de API para el servicio LLM.");

        try
        {
            var systemPrompt = RenderTemplate(preset.SystemPrompt, context);
            var userPrompt = RenderTemplate(preset.UserPromptTemplate, context)
                .Replace("{TRANSCRIPCION}", transcriptText);

            var payload = new
            {
                model = _model,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                temperature = 0.3
            };

            var json = JsonSerializer.Serialize(payload);
            using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            if (!string.IsNullOrWhiteSpace(_apiKey))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey.Trim());
            }

            using var response = await _http.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorSnippet = body.Length > 250 ? body[..250] + "..." : body;
                return LlmAnalysisResult.Failed($"Error de API LLM ({response.StatusCode}): {errorSnippet}");
            }

            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("choices", out var choices) &&
                choices.GetArrayLength() > 0 &&
                choices[0].TryGetProperty("message", out var msg) &&
                msg.TryGetProperty("content", out var contentProp))
            {
                var markdown = contentProp.GetString() ?? "";
                return LlmAnalysisResult.Succeeded(markdown.Trim(), _model);
            }

            return LlmAnalysisResult.Failed("No se encontró contenido en la respuesta del modelo.");
        }
        catch (Exception ex)
        {
            return LlmAnalysisResult.Failed($"Excepción al conectar con el LLM: {ex.Message}");
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
