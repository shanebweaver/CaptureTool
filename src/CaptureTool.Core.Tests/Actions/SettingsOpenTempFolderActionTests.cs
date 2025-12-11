using AutoFixture;
using AutoFixture.AutoMoq;
using CaptureTool.Core.Implementations.Actions.Settings;
using CaptureTool.Services.Interfaces.Storage;
using Moq;

namespace CaptureTool.Core.Tests.Actions;

[TestClass]
public class SettingsOpenTempFolderActionTests
{
    private IFixture Fixture { get; set; } = null!;

    [TestInitialize]
    public void Init()
    {
        Fixture = new Fixture().Customize(new AutoMoqCustomization { ConfigureMembers = true });
    }

    [TestMethod]
    public void Execute_ShouldOpenExplorer_WhenFolderExists()
    {
        var storage = Fixture.Freeze<Mock<IStorageService>>();
        var temp = Path.Combine(Path.GetTempPath(), "capturetool-tests");
        Directory.CreateDirectory(temp);
        storage.Setup(s => s.GetApplicationTemporaryFolderPath()).Returns(temp);

        var action = Fixture.Create<SettingsOpenTempFolderAction>();
        action.Execute();

        storage.Verify(s => s.GetApplicationTemporaryFolderPath(), Times.Once);

        // Cleanup
        Directory.Delete(temp, true);
    }

    [TestMethod]
    [ExpectedException(typeof(DirectoryNotFoundException))]
    public void Execute_ShouldThrow_WhenFolderMissing()
    {
        var storage = Fixture.Freeze<Mock<IStorageService>>();
        var temp = Path.Combine(Path.GetTempPath(), "capturetool-tests-missing");
        if (Directory.Exists(temp)) Directory.Delete(temp, true);
        storage.Setup(s => s.GetApplicationTemporaryFolderPath()).Returns(temp);

        var action = Fixture.Create<SettingsOpenTempFolderAction>();
        action.Execute();
    }
}
