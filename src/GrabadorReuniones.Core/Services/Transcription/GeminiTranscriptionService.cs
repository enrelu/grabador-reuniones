using System.Text;
using System.Text.Json;

namespace GrabadorReuniones.Core;

public sealed class GeminiTranscriptionService : ITranscriptionService
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _model;

    public GeminiTranscriptionService(HttpClient http, string apiKey, string model = "gemini-2.0-flash")
    {
        _http = http;
        _apiKey = apiKey;
        _model = string.IsNullOrWhiteSpace(model) ? "gemini-2.0-flash" : model;
    }

    public async Task<TranscriptionResult> TranscribeAsync(string audioFilePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(audioFilePath))
            return TranscriptionResult.Failed($"El archivo de audio no existe: {audioFilePath}");

        if (string.IsNullOrWhiteSpace(_apiKey))
            return TranscriptionResult.Failed("No se ha configurado la clave de API de Gemini.");

        try
        {
            var bytes = await File.ReadAllBytesAsync(audioFilePath, cancellationToken);
            var base64 = Convert.ToBase64String(bytes);
            var ext = Path.GetExtension(audioFilePath).ToLowerInvariant();
            var mimeType = ext == ".mp3" ? "audio/mp3" : "audio/wav";

            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent?key={_apiKey.Trim()}";

            var payload = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new object[]
                        {
                            new
                            {
                                inline_data = new
                                {
                                    mime_type = mimeType,
                                    data = base64
                                }
                            },
                            new
                            {
                                text = "Transcribe el audio adjunto de manera literal, fidedigna y completa en español. Devuelve únicamente el texto de la transcripción, sin introducciones ni comentarios adicionales."
                            }
                        }
                    }
                }
            };

            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _http.PostAsync(url, content, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorSnippet = body.Length > 250 ? body[..250] + "..." : body;
                return TranscriptionResult.Failed($"Error de API de Gemini ({response.StatusCode}): {errorSnippet}");
            }

            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("candidates", out var candidates) &&
                candidates.GetArrayLength() > 0 &&
                candidates[0].TryGetProperty("content", out var candContent) &&
                candContent.TryGetProperty("parts", out var parts) &&
                parts.GetArrayLength() > 0 &&
                parts[0].TryGetProperty("text", out var textProp))
            {
                var text = textProp.GetString() ?? "";
                return TranscriptionResult.Succeeded(text.Trim());
            }

            return TranscriptionResult.Failed("No se pudo extraer la transcripción de la respuesta de Gemini.");
        }
        catch (Exception ex)
        {
            return TranscriptionResult.Failed($"Excepción en transcripción con Gemini: {ex.Message}");
        }
    }
}
