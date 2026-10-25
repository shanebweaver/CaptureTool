namespace CaptureTool.Application.Abstractions.Security;

/// <summary>Authenticated, current-user protection. Failure must never return unprotected data.</summary>
public interface IUserDataProtector
{
    byte[] Protect(byte[] plaintext);
    byte[] Unprotect(byte[] ciphertext);
}
