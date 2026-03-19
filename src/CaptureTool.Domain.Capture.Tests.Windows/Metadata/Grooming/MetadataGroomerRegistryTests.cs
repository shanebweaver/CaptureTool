using CaptureTool.Domain.Capture.Implementations.Windows.Metadata.Grooming;
using CaptureTool.Domain.Capture.Interfaces.Metadata.Grooming;
using FluentAssertions;
using Moq;

namespace CaptureTool.Domain.Capture.Tests.Windows.Metadata.Grooming;

[TestClass]
public class MetadataGroomerRegistryTests
{
    private MetadataGroomerRegistry _registry = null!;

    [TestInitialize]
    public void Setup()
    {
        _registry = new MetadataGroomerRegistry();
    }

    [TestMethod]
    public void Register_ShouldAddGroomer()
    {
        var groomer = CreateMockGroomer("groomer-1", "Groomer One");

        _registry.Register(groomer);

        _registry.GetAll().Should().ContainSingle()
            .Which.GroomerId.Should().Be("groomer-1");
    }

    [TestMethod]
    public void Register_ShouldThrow_WhenSameIdRegisteredTwice()
    {
        var groomer = CreateMockGroomer("groomer-dup", "Duplicate");

        _registry.Register(groomer);

        Action act = () => _registry.Register(groomer);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*groomer-dup*");
    }

    [TestMethod]
    public void Register_ShouldThrow_WhenGroomerIsNull()
    {
        Action act = () => _registry.Register(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [TestMethod]
    public void Unregister_ShouldRemoveGroomer()
    {
        var groomer = CreateMockGroomer("groomer-x", "Groomer X");
        _registry.Register(groomer);

        bool removed = _registry.Unregister("groomer-x");

        removed.Should().BeTrue();
        _registry.GetAll().Should().BeEmpty();
    }

    [TestMethod]
    public void Unregister_ShouldReturnFalse_WhenGroomerNotFound()
    {
        bool removed = _registry.Unregister("nonexistent");
        removed.Should().BeFalse();
    }

    [TestMethod]
    public void GetAll_ShouldPreserveRegistrationOrder()
    {
        var g1 = CreateMockGroomer("g1", "G1");
        var g2 = CreateMockGroomer("g2", "G2");
        var g3 = CreateMockGroomer("g3", "G3");

        _registry.Register(g1);
        _registry.Register(g2);
        _registry.Register(g3);

        var all = _registry.GetAll();
        all.Select(g => g.GroomerId).Should().ContainInOrder("g1", "g2", "g3");
    }

    private static IMetadataGroomer CreateMockGroomer(string id, string name)
    {
        var mock = new Mock<IMetadataGroomer>();
        mock.Setup(g => g.GroomerId).Returns(id);
        mock.Setup(g => g.Name).Returns(name);
        mock.Setup(g => g.SupportedKeys).Returns([]);
        return mock.Object;
    }
}
