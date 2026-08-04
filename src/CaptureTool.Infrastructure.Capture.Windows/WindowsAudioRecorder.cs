using CaptureKit.Abstractions;
using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Domain.FileSystem;

namespace CaptureTool.Infrastructure.Capture.Windows;

public class WindowsAudioRecorder : IAudioRecorder
{
    private readonly IAudioCaptureService _audioCaptureService;
    private IAudioCaptureSession? _session;
    private string? _outputPath;

    public event EventHandler<AudioCaptureLevel>? AudioLevelCaptured;

    public WindowsAudioRecorder(IAudioCaptureService audioCaptureService)
    {
        _audioCaptureService = audioCaptureService;
    }

    public void Pause()
    {
        GetActiveSession().Pause();
    }

    public void Resume()
    {
        GetActiveSession().Resume();
    }

    public void StartCapture(AudioCaptureRecordingOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.OutputPath))
        {
            throw new ArgumentException("Audio output path is required.", nameof(options));
        }

        _outputPath = options.OutputPath;

        var captureOptions = new AudioCaptureOptions(
            options.OutputPath,
            options.CaptureDesktopAudio,
            options.AudioInputSourceId,
            options.AudioInputVolumePercentage);

        try
        {
            _session = _audioCaptureService.CreateSession(captureOptions);
            _session.SampleCaptured += OnSampleCaptured;
            _session.Start();
        }
        catch
        {
            _outputPath = null;
            DisposeSession();
            throw;
        }
    }

    public AudioFile StopCapture()
    {
        if (string.IsNullOrWhiteSpace(_outputPath))
        {
            throw new InvalidOperationException("Cannot stop, no audio is recording.");
        }

        try
        {
            GetActiveSession().Stop();
            return new AudioFile(_outputPath);
        }
        finally
        {
            _outputPath = null;
            DisposeSession();
        }
    }

    public void SetDesktopAudioEnabled(bool enabled)
    {
        GetActiveSession().SetAudioCaptureEnabled(enabled);
    }

    public void SetAudioInputSource(string? sourceId)
    {
        string? normalizedSourceId = string.IsNullOrWhiteSpace(sourceId)
            ? null
            : sourceId;
        GetActiveSession().SetAudioInputSource(normalizedSourceId);
    }

    private IAudioCaptureSession GetActiveSession()
        => _session ?? throw new InvalidOperationException("No audio capture session is active.");

    private void OnSampleCaptured(object? sender, AudioSampleCapturedEventArgs e)
    {
        AudioLevelCaptured?.Invoke(this, AudioLevelCalculator.Calculate(e.SampleData));
    }

    private void DisposeSession()
    {
        if (_session is null)
        {
            return;
        }

        _session.SampleCaptured -= OnSampleCaptured;
        _session.Dispose();
        _session = null;
    }

    private static class AudioLevelCalculator
    {
        public static unsafe AudioCaptureLevel Calculate(AudioSampleData sampleData)
        {
            long sampleCount = (long)sampleData.NumFrames * sampleData.Channels;
            if (sampleData.DataPointer == IntPtr.Zero || sampleCount <= 0)
            {
                return new AudioCaptureLevel(0, 0, sampleData.Timestamp);
            }

            return sampleData.BitsPerSample switch
            {
                8 => CalculateUnsigned8Bit((byte*)sampleData.DataPointer, sampleCount, sampleData.Timestamp),
                16 => CalculateSigned16Bit((short*)sampleData.DataPointer, sampleCount, sampleData.Timestamp),
                24 => CalculateSigned24Bit((byte*)sampleData.DataPointer, sampleCount, sampleData.Timestamp),
                32 => Calculate32Bit(sampleData.DataPointer, sampleCount, sampleData.Timestamp),
                _ => new AudioCaptureLevel(0, 0, sampleData.Timestamp)
            };
        }

        private static unsafe AudioCaptureLevel CalculateUnsigned8Bit(byte* samples, long sampleCount, long timestamp)
        {
            return Calculate(sampleCount, timestamp, index => (samples[index] - 128) / 128d);
        }

        private static unsafe AudioCaptureLevel CalculateSigned16Bit(short* samples, long sampleCount, long timestamp)
        {
            return Calculate(sampleCount, timestamp, index => samples[index] / 32768d);
        }

        private static unsafe AudioCaptureLevel CalculateSigned24Bit(byte* samples, long sampleCount, long timestamp)
        {
            return Calculate(sampleCount, timestamp, index =>
            {
                long offset = index * 3;
                int value = samples[offset] | (samples[offset + 1] << 8) | (samples[offset + 2] << 16);
                if ((value & 0x800000) != 0)
                {
                    value |= unchecked((int)0xFF000000);
                }

                return value / 8388608d;
            });
        }

        private static unsafe AudioCaptureLevel Calculate32Bit(IntPtr dataPointer, long sampleCount, long timestamp)
        {
            float* floatSamples = (float*)dataPointer;
            bool looksLikeFloat = true;
            long probeCount = Math.Min(sampleCount, 16);
            for (long i = 0; i < probeCount; i++)
            {
                float sample = floatSamples[i];
                if (!float.IsFinite(sample) || Math.Abs(sample) > 4)
                {
                    looksLikeFloat = false;
                    break;
                }
            }

            if (looksLikeFloat)
            {
                return Calculate(sampleCount, timestamp, index => floatSamples[index]);
            }

            int* intSamples = (int*)dataPointer;
            return Calculate(sampleCount, timestamp, index => intSamples[index] / 2147483648d);
        }

        private static AudioCaptureLevel Calculate(long sampleCount, long timestamp, Func<long, double> getSample)
        {
            double peak = 0;
            double sumOfSquares = 0;

            for (long i = 0; i < sampleCount; i++)
            {
                double sample = getSample(i);
                if (!double.IsFinite(sample))
                {
                    continue;
                }

                double normalized = Math.Clamp(sample, -1, 1);
                double absolute = Math.Abs(normalized);
                peak = Math.Max(peak, absolute);
                sumOfSquares += normalized * normalized;
            }

            double rms = Math.Sqrt(sumOfSquares / sampleCount);
            return new AudioCaptureLevel(peak, rms, timestamp);
        }
    }
}
