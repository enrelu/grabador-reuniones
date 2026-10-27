using System.Net;
using System.Text;
using System.Text.Json;
using GrabadorReuniones.Core;
using Xunit;

namespace GrabadorReuniones.Core.Tests;

public class RecordingFileNamesTests
{
    [Fact]
    public void MakeBaseName_UsesLocalTimestampPattern()
    {
        var t = new DateTimeOffset(2026, 9, 15, 9, 7, 8, TimeSpan.FromHours(2));
        Assert.Equal("2026-09-15_090708_reunion", RecordingFileNames.MakeBaseName(t));
    }

    [Fact]
    public void AllocatePaths_AvoidsCollisions()
    {
        var dir = Directory.CreateTempSubdirectory("grabador-test-");
        try
        {
            var started = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
            var first = RecordingFileNames.AllocatePaths(dir.FullName, started);
            File.WriteAllText(first.Mp3Path, "x");
            var second = RecordingFileNames.AllocatePaths(dir.FullName, started);
            Assert.NotEqual(first.BasePath, second.BasePath);
            Assert.EndsWith("_reunion_2", second.BasePath);
            Assert.Equal(first.BasePath + ".grabando.wav", first.TempWavPath);
        }
        finally
        {
            dir.Delete(true);
        }
    }
}

public class RecordingSidecarTests
{
    [Fact]
    public void Writer_CreatesExpectedFieldsAndFile()
    {
        var dir = Directory.CreateTempSubdirectory("grabador-side-");
        try
        {
            var audio = Path.Combine(dir.FullName, "2026-09-15_100000_reunion.mp3");
            File.WriteAllText(audio, "audio");
            var started = new DateTimeOffset(2026, 9, 15, 10, 0, 0, TimeSpan.FromHours(2));
            var ended = started.AddSeconds(320);
            var sidecar = RecordingSidecarWriter.Create(started, ended, audio, "PC-TEST", "2.0.0");
            var jsonPath = RecordingSidecarWriter.Write(sidecar, audio);

            Assert.Equal(Path.ChangeExtension(audio, ".json"), jsonPath);
            Assert.Equal(320, sidecar.DurationSec);
            Assert.Equal(audio, sidecar.Path);
            Assert.Equal("PC-TEST", sidecar.Machine);
            Assert.Equal("2.0.0", sidecar.AppVersion);

            using var doc = JsonDocument.Parse(File.ReadAllText(jsonPath));
            var root = doc.RootElement;
            Assert.True(root.TryGetProperty("startedAt", out _));
            Assert.True(root.TryGetProperty("endedAt", out _));
            Assert.True(root.TryGetProperty("durationSec", out _));
            Assert.True(root.TryGetProperty("path", out _));
            Assert.True(root.TryGetProperty("machine", out _));
            Assert.True(root.TryGetProperty("appVersion", out _));
            Assert.Contains("+02:00", sidecar.StartedAt);
        }
        finally
        {
            dir.Delete(true);
        }
    }
}

