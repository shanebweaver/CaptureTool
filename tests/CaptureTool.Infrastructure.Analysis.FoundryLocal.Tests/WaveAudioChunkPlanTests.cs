using System.Text;

namespace CaptureTool.Infrastructure.Analysis.FoundryLocal.Tests;

[TestClass]
public sealed class WaveAudioChunkPlanTests
{
    [TestMethod]
    public async Task Plan_ShouldCreateSourceRelativeWindowsAndWriteValidWaveChunk()
    {
        string sourcePath = CreateWaveFile(durationSeconds: 35);
        string chunkPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.wav");
        try
        {
            bool created = WaveAudioChunkPlan.TryCreate(
                sourcePath,
                TimeSpan.FromSeconds(15),
                out WaveAudioChunkPlan? plan);

            Assert.IsTrue(created);
            Assert.IsNotNull(plan);
            Assert.HasCount(3, plan.Chunks);
            Assert.AreEqual(TimeSpan.Zero, plan.Chunks[0].StartTime);
            Assert.AreEqual(TimeSpan.FromSeconds(15), plan.Chunks[0].EndTime);
            Assert.AreEqual(TimeSpan.FromSeconds(15), plan.Chunks[1].StartTime);
            Assert.AreEqual(TimeSpan.FromSeconds(30), plan.Chunks[1].EndTime);
            Assert.AreEqual(TimeSpan.FromSeconds(35), plan.Chunks[2].EndTime);

            await plan.WriteChunkAsync(plan.Chunks[1], chunkPath, CancellationToken.None);

            Assert.IsTrue(WaveAudioChunkPlan.TryCreate(
                chunkPath,
                TimeSpan.FromMinutes(1),
                out WaveAudioChunkPlan? writtenPlan));
            Assert.IsNotNull(writtenPlan);
            Assert.HasCount(1, writtenPlan.Chunks);
            Assert.AreEqual(TimeSpan.FromSeconds(15), writtenPlan.Chunks[0].EndTime);
        }
        finally
        {
            TryDelete(sourcePath);
            TryDelete(chunkPath);
        }
    }

    [TestMethod]
    public void TryCreate_ShouldRejectNonWaveAndInvalidWindow()
    {
        string sourcePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.wav");
        try
        {
            File.WriteAllText(sourcePath, "not wave data");

            Assert.IsFalse(WaveAudioChunkPlan.TryCreate(
                sourcePath,
                TimeSpan.FromSeconds(15),
                out WaveAudioChunkPlan? plan));
            Assert.IsNull(plan);
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
                WaveAudioChunkPlan.TryCreate(sourcePath, TimeSpan.Zero, out _));
        }
        finally
        {
            TryDelete(sourcePath);
        }
    }

    private static string CreateWaveFile(int durationSeconds)
    {
        const int sampleRate = 1_000;
        const short channelCount = 1;
        const short bitsPerSample = 16;
        const short blockAlignment = channelCount * bitsPerSample / 8;
        const int bytesPerSecond = sampleRate * blockAlignment;
        int dataLength = checked(durationSeconds * bytesPerSecond);
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.wav");
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: false);
        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + dataLength);
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));
        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write(channelCount);
        writer.Write(sampleRate);
        writer.Write(bytesPerSecond);
        writer.Write(blockAlignment);
        writer.Write(bitsPerSample);
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(dataLength);
        writer.Write(new byte[dataLength]);
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
            // Test-created disposable audio is safe for the temp scavenger to remove later.
        }
    }
}
