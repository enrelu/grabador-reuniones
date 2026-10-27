using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace GrabadorReuniones;

/// <summary>
/// Rellena con silencio si la fuente no tiene suficientes muestras.
/// Evita que MixingSampleProvider elimine entradas en vivo al devolver 0.
/// </summary>
internal sealed class AlwaysFullSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;

    public AlwaysFullSampleProvider(ISampleProvider source) => _source = source;

    public WaveFormat WaveFormat => _source.WaveFormat;

    public int Read(float[] buffer, int offset, int count)
    {
        var read = _source.Read(buffer, offset, count);
        if (read < count)
            Array.Clear(buffer, offset + read, count - read);
        return count;
    }
}

internal sealed class ClampSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;

    public ClampSampleProvider(ISampleProvider source) => _source = source;

    public WaveFormat WaveFormat => _source.WaveFormat;

    public int Read(float[] buffer, int offset, int count)
    {
        var read = _source.Read(buffer, offset, count);
        for (var i = 0; i < read; i++)
        {
            var x = buffer[offset + i];
            if (x > 1f) buffer[offset + i] = 1f;
            else if (x < -1f) buffer[offset + i] = -1f;
        }

        return read;
    }
}

internal static class CaptureSampleChain
{
    public const int MixSampleRate = 48000;

    public static ISampleProvider Build(BufferedWaveProvider buffer)
    {
        ISampleProvider samples = buffer.ToSampleProvider();
        if (samples.WaveFormat.Channels == 1)
            samples = new MonoToStereoSampleProvider(samples);
        else if (samples.WaveFormat.Channels > 2)
            samples = new StereoToStereoSampleProvider(samples);

        if (samples.WaveFormat.SampleRate != MixSampleRate)
            samples = new WdlResamplingSampleProvider(samples, MixSampleRate);

        return new AlwaysFullSampleProvider(samples);
    }
}

/// <summary>Descarta canales extra y deja estéreo.</summary>
internal sealed class StereoToStereoSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly int _channels;
    private float[]? _sourceBuffer;

    public StereoToStereoSampleProvider(ISampleProvider source)
    {
        _source = source;
        _channels = source.WaveFormat.Channels;
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(source.WaveFormat.SampleRate, 2);
    }

    public WaveFormat WaveFormat { get; }

    public int Read(float[] buffer, int offset, int count)
    {
        var frames = count / 2;
        var needed = frames * _channels;
        if (_sourceBuffer is null || _sourceBuffer.Length < needed)
            _sourceBuffer = new float[needed];

        var read = _source.Read(_sourceBuffer, 0, needed);
        var framesRead = read / _channels;
        for (var i = 0; i < framesRead; i++)
        {
            buffer[offset + i * 2] = _sourceBuffer[i * _channels];
            buffer[offset + i * 2 + 1] = _channels > 1 ? _sourceBuffer[i * _channels + 1] : _sourceBuffer[i * _channels];
        }

        return framesRead * 2;
    }
}
