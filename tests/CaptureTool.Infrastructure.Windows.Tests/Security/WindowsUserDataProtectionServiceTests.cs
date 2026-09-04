using CaptureTool.Infrastructure.Windows.Security;
using System.Text;

namespace CaptureTool.Infrastructure.Windows.Tests.Security;

[TestClass]
public sealed class WindowsUserDataProtectionServiceTests
{
    [TestMethod]
    public void Protect_ShouldRoundTripForCurrentUserWithoutPlaintextStorage()
    {
        var service = new WindowsUserDataProtectionService();
        byte[] plaintext = Encoding.UTF8.GetBytes($"capture-asset-canary-{Guid.NewGuid():N}");

        byte[] protectedData = service.Protect(plaintext);
        byte[] roundTripped = service.Unprotect(protectedData);

        CollectionAssert.AreNotEqual(plaintext, protectedData);
        CollectionAssert.AreEqual(plaintext, roundTripped);
        Assert.IsFalse(Encoding.UTF8.GetString(protectedData).Contains(
            Encoding.UTF8.GetString(plaintext),
            StringComparison.Ordinal));
    }
}
