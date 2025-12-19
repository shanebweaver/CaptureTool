#include "pch.h"
#include "CppUnitTest.h"
#include "MP4SinkWriter.h"
#include "AudioCaptureDevice.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace CaptureInteropTests
{
	TEST_CLASS(MP4SinkWriterTests)
	{
	public:
		
		TEST_METHOD(TestMP4SinkWriterConstruction)
		{
			// Test that we can construct and destruct an MP4SinkWriter
			MP4SinkWriter writer;
			// If we get here, construction and destruction worked
			Assert::IsTrue(true);
		}

		TEST_METHOD(TestMP4SinkWriterGetRecordingStartTimeReturnsZeroInitially)
		{
			MP4SinkWriter writer;
			LONGLONG startTime = writer.GetRecordingStartTime();
			Assert::AreEqual(0LL, startTime, L"Recording start time should be 0 before SetRecordingStartTime is called");
		}

		TEST_METHOD(TestMP4SinkWriterSetRecordingStartTime)
		{
			MP4SinkWriter writer;
			LONGLONG testTime = 12345678LL;
			writer.SetRecordingStartTime(testTime);
			Assert::AreEqual(testTime, writer.GetRecordingStartTime(), L"Recording start time should match the set value");
		}
	};

	TEST_CLASS(AudioCaptureDeviceTests)
	{
	public:
		
		TEST_METHOD(TestAudioCaptureDeviceConstruction)
		{
			// Test that we can construct and destruct an AudioCaptureDevice
			AudioCaptureDevice device;
			// If we get here, construction and destruction worked
			Assert::IsTrue(true);
		}

		TEST_METHOD(TestAudioCaptureDeviceGetFormatReturnsNullBeforeInit)
		{
			AudioCaptureDevice device;
			WAVEFORMATEX* format = device.GetFormat();
			Assert::IsNull(format, L"Format should be null before initialization");
		}
	};
}
