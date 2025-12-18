#include "pch.h"
#include "WindowsVersionHelper.h"
#include <winternl.h>  // For RTL_OSVERSIONINFOW

bool WindowsVersionHelper::IsWindows11_22H2OrLater()
{
    // Windows 11 22H2 is build 22621
    return GetBuildNumber() >= 22621;
}

DWORD WindowsVersionHelper::GetBuildNumber()
{
    // Use RtlGetVersion to get true Windows version
    // (GetVersionEx lies about version due to application compatibility)
    typedef LONG(WINAPI* RtlGetVersionPtr)(PRTL_OSVERSIONINFOW);
    
    HMODULE ntdll = GetModuleHandleW(L"ntdll.dll");
    if (!ntdll)
    {
        return 0;
    }
    
    auto RtlGetVersion = (RtlGetVersionPtr)GetProcAddress(ntdll, "RtlGetVersion");
    if (!RtlGetVersion)
    {
        return 0;
    }
    
    RTL_OSVERSIONINFOW versionInfo = {};
    versionInfo.dwOSVersionInfoSize = sizeof(versionInfo);
    
    if (RtlGetVersion(&versionInfo) == 0)  // STATUS_SUCCESS
    {
        return versionInfo.dwBuildNumber;
    }
    
    return 0;
}
