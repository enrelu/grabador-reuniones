using System.Runtime.Versioning;
using Microsoft.Win32;

namespace GrabadorReuniones.Core;

[SupportedOSPlatform("windows")]
public static class MicrophoneStatus
{
    private const string MicConsentPath = @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone";

    /// <summary>
    /// Comprueba si alguna aplicación cuyo nombre o ruta coincida con 'appKeyword'
    /// está usando el micrófono de forma activa en Windows 10/11.
    /// </summary>
    public static bool IsApplicationUsingMicrophone(string appKeyword)
    {
        if (!OperatingSystem.IsWindows())
            return false;

        try
        {
            using var rootKey = Registry.CurrentUser.OpenSubKey(MicConsentPath);
            if (rootKey == null)
                return false;

            // 1. Aplicaciones empaquetadas (MSIX / Store / UWP, como New Teams 'MSTeams_8wekyb3d8bbwe')
            foreach (var subName in rootKey.GetSubKeyNames())
            {
                if (subName.Equals("NonPackaged", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (subName.Contains(appKeyword, StringComparison.OrdinalIgnoreCase))
                {
                    using var subKey = rootKey.OpenSubKey(subName);
                    if (subKey != null && IsSubKeyActive(subKey))
                        return true;
                }
            }

            // 2. Aplicaciones clásicas de escritorio Win32 (como Chrome, Teams.exe clásico, Zoom.exe)
            using var nonPackagedKey = rootKey.OpenSubKey("NonPackaged");
            if (nonPackagedKey != null)
            {
                foreach (var subName in nonPackagedKey.GetSubKeyNames())
                {
                    if (subName.Contains(appKeyword, StringComparison.OrdinalIgnoreCase))
                    {
                        using var subKey = nonPackagedKey.OpenSubKey(subName);
                        if (subKey != null && IsSubKeyActive(subKey))
                            return true;
                    }
                }
            }
        }
        catch
        {
            // En caso de error de acceso al registro, no lanzar excepción
        }

        return false;
    }

    /// <summary>
    /// Comprueba si el micrófono está activo en general por alguna aplicación de reuniones o navegadores.
    /// </summary>
    public static bool IsAnyMeetingAppUsingMicrophone()
    {
        if (!OperatingSystem.IsWindows())
            return false;

        var targets = new[] { "Teams", "MSTeams", "chrome", "msedge", "brave", "firefox", "Zoom", "webex" };
        foreach (var t in targets)
        {
            if (IsApplicationUsingMicrophone(t))
                return true;
        }

        return false;
    }

    private static bool IsSubKeyActive(RegistryKey subKey)
    {
        if (!OperatingSystem.IsWindows())
            return false;

        var stopObj = subKey.GetValue("LastUsedTimeStop");
        var startObj = subKey.GetValue("LastUsedTimeStart");

        if (stopObj is long stopTime && startObj is long startTime)
        {
            // LastUsedTimeStop == 0 indica captura activa en curso
            // startTime > stopTime indica que se inició la captura y no se ha parado
            return stopTime == 0 || startTime > stopTime;
        }

        return false;
    }
}
