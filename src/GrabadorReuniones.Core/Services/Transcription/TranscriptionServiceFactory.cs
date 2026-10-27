namespace GrabadorReuniones.Core;

public static class TranscriptionServiceFactory
{
    public static ITranscriptionService Create(AppSettings settings, HttpClient http)
    {
        var mode = (settings.TranscriptionExecutionMode ?? "CloudApi").Trim();

        if (mode.Equals("Cli", StringComparison.OrdinalIgnoreCase))
        {
            return new CliTranscriptionService(
                settings.TranscriptionCliTool,
                settings.TranscriptionCliCustomCommand,
                settings.TranscriptionLanguage);
        }

        var provider = (settings.TranscriptionProvider ?? "Groq").Trim();

        if (provider.Equals("Groq", StringComparison.OrdinalIgnoreCase))
        {
            var endpoint = string.IsNullOrWhiteSpace(settings.TranscriptionEndpoint)
                ? "https://api.groq.com/openai/v1/audio/transcriptions"
                : settings.TranscriptionEndpoint;
            var model = string.IsNullOrWhiteSpace(settings.TranscriptionModel)
                ? "whisper-large-v3-turbo"
                : settings.TranscriptionModel;
            return new OpenAiCompatibleTranscriptionService(http, endpoint, settings.TranscriptionApiKey, model, settings.TranscriptionLanguage);
        }

        if (provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase))
        {
            var endpoint = string.IsNullOrWhiteSpace(settings.TranscriptionEndpoint)
                ? "https://api.openai.com/v1/audio/transcriptions"
                : settings.TranscriptionEndpoint;
            var model = string.IsNullOrWhiteSpace(settings.TranscriptionModel)
                ? "whisper-1"
                : settings.TranscriptionModel;
            return new OpenAiCompatibleTranscriptionService(http, endpoint, settings.TranscriptionApiKey, model, settings.TranscriptionLanguage);
        }

        if (provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase))
        {
            var model = string.IsNullOrWhiteSpace(settings.TranscriptionModel)
                ? "gemini-2.0-flash"
                : settings.TranscriptionModel;
            return new GeminiTranscriptionService(http, settings.TranscriptionApiKey, model);
        }

        // Custom / Local endpoint (Ollama / LocalAI / Whisper.cpp server)
        var customEndpoint = string.IsNullOrWhiteSpace(settings.TranscriptionEndpoint)
            ? "http://localhost:8000/v1/audio/transcriptions"
            : settings.TranscriptionEndpoint;
        var customModel = string.IsNullOrWhiteSpace(settings.TranscriptionModel)
            ? "whisper-1"
            : settings.TranscriptionModel;
        return new OpenAiCompatibleTranscriptionService(http, customEndpoint, settings.TranscriptionApiKey, customModel, settings.TranscriptionLanguage);
    }
}
