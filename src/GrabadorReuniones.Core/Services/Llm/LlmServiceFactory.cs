namespace GrabadorReuniones.Core;

public static class LlmServiceFactory
{
    public static ILlmService Create(AppSettings settings, HttpClient http)
    {
        var mode = (settings.LlmExecutionMode ?? "CloudApi").Trim();

        if (mode.Equals("Cli", StringComparison.OrdinalIgnoreCase))
        {
            return new CliLlmService(settings.CliTool, settings.CliCustomCommand);
        }

        var provider = (settings.LlmProvider ?? "Gemini").Trim();

        if (provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase))
        {
            var model = string.IsNullOrWhiteSpace(settings.LlmModel) ? "gemini-2.0-flash" : settings.LlmModel;
            return new GeminiLlmService(http, settings.LlmApiKey, model);
        }

        if (provider.Equals("Groq", StringComparison.OrdinalIgnoreCase))
        {
            var endpoint = string.IsNullOrWhiteSpace(settings.LlmEndpoint)
                ? "https://api.groq.com/openai/v1/chat/completions"
                : settings.LlmEndpoint;
            var model = string.IsNullOrWhiteSpace(settings.LlmModel)
                ? "llama-3.3-70b-versatile"
                : settings.LlmModel;
            return new OpenAiCompatibleLlmService(http, endpoint, settings.LlmApiKey, model);
        }

        if (provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase))
        {
            var endpoint = string.IsNullOrWhiteSpace(settings.LlmEndpoint)
                ? "https://api.openai.com/v1/chat/completions"
                : settings.LlmEndpoint;
            var model = string.IsNullOrWhiteSpace(settings.LlmModel)
                ? "gpt-4o-mini"
                : settings.LlmModel;
            return new OpenAiCompatibleLlmService(http, endpoint, settings.LlmApiKey, model);
        }

        // Ollama / Custom OpenAI-compatible endpoint
        var customEndpoint = string.IsNullOrWhiteSpace(settings.LlmEndpoint)
            ? "http://localhost:11434/v1/chat/completions"
            : settings.LlmEndpoint;
        var customModel = string.IsNullOrWhiteSpace(settings.LlmModel)
            ? "llama3"
            : settings.LlmModel;
        return new OpenAiCompatibleLlmService(http, customEndpoint, settings.LlmApiKey, customModel);
    }
}
