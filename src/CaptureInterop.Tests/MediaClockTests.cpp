#include "pch.h"
#include "CppUnitTest.h"
#include "SimpleMediaClock.h"
#include "IMediaClockAdvancer.h"

#include <thread>
#include <chrono>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace CaptureInteropTests
{
    // Mock clock advancer for testing
    class MockClockAdvancer : public IMediaClockAdvancer
    {
    private:
        IMediaClockWriter* m_clockWriter = nullptr;

    public:
        void SetClockWriter(IMediaClockWriter* clockWriter) override
        {
            m_clockWriter = clockWriter;
        }

        IMediaClockWriter* GetClockWriter() const { return m_clockWriter; }
    };

    TEST_CLASS(MediaClockTests)
    {
    public:
        TEST_METHOD(Clock_StartsAtZero)
        {
            SimpleMediaClock clock;
            
            Assert::AreEqual(0LL, clock.GetCurrentTime());
            Assert::IsFalse(clock.IsRunning());
        }

        TEST_METHOD(Clock_StartsCorrectly)
        {
            SimpleMediaClock clock;
            
            LARGE_INTEGER qpc;
            QueryPerformanceCounter(&qpc);
            
            clock.Start(qpc.QuadPart);
            
            Assert::IsTrue(clock.IsRunning());
            Assert::AreEqual(qpc.QuadPart, clock.GetStartTime());
        }

        TEST_METHOD(Clock_AdvancesByAudioSamples)
        {
            SimpleMediaClock clock;
            
            LARGE_INTEGER qpc;
            QueryPerformanceCounter(&qpc);
            clock.Start(qpc.QuadPart);
            
            // Advance by 480 frames at 48kHz (10ms)
            const UINT32 numFrames = 480;
            const UINT32 sampleRate = 48000;
            const LONGLONG expectedTime = 100000LL; // 10ms in 100ns ticks
            
            clock.AdvanceByAudioSamples(numFrames, sampleRate);
            
            LONGLONG currentTime = clock.GetCurrentTime();
            Assert::AreEqual(expectedTime, currentTime);
        }

        TEST_METHOD(Clock_AdvancesCorrectly_WithMultipleSamples)
        {
            SimpleMediaClock clock;
            
            LARGE_INTEGER qpc;
            QueryPerformanceCounter(&qpc);
            clock.Start(qpc.QuadPart);
            
            const UINT32 numFrames = 480;  // 10ms at 48kHz
            const UINT32 sampleRate = 48000;
            const LONGLONG expectedTimePerSample = 100000LL; // 10ms in 100ns ticks
            
            // Advance 100 times (simulate 1 second)
            for (int i = 0; i < 100; i++)
            {
                clock.AdvanceByAudioSamples(numFrames, sampleRate);
            }
            
            LONGLONG currentTime = clock.GetCurrentTime();
            LONGLONG expectedTime = expectedTimePerSample * 100; // 1 second
            
            // Allow small tolerance for rounding
            LONGLONG tolerance = 10LL;
            Assert::IsTrue(std::abs(currentTime - expectedTime) <= tolerance, 
                L"Clock should advance to approximately 1 second");
        }

        TEST_METHOD(Clock_DoesNotAdvance_WhenPaused)
        {
            SimpleMediaClock clock;
            
            LARGE_INTEGER qpc;
            QueryPerformanceCounter(&qpc);
            clock.Start(qpc.QuadPart);
            
            // Advance some time
            clock.AdvanceByAudioSamples(480, 48000);
            LONGLONG timeBeforePause = clock.GetCurrentTime();
            
            // Pause the clock
            clock.Pause();
            
            // Try to advance - should not work
            clock.AdvanceByAudioSamples(480, 48000);
            
            LONGLONG timeAfterPause = clock.GetCurrentTime();
            Assert::AreEqual(timeBeforePause, timeAfterPause);
        }

        TEST_METHOD(Clock_ResetsCorrectly)
        {
            SimpleMediaClock clock;
            
            LARGE_INTEGER qpc;
            QueryPerformanceCounter(&qpc);
            clock.Start(qpc.QuadPart);
            
            // Advance some time
            clock.AdvanceByAudioSamples(480, 48000);
            Assert::IsTrue(clock.GetCurrentTime() > 0);
            
            // Reset
            clock.Reset();
            
            Assert::AreEqual(0LL, clock.GetCurrentTime());
            Assert::AreEqual(0LL, clock.GetStartTime());
            Assert::IsFalse(clock.IsRunning());
        }

        TEST_METHOD(Clock_SetClockAdvancer_SetsWriter)
        {
            SimpleMediaClock clock;
            MockClockAdvancer advancer;
            
            clock.SetClockAdvancer(&advancer);
            
            Assert::IsNotNull(advancer.GetClockWriter());
        }

        TEST_METHOD(Clock_AdvancesCorrectly_With20SecondsOfSilence)
        {
            SimpleMediaClock clock;
            
            LARGE_INTEGER qpc;
            QueryPerformanceCounter(&qpc);
            clock.Start(qpc.QuadPart);
            
            const UINT32 sampleRate = 48000;
            const UINT32 sleepDurationMs = 10;
            const UINT32 virtualFramesPerSleep = (sampleRate * sleepDurationMs) / 1000; // 480 frames
            const int iterations = 2000; // 20 seconds at 10ms intervals
            
            // Simulate audio capture thread with no audio playing
            for (int i = 0; i < iterations; i++)
            {
                clock.AdvanceByAudioSamples(virtualFramesPerSleep, sampleRate);
            }
            
            LONGLONG currentTime = clock.GetCurrentTime();
            LONGLONG expectedTime = 200000000LL; // 20 seconds in 100ns ticks
            
            // Calculate the difference
            LONGLONG diff = currentTime - expectedTime;
            
            char msg[256];
            sprintf_s(msg, "Expected: %lld, Got: %lld, Diff: %lld", expectedTime, currentTime, diff);
            Logger::WriteMessage(msg);
            
            // Allow tolerance of 1ms
            LONGLONG tolerance = 10000LL; // 1ms in 100ns ticks
            Assert::IsTrue(std::abs(diff) <= tolerance, L"Clock should advance to approximately 20 seconds");
        }

        TEST_METHOD(Clock_AdvancesCorrectly_WithMixedSamples)
        {
            SimpleMediaClock clock;
            
            LARGE_INTEGER qpc;
            QueryPerformanceCounter(&qpc);
            clock.Start(qpc.QuadPart);
            
            const UINT32 sampleRate = 48000;
            
            // Simulate mixed scenario: 5 seconds of silence, then 5 seconds of audio, then 10 seconds of silence
            // Each period uses different frame counts to simulate real vs virtual frames
            
            // 5 seconds of silence (500 iterations of 10ms virtual frames)
            const UINT32 virtualFrames = 480; // 10ms
            for (int i = 0; i < 500; i++)
            {
                clock.AdvanceByAudioSamples(virtualFrames, sampleRate);
            }
            
            LONGLONG time5sec = clock.GetCurrentTime();
            Assert::IsTrue(std::abs(time5sec - 50000000LL) <= 10000LL, L"Should be ~5 seconds");
            
            // 5 seconds of audio (varying frame sizes to simulate real WASAPI behavior)
            for (int i = 0; i < 500; i++)
            {
                // Alternate between 479, 480, and 481 frames to simulate real timing
                UINT32 frames = 480 + (i % 3) - 1;
                clock.AdvanceByAudioSamples(frames, sampleRate);
            }
            
            LONGLONG time10sec = clock.GetCurrentTime();
            Assert::IsTrue(std::abs(time10sec - 100000000LL) <= 10000LL, L"Should be ~10 seconds");
            
            // 10 seconds of silence (1000 iterations of 10ms virtual frames)
            for (int i = 0; i < 1000; i++)
            {
                clock.AdvanceByAudioSamples(virtualFrames, sampleRate);
            }
            
            LONGLONG time20sec = clock.GetCurrentTime();
            Assert::IsTrue(std::abs(time20sec - 200000000LL) <= 10000LL, L"Should be ~20 seconds");
        }

        TEST_METHOD(Clock_GetRelativeTime_CalculatesCorrectly)
        {
            SimpleMediaClock clock;
            
            LARGE_INTEGER qpc;
            QueryPerformanceCounter(&qpc);
            LONGLONG startQpc = qpc.QuadPart;
            
            clock.Start(startQpc);
            
            // Wait a bit and get another QPC timestamp
            std::this_thread::sleep_for(std::chrono::milliseconds(100));
            
            QueryPerformanceCounter(&qpc);
            LONGLONG laterQpc = qpc.QuadPart;
            
            LONGLONG relativeTime = clock.GetRelativeTime(laterQpc);
            
            // Should be approximately 100ms (1,000,000 ticks in 100ns units)
            // Allow 20ms tolerance due to system scheduling
            Assert::IsTrue(relativeTime >= 800000LL && relativeTime <= 1200000LL, 
                L"Relative time should be approximately 100ms");
        }
    };
}
