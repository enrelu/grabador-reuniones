using System.Diagnostics;
using System.Text.RegularExpressions;

namespace GrabadorReuniones.Core;

public sealed record MeetingDetectionInfo(string AppName, string MeetingTitle, string WindowTitle);

public sealed class MeetingDetector : IDisposable
{
    private readonly System.Threading.Timer _timer;
    private readonly object _gate = new();
    private bool _enabled;
    private string _lastDetectedSignature = "";
    private DateTimeOffset _lastAlertTime = DateTimeOffset.MinValue;
    private static readonly TimeSpan AlertCooldown = TimeSpan.FromMinutes(10);

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

    private void CheckForMeetings(object? state)
    {
        lock (_gate)
        {
            if (!_enabled)
                return;
        }

        try
        {
            var detected = ScanActiveMeeting();
            if (detected == null)
                return;

            lock (_gate)
            {
                var sig = $"{detected.AppName}:{detected.MeetingTitle}";
                if (sig == _lastDetectedSignature && DateTimeOffset.Now - _lastAlertTime < AlertCooldown)
                    return;

                _lastDetectedSignature = sig;
                _lastAlertTime = DateTimeOffset.Now;
            }

            MeetingDetected?.Invoke(detected);
        }
        catch
        {
            // Evitar que excepciones en la detección afecten a la app
        }
    }

    public static MeetingDetectionInfo? ScanActiveMeeting()
    {
        // 1. Detección en navegadores (Google Meet / Teams Web / Zoom Web)
        var browserNames = new[] { "chrome", "msedge", "firefox", "brave", "opera" };
        foreach (var browser in browserNames)
        {
            Process[] processes;
            try { processes = Process.GetProcessesByName(browser); }
            catch { continue; }

            foreach (var proc in processes)
            {
                try
                {
                    var title = proc.MainWindowTitle;
                    if (string.IsNullOrWhiteSpace(title))
                        continue;

                    // Patrón de Google Meet: "Meet – abc-defg-hij" o "Meet - Reunión..."
                    if (title.Contains("Meet –") || title.Contains("Meet -") || title.Contains("Google Meet"))
                    {
                        var meetingName = ExtractMeetTitle(title);
                        return new MeetingDetectionInfo("Google Meet", meetingName, title);
                    }

                    // Teams Web en navegador
                    if (title.Contains("Microsoft Teams") && (title.Contains("Reunión") || title.Contains("Meeting")))
                    {
                        return new MeetingDetectionInfo("Microsoft Teams (Web)", "Reunión de Teams", title);
                    }
                }
                catch
                {
                    // Ignorar procesos sin permisos de lectura de título
                }
                finally
                {
                    proc.Dispose();
                }
            }
        }

        // 2. Detección de Microsoft Teams de escritorio
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
                    // Si la ventana tiene título de reunión activa
                    if (title.Contains("Reunión") || title.Contains("Meeting") || title.Contains("| Microsoft Teams"))
                    {
                        var name = title.Replace("| Microsoft Teams", "").Trim();
                        if (string.IsNullOrWhiteSpace(name)) name = "Reunión de Teams";
                        return new MeetingDetectionInfo("Microsoft Teams", name, title);
                    }
                }
            }
            catch { }
            finally { proc.Dispose(); }
        }

        // 3. Detección de Zoom
        var zoomProcs = Process.GetProcessesByName("Zoom");
        foreach (var proc in zoomProcs)
        {
            try
            {
                var title = proc.MainWindowTitle;
                if (!string.IsNullOrWhiteSpace(title) &&
                    (title.Contains("Zoom Meeting") || title.Contains("Reunión de Zoom") || title.Contains("Zoom")))
                {
                    return new MeetingDetectionInfo("Zoom", "Reunión de Zoom", title);
                }
            }
            catch { }
            finally { proc.Dispose(); }
        }

        // 4. Webex
        var webexProcs = Process.GetProcessesByName("CiscoCollabHost").Concat(Process.GetProcessesByName("webex"));
        foreach (var proc in webexProcs)
        {
            try
            {
                var title = proc.MainWindowTitle;
                if (!string.IsNullOrWhiteSpace(title))
                {
                    return new MeetingDetectionInfo("Webex", "Reunión de Webex", title);
                }
            }
            catch { }
            finally { proc.Dispose(); }
        }

        return null;
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
