using CaptureTool.Domain.Capture.Implementations.Windows.Metadata.Processing;
using CaptureTool.Domain.Capture.Interfaces.Metadata.Processing;
using FluentAssertions;
using Moq;

namespace CaptureTool.Domain.Capture.Tests.Windows.Metadata.Processing;

[TestClass]
public class MetadataProcessorRegistryTests
{
    private MetadataProcessorRegistry _registry = null!;

    [TestInitialize]
    public void Setup()
    {
        _registry = new MetadataProcessorRegistry();
    }

    [TestMethod]
    public void Register_ShouldAddProcessor()
    {
        var processor = CreateMockProcessor("processor-1", "Processor One");

        _registry.Register(processor);

        _registry.GetAll().Should().ContainSingle()
            .Which.ProcessorId.Should().Be("processor-1");
    }

    [TestMethod]
    public void Register_ShouldThrow_WhenSameIdRegisteredTwice()
    {
        var processor = CreateMockProcessor("processor-dup", "Duplicate");

        _registry.Register(processor);

        Action act = () => _registry.Register(processor);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*processor-dup*");
    }

    [TestMethod]
    public void Register_ShouldThrow_WhenProcessorIsNull()
    {
        Action act = () => _registry.Register(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [TestMethod]
    public void Unregister_ShouldRemoveProcessor()
    {
        var processor = CreateMockProcessor("processor-x", "Processor X");
        _registry.Register(processor);

        bool removed = _registry.Unregister("processor-x");

        removed.Should().BeTrue();
        _registry.GetAll().Should().BeEmpty();
    }

    [TestMethod]
    public void Unregister_ShouldReturnFalse_WhenProcessorNotFound()
    {
        bool removed = _registry.Unregister("nonexistent");
        removed.Should().BeFalse();
    }

    [TestMethod]
    public void GetAll_ShouldPreserveRegistrationOrder()
    {
        var g1 = CreateMockProcessor("g1", "G1");
        var g2 = CreateMockProcessor("g2", "G2");
        var g3 = CreateMockProcessor("g3", "G3");

        _registry.Register(g1);
        _registry.Register(g2);
        _registry.Register(g3);

        var all = _registry.GetAll();
        all.Select(g => g.ProcessorId).Should().ContainInOrder("g1", "g2", "g3");
    }

    private static IMetadataProcessor CreateMockProcessor(string id, string name)
    {
        var mock = new Mock<IMetadataProcessor>();
        mock.Setup(g => g.ProcessorId).Returns(id);
        mock.Setup(g => g.Name).Returns(name);
        mock.Setup(g => g.SupportedKeys).Returns([]);
        return mock.Object;
    }
}
