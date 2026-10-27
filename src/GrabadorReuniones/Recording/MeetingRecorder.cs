using System.Diagnostics;
using GrabadorReuniones.Core;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace GrabadorReuniones;

internal sealed class RawRecordingResult
{
    public required string WavPath { get; init; }
    public required DateTimeOffset StartedAt { get; init; }
    public required DateTimeOffset EndedAt { get; init; }
    public required bool RecordedMicrophone { get; init; }
    public required bool RecordedLoopback { get; init; }

    public RecordingPaths Paths
    {
        get
        {
            const string suffix = ".grabando.wav";
            var basePath = WavPath.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
                ? WavPath[..^suffix.Length]
                : Path.Combine(
                    Path.GetDirectoryName(WavPath) ?? "",
                    Path.GetFileNameWithoutExtension(WavPath));
            return new RecordingPaths(basePath);
        }
    }
}

internal sealed class MeetingRecorder : IDisposable
{
    private readonly object _gate = new();
    private WasapiCapture? _mic;
    private WasapiLoopbackCapture? _loopback;
    private MMDevice? _micDevice;
    private MMDevice? _renderDevice;
    private BufferedWaveProvider? _micBuffer;
    private BufferedWaveProvider? _loopBuffer;
    private WaveFileWriter? _writer;
    private IWaveProvider? _pcm16;
    private Thread? _writeThread;
    private volatile bool _running;
    private Stopwatch? _clock;
    private DateTimeOffset _startedAt;
    private string? _wavPath;
    private bool _recordedMic;
    private bool _recordedLoopback;

    public bool IsRecording
    {
        get { lock (_gate) return _running; }
    }

    public bool HasMicrophone { get; private set; }
    public bool HasLoopback { get; private set; }

    public TimeSpan Elapsed => _clock?.Elapsed ?? TimeSpan.Zero;

    /// <summary>Inicia micrófono y/o loopback. Lanza si no hay ninguna fuente.</summary>
    public void Start(AppSettings settings)
    {
        lock (_gate)
        {
            if (_running)
                throw new InvalidOperationException("Ya hay una grabación en curso.");

            Directory.CreateDirectory(settings.OutputFolder);
            _startedAt = DateTimeOffset.Now;
            var paths = RecordingFileNames.AllocatePaths(settings.OutputFolder, _startedAt);
            _wavPath = paths.TempWavPath;
            _recordedMic = false;
            _recordedLoopback = false;
            HasMicrophone = false;
            HasLoopback = false;

            var mixFormat = WaveFormat.CreateIeeeFloatWaveFormat(CaptureSampleChain.MixSampleRate, 2);
            var mixer = new MixingSampleProvider(mixFormat) { ReadFully = true };

            var enumerator = new MMDeviceEnumerator();
            try
            {
                var micDevice = AudioDeviceCatalog.ResolveCapture(enumerator, settings.MicrophoneDeviceId);
                if (micDevice is not null)
                {
                    try
                    {
                        _micDevice = micDevice;
                        _mic = new WasapiCapture(micDevice) { ShareMode = AudioClientShareMode.Shared };
                        var micBuffer = CreateBuffer(_mic.WaveFormat);
                        _micBuffer = micBuffer;
                        _mic.DataAvailable += (_, e) =>
                        {
                            if (e.BytesRecorded > 0)
                                micBuffer.AddSamples(e.Buffer, 0, e.BytesRecorded);
                        };
                        mixer.AddMixerInput(CaptureSampleChain.Build(micBuffer));
                        _recordedMic = true;
                        HasMicrophone = true;
                    }
                    catch
                    {
                        _mic?.Dispose();
                        _mic = null;
                        _micBuffer = null;
                        _micDevice?.Dispose();
                        _micDevice = null;
                    }
                }

                var renderDevice = AudioDeviceCatalog.ResolveRender(enumerator, settings.LoopbackDeviceId);
                if (renderDevice is not null)
                {
                    try
                    {
                        _renderDevice = renderDevice;
                        _loopback = new WasapiLoopbackCapture(renderDevice) { ShareMode = AudioClientShareMode.Shared };
                        var loopBuffer = CreateBuffer(_loopback.WaveFormat);
                        _loopBuffer = loopBuffer;
                        _loopback.DataAvailable += (_, e) =>
                        {
                            if (e.BytesRecorded > 0)
                                loopBuffer.AddSamples(e.Buffer, 0, e.BytesRecorded);
                        };
                        mixer.AddMixerInput(CaptureSampleChain.Build(loopBuffer));
                        _recordedLoopback = true;
                        HasLoopback = true;
                    }
                    catch
                    {
                        _loopback?.Dispose();
                        _loopback = null;
                        _loopBuffer = null;
                        _renderDevice?.Dispose();
                        _renderDevice = null;
                    }
                }
            }
            finally
            {
                enumerator.Dispose();
            }

            if (!_recordedMic && !_recordedLoopback)
            {
                CleanupUnlocked();
                throw new InvalidOperationException(
                    "No se encontró micrófono ni dispositivo de reproducción. Revise los permisos de audio de Windows.");
            }

            _pcm16 = new SampleToWaveProvider16(new ClampSampleProvider(mixer));
            _writer = new WaveFileWriter(_wavPath, _pcm16.WaveFormat);
            _running = true;
            _clock = Stopwatch.StartNew();
            NativePower.PreventSleep();

            try
            {
                _mic?.StartRecording();
                _loopback?.StartRecording();
            }
            catch (Exception)
            {
                CleanupUnlocked();
                throw;
            }

            _writeThread = new Thread(WriteLoop)
            {
                IsBackground = true,
                Name = "AudioWriter"
            };
            _writeThread.Start();
        }
    }

