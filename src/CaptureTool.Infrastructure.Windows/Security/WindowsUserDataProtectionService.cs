using CaptureTool.Application.Abstractions.Security;
using System.Security.Cryptography;

namespace CaptureTool.Infrastructure.Windows.Security;

internal sealed class WindowsUserDataProtectionService : IUserDataProtectionService
{
    public byte[] Protect(byte[] plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        return ProtectedData.Protect(plaintext, optionalEntropy: null, DataProtectionScope.CurrentUser);
    }

    public byte[] Unprotect(byte[] protectedData)
    {
        ArgumentNullException.ThrowIfNull(protectedData);
        return ProtectedData.Unprotect(protectedData, optionalEntropy: null, DataProtectionScope.CurrentUser);
    }
}
