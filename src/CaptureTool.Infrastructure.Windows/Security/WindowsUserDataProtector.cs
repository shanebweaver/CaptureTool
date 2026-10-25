using CaptureTool.Application.Abstractions.Security;
using System.Security.Cryptography;

namespace CaptureTool.Infrastructure.Windows.Security;

internal sealed class WindowsUserDataProtector : IUserDataProtector
{
    public byte[] Protect(byte[] plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        return ProtectedData.Protect(plaintext, optionalEntropy: null, DataProtectionScope.CurrentUser);
    }

    public byte[] Unprotect(byte[] ciphertext)
    {
        ArgumentNullException.ThrowIfNull(ciphertext);
        return ProtectedData.Unprotect(ciphertext, optionalEntropy: null, DataProtectionScope.CurrentUser);
    }
}
