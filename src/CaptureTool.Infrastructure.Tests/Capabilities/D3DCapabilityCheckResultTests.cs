using CaptureTool.Infrastructure.Interfaces.Capabilities;

namespace CaptureTool.Infrastructure.Tests.Capabilities;

[TestClass]
public sealed class D3DCapabilityCheckResultTests
{
    [TestMethod]
    public void Success_CreatesSuccessfulResult()
    {
        var result = D3DCapabilityCheckResult.Success();

        Assert.IsNotNull(result);
        Assert.IsTrue(result.IsSupported);
        Assert.IsNull(result.ErrorMessage);
        Assert.IsNull(result.FailureReason);
        Assert.IsNull(result.HResult);
    }

    [TestMethod]
    public void Failure_WithAllParameters_CreatesFailedResult()
    {
        const string failureReason = "Direct3D 11 Feature Level 11.0";
        const string errorMessage = "Hardware does not support D3D11.";
        const int hresult = unchecked((int)0x887A0001); // DXGI_ERROR_UNSUPPORTED

        var result = D3DCapabilityCheckResult.Failure(failureReason, errorMessage, hresult);

        Assert.IsNotNull(result);
        Assert.IsFalse(result.IsSupported);
        Assert.AreEqual(failureReason, result.FailureReason);
        Assert.AreEqual(errorMessage, result.ErrorMessage);
        Assert.AreEqual(hresult, result.HResult);
    }

    [TestMethod]
    public void Failure_WithoutHResult_CreatesFailedResult()
    {
        const string failureReason = "Win2D CanvasDevice";
        const string errorMessage = "Failed to create Win2D device.";

        var result = D3DCapabilityCheckResult.Failure(failureReason, errorMessage);

        Assert.IsNotNull(result);
        Assert.IsFalse(result.IsSupported);
        Assert.AreEqual(failureReason, result.FailureReason);
        Assert.AreEqual(errorMessage, result.ErrorMessage);
        Assert.IsNull(result.HResult);
    }

    [TestMethod]
    public void IsSupported_WhenTrue_IndicatesSuccess()
    {
        var result = D3DCapabilityCheckResult.Success();

        Assert.IsTrue(result.IsSupported);
    }

    [TestMethod]
    public void IsSupported_WhenFalse_IndicatesFailure()
    {
        var result = D3DCapabilityCheckResult.Failure("Test", "Test message");

        Assert.IsFalse(result.IsSupported);
    }
}
