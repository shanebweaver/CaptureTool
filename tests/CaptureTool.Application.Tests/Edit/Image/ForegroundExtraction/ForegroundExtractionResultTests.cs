using CaptureTool.Application.Abstractions.Edit.Image.ForegroundExtraction;
using CaptureTool.Domain.FileSystem;
using FluentAssertions;

namespace CaptureTool.Application.Tests.Edit.Image.ForegroundExtraction;

[TestClass]
public sealed class ForegroundExtractionResultTests
{
    [TestMethod]
    public void PreparationResults_ShouldExposeExpectedStatusesAndErrors()
    {
        ForegroundExtractionPreparationResult.Success.Status.Should().Be(ForegroundExtractionPreparationStatus.Success);
        ForegroundExtractionPreparationResult.Cancelled.Status.Should().Be(ForegroundExtractionPreparationStatus.Cancelled);
        ForegroundExtractionPreparationResult.NotSupported.Status.Should().Be(ForegroundExtractionPreparationStatus.NotSupported);

        ForegroundExtractionPreparationResult failed = ForegroundExtractionPreparationResult.Failed("Preparation failed.");

        failed.Status.Should().Be(ForegroundExtractionPreparationStatus.Failed);
        failed.ErrorMessage.Should().Be("Preparation failed.");
    }

    [TestMethod]
    public void ExtractionResults_ShouldExposeExpectedStatusesFilesAndErrors()
    {
        var imageFile = new ImageFile("foreground.png");

        ForegroundExtractionResult success = ForegroundExtractionResult.Success(imageFile);
        ForegroundExtractionResult failed = ForegroundExtractionResult.Failed("Extraction failed.");

        success.Status.Should().Be(ForegroundExtractionStatus.Success);
        success.ImageFile.Should().BeSameAs(imageFile);
        ForegroundExtractionResult.Cancelled.Status.Should().Be(ForegroundExtractionStatus.Cancelled);
        ForegroundExtractionResult.NotReady.Status.Should().Be(ForegroundExtractionStatus.NotReady);
        ForegroundExtractionResult.NotSupported.Status.Should().Be(ForegroundExtractionStatus.NotSupported);
        failed.Status.Should().Be(ForegroundExtractionStatus.Failed);
        failed.ErrorMessage.Should().Be("Extraction failed.");
    }
}
