using NAudio.MediaFoundation;
using NAudio.Wave;

namespace GrabadorReuniones;

internal static class Mp3Encoder
{
    private static readonly object Gate = new();
    private static bool _started;

    public static bool TryEncode(string wavPath, string mp3Path, out string? error, int bitrateKbps = 64)
    {
        error = null;
        try
        {
            EnsureStarted();
            using var reader = new WaveFileReader(wavPath);
            var bps = (bitrateKbps > 0 ? bitrateKbps : 64) * 1000;
            MediaFoundationEncoder.EncodeToMp3(reader, mp3Path, bps);
            return File.Exists(mp3Path) && new FileInfo(mp3Path).Length > 0;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            try
            {
                if (File.Exists(mp3Path))
                    File.Delete(mp3Path);
            }
            catch
            {
                // ignore
            }

            return false;
        }
    }

    private static void EnsureStarted()
    {
        lock (Gate)
        {
            if (_started)
                return;
            MediaFoundationApi.Startup();
            _started = true;
        }
    }
}