public class SettingsStoreTests
{
    [Fact]
    public void Roundtrip_PersistsValuesAndProtectsTokens()
    {
        var dir = Directory.CreateTempSubdirectory("grabador-set-");
        try
        {
            var store = new SettingsStore(dir.FullName, new PrefixProtector());
            var settings = new AppSettings
            {
                OutputFolder = @"C:\MisGrabaciones",
                MicrophoneDeviceId = "mic-1",
                LoopbackDeviceId = "spk-1",
                Mp3BitrateKbps = 96,
                EnableAutoMeetingDetection = true,
                AutoDetectPromptPreset = "comercial",
                TranscriptionProvider = "Groq",
                TranscriptionApiKey = "groq-key-secret",
                LlmExecutionMode = "CloudApi",
                LlmProvider = "Gemini",
                LlmApiKey = "gemini-key-secret",
                ActivePromptPresetId = "comercial",
                WebhookUrl = "https://bot.example/hook",
                WebhookBearerToken = "super-secret",
                Hotkey = "Ctrl+Alt+F9",
                StartWithWindows = true,
                HasCompletedFirstRun = true
            };
            store.Save(settings);

            var onDisk = File.ReadAllText(Path.Combine(dir.FullName, "settings.json"));
            Assert.DoesNotContain("\"webhookBearerToken\"", onDisk);
            Assert.DoesNotContain("\"transcriptionApiKey\"", onDisk);
            Assert.DoesNotContain("\"llmApiKey\"", onDisk);
            Assert.Contains("\"webhookTokenProtected\": \"P:super-secret\"", onDisk);
            Assert.Contains("\"transcriptionTokenProtected\": \"P:groq-key-secret\"", onDisk);
            Assert.Contains("\"llmTokenProtected\": \"P:gemini-key-secret\"", onDisk);

            var loaded = store.Load();
            Assert.Equal(settings.OutputFolder, loaded.OutputFolder);
            Assert.Equal(96, loaded.Mp3BitrateKbps);
            Assert.Equal("comercial", loaded.AutoDetectPromptPreset);
            Assert.Equal("Groq", loaded.TranscriptionProvider);
            Assert.Equal("groq-key-secret", loaded.TranscriptionApiKey);
            Assert.Equal("Gemini", loaded.LlmProvider);
            Assert.Equal("gemini-key-secret", loaded.LlmApiKey);
            Assert.Equal("super-secret", loaded.WebhookBearerToken);
            Assert.Equal("Ctrl+Alt+F9", loaded.Hotkey);
            Assert.True(loaded.StartWithWindows);
            Assert.True(loaded.HasCompletedFirstRun);
        }
        finally
        {
            dir.Delete(true);
        }
    }

    [Fact]
    public void Load_MissingFile_ReturnsDefaults()
    {
        var dir = Directory.CreateTempSubdirectory("grabador-def-");
        try
        {
            var loaded = new SettingsStore(dir.FullName, new NullSecretProtector()).Load();
            Assert.Equal("Ctrl+Shift+R", loaded.Hotkey);
            Assert.False(loaded.StartWithWindows);
            Assert.Contains("GrabadorReunionesIA", loaded.OutputFolder);
            Assert.Contains("Grabaciones", loaded.OutputFolder);
            Assert.Equal("Groq", loaded.TranscriptionProvider);
            Assert.Equal("Gemini", loaded.LlmProvider);
            Assert.True(loaded.EnableAutoMeetingDetection);
        }
        finally
        {
            dir.Delete(true);
        }
    }

    private sealed class PrefixProtector : ISecretProtector
    {
        public string Protect(string plaintext) => string.IsNullOrEmpty(plaintext) ? "" : "P:" + plaintext;
        public string Unprotect(string protectedValue) =>
            protectedValue.StartsWith("P:", StringComparison.Ordinal) ? protectedValue[2..] : protectedValue;
    }
}

public class PromptPresetTests
{
    [Fact]
    public void PromptPresetStore_LoadsBuiltinPresets()
    {
        var dir = Directory.CreateTempSubdirectory("grabador-presets-");
        try
        {
            var store = new PromptPresetStore(dir.FullName);
            var presets = store.GetAllPresets();

            Assert.True(presets.Count >= 6);
            Assert.Contains(presets, p => p.Id == "general");
            Assert.Contains(presets, p => p.Id == "comercial");
            Assert.Contains(presets, p => p.Id == "daily");
            Assert.Contains(presets, p => p.Id == "tecnica");
            Assert.Contains(presets, p => p.Id == "one-on-one");
            Assert.Contains(presets, p => p.Id == "brainstorming");
        }
        finally
        {
            dir.Delete(true);
        }
    }

