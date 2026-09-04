using System.Text;

namespace CaptureTool.Infrastructure.Analysis.FoundryLocal.Tests;

[TestClass]
public sealed class WavePcm16MonoNormalizerTests
{
    [TestMethod]
    [DataRow((short)1, (short)16)]
    [DataRow((short)3, (short)32)]
    public void TryNormalize_ConvertsCommonStereoWaveFormatsToSixteenKhzMonoPcm(
        short formatTag,
        short bitsPerSample)
    {
        string sourcePath = CreateStereoWave(formatTag, bitsPerSample);
        string destinationPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.wav");
        try
        {
            bool normalized = WavePcm16MonoNormalizer.TryNormalize(
                sourcePath,
                destinationPath,
                CancellationToken.None);

            Assert.IsTrue(normalized);
            Assert.IsTrue(WavePcm16MonoNormalizer.TryGetPcmDataRange(
                destinationPath,
                out long dataOffset,
                out long dataLength));
            Assert.AreEqual(44, dataOffset);
            Assert.AreEqual(320, dataLength);
        }
        finally
        {
            TryDelete(sourcePath);
            TryDelete(destinationPath);
        }
    }

    [TestMethod]
    public void TryNormalize_InvalidInput_FailsWithoutLeavingOutput()
    {
        string sourcePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.wav");
        string destinationPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.wav");
        try
        {
            File.WriteAllText(sourcePath, "not wave audio");

            Assert.IsFalse(WavePcm16MonoNormalizer.TryNormalize(
                sourcePath,
                destinationPath,
                CancellationToken.None));
            Assert.IsFalse(File.Exists(destinationPath));
        }
        finally
        {
            TryDelete(sourcePath);
            TryDelete(destinationPath);
        }
    }

    private static string CreateStereoWave(short formatTag, short bitsPerSample)
    {
        const int sampleRate = 48_000;
        const short channels = 2;
        const int frameCount = 480;
        short blockAlignment = checked((short)(channels * bitsPerSample / 8));
        int dataLength = checked(frameCount * blockAlignment);
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.wav");
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: false);
        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + dataLength);
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));
        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);
        writer.Write(formatTag);
        writer.Write(channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * blockAlignment);
        writer.Write(blockAlignment);
        writer.Write(bitsPerSample);
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(dataLength);
        for (var frame = 0; frame < frameCount; frame++)
        {
            if (formatTag == 3)
            {
                writer.Write(0.25f);
                writer.Write(-0.125f);
            }
            else
            {
                writer.Write((short)8192);
                writer.Write((short)-4096);
            }
        }

        return path;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Test-created disposable audio is safe for the temp scavenger.
        }
    }
}
