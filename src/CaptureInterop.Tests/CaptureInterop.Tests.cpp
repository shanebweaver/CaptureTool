#include "pch.h"
#include "CppUnitTest.h"
// Now we can include internal headers from CaptureInterop.Lib
#include "MP4SinkWriter.h"
#include "AudioCaptureDevice.h"
#include "FrameArrivedHandler.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace CaptureInteropTests
{
	TEST_CLASS(CaptureInteropTests)
	{
	public:
		
		TEST_METHOD(TestMethod1)
		{
			// Example: We can now instantiate internal classes directly
			// MP4SinkWriter writer; // This would now compile
			// Note: Actual instantiation would require proper initialization
			// This is just to show that the headers are accessible
			Assert::IsTrue(true, L"Test infrastructure works");
		}
	};
}