    [Fact]
    public void PromptPresetStore_CanSaveAndGetCustomPreset()
    {
        var dir = Directory.CreateTempSubdirectory("grabador-presets-");
        try
        {
            var store = new PromptPresetStore(dir.FullName);
            var custom = new PromptPreset
            {
                Id = "custom-test",
                Name = "Mi Preset Test",
                Description = "Descripción test",
                Icon = "⭐",
                SystemPrompt = "Prompt test",
                IsBuiltIn = false
            };

            store.SavePreset(custom);
            var loaded = store.GetPresetOrDefault("custom-test");

            Assert.Equal("custom-test", loaded.Id);
            Assert.Equal("Mi Preset Test", loaded.Name);
            Assert.Equal("Prompt test", loaded.SystemPrompt);
        }
        finally
        {
            dir.Delete(true);
        }
    }
}

public class WebhookNotifierTests
{
    [Fact]
    public void Payload_SerializesEventName()
    {
        var payload = new RecordingReadyPayload
        {
            Path = @"C:\a\b.mp3",
            Sidecar = @"C:\a\b.json",
            EndedAt = "2026-09-15T10:00:00+02:00",
            DurationSec = 12
        };
        var json = JsonSerializer.Serialize(payload);
        Assert.Contains("\"event\":\"recording.ready\"", json);
        Assert.Contains("\"path\":", json);
        Assert.Contains("\"sidecar\":", json);
        Assert.Contains("\"endedAt\":", json);
        Assert.Contains("\"durationSec\":12", json);
    }

    [Fact]
    public async Task EmptyUrl_DoesNotAttemptRequest()
    {
        var handler = new RecordingHandler();
        using var http = new HttpClient(handler);
        var notifier = new WebhookNotifier(http);
        var result = await notifier.NotifyRecordingReadyAsync("", "token", new RecordingReadyPayload());
        Assert.False(result.Attempted);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task PostsJsonWithBearerAndDoesNotFailCallerOnHttpError()
    {
        var handler = new RecordingHandler { Status = HttpStatusCode.InternalServerError, Body = "nope" };
        using var http = new HttpClient(handler);
        var notifier = new WebhookNotifier(http);
        var result = await notifier.NotifyRecordingReadyAsync(
            "https://bot.example/wake",
            "abc123",
            new RecordingReadyPayload { Path = @"C:\f.mp3", Sidecar = @"C:\f.json", DurationSec = 3, EndedAt = "t" });

        Assert.True(result.Attempted);
        Assert.False(result.Succeeded);
        Assert.NotNull(result.Error);
        Assert.Equal(1, handler.Calls);
        Assert.Equal(HttpMethod.Post, handler.LastMethod);
        Assert.Equal("Bearer", handler.LastAuthScheme);
        Assert.Equal("abc123", handler.LastAuthValue);
        Assert.Contains("recording.ready", handler.LastBody);
    }

    [Fact]
    public async Task Success_ReturnsSucceeded()
    {
        var handler = new RecordingHandler { Status = HttpStatusCode.NoContent };
        using var http = new HttpClient(handler);
        var notifier = new WebhookNotifier(http);
        var result = await notifier.NotifyRecordingReadyAsync(
            "https://bot.example/wake",
            null,
            new RecordingReadyPayload { Path = "p" });
        Assert.True(result.Succeeded);
        Assert.Null(handler.LastAuthScheme);
    }

    [Fact]
    public void InvalidUrl_IsRejected()
    {
        Assert.False(WebhookNotifier.TryGetWebhookUri("not-a-url", out _, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
        public string Body { get; set; } = "";
        public int Calls { get; private set; }
        public HttpMethod? LastMethod { get; private set; }
        public string LastBody { get; private set; } = "";
        public string? LastAuthScheme { get; private set; }
        public string? LastAuthValue { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            LastMethod = request.Method;
            LastBody = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            LastAuthScheme = request.Headers.Authorization?.Scheme;
            LastAuthValue = request.Headers.Authorization?.Parameter;
            return new HttpResponseMessage(Status)
            {
                Content = new StringContent(Body, Encoding.UTF8, "application/json")
            };
        }
    }
}
