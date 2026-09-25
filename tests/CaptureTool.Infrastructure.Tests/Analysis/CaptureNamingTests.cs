using CaptureTool.Application.Abstractions.Library.RecentCaptures;
using CaptureTool.Application.Capture;
using CaptureTool.Domain;
using CaptureTool.Domain.Capture;
using CaptureTool.Infrastructure.Files;

namespace CaptureTool.Infrastructure.Tests.Analysis;

[TestClass]
public sealed class CaptureNamingTests
{
    public TestContext TestContext { get; set; } = null!;
    private CancellationToken Ct => TestContext.CancellationToken;

    [TestMethod]
    public async Task ManualRenameNeedsNoConsentAndSavedNameSurvivesRestart()
    {
        using var environment = new AnalysisTestEnvironment();
        using var catalog = environment.CreateCatalog();
        using var names = new CaptureNamingService(catalog, catalog, new TestNotifier(), new LocalFileSystem());
        Directory.CreateDirectory(environment.Root);
        string path = Path.Combine(environment.Root, "import.wav");
        await File.WriteAllTextAsync(path, "media", Ct);
        Assert.IsNull(await names.GetNameAsync(path, Ct));
        Assert.IsFalse((await names.GetStateAsync(path, Ct)).IsGenerating);
        Assert.IsEmpty(await catalog.ReadAllAsync(Ct), "Reading a name must not enroll or schedule a capture.");
        var renamed = await names.RenameAsync(path, CaptureFileType.Audio, "Personal note", Ct);
        Assert.IsNotNull(renamed);
        Assert.IsTrue(File.Exists(renamed.NewPath));
        Assert.IsFalse(File.Exists(path));
        using var reopened = environment.CreateCatalog();
        Assert.AreEqual(new CaptureName("Personal note", false), (await reopened.ReadAllAsync(Ct)).Single().Name);
        await names.InvalidatePendingAsync(Ct);
        Assert.AreEqual("Personal note", (await names.GetNameAsync(renamed.NewPath, Ct))!.Text);
        environment.Protector.FailProtection = true;
        Assert.IsNull(await names.RenameAsync(renamed.NewPath, CaptureFileType.Audio, "Unsaved name", Ct));
        Assert.IsTrue(File.Exists(renamed.NewPath));
        Assert.AreEqual("Personal note", (await names.GetNameAsync(renamed.NewPath, Ct))!.Text);
    }

    [TestMethod]
    public async Task StoppedNamingServiceCannotMoveFiles()
    {
        using var environment = new AnalysisTestEnvironment();
        using var catalog = environment.CreateCatalog();
        using var names = new CaptureNamingService(catalog, catalog, new TestNotifier(), new LocalFileSystem());
        Directory.CreateDirectory(environment.Root);
        string path = Path.Combine(environment.Root, "capture.png");
        await File.WriteAllTextAsync(path, "media", Ct);
        await names.StopAsync();
        Assert.IsNull(await names.RenameAsync(path, CaptureFileType.Image, "Late name", Ct));
        Assert.IsTrue(File.Exists(path));
    }

    [TestMethod]
    [DataRow("CON", "_CON")]
    [DataRow("aux.txt", "_aux.txt")]
    [DataRow("LPT¹", "_LPT¹")]
    [DataRow("Roadmap: résumé / 東京?", "Roadmap_ résumé _ 東京_")]
    [DataRow("...", "Capture")]
    public void SuggestedNamesAreSafeWithoutDiscardingUnicode(string text, string expected) =>
        Assert.AreEqual(expected, new CaptureName(text, false).SuggestedFileName());

    [TestMethod]
    public void NamesRejectMultilineAndOverlongInputAndNeverSplitASurrogate()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new CaptureName("  ", false));
        Assert.ThrowsExactly<ArgumentException>(() => new CaptureName("two\nlines", false));
        Assert.ThrowsExactly<ArgumentException>(() => new CaptureName(new string('a', 161), false));
        Assert.ThrowsExactly<ArgumentException>(() => new CaptureName("\ud800", false));
        string suggestion = new CaptureName(new string('a', 119) + "📷", true).SuggestedFileName();
        Assert.AreEqual(119, suggestion.Length);
    }

    private sealed class TestNotifier : IRecentCapturesChangeNotifier
    {
        public event EventHandler? RecentCapturesChanged;
        public void NotifyRecentCapturesChanged() => RecentCapturesChanged?.Invoke(this, EventArgs.Empty);
    }
}
