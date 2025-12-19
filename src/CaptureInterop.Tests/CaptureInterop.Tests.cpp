#include "pch.h"
#include "CppUnitTest.h"
#include "TestUtility.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace CaptureInteropTests
{
	TEST_CLASS(CaptureInteropTests)
	{
	public:
		
		TEST_METHOD(TestAddNumbers)
		{
			// Test basic addition
			int result = AddNumbers(2, 3);
			Assert::AreEqual(5, result);

			// Test negative numbers
			result = AddNumbers(-5, 3);
			Assert::AreEqual(-2, result);

			// Test zero
			result = AddNumbers(0, 0);
			Assert::AreEqual(0, result);
		}

		TEST_METHOD(TestIsValidPath)
		{
			// Test valid path
			Assert::IsTrue(IsValidPath(L"C:\\test\\path"));

			// Test empty path
			Assert::IsFalse(IsValidPath(L""));

			// Test null path
			Assert::IsFalse(IsValidPath(nullptr));
		}
	};
}
