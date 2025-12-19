#include "pch.h"
#include "TestUtility.h"

int AddNumbers(int a, int b)
{
	return a + b;
}

bool IsValidPath(const wchar_t* path)
{
	if (path == nullptr)
	{
		return false;
	}
	if (wcslen(path) == 0)
	{
		return false;
	}
	return true;
}
