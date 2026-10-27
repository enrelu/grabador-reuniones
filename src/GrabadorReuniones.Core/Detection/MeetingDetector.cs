using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;

namespace GrabadorReuniones.Core;

public sealed record MeetingDetectionInfo(string AppName, string MeetingTitle, string WindowTitle);

public sealed class MeetingDetector : IDisposable
{
    private readonly System.Threading.Timer _timer;
    private readonly object _gate = new();
    private bool _enabled;
    private readonly Dictionary<string, DateTimeOffset> _lastAlertsByApp = new(StringComparer.OrdinalIgnoreCase);
    private static readonly TimeSpan AlertCooldownPerApp = TimeSpan.FromMinutes(25);

    private static readonly string[] NonMeetingPrefixes = new[]
    {
        "Chat |", "Equipos |", "Teams |", "Calendario |", "Calendar |",
        "Actividad |", "Activity |", "Archivos |", "Files |",
        "Llamadas |", "Calls |", "Configuración", "Settings"
    };

    public event Action<MeetingDetectionInfo>? MeetingDetected;

    public bool IsEnabled
    {
        get { lock (_gate) return _enabled; }
        set { lock (_gate) _enabled = value; }
    }

    public MeetingDetector(bool enabled = true)
    {
        _enabled = enabled;
        _timer = new System.Threading.Timer(CheckForMeetings, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(4));
    }

    public void DismissAppAlert(string appName, TimeSpan? suppressionDuration = null)
    {
        lock (_gate)
        {
            var duration = suppressionDuration ?? TimeSpan.FromMinutes(30);
            _lastAlertsByApp[appName] = DateTimeOffset.Now - AlertCooldownPerApp + duration;
        }
    }

    private void CheckForMeetings(object? state)
    {
        lock (_gate)
        {
            if (!_enabled)
                return;
        }

        try
        {
            if (!OperatingSystem.IsWindows())
                return;

            var detected = ScanActiveMeeting();
            if (detected == null)
                return;

            lock (_gate)
            {
                if (_lastAlertsByApp.TryGetValue(detected.AppName, out var lastTime))
                {
                    if (DateTimeOffset.Now - lastTime < AlertCooldownPerApp)
                        return;
                }

                _lastAlertsByApp[detected.AppName] = DateTimeOffset.Now;
            }

            MeetingDetected?.Invoke(detected);
        }
        catch
        {
            // Evitar que excepciones en la detección afecten a la app
        }
    }

