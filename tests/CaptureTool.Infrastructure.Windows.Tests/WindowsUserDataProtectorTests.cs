using CaptureTool.Infrastructure.Windows.Security;
using System.Security.Cryptography;
using System.Text;

namespace CaptureTool.Infrastructure.Windows.Tests;

[TestClass]
public sealed class WindowsUserDataProtectorTests
{
    [TestMethod]
    public void CurrentUserCanReadProtectedMetadataUsingAnotherProtectorInstance()
    {
        byte[] plaintext = Encoding.UTF8.GetBytes("capture metadata: private OCR and speech");
        byte[] ciphertext = new WindowsUserDataProtector().Protect(plaintext);
        Assert.DoesNotContain("private OCR", Encoding.UTF8.GetString(ciphertext));
        CollectionAssert.AreEqual(plaintext, new WindowsUserDataProtector().Unprotect(ciphertext));
    }

    [TestMethod]
    public void TamperingOrPlaintextCannotBeReadAsProtectedMetadata()
    {
        var protector = new WindowsUserDataProtector();
        byte[] plaintext = Encoding.UTF8.GetBytes("capture metadata");
        byte[] ciphertext = protector.Protect(plaintext);
        ciphertext[^1] ^= 1;
        Assert.ThrowsExactly<CryptographicException>(() => protector.Unprotect(ciphertext));
        Assert.ThrowsExactly<CryptographicException>(() => protector.Unprotect(plaintext));
    }
}
