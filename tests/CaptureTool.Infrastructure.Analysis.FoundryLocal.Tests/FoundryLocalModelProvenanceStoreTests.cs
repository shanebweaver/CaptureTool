using CaptureTool.Application.Abstractions.Storage;

namespace CaptureTool.Infrastructure.Analysis.FoundryLocal.Tests;

[TestClass]
public sealed class FoundryLocalModelProvenanceStoreTests
{
    [TestMethod]
    public void WriteAndRead_ShouldRoundTripBoundedModelMetadata()
    {
        string root = CreateRoot();
        try
        {
            var store = new FoundryLocalModelProvenanceStore(new StubPathProvider(root));
            var expected = new FoundryLocalModelProvenance(
                "whisper-tiny",
                "whisper-tiny-winml-gpu-v4",
                "4",
                "GPU",
                "WinMLExecutionProvider",
                $"sha256:{new string('a', 64)}");

            store.Write(expected);
            FoundryLocalModelProvenance? actual = store.TryRead("whisper-tiny");

            Assert.AreEqual(expected, actual);
        }
        finally
        {
            TryDeleteRoot(root);
        }
    }

    [TestMethod]
    public void TryRead_ShouldFailClosedForCorruptOrDifferentAliasState()
    {
        string root = CreateRoot();
        try
        {
            var store = new FoundryLocalModelProvenanceStore(new StubPathProvider(root));
            var provenance = new FoundryLocalModelProvenance(
                "whisper-tiny",
                "whisper-tiny-cpu-v4",
                "4",
                "CPU",
                "CPUExecutionProvider",
                $"sha256:{new string('b', 64)}");
            store.Write(provenance);

            Assert.IsNull(store.TryRead("another-model"));
            string path = store.GetPath("whisper-tiny");
            File.WriteAllText(path, "not json");
            Assert.IsNull(store.TryRead("whisper-tiny"));
        }
        finally
        {
            TryDeleteRoot(root);
        }
    }

    [TestMethod]
    public void WriteAndRead_ShouldPreserveIndependentModelAliases()
    {
        string root = CreateRoot();
        try
        {
            var store = new FoundryLocalModelProvenanceStore(new StubPathProvider(root));
            var whisper = new FoundryLocalModelProvenance(
                "whisper-tiny",
                "whisper-tiny-winml-gpu-v4",
                "4",
                "GPU",
                "WinMLExecutionProvider",
                $"sha256:{new string('a', 64)}");
            var nemotron = new FoundryLocalModelProvenance(
                "nvidia-nemotron-3.5-asr-streaming-multilingual-0.6b",
                "nemotron-multilingual-winml-gpu-v1",
                "1",
                "GPU",
                "WinMLExecutionProvider",
                $"sha256:{new string('b', 64)}");

            store.Write(whisper);
            store.Write(nemotron);

            Assert.AreEqual(whisper, store.TryRead(whisper.RequestedAlias));
            Assert.AreEqual(nemotron, store.TryRead(nemotron.RequestedAlias));
            Assert.AreNotEqual(
                store.GetPath(whisper.RequestedAlias),
                store.GetPath(nemotron.RequestedAlias));
        }
        finally
        {
            TryDeleteRoot(root);
        }
    }

    private static string CreateRoot()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "CaptureToolFoundryTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void TryDeleteRoot(string root)
    {
        try
        {
            string resolvedRoot = Path.GetFullPath(root);
            string expectedParent = Path.GetFullPath(Path.Combine(
                Path.GetTempPath(),
                "CaptureToolFoundryTests"));
            if (resolvedRoot.StartsWith(expectedParent, StringComparison.OrdinalIgnoreCase) &&
                Directory.Exists(resolvedRoot))
            {
                Directory.Delete(resolvedRoot, recursive: true);
            }
        }
        catch
        {
            // Test-created non-user metadata is safe for the temp scavenger to remove later.
        }
    }

    private sealed class StubPathProvider(string root) : IApplicationLocalCachePathProvider
    {
        public string GetApplicationLocalCacheFolderPath() => root;
    }
}
