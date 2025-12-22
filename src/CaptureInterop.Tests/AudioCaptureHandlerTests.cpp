#include "pch.h"
#include "CppUnitTest.h"
#include "WindowsLocalAudioCaptureSource.h"
#include "SimpleMediaClock.h"

#include <thread>
#include <chrono>
#include <atomic>
#include <algorithm>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace CaptureInteropTests
{
    TEST_CLASS(AudioCaptureSourceTests)
    {
    private:
        class ClockMonitor
        {
        public:
            SimpleMediaClock* clock;
            std::atomic<bool> running{false};
            std::vector<LONGLONG> samples;
            std::thread monitorThread;

            ClockMonitor(SimpleMediaClock* c) : clock(c) {}

            void Start()
            {
                running = true;
                monitorThread = std::thread([this]() {
                    while (running)
                    {
                        if (clock && clock->IsRunning())
                        {
                            samples.push_back(clock->GetCurrentTime());
                        }
                        std::this_thread::sleep_for(std::chrono::milliseconds(100));
                    }
                });
            }

            void Stop()
            {
                running = false;
                if (monitorThread.joinable())
                {
                    monitorThread.join();
                }
            }

            ~ClockMonitor()
            {
                Stop();
            }

            bool IsClockAdvancing()
            {
                if (samples.size() < 2) return false;
                
                // Check if clock is advancing monotonically
                for (size_t i = 1; i < samples.size(); i++)
                {
                    if (samples[i] <= samples[i - 1])
                    {
                        return false;
                    }
                }
                return true;
            }

            LONGLONG GetTotalAdvancement()
            {
                if (samples.empty()) return 0;
                return samples.back() - samples.front();
            }
        };

    public:
        TEST_METHOD(AudioSource_Initializes_WithValidLoopback)
        {
            SimpleMediaClock clock;
            WindowsLocalAudioCaptureSource audioSource(&clock);
            
            HRESULT hr;
            bool result = audioSource.Initialize(&hr);
            
            // Note: This might fail if no audio device is available
            if (result)
            {
                Assert::IsTrue(SUCCEEDED(hr));
                Assert::IsNotNull(audioSource.GetFormat());
            }
        }

        TEST_METHOD(AudioSource_CachesSampleRate_AfterInitialization)
        {
            SimpleMediaClock clock;
            WindowsLocalAudioCaptureSource audioSource(&clock);
            
            HRESULT hr;
            if (audioSource.Initialize(&hr))
            {
                WAVEFORMATEX* format = audioSource.GetFormat();
                Assert::IsNotNull(format);
                Assert::IsTrue(format->nSamplesPerSec > 0);
                
                // Sample rate should be standard (typically 48000 Hz or 44100 Hz)
                Assert::IsTrue(format->nSamplesPerSec == 48000 || 
                             format->nSamplesPerSec == 44100);
            }
        }

        TEST_METHOD(AudioSource_AdvancesClock_DuringSilence)
        {
            SimpleMediaClock clock;
            WindowsLocalAudioCaptureSource audioSource(&clock);
            
            HRESULT hr;
            if (!audioSource.Initialize(&hr))
            {
                Logger::WriteMessage("Skipping test - no audio device available");
                return;
            }

            // Set up clock
            LARGE_INTEGER qpc;
            QueryPerformanceCounter(&qpc);
            clock.Start(qpc.QuadPart);
            clock.SetClockAdvancer(&audioSource);

            // Start monitor before audio source
            ClockMonitor monitor(&clock);
            monitor.Start();

            // Start audio source
            if (!audioSource.Start(&hr))
            {
                Logger::WriteMessage("Skipping test - failed to start audio capture");
                return;
            }

            // Let it run for 2 seconds
            std::this_thread::sleep_for(std::chrono::seconds(2));

            // Stop audio source
            audioSource.Stop();
            monitor.Stop();

            // Check that clock advanced
            Assert::IsTrue(monitor.IsClockAdvancing(), L"Clock should be advancing even during silence");
            
            LONGLONG advancement = monitor.GetTotalAdvancement();
            char msg[256];
            sprintf_s(msg, "Clock advanced by: %lld ticks (%.2f seconds)", 
                     advancement, advancement / 10000000.0);
            Logger::WriteMessage(msg);
            
            // Log all samples to understand the pattern
            sprintf_s(msg, "Collected %zu samples:", monitor.samples.size());
            Logger::WriteMessage(msg);
            size_t maxSamples = monitor.samples.size() > 10 ? 10 : monitor.samples.size();
            for (size_t i = 0; i < maxSamples; i++)
            {
                sprintf_s(msg, "  Sample %zu: %.3f seconds", i, monitor.samples[i] / 10000000.0);
                Logger::WriteMessage(msg);
            }

            // Should have advanced approximately 2 seconds (allow 0.5s tolerance)
            LONGLONG expectedMin = 15000000LL; // 1.5 seconds
            LONGLONG expectedMax = 25000000LL; // 2.5 seconds
            Assert::IsTrue(advancement >= expectedMin && advancement <= expectedMax,
                          L"Clock should advance approximately 2 seconds");
        }

        TEST_METHOD(AudioSource_AdvancesClock_For5Seconds)
        {
            SimpleMediaClock clock;
            WindowsLocalAudioCaptureSource audioSource(&clock);
            
            HRESULT hr;
            if (!audioSource.Initialize(&hr))
            {
                Logger::WriteMessage("Skipping test - no audio device available");
                return;
            }

            LARGE_INTEGER qpc;
            QueryPerformanceCounter(&qpc);
            clock.Start(qpc.QuadPart);
            clock.SetClockAdvancer(&audioSource);

            if (!audioSource.Start(&hr))
            {
                Logger::WriteMessage("Skipping test - failed to start audio capture");
                return;
            }

            // Sample clock at start
            LONGLONG startTime = clock.GetCurrentTime();
            
            // Run for 5 seconds
            std::this_thread::sleep_for(std::chrono::seconds(5));
            
            // Sample clock at end
            LONGLONG endTime = clock.GetCurrentTime();
            
            audioSource.Stop();

            LONGLONG advancement = endTime - startTime;
            char msg[256];
            sprintf_s(msg, "Clock advanced by: %lld ticks (%.2f seconds)", 
                     advancement, advancement / 10000000.0);
            Logger::WriteMessage(msg);

            // Should have advanced approximately 5 seconds (allow 1s tolerance)
            LONGLONG expectedMin = 40000000LL; // 4 seconds
            LONGLONG expectedMax = 60000000LL; // 6 seconds
            Assert::IsTrue(advancement >= expectedMin && advancement <= expectedMax,
                          L"Clock should advance approximately 5 seconds");
        }

        TEST_METHOD(AudioSource_MaintainsConsistentRate)
        {
            SimpleMediaClock clock;
            WindowsLocalAudioCaptureSource audioSource(&clock);
            
            HRESULT hr;
            if (!audioSource.Initialize(&hr) || !audioSource.Start(&hr))
            {
                Logger::WriteMessage("Skipping test - audio not available");
                return;
            }

            LARGE_INTEGER qpc;
            QueryPerformanceCounter(&qpc);
            clock.Start(qpc.QuadPart);
            clock.SetClockAdvancer(&audioSource);

            // Take samples every second for 5 seconds
            std::vector<LONGLONG> samples;
            for (int i = 0; i < 6; i++)
            {
                samples.push_back(clock.GetCurrentTime());
                std::this_thread::sleep_for(std::chrono::seconds(1));
            }

            audioSource.Stop();

            // Calculate intervals between samples
            std::vector<LONGLONG> intervals;
            for (size_t i = 1; i < samples.size(); i++)
            {
                intervals.push_back(samples[i] - samples[i - 1]);
            }

            // Log the intervals
            for (size_t i = 0; i < intervals.size(); i++)
            {
                char msg[256];
                sprintf_s(msg, "Interval %zu: %.2f seconds", 
                         i, intervals[i] / 10000000.0);
                Logger::WriteMessage(msg);
            }

            // Each interval should be approximately 1 second (allow 0.3s tolerance)
            for (LONGLONG interval : intervals)
            {
                Assert::IsTrue(interval >= 7000000LL && interval <= 13000000LL,
                              L"Each interval should be approximately 1 second");
            }
        }
    };
}
