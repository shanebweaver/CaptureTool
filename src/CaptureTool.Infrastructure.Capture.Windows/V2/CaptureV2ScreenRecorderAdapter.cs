using CaptureTool.Domain.Capture;
using CaptureTool.Domain.Capture.V2;
using System.Drawing;

namespace CaptureTool.Infrastructure.Capture.Windows.V2;

public sealed class CaptureV2ScreenRecorderAdapter : IScreenRecorder, IDisposable
{
    private const uint DesktopSourceId = 1;
    private const uint SystemAudioSourceId = 2;

    private readonly object _syncRoot = new();
    private CaptureRecorder? _recorder;
    private bool _captureAudio;

    public bool StartRecording(nint hMonitor, string outputPath, bool captureAudio = false)
    {
        lock (_syncRoot)
        {
            if (_recorder is not null)
            {
                return false;
            }

            var recorder = new CaptureRecorder();
            try
            {
                recorder.StartAsync(CreateOptions(hMonitor, outputPath, captureAudio)).GetAwaiter().GetResult();
                _recorder = recorder;
                _captureAudio = captureAudio;
                return true;
            }
            catch
            {
                recorder.DisposeAsync().AsTask().GetAwaiter().GetResult();
                return false;
            }
        }
    }

    public void StopRecording()
    {
        CaptureRecorder? recorder;
        lock (_syncRoot)
        {
            recorder = _recorder;
            _recorder = null;
            _captureAudio = false;
        }

        if (recorder is null)
        {
            return;
        }

        recorder.StopAsync().GetAwaiter().GetResult();
        recorder.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    public void PauseRecording()
    {
        lock (_syncRoot)
        {
            _recorder?.PauseAsync().GetAwaiter().GetResult();
        }
    }

    public void ResumeRecording()
    {
        lock (_syncRoot)
        {
            _recorder?.ResumeAsync().GetAwaiter().GetResult();
        }
    }

    public void ToggleAudioCapture(bool enabled)
    {
        lock (_syncRoot)
        {
            if (_recorder is not null && _captureAudio)
            {
                _recorder.SetAudioMutedAsync(new CaptureSourceId(SystemAudioSourceId), !enabled)
                    .GetAwaiter()
                    .GetResult();
            }
        }
    }

    public void SetVideoFrameCallback(VideoFrameCallback? callback)
    {
        _ = callback;
    }

    public void SetAudioSampleCallback(AudioSampleCallback? callback)
    {
        _ = callback;
    }

    public void Dispose()
    {
        CaptureRecorder? recorder;
        lock (_syncRoot)
        {
            recorder = _recorder;
            _recorder = null;
            _captureAudio = false;
        }

        recorder?.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    private static CapturePipelineOptions CreateOptions(nint hMonitor, string outputPath, bool captureAudio)
    {
        List<CaptureSourceOptions> sources =
        [
            new DesktopCaptureSourceOptions
            {
                SourceId = new CaptureSourceId(DesktopSourceId),
                MonitorHandle = hMonitor,
                CaptureArea = new Rectangle(0, 0, 1, 1),
            },
        ];

        if (captureAudio)
        {
            sources.Add(new SystemAudioCaptureSourceOptions
            {
                SourceId = new CaptureSourceId(SystemAudioSourceId),
            });
        }

        return new CapturePipelineOptions
        {
            Sources = sources,
            Output = new CaptureOutputOptions
            {
                OutputPath = outputPath,
                Container = CaptureContainerFormat.Mp4,
                Video = VideoEncodingOptions.DefaultH264,
                Audio = captureAudio ? AudioEncodingOptions.DefaultAac : null,
            },
            Controls = captureAudio
                ? new CaptureControlOptions
                {
                    AudioGains = [CaptureAudioGainOptions.Unity(new CaptureSourceId(SystemAudioSourceId))],
                }
                : CaptureControlOptions.Default,
        };
    }
}
