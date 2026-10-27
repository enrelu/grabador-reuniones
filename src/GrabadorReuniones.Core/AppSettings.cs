namespace GrabadorReuniones.Core;

public sealed class AppSettings
{
    public string OutputFolder { get; set; } = AppPaths.DefaultOutputFolder;

    /// <summary>Id WASAPI del micrófono; vacío = dispositivo predeterminado de comunicaciones.</summary>
    public string MicrophoneDeviceId { get; set; } = "";

    /// <summary>Id WASAPI del dispositivo de reproducción (loopback); vacío = predeterminado multimedia.</summary>
    public string LoopbackDeviceId { get; set; } = "";

    /// <summary>Tasa de bits para el codificador MP3 en kbps (64, 96, 128, 192). 64 es óptimo para voz.</summary>
    public int Mp3BitrateKbps { get; set; } = 64;

    // Detección automática de reuniones
    public bool EnableAutoMeetingDetection { get; set; } = true;
    public string AutoDetectPromptPreset { get; set; } = "general";

    // Transcripción de Audio
    public string TranscriptionExecutionMode { get; set; } = "CloudApi"; // CloudApi, Cli
    public string TranscriptionCliTool { get; set; } = "agy"; // agy, claude, whisper, custom
    public string TranscriptionCliCustomCommand { get; set; } = "";
    public string TranscriptionProvider { get; set; } = "Groq"; // Groq, Gemini, OpenAI, Custom
    public string TranscriptionApiKey { get; set; } = "";
    public string TranscriptionModel { get; set; } = "whisper-large-v3-turbo";
    public string TranscriptionEndpoint { get; set; } = "";
    public string TranscriptionLanguage { get; set; } = "es";

    // LLM y Análisis
    public string LlmExecutionMode { get; set; } = "CloudApi"; // CloudApi, Cli
    public string LlmProvider { get; set; } = "Gemini"; // Gemini, Groq, OpenAI, Anthropic, Ollama, Custom
    public string LlmApiKey { get; set; } = "";
    public string LlmModel { get; set; } = "gemini-2.0-flash";
    public string LlmEndpoint { get; set; } = "";

    // Configuración de CLI (Google Antigravity / Claude / Gemini)
    public string CliTool { get; set; } = "agy"; // agy, claude, gemini, custom
    public string CliCustomCommand { get; set; } = "";

    // Prompts y UI
    public string ActivePromptPresetId { get; set; } = "general";
    public bool AutoOpenViewerOnFinish { get; set; } = true;
    public bool AutoCopyToClipboard { get; set; } = true;

    // Webhook opcional
    public string WebhookUrl { get; set; } = "";
    public string WebhookBearerToken { get; set; } = "";

    public string Hotkey { get; set; } = "Ctrl+Shift+R";
    public bool StartWithWindows { get; set; }
    public bool HasCompletedFirstRun { get; set; }

    public AppSettings Clone() => new()
    {
        OutputFolder = OutputFolder,
        MicrophoneDeviceId = MicrophoneDeviceId,
        LoopbackDeviceId = LoopbackDeviceId,
        Mp3BitrateKbps = Mp3BitrateKbps,
        EnableAutoMeetingDetection = EnableAutoMeetingDetection,
        AutoDetectPromptPreset = AutoDetectPromptPreset,
        TranscriptionExecutionMode = TranscriptionExecutionMode,
        TranscriptionCliTool = TranscriptionCliTool,
        TranscriptionCliCustomCommand = TranscriptionCliCustomCommand,
        TranscriptionProvider = TranscriptionProvider,
        TranscriptionApiKey = TranscriptionApiKey,
        TranscriptionModel = TranscriptionModel,
        TranscriptionEndpoint = TranscriptionEndpoint,
        TranscriptionLanguage = TranscriptionLanguage,
        LlmExecutionMode = LlmExecutionMode,
        LlmProvider = LlmProvider,
        LlmApiKey = LlmApiKey,
        LlmModel = LlmModel,
        LlmEndpoint = LlmEndpoint,
        CliTool = CliTool,
        CliCustomCommand = CliCustomCommand,
        ActivePromptPresetId = ActivePromptPresetId,
        AutoOpenViewerOnFinish = AutoOpenViewerOnFinish,
        AutoCopyToClipboard = AutoCopyToClipboard,
        WebhookUrl = WebhookUrl,
        WebhookBearerToken = WebhookBearerToken,
        Hotkey = Hotkey,
        StartWithWindows = StartWithWindows,
        HasCompletedFirstRun = HasCompletedFirstRun
    };
}

public static class AppPaths
{
    public const string AppVersion = "2.0.0";

    public static string DefaultOutputFolder =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "GrabadorReunionesIA",
            "Grabaciones");

    public static string SettingsDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "GrabadorReunionesIA");

    public static string PromptsDirectory => Path.Combine(SettingsDirectory, "Prompts");

    public static string SettingsFilePath => Path.Combine(SettingsDirectory, "settings.json");

    public static string LogFilePath => Path.Combine(SettingsDirectory, "grabador.log");
}