    public RawRecordingResult Stop()
    {
        lock (_gate)
        {
            if (!_running)
                throw new InvalidOperationException("No hay una grabación en curso.");

            _running = false;
        }

        try { _mic?.StopRecording(); } catch { /* ignore */ }
        try { _loopback?.StopRecording(); } catch { /* ignore */ }

        _writeThread?.Join(TimeSpan.FromSeconds(8));

        lock (_gate)
        {
            var endedAt = DateTimeOffset.Now;
            var wavPath = _wavPath ?? throw new InvalidOperationException("Falta la ruta temporal.");
            var startedAt = _startedAt;
            var mic = _recordedMic;
            var loop = _recordedLoopback;
            CleanupUnlocked();
            return new RawRecordingResult
            {
                WavPath = wavPath,
                StartedAt = startedAt,
                EndedAt = endedAt,
                RecordedMicrophone = mic,
                RecordedLoopback = loop
            };
        }
    }

    public void Dispose()
    {
        if (IsRecording)
        {
            try { Stop(); } catch { /* ignore */ }
        }

        lock (_gate)
            CleanupUnlocked();
    }

    private void WriteLoop()
    {
        var pcm = _pcm16;
        var writer = _writer;
        if (pcm is null || writer is null)
            return;

        var format = pcm.WaveFormat;
        var buffer = new byte[Math.Max(format.AverageBytesPerSecond / 10, format.BlockAlign * 64)];
        var sw = Stopwatch.StartNew();
        long bytesWritten = 0;

        while (_running)
        {
            var targetBytes = (long)(sw.Elapsed.TotalSeconds * format.AverageBytesPerSecond);
            var due = (int)Math.Min(buffer.Length, Math.Max(0, targetBytes - bytesWritten));
            due -= due % format.BlockAlign;
            if (due < format.BlockAlign * 48)
            {
                Thread.Sleep(15);
                continue;
            }

            int read;
            try
            {
                read = pcm.Read(buffer, 0, due);
            }
            catch
            {
                break;
            }

            if (read > 0)
            {
                lock (writer)
                    writer.Write(buffer, 0, read);
                bytesWritten += read;
            }
            else
            {
                Thread.Sleep(15);
            }
        }

        try
        {
            var drain = Math.Min(buffer.Length, format.AverageBytesPerSecond / 5);
            drain -= drain % format.BlockAlign;
            if (drain > 0)
            {
                var read = pcm.Read(buffer, 0, drain);
                if (read > 0)
                {
                    lock (writer)
                        writer.Write(buffer, 0, read);
                }
            }
        }
        catch
        {
            // ignore drain errors
        }

        try
        {
            lock (writer)
                writer.Flush();
        }
        catch
        {
            // ignore
        }
    }

    private void CleanupUnlocked()
    {
        _running = false;
        NativePower.AllowSleep();
        _clock?.Stop();

        try { _mic?.Dispose(); } catch { /* ignore */ }
        try { _loopback?.Dispose(); } catch { /* ignore */ }
        try { _micDevice?.Dispose(); } catch { /* ignore */ }
        try { _renderDevice?.Dispose(); } catch { /* ignore */ }
        try { _writer?.Dispose(); } catch { /* ignore */ }

        _mic = null;
        _loopback = null;
        _micDevice = null;
        _renderDevice = null;
        _micBuffer = null;
        _loopBuffer = null;
        _writer = null;
        _pcm16 = null;
        _writeThread = null;
        _clock = null;
        _wavPath = null;
        HasMicrophone = false;
        HasLoopback = false;
    }

    private static BufferedWaveProvider CreateBuffer(WaveFormat format) =>
        new(format)
        {
            DiscardOnBufferOverflow = true,
            BufferDuration = TimeSpan.FromSeconds(8),
            ReadFully = false
        };
}

internal static class NativePower
{
    private const uint EsContinuous = 0x80000000u;
    private const uint EsSystemRequired = 0x00000001;
    private const uint EsAwaymodeRequired = 0x00000040;

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern uint SetThreadExecutionState(uint esFlags);

    public static void PreventSleep() =>
        SetThreadExecutionState(EsContinuous | EsSystemRequired | EsAwaymodeRequired);

    public static void AllowSleep() =>
        SetThreadExecutionState(EsContinuous);
}
