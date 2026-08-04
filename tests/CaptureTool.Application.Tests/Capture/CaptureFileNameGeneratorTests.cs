using CaptureTool.Application.Capture.Audio;
using CaptureTool.Application.Capture.Image;
using CaptureTool.Application.Capture.Video;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace CaptureTool.Application.Tests.Capture;

[TestClass]
public sealed class CaptureFileNameGeneratorTests
{
    [TestMethod]
    public void ImageGenerator_WhenClockDoesNotAdvance_ProducesUniqueCanonicalNames()
    {
        var generator = new ImageCaptureFileNameGenerator(TestClock.Instance);

        AssertUniqueCanonicalNames(generator.GetNewCaptureFileName, "png");
    }

    [TestMethod]
    public void VideoGenerator_WhenClockDoesNotAdvance_ProducesUniqueCanonicalNames()
    {
        var generator = new VideoCaptureFileNameGenerator(TestClock.Instance);

        AssertUniqueCanonicalNames(generator.GetNewCaptureFileName, "mp4");
    }

    [TestMethod]
    public void AudioGenerator_WhenClockDoesNotAdvance_ProducesUniqueCanonicalNames()
    {
        var generator = new AudioCaptureFileNameGenerator(TestClock.Instance);

        AssertUniqueCanonicalNames(generator.GetNewCaptureFileName, "wav");
    }

    private static void AssertUniqueCanonicalNames(Func<string> createFileName, string extension)
    {
        const int Count = 256;
        var names = new ConcurrentBag<string>();

        Parallel.For(0, Count, _ => names.Add(createFileName()));

        Assert.AreEqual(Count, names.Distinct(StringComparer.Ordinal).Count());

        var pattern = new Regex(
            $"^Capture_2026-01-02_030405_0000000_[0-9a-f]{{32}}\\.{Regex.Escape(extension)}$",
            RegexOptions.CultureInvariant);

        foreach (string name in names)
        {
            Assert.IsTrue(pattern.IsMatch(name), $"Unexpected capture filename: {name}");
        }
    }
}
