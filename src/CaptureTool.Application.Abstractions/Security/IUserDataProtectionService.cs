namespace CaptureTool.Application.Abstractions.Security;

public interface IUserDataProtectionService
{
    byte[] Protect(byte[] plaintext);

    byte[] Unprotect(byte[] protectedData);
}
