using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace GrabadorReuniones;

internal sealed record AudioDeviceInfo(string Id, string Name, bool IsDefault)
{
    public override string ToString() => IsDefault ? $"{Name} (predeterminado)" : Name;
}

internal static class AudioDeviceCatalog
{
    public static IReadOnlyList<AudioDeviceInfo> ListCaptureDevices()
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            var defaultId = SafeDefaultId(enumerator, DataFlow.Capture, Role.Communications);
            return List(enumerator, DataFlow.Capture, defaultId);
        }
        catch
        {
            return Array.Empty<AudioDeviceInfo>();
        }
    }

    public static IReadOnlyList<AudioDeviceInfo> ListRenderDevices()
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            var defaultId = SafeDefaultId(enumerator, DataFlow.Render, Role.Multimedia);
            return List(enumerator, DataFlow.Render, defaultId);
        }
        catch
        {
            return Array.Empty<AudioDeviceInfo>();
        }
    }

    public static MMDevice? ResolveCapture(MMDeviceEnumerator enumerator, string? deviceId) =>
        Resolve(enumerator, deviceId, DataFlow.Capture, Role.Communications);

    public static MMDevice? ResolveRender(MMDeviceEnumerator enumerator, string? deviceId) =>
        Resolve(enumerator, deviceId, DataFlow.Render, Role.Multimedia);

    private static MMDevice? Resolve(MMDeviceEnumerator enumerator, string? deviceId, DataFlow flow, Role role)
    {
        if (!string.IsNullOrWhiteSpace(deviceId))
        {
            try
            {
                return enumerator.GetDevice(deviceId);
            }
            catch
            {
                // Dispositivo desconectado: caer al predeterminado.
            }
        }

        try
        {
            return enumerator.GetDefaultAudioEndpoint(flow, role);
        }
        catch
        {
            return null;
        }
    }

    private static string? SafeDefaultId(MMDeviceEnumerator enumerator, DataFlow flow, Role role)
    {
        try
        {
            using var d = enumerator.GetDefaultAudioEndpoint(flow, role);
            return d.ID;
        }
        catch
        {
            return null;
        }
    }

    private static List<AudioDeviceInfo> List(MMDeviceEnumerator enumerator, DataFlow flow, string? defaultId)
    {
        var list = new List<AudioDeviceInfo>();
        foreach (var device in enumerator.EnumerateAudioEndPoints(flow, DeviceState.Active))
        {
            try
            {
                list.Add(new AudioDeviceInfo(device.ID, device.FriendlyName, device.ID == defaultId));
            }
            finally
            {
                device.Dispose();
            }
        }

        return list.OrderByDescending(d => d.IsDefault).ThenBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }
}
