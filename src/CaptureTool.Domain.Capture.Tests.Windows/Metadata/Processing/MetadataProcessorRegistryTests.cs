using CaptureTool.Domain.Capture.Abstractions.Metadata;
using CaptureTool.Domain.Capture.Abstractions.Metadata.Processing;
using CaptureTool.Domain.Capture.Windows.Metadata.Processing;
using FluentAssertions;

namespace CaptureTool.Domain.Capture.Tests.Windows.Metadata.Processing;

[TestClass]
public class MetadataProcessorRegistryTests
{
    [TestMethod]
    public void GetAll_ShouldPreserveRegistrationOrder()
    {
        var registry = new MetadataProcessorRegistry();
        registry.Register(new TestProcessor("first"));
        registry.Register(new TestProcessor("second"));

        registry.GetAll().Select(processor => processor.ProcessorId)
            .Should()
            .ContainInOrder("first", "second");
    }

    [TestMethod]
    public void Register_ShouldThrow_WhenProcessorIdAlreadyExists()
    {
        var registry = new MetadataProcessorRegistry();
        registry.Register(new TestProcessor("processor"));

        Action act = () => registry.Register(new TestProcessor("processor"));

        act.Should().Throw<InvalidOperationException>();
    }

    [TestMethod]
    public void Unregister_ShouldRemoveProcessor()
    {
        var registry = new MetadataProcessorRegistry();
        registry.Register(new TestProcessor("processor"));

        bool removed = registry.Unregister("processor");

        removed.Should().BeTrue();
        registry.GetAll().Should().BeEmpty();
    }

    private sealed class TestProcessor : IMetadataProcessor
    {
        public TestProcessor(string processorId)
        {
            ProcessorId = processorId;
        }

        public string ProcessorId { get; }

        public string Name => ProcessorId;

        public IReadOnlyList<string> SupportedKeys { get; } = [];

        public Task<IReadOnlyList<InsightEntry>> ProcessAsync(
            IReadOnlyList<MetadataEntry> rawEntries,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<InsightEntry>>([]);
        }
    }
}
