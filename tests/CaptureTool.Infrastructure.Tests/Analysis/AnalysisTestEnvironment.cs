using CaptureTool.Application.Abstractions.Security;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;
using CaptureTool.Infrastructure.Analysis.Persistence;
using CaptureTool.Infrastructure.CaptureAssets;
using CaptureTool.Infrastructure.Persistence;
using System.Security.Cryptography;

namespace CaptureTool.Infrastructure.Tests.Analysis;

internal sealed class AnalysisTestEnvironment : IDisposable, IStorageService
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "CaptureToolAnalysisTests", Guid.NewGuid().ToString("N"));
    public TestProtector Protector { get; } = new();
    public FaultingFileSystem Files { get; } = new();

    public LocalCaptureAnalysisStore CreateStore() => new(this, Protector, Files);
    public LocalCaptureAssetCatalog CreateCatalog() => new(this, Protector, Files);
    public string ControlPath => Path.Combine(Root, "CaptureAnalysis", "control.bin");
    public string[] MetadataPaths => Directory.GetFiles(Path.Combine(Root, "CaptureAnalysis"), "*.analysis", SearchOption.AllDirectories);

    public static SourceRevision Revision(char value = 'a') => new(new string(value, 64));
    public static AnalysisResult Description(string text = "private description", string plan = "v1") =>
        new(new DescriptionMetadata([new(text)]), Producer(), DateTimeOffset.Parse("2026-09-24T12:00:00Z"), plan);
    public static AnalyzerProvenance Producer() => new("test", "local", "actual-model", "adapter-v1", "resolved-v2");

    public string GetApplicationDataFolderPath() => Root;
    public string GetApplicationRetainedCaptureFolderPath() => Path.Combine(Root, "Captures");
    public string GetApplicationScratchFolderPath() => Path.Combine(Root, "Scratch");
    public string GetSystemDefaultScreenshotsFolderPath() => Path.Combine(Root, "Pictures");
    public string GetSystemDefaultMusicFolderPath() => Path.Combine(Root, "Music");
    public string GetSystemDefaultVideosFolderPath() => Path.Combine(Root, "Videos");
    public string GetTemporaryFileName() => Guid.NewGuid().ToString("N");

    public void Dispose()
    {
        if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        Protector.Dispose();
    }

    // Test-only authenticated encryption allows restarts with one key and deterministic protection failures.
    internal sealed class TestProtector : IUserDataProtector, IDisposable
    {
        private readonly byte[] _key = RandomNumberGenerator.GetBytes(32);
        public bool FailProtection { get; set; }

        public byte[] Protect(byte[] plaintext)
        {
            if (FailProtection) throw new CryptographicException("Injected protection failure.");
            byte[] output = new byte[28 + plaintext.Length];
            RandomNumberGenerator.Fill(output.AsSpan(0, 12));
            using var aes = new AesGcm(_key, 16);
            aes.Encrypt(output.AsSpan(0, 12), plaintext, output.AsSpan(28), output.AsSpan(12, 16));
            return output;
        }

        public byte[] Unprotect(byte[] ciphertext)
        {
            if (ciphertext.Length < 28) throw new CryptographicException("Invalid test ciphertext.");
            byte[] plaintext = new byte[ciphertext.Length - 28];
            using var aes = new AesGcm(_key, 16);
            aes.Decrypt(ciphertext.AsSpan(0, 12), ciphertext.AsSpan(28), ciphertext.AsSpan(12, 16), plaintext);
            return plaintext;
        }

        public void Dispose() => CryptographicOperations.ZeroMemory(_key);
    }

    internal sealed class FaultingFileSystem : IProtectedFileSystem
    {
        private readonly LocalProtectedFileSystem _inner = new();
        public Func<string, CancellationToken, Task>? BeforeWrite { get; set; }
        public bool FailCleanup { get; set; }
        public List<byte[]> PublishedBytes { get; } = [];

        public Task<byte[]?> ReadAsync(string path, CancellationToken cancellationToken) => _inner.ReadAsync(path, cancellationToken);

        public async Task WriteAtomicallyAsync(string path, byte[] ciphertext, CancellationToken cancellationToken)
        {
            if (BeforeWrite != null) await BeforeWrite(path, cancellationToken);
            await _inner.WriteAtomicallyAsync(path, ciphertext, cancellationToken);
            PublishedBytes.Add(ciphertext.ToArray());
        }

        public string[] GetFiles(string directory) => _inner.GetFiles(directory);
        public string[] GetDirectories(string directory) => _inner.GetDirectories(directory);
        public void DeleteFile(string path) => _inner.DeleteFile(path);
        public void DeleteDocumentDirectory(string directory)
        {
            if (FailCleanup) throw new IOException("Injected cleanup interruption.");
            _inner.DeleteDocumentDirectory(directory);
        }
    }
}
