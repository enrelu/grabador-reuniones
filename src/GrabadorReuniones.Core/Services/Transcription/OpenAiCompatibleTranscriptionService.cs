using System.Net.Http.Headers;
using System.Text.Json;

namespace GrabadorReuniones.Core;

public sealed class OpenAiCompatibleTranscriptionService : ITranscriptionService
{
    private readonly HttpClient _http;
    private readonly string _endpoint;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly string _language;

    public OpenAiCompatibleTranscriptionService(
        HttpClient http,
        string endpoint,
        string apiKey,
        string model = "whisper-large-v3-turbo",
        string language = "es")
    {
        _http = http;
        _endpoint = endpoint;
        _apiKey = apiKey;
        _model = model;
        _language = language;
    }

    public async Task<TranscriptionResult> TranscribeAsync(string audioFilePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(audioFilePath))
            return TranscriptionResult.Failed($"El archivo de audio no existe: {audioFilePath}");

        if (string.IsNullOrWhiteSpace(_apiKey) && !_endpoint.Contains("localhost") && !_endpoint.Contains("127.0.0.1"))
            return TranscriptionResult.Failed("No se ha configurado la clave de API para la transcripción.");

        try
        {
            using var form = new MultipartFormDataContent();

            var fileBytes = await File.ReadAllBytesAsync(audioFilePath, cancellationToken);
            var fileContent = new ByteArrayContent(fileBytes);
            var ext = Path.GetExtension(audioFilePath).ToLowerInvariant();
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(ext == ".mp3" ? "audio/mpeg" : "audio/wav");

            form.Add(fileContent, "file", Path.GetFileName(audioFilePath));
            form.Add(new StringContent(_model), "model");
            if (!string.IsNullOrWhiteSpace(_language))
                form.Add(new StringContent(_language), "language");
            form.Add(new StringContent("json"), "response_format");

            using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint)
            {
                Content = form
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
                return TranscriptionResult.Failed($"Error de API de transcripción ({response.StatusCode}): {errorSnippet}");
            }

            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("text", out var textProp))
            {
                var text = textProp.GetString() ?? "";
                return TranscriptionResult.Succeeded(text.Trim(), language: _language);
            }

            return TranscriptionResult.Failed("La respuesta de la API no contiene el campo 'text'.");
        }
        catch (Exception ex)
        {
            return TranscriptionResult.Failed($"Excepción al transcribir: {ex.Message}");
        }
    }
}
