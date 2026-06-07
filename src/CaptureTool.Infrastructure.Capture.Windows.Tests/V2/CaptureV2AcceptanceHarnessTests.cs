using CaptureTool.Infrastructure.Capture.Windows.V2;
using FluentAssertions;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;

namespace CaptureTool.Infrastructure.Capture.Windows.Tests.V2;

[TestClass]
[DoNotParallelize]
public sealed class CaptureV2AcceptanceHarnessTests
{
    [TestMethod]
    public async Task Probe_RecordPrimaryMonitorVideoOnlyMp4_FinalizesOutput()
    {
        if (Environment.GetEnvironmentVariable("CAPTURETOOL_V2_ACCEPTANCE_PROBE") != "1")
        {
            Assert.Inconclusive("Set CAPTURETOOL_V2_ACCEPTANCE_PROBE=1 to run the local desktop capture probe.");
        }

        Environment.SetEnvironmentVariable("CAPTURETOOL_V2_FAKE_NATIVE_SESSION", null);
        string outputPath = Path.Combine(Path.GetTempPath(), $"capture-v2-probe-{Guid.NewGuid():N}.mp4");
        nint primaryMonitor = NativeMethods.MonitorFromPoint(new NativeMethods.Point(), NativeMethods.MONITOR_DEFAULTTOPRIMARY);
        using var recorder = new CaptureV2ScreenRecorderAdapter();

        bool started = recorder.StartRecording(primaryMonitor, outputPath, captureAudio: false);
        started.Should().BeTrue();

        await Task.Delay(TimeSpan.FromSeconds(2));
        recorder.PauseRecording();
        await Task.Delay(TimeSpan.FromMilliseconds(250));
        recorder.ResumeRecording();
        await Task.Delay(TimeSpan.FromSeconds(1));
        recorder.StopRecording();

        File.Exists(outputPath).Should().BeTrue();
        new FileInfo(outputPath).Length.Should().BeGreaterThan(0);
        AssertMp4Tracks(outputPath, expectVideo: true, expectAudio: false);

        File.Delete(outputPath);
    }

    [TestMethod]
    public async Task Probe_RecordPrimaryMonitorWithSystemAudioMp4_PauseMuteAndFinalize()
    {
        if (Environment.GetEnvironmentVariable("CAPTURETOOL_V2_ACCEPTANCE_AUDIO_PROBE") != "1")
        {
            Assert.Inconclusive("Set CAPTURETOOL_V2_ACCEPTANCE_AUDIO_PROBE=1 to run the local desktop+audio capture probe.");
        }

        Environment.SetEnvironmentVariable("CAPTURETOOL_V2_FAKE_NATIVE_SESSION", null);
        string outputPath = Path.Combine(Path.GetTempPath(), $"capture-v2-audio-probe-{Guid.NewGuid():N}.mp4");
        nint primaryMonitor = NativeMethods.MonitorFromPoint(new NativeMethods.Point(), NativeMethods.MONITOR_DEFAULTTOPRIMARY);
        using var recorder = new CaptureV2ScreenRecorderAdapter();

        bool started = recorder.StartRecording(primaryMonitor, outputPath, captureAudio: true);
        if (!started)
        {
            Assert.Inconclusive("V2 desktop+audio probe could not start. The machine may not expose a usable loopback endpoint.");
        }

        await Task.Delay(TimeSpan.FromSeconds(1));
        recorder.ToggleAudioCapture(false);
        await Task.Delay(TimeSpan.FromMilliseconds(500));
        recorder.PauseRecording();
        await Task.Delay(TimeSpan.FromMilliseconds(250));
        recorder.ResumeRecording();
        recorder.ToggleAudioCapture(true);
        await Task.Delay(TimeSpan.FromSeconds(1));
        recorder.StopRecording();

        File.Exists(outputPath).Should().BeTrue();
        new FileInfo(outputPath).Length.Should().BeGreaterThan(0);
        AssertMp4Tracks(outputPath, expectVideo: true, expectAudio: true);

        File.Delete(outputPath);
    }

    private static void AssertMp4Tracks(string outputPath, bool expectVideo, bool expectAudio)
    {
        byte[] fileBytes = File.ReadAllBytes(outputPath);
        bool hasVideo = ContainsHandler(fileBytes, "vide");
        bool hasAudio = ContainsHandler(fileBytes, "soun");

        hasVideo.Should().Be(expectVideo);
        hasAudio.Should().Be(expectAudio);
    }

    private static bool ContainsHandler(ReadOnlySpan<byte> data, string handlerType)
    {
        return ContainsHandler(data, 0, data.Length, handlerType);
    }

    private static bool ContainsHandler(ReadOnlySpan<byte> data, int start, int end, string handlerType)
    {
        int offset = start;
        while (offset + 8 <= end)
        {
            ulong size = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(offset, 4));
            string type = Encoding.ASCII.GetString(data.Slice(offset + 4, 4));
            int headerSize = 8;

            if (size == 1)
            {
                if (offset + 16 > end)
                {
                    return false;
                }

                size = BinaryPrimitives.ReadUInt64BigEndian(data.Slice(offset + 8, 8));
                headerSize = 16;
            }
            else if (size == 0)
            {
                size = (ulong)(end - offset);
            }

            if (size < (ulong)headerSize || offset + (long)size > end)
            {
                return false;
            }

            int contentStart = offset + headerSize;
            int contentEnd = offset + checked((int)size);

            if (type == "hdlr" && contentStart + 12 <= contentEnd)
            {
                string actualHandler = Encoding.ASCII.GetString(data.Slice(contentStart + 8, 4));
                if (actualHandler == handlerType)
                {
                    return true;
                }
            }

            if ((type == "moov" || type == "trak" || type == "mdia") &&
                ContainsHandler(data, contentStart, contentEnd, handlerType))
            {
                return true;
            }

            offset = contentEnd;
        }

        return false;
    }

    private static partial class NativeMethods
    {
        public const uint MONITOR_DEFAULTTOPRIMARY = 1;

        [DllImport("user32.dll")]
        public static extern nint MonitorFromPoint(Point point, uint flags);

        [StructLayout(LayoutKind.Sequential)]
        public readonly struct Point
        {
            public readonly int X;
            public readonly int Y;
        }
    }
}
