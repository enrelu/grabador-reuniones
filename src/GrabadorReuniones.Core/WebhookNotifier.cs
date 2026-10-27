using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace GrabadorReuniones.Core;

public readonly record struct WebhookNotifyResult(bool Attempted, bool Succeeded, string? Error);

public sealed class WebhookNotifier
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = null
    };

    private readonly HttpClient _http;

    public WebhookNotifier(HttpClient http)
    {
        _http = http;
    }

    public static bool TryGetWebhookUri(string? url, out Uri? uri, out string? error)
    {
        uri = null;
        error = null;
        if (string.IsNullOrWhiteSpace(url))
            return false;

        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var parsed) ||
            (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
        {
            error = "La URL del webhook no es válida.";
            return false;
        }

        uri = parsed;
        return true;
    }

    public async Task<WebhookNotifyResult> NotifyRecordingReadyAsync(
        string? url,
        string? bearerToken,
        RecordingReadyPayload payload,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetWebhookUri(url, out var uri, out var parseError))
        {
            if (parseError is not null)
                return new WebhookNotifyResult(true, false, parseError);
            return new WebhookNotifyResult(false, false, null);
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, uri);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            if (!string.IsNullOrWhiteSpace(bearerToken))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken.Trim());

            var json = JsonSerializer.Serialize(payload, JsonOptions);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
                return new WebhookNotifyResult(true, true, null);

            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var snippet = string.IsNullOrWhiteSpace(body) ? "" : " " + Truncate(body, 180);
            return new WebhookNotifyResult(true, false, $"HTTP {(int)response.StatusCode}{snippet}");
        }
        catch (Exception ex)
        {
            return new WebhookNotifyResult(true, false, ex.Message);
        }
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max] + "…";
}
