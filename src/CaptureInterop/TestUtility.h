#pragma once

extern "C"
{
	// Simple test utility function to verify DLL loading and linking
	__declspec(dllexport) int AddNumbers(int a, int b);
	
	// Simple test utility function to verify string operations
	__declspec(dllexport) bool IsValidPath(const wchar_t* path);
}
