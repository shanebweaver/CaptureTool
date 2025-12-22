#include "pch.h"
#include "CppUnitTest.h"
#include "SimpleMediaClock.h"
#include "WindowsLocalAudioCaptureSource.h"

#include <thread>
#include <chrono>
#include <Windows.h>
#include <algorithm>
#include <numeric>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace CaptureInteropTests
{
    TEST_CLASS(CaptureSessionIntegrationTests)
    {
    public:
        TEST_METHOD(Integration_ClockAdvances_DuringSilentRecording)
        {
            // This test verifies that the media clock advances correctly
            // even when there's no audio playing (silent recording scenario)
            
            SimpleMediaClock mediaClock;
            WindowsLocalAudioCaptureSource audioSource(&mediaClock);

            // Initialize audio
            HRESULT hr;
            bool result = audioSource.Initialize(&hr);
            if (!result)
            {
                Logger::WriteMessage("Skipping test - no audio device available");
                return;
            }

            // Start the clock
            LARGE_INTEGER qpc;
            QueryPerformanceCounter(&qpc);
            mediaClock.Start(qpc.QuadPart);
            mediaClock.SetClockAdvancer(&audioSource);

            // Record initial clock time
            LONGLONG startClockTime = mediaClock.GetCurrentTime();
            
            char msg[512];
            sprintf_s(msg, "[Integration] Starting test - Clock time: %.3f seconds", 
                     startClockTime / 10000000.0);
            Logger::WriteMessage(msg);

            // Start audio capture
            result = audioSource.Start(&hr);
            Assert::IsTrue(result, L"Audio source should start");

            // Sample clock periodically for 5 seconds
            std::vector<LONGLONG> clockSamples;
            for (int i = 0; i < 50; i++) // 50 samples over 5 seconds
            {
                std::this_thread::sleep_for(std::chrono::milliseconds(100));
                clockSamples.push_back(mediaClock.GetCurrentTime());
            }

            // Stop audio
            audioSource.Stop();

            // Analyze the samples
            LONGLONG totalAdvancement = clockSamples.back() - clockSamples.front();
            double advancementSeconds = totalAdvancement / 10000000.0;
            
            sprintf_s(msg, "[Integration] Clock advanced: %.3f seconds (expected ~5.0 seconds)", 
                     advancementSeconds);
            Logger::WriteMessage(msg);

            // Check if clock is advancing monotonically
            bool monotonic = true;
            for (size_t i = 1; i < clockSamples.size(); i++)
            {
                if (clockSamples[i] <= clockSamples[i - 1])
                {
                    monotonic = false;
                    sprintf_s(msg, "[Integration] Clock NOT advancing between sample %zu and %zu", i-1, i);
                    Logger::WriteMessage(msg);
                    break;
                }
            }

            Assert::IsTrue(monotonic, L"Clock should advance monotonically");

            // Calculate average advancement per 100ms
            std::vector<double> advancements;
            for (size_t i = 1; i < clockSamples.size(); i++)
            {
                double advance = (clockSamples[i] - clockSamples[i-1]) / 10000000.0;
                advancements.push_back(advance);
            }

            // Log statistics
            double minAdvance = *std::min_element(advancements.begin(), advancements.end());
            double maxAdvance = *std::max_element(advancements.begin(), advancements.end());
            double avgAdvance = std::accumulate(advancements.begin(), advancements.end(), 0.0) / advancements.size();

            sprintf_s(msg, "[Integration] Advancement stats: min=%.4f, max=%.4f, avg=%.4f seconds per 100ms", 
                     minAdvance, maxAdvance, avgAdvance);
            Logger::WriteMessage(msg);

            // Clock should advance approximately 5 seconds (allow 1s tolerance)
            Assert::IsTrue(advancementSeconds >= 4.0 && advancementSeconds <= 6.0,
                          L"Clock should advance approximately 5 seconds");

            // Average advancement should be approximately 0.1 seconds per sample (allow 50% tolerance)
            Assert::IsTrue(avgAdvance >= 0.05 && avgAdvance <= 0.15,
                          L"Average advancement should be approximately 0.1 seconds per 100ms");
        }

        TEST_METHOD(Integration_ClockRate_MatchesWallClockTime)
        {
            // This test verifies that the clock rate matches actual wall-clock time
            // This is the critical test for the 20-second ? 3-second bug
            
            SimpleMediaClock mediaClock;
            WindowsLocalAudioCaptureSource audioSource(&mediaClock);

            HRESULT hr;
            if (!audioSource.Initialize(&hr) || !audioSource.Start(&hr))
            {
                Logger::WriteMessage("Skipping test - audio not available");
                return;
            }

            LARGE_INTEGER qpc, qpcFreq;
            QueryPerformanceFrequency(&qpcFreq);
            QueryPerformanceCounter(&qpc);
            
            LONGLONG startQpc = qpc.QuadPart;
            mediaClock.Start(startQpc);
            mediaClock.SetClockAdvancer(&audioSource);

            // Run for 10 seconds, sampling both clocks
            const int testDurationSeconds = 10;
            const int samplesPerSecond = 10;
            const int totalSamples = testDurationSeconds * samplesPerSecond;

            std::vector<std::pair<double, double>> samples; // <wallClock, mediaClock>

            for (int i = 0; i < totalSamples; i++)
            {
                std::this_thread::sleep_for(std::chrono::milliseconds(100));
                
                QueryPerformanceCounter(&qpc);
                double wallClockSeconds = (qpc.QuadPart - startQpc) / (double)qpcFreq.QuadPart;
                double mediaClockSeconds = mediaClock.GetCurrentTime() / 10000000.0;
                
                samples.push_back({wallClockSeconds, mediaClockSeconds});
            }

            audioSource.Stop();

            // Calculate the ratio of media clock to wall clock
            double finalWallClock = samples.back().first;
            double finalMediaClock = samples.back().second;
            double clockRatio = finalMediaClock / finalWallClock;

            char msg[512];
            sprintf_s(msg, "[Integration] Wall clock: %.3f sec, Media clock: %.3f sec, Ratio: %.4f", 
                     finalWallClock, finalMediaClock, clockRatio);
            Logger::WriteMessage(msg);

            // Log some sample points (avoid std::min macro conflict)
            size_t maxSamples = samples.size() > 10 ? 10 : samples.size();
            for (size_t i = 0; i < maxSamples; i++)
            {
                sprintf_s(msg, "[Integration] Sample %zu: Wall=%.3f, Media=%.3f, Diff=%.3f", 
                         i, samples[i].first, samples[i].second, 
                         samples[i].first - samples[i].second);
                Logger::WriteMessage(msg);
            }

            // The ratio should be very close to 1.0 (within 5%)
            Assert::IsTrue(clockRatio >= 0.95 && clockRatio <= 1.05,
                          L"Media clock should advance at the same rate as wall clock (within 5%)");

            // The final times should be close (within 0.5 seconds)
            double timeDifference = std::abs(finalWallClock - finalMediaClock);
            sprintf_s(msg, "[Integration] Time difference: %.3f seconds", timeDifference);
            Logger::WriteMessage(msg);
            
            Assert::IsTrue(timeDifference <= 0.5,
                          L"Media clock should match wall clock time within 0.5 seconds");
        }

        TEST_METHOD(Integration_20SecondRecording_ProducesCorrectDuration)
        {
            // This is the ultimate integration test - simulating the exact scenario
            // that was producing 3-second videos from 20-second recordings
            
            SimpleMediaClock mediaClock;
            WindowsLocalAudioCaptureSource audioSource(&mediaClock);

            HRESULT hr;
            if (!audioSource.Initialize(&hr) || !audioSource.Start(&hr))
            {
                Logger::WriteMessage("Skipping test - audio not available");
                return;
            }

            LARGE_INTEGER qpc;
            QueryPerformanceCounter(&qpc);
            mediaClock.Start(qpc.QuadPart);
            mediaClock.SetClockAdvancer(&audioSource);

            Logger::WriteMessage("[Integration] Starting 20-second simulation...");

            // Simulate 20 seconds of recording
            const int RECORDING_DURATION_SECONDS = 20;
            auto startTime = std::chrono::steady_clock::now();
            
            // Sample the clock every second
            std::vector<double> clockReadings;
            for (int i = 0; i <= RECORDING_DURATION_SECONDS; i++)
            {
                double mediaClockSeconds = mediaClock.GetCurrentTime() / 10000000.0;
                clockReadings.push_back(mediaClockSeconds);
                
                char msg[256];
                sprintf_s(msg, "[Integration] Second %d: Media clock = %.3f seconds", 
                         i, mediaClockSeconds);
                Logger::WriteMessage(msg);
                
                if (i < RECORDING_DURATION_SECONDS)
                {
                    std::this_thread::sleep_for(std::chrono::seconds(1));
                }
            }

            auto endTime = std::chrono::steady_clock::now();
            auto actualDuration = std::chrono::duration_cast<std::chrono::milliseconds>(endTime - startTime).count() / 1000.0;

            audioSource.Stop();

            double finalClockReading = clockReadings.back();

            char msg[512];
            sprintf_s(msg, "[Integration] 20-second test completed: Actual duration=%.3f sec, Media clock=%.3f sec",
                     actualDuration, finalClockReading);
            Logger::WriteMessage(msg);

            // The media clock should show approximately 20 seconds (within 1 second tolerance)
            Assert::IsTrue(finalClockReading >= 19.0 && finalClockReading <= 21.0,
                          L"Media clock should show approximately 20 seconds");

            // Check that the clock was advancing consistently
            bool consistentAdvancement = true;
            for (size_t i = 1; i < clockReadings.size(); i++)
            {
                double advancement = clockReadings[i] - clockReadings[i-1];
                if (advancement < 0.5 || advancement > 1.5) // Should be ~1 second per reading
                {
                    consistentAdvancement = false;
                    sprintf_s(msg, "[Integration] Inconsistent advancement at second %zu: %.3f seconds", 
                             i, advancement);
                    Logger::WriteMessage(msg);
                }
            }

            Assert::IsTrue(consistentAdvancement, 
                          L"Clock should advance consistently (~1 second per second)");
        }
    };
}
