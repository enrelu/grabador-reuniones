using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GrabadorReuniones.Core;

public interface ISecretProtector
{
    string Protect(string plaintext);
    string Unprotect(string protectedValue);
}

/// <summary>DPAPI del usuario actual (Windows). No requiere administrador.</summary>
[SupportedOSPlatform("windows")]
public sealed class DpapiSecretProtector : ISecretProtector
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("GrabadorReunionesIA.v2");

    public string Protect(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
            return "";

        var bytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(plaintext), Entropy, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(bytes);
    }

    public string Unprotect(string protectedValue)
    {
        if (string.IsNullOrWhiteSpace(protectedValue))
            return "";

        var bytes = ProtectedData.Unprotect(Convert.FromBase64String(protectedValue), Entropy, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(bytes);
    }
}

/// <summary>Sin cifrado: solo para pruebas o plataformas sin DPAPI.</summary>
public sealed class NullSecretProtector : ISecretProtector
{
    public string Protect(string plaintext) => plaintext ?? "";
    public string Unprotect(string protectedValue) => protectedValue ?? "";
}

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string _filePath;
    private readonly ISecretProtector _protector;

    public SettingsStore(string? settingsDirectory = null, ISecretProtector? protector = null)
    {
        var dir = settingsDirectory ?? AppPaths.SettingsDirectory;
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "settings.json");
        _protector = protector ?? CreateDefaultProtector();
    }

    public static ISecretProtector CreateDefaultProtector()
    {
        if (OperatingSystem.IsWindows())
            return new DpapiSecretProtector();
        return new NullSecretProtector();
    }

    public AppSettings Load()
    {
        if (!File.Exists(_filePath))
            return new AppSettings();

        var dto = JsonSerializer.Deserialize<SettingsFile>(_fileContents(), JsonOptions) ?? new SettingsFile();
        var settings = new AppSettings
        {
            OutputFolder = string.IsNullOrWhiteSpace(dto.OutputFolder) ? AppPaths.DefaultOutputFolder : dto.OutputFolder,
            MicrophoneDeviceId = dto.MicrophoneDeviceId ?? "",
            LoopbackDeviceId = dto.LoopbackDeviceId ?? "",
            Mp3BitrateKbps = dto.Mp3BitrateKbps > 0 ? dto.Mp3BitrateKbps : 64,
            EnableAutoMeetingDetection = dto.EnableAutoMeetingDetection ?? true,
            AutoDetectPromptPreset = string.IsNullOrWhiteSpace(dto.AutoDetectPromptPreset) ? "general" : dto.AutoDetectPromptPreset,
            TranscriptionExecutionMode = string.IsNullOrWhiteSpace(dto.TranscriptionExecutionMode) ? "CloudApi" : dto.TranscriptionExecutionMode,
            TranscriptionCliTool = string.IsNullOrWhiteSpace(dto.TranscriptionCliTool) ? "agy" : dto.TranscriptionCliTool,
            TranscriptionCliCustomCommand = dto.TranscriptionCliCustomCommand ?? "",
            TranscriptionProvider = string.IsNullOrWhiteSpace(dto.TranscriptionProvider) ? "Groq" : dto.TranscriptionProvider,
            TranscriptionModel = string.IsNullOrWhiteSpace(dto.TranscriptionModel) ? "whisper-large-v3-turbo" : dto.TranscriptionModel,
            TranscriptionEndpoint = dto.TranscriptionEndpoint ?? "",
            TranscriptionLanguage = string.IsNullOrWhiteSpace(dto.TranscriptionLanguage) ? "es" : dto.TranscriptionLanguage,
            LlmExecutionMode = string.IsNullOrWhiteSpace(dto.LlmExecutionMode) ? "CloudApi" : dto.LlmExecutionMode,
            LlmProvider = string.IsNullOrWhiteSpace(dto.LlmProvider) ? "Gemini" : dto.LlmProvider,
            LlmModel = string.IsNullOrWhiteSpace(dto.LlmModel) ? "gemini-2.0-flash" : dto.LlmModel,
            LlmEndpoint = dto.LlmEndpoint ?? "",
            CliTool = string.IsNullOrWhiteSpace(dto.CliTool) ? "agy" : dto.CliTool,
            CliCustomCommand = dto.CliCustomCommand ?? "",
            ActivePromptPresetId = string.IsNullOrWhiteSpace(dto.ActivePromptPresetId) ? "general" : dto.ActivePromptPresetId,
            AutoOpenViewerOnFinish = dto.AutoOpenViewerOnFinish ?? true,
            AutoCopyToClipboard = dto.AutoCopyToClipboard ?? true,
            WebhookUrl = dto.WebhookUrl ?? "",
            Hotkey = string.IsNullOrWhiteSpace(dto.Hotkey) ? "Ctrl+Shift+R" : dto.Hotkey,
            StartWithWindows = dto.StartWithWindows,
            HasCompletedFirstRun = dto.HasCompletedFirstRun
        };

        try { settings.WebhookBearerToken = _protector.Unprotect(dto.WebhookTokenProtected ?? ""); }
        catch { settings.WebhookBearerToken = ""; }

        try { settings.TranscriptionApiKey = _protector.Unprotect(dto.TranscriptionTokenProtected ?? ""); }
        catch { settings.TranscriptionApiKey = ""; }

        try { settings.LlmApiKey = _protector.Unprotect(dto.LlmTokenProtected ?? ""); }
        catch { settings.LlmApiKey = ""; }

        return settings;
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        var dto = new SettingsFile
        {
            OutputFolder = settings.OutputFolder,
            MicrophoneDeviceId = settings.MicrophoneDeviceId,
            LoopbackDeviceId = settings.LoopbackDeviceId,
            Mp3BitrateKbps = settings.Mp3BitrateKbps,
            EnableAutoMeetingDetection = settings.EnableAutoMeetingDetection,
            AutoDetectPromptPreset = settings.AutoDetectPromptPreset,
            TranscriptionExecutionMode = settings.TranscriptionExecutionMode,
            TranscriptionCliTool = settings.TranscriptionCliTool,
            TranscriptionCliCustomCommand = settings.TranscriptionCliCustomCommand,
            TranscriptionProvider = settings.TranscriptionProvider,
            TranscriptionTokenProtected = _protector.Protect(settings.TranscriptionApiKey ?? ""),
            TranscriptionModel = settings.TranscriptionModel,
            TranscriptionEndpoint = settings.TranscriptionEndpoint,
            TranscriptionLanguage = settings.TranscriptionLanguage,
            LlmExecutionMode = settings.LlmExecutionMode,
            LlmProvider = settings.LlmProvider,
            LlmTokenProtected = _protector.Protect(settings.LlmApiKey ?? ""),
            LlmModel = settings.LlmModel,
            LlmEndpoint = settings.LlmEndpoint,
            CliTool = settings.CliTool,
            CliCustomCommand = settings.CliCustomCommand,
            ActivePromptPresetId = settings.ActivePromptPresetId,
            AutoOpenViewerOnFinish = settings.AutoOpenViewerOnFinish,
            AutoCopyToClipboard = settings.AutoCopyToClipboard,
            WebhookUrl = settings.WebhookUrl,
            WebhookTokenProtected = _protector.Protect(settings.WebhookBearerToken ?? ""),
            Hotkey = settings.Hotkey,
            StartWithWindows = settings.StartWithWindows,
            HasCompletedFirstRun = settings.HasCompletedFirstRun
        };

        var json = JsonSerializer.Serialize(dto, JsonOptions);
        var tmp = _filePath + ".tmp";
        File.WriteAllText(tmp, json);
        File.Copy(tmp, _filePath, overwrite: true);
        File.Delete(tmp);
    }

    private string _fileContents() => File.ReadAllText(_filePath);

    private sealed class SettingsFile
    {
        public string? OutputFolder { get; set; }
        public string? MicrophoneDeviceId { get; set; }
        public string? LoopbackDeviceId { get; set; }
        public int Mp3BitrateKbps { get; set; }
        public bool? EnableAutoMeetingDetection { get; set; }
        public string? AutoDetectPromptPreset { get; set; }
        public string? TranscriptionExecutionMode { get; set; }
        public string? TranscriptionCliTool { get; set; }
        public string? TranscriptionCliCustomCommand { get; set; }
        public string? TranscriptionProvider { get; set; }
        public string? TranscriptionTokenProtected { get; set; }
        public string? TranscriptionModel { get; set; }
        public string? TranscriptionEndpoint { get; set; }
        public string? TranscriptionLanguage { get; set; }
        public string? LlmExecutionMode { get; set; }
        public string? LlmProvider { get; set; }
        public string? LlmTokenProtected { get; set; }
        public string? LlmModel { get; set; }
        public string? LlmEndpoint { get; set; }
        public string? CliTool { get; set; }
        public string? CliCustomCommand { get; set; }
        public string? ActivePromptPresetId { get; set; }
        public bool? AutoOpenViewerOnFinish { get; set; }
        public bool? AutoCopyToClipboard { get; set; }
        public string? WebhookUrl { get; set; }
        public string? WebhookTokenProtected { get; set; }
        public string? Hotkey { get; set; }
        public bool StartWithWindows { get; set; }
        public bool HasCompletedFirstRun { get; set; }
    }
}
