using System.Text.Json;
using System.Text.Json.Serialization;

namespace GrabadorReuniones.Core;

public static class RecordingFileNames
{
    public const string AudioStemSuffix = "_reunion";

    public static string MakeBaseName(DateTimeOffset startedAt) =>
        startedAt.ToString("yyyy-MM-dd_HHmmss") + AudioStemSuffix;

    public static RecordingPaths AllocatePaths(string outputFolder, DateTimeOffset startedAt)
    {
        Directory.CreateDirectory(outputFolder);
        var stem = MakeBaseName(startedAt);
        var basePath = Path.Combine(outputFolder, stem);
        var n = 2;
        while (PathsExist(basePath))
        {
            basePath = Path.Combine(outputFolder, $"{stem}_{n}");
            n++;
        }

        return new RecordingPaths(basePath);
    }

    private static bool PathsExist(string basePath) =>
        File.Exists(basePath + ".mp3") ||
        File.Exists(basePath + ".wav") ||
        File.Exists(basePath + ".json") ||
        File.Exists(basePath + ".grabando.wav");
}

public sealed class RecordingPaths
{
    public RecordingPaths(string basePathWithoutExtension) => BasePath = basePathWithoutExtension;

    public string BasePath { get; }
    public string Mp3Path => BasePath + ".mp3";
    public string WavPath => BasePath + ".wav";
    public string JsonPath => BasePath + ".json";
    public string TempWavPath => BasePath + ".grabando.wav";
}

public sealed class RecordingSidecar
{
    public string StartedAt { get; set; } = "";
    public string EndedAt { get; set; } = "";
    public int DurationSec { get; set; }
    public string Path { get; set; } = "";
    public string Machine { get; set; } = "";
    public string AppVersion { get; set; } = "";
}

public static class RecordingSidecarWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static RecordingSidecar Create(
        DateTimeOffset startedAt,
        DateTimeOffset endedAt,
        string audioPath,
        string? machineName = null,
        string? appVersion = null)
    {
        var duration = Math.Max(0, (int)Math.Round((endedAt - startedAt).TotalSeconds));
        return new RecordingSidecar
        {
            StartedAt = startedAt.ToString("o"),
            EndedAt = endedAt.ToString("o"),
            DurationSec = duration,
            Path = audioPath,
            Machine = machineName ?? Environment.MachineName,
            AppVersion = appVersion ?? AppPaths.AppVersion
        };
    }

    public static string Write(RecordingSidecar sidecar, string audioPath)
    {
        var jsonPath = System.IO.Path.ChangeExtension(audioPath, ".json");
        var json = JsonSerializer.Serialize(sidecar, JsonOptions);
        File.WriteAllText(jsonPath, json);
        return jsonPath;
    }
}

public sealed class RecordingReadyPayload
{
    [JsonPropertyName("event")]
    public string Event { get; set; } = "recording.ready";

    [JsonPropertyName("path")]
    public string Path { get; set; } = "";

    [JsonPropertyName("sidecar")]
    public string Sidecar { get; set; } = "";

    [JsonPropertyName("endedAt")]
    public string EndedAt { get; set; } = "";

    [JsonPropertyName("durationSec")]
    public int DurationSec { get; set; }
}