    [SupportedOSPlatform("windows")]
    public static MeetingDetectionInfo? ScanActiveMeeting()
    {
        // 1. Detección de Microsoft Teams de escritorio (New Teams ms-teams y Teams clásico)
        var teamsMicActive = MicrophoneStatus.IsApplicationUsingMicrophone("Teams") ||
                             MicrophoneStatus.IsApplicationUsingMicrophone("MSTeams");

        var teamsProcs = Process.GetProcessesByName("ms-teams")
            .Concat(Process.GetProcessesByName("Teams"))
            .ToArray();

        foreach (var proc in teamsProcs)
        {
            try
            {
                var title = proc.MainWindowTitle;
                if (!string.IsNullOrWhiteSpace(title))
                {
                    if (IsTeamsMeeting(title, teamsMicActive))
                    {
                        var name = CleanTeamsTitle(title);
                        return new MeetingDetectionInfo("Microsoft Teams", name, title);
                    }
                }
            }
            catch { }
            finally { proc.Dispose(); }
        }

        // 2. Detección en navegadores (Google Meet / Teams Web / Zoom Web)
        var browserNames = new[] { "chrome", "msedge", "firefox", "brave", "opera" };
        foreach (var browser in browserNames)
        {
            Process[] processes;
            try { processes = Process.GetProcessesByName(browser); }
            catch { continue; }

            bool micInBrowser = MicrophoneStatus.IsApplicationUsingMicrophone(browser);

            foreach (var proc in processes)
            {
                try
                {
                    var title = proc.MainWindowTitle;
                    if (string.IsNullOrWhiteSpace(title))
                        continue;

                    // Patrón de Google Meet: "Meet – abc-defg-hij" o "Meet - Reunión..."
                    if (title.Contains("Meet –") || title.Contains("Meet -") || (title.Contains("Google Meet") && micInBrowser))
                    {
                        var meetingName = ExtractMeetTitle(title);
                        // Solo alertar si el micro está activo o si el título contiene el código específico de reunión
                        if (micInBrowser || meetingName != "Google Meet")
                        {
                            return new MeetingDetectionInfo("Google Meet", meetingName, title);
                        }
                    }

                    // Teams Web en navegador
                    if (title.Contains("Microsoft Teams") && (title.Contains("Reunión") || title.Contains("Meeting")) && micInBrowser)
                    {
                        return new MeetingDetectionInfo("Microsoft Teams (Web)", "Reunión de Teams", title);
                    }
                }
                catch { }
                finally { proc.Dispose(); }
            }
        }

        // 3. Detección de Zoom
        var zoomProcs = Process.GetProcessesByName("Zoom");
        bool zoomMic = MicrophoneStatus.IsApplicationUsingMicrophone("Zoom");
        foreach (var proc in zoomProcs)
        {
            try
            {
                var title = proc.MainWindowTitle;
                if (!string.IsNullOrWhiteSpace(title))
                {
                    if (title.Contains("Zoom Meeting") || title.Contains("Reunión de Zoom") ||
                        title.Contains("Zoom Webinar") || (zoomMic && !title.Equals("Zoom", StringComparison.OrdinalIgnoreCase)))
                    {
                        return new MeetingDetectionInfo("Zoom", "Reunión de Zoom", title);
                    }
                }
            }
            catch { }
            finally { proc.Dispose(); }
        }

        // 4. Webex
        var webexProcs = Process.GetProcessesByName("CiscoCollabHost").Concat(Process.GetProcessesByName("webex"));
        bool webexMic = MicrophoneStatus.IsApplicationUsingMicrophone("webex") || MicrophoneStatus.IsApplicationUsingMicrophone("CiscoCollabHost");
        foreach (var proc in webexProcs)
        {
            try
            {
                var title = proc.MainWindowTitle;
                if (!string.IsNullOrWhiteSpace(title) && webexMic)
                {
                    return new MeetingDetectionInfo("Webex", "Reunión de Webex", title);
                }
            }
            catch { }
            finally { proc.Dispose(); }
        }

        return null;
    }

    private static bool IsTeamsMeeting(string title, bool micActive)
    {
        // 1. Títulos inequívocos de reunión/llamada activa
        if (title.StartsWith("Reunión", StringComparison.OrdinalIgnoreCase) ||
            title.StartsWith("Meeting", StringComparison.OrdinalIgnoreCase) ||
            title.Contains("Reunión |", StringComparison.OrdinalIgnoreCase) ||
            title.Contains("Meeting |", StringComparison.OrdinalIgnoreCase) ||
            title.Contains("Llamada con", StringComparison.OrdinalIgnoreCase) ||
            title.Contains("Call with", StringComparison.OrdinalIgnoreCase) ||
            title.Contains("[Llamada]", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // 2. Si Teams está usando el micrófono de forma activa:
        if (micActive && title.Contains("| Microsoft Teams"))
        {
            // Descartar si el título es explícitamente una pestaña normal de navegación
            foreach (var prefix in NonMeetingPrefixes)
            {
                if (title.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return true;
        }

        return false;
    }

    private static string CleanTeamsTitle(string title)
    {
        var cleaned = title.Replace("| Microsoft Teams", "").Trim();
        if (string.IsNullOrWhiteSpace(cleaned) || cleaned.Equals("Microsoft Teams", StringComparison.OrdinalIgnoreCase))
            return "Reunión de Teams";
        return cleaned;
    }

    private static string ExtractMeetTitle(string windowTitle)
    {
        // Ejemplos: "Meet – abc-defg-hij - Google Chrome" -> "abc-defg-hij"
        var match = Regex.Match(windowTitle, @"Meet\s*[\u2013-]\s*([^–-]+)");
        if (match.Success)
        {
            return match.Groups[1].Value.Trim();
        }

        return "Google Meet";
    }

    public void Dispose()
    {
        _timer.Dispose();
    }
}
