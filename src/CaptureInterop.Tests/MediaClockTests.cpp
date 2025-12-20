#include "pch.h"
#include "CppUnitTest.h"
#include "MediaClock.h"
#include <thread>
#include <chrono>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace CaptureInteropTests
{
    TEST_CLASS(MediaClockTests)
    {
    public:
        TEST_METHOD(Constructor_InitializesWithZeroTime)
        {
            MediaClock clock(MediaClock::SampleRate{48000});
            
            auto currentTime = clock.CurrentTime();
            Assert::AreEqual(0LL, currentTime.ticks);
            Assert::AreEqual(0LL, clock.GetTotalSamplesCaptured());
        }

        TEST_METHOD(Constructor_InitializesWithStartTime)
        {
            const int64_t startTicks = 10'000'000LL; // 1 second
            MediaClock clock(MediaClock::SampleRate{48000}, MediaClock::MediaTime{startTicks});
            
            auto currentTime = clock.CurrentTime();
            Assert::AreEqual(startTicks, currentTime.ticks);
            Assert::AreEqual(0LL, clock.GetTotalSamplesCaptured());
        }

        TEST_METHOD(GetSampleRate_ReturnsCorrectRate)
        {
            MediaClock clock(MediaClock::SampleRate{48000});
            
            auto rate = clock.GetSampleRate();
            Assert::AreEqual(48000u, rate.Hz);
        }

        TEST_METHOD(GetStartTime_ReturnsCorrectStartTime)
        {
            const int64_t startTicks = 5'000'000LL; // 0.5 second
            MediaClock clock(MediaClock::SampleRate{48000}, MediaClock::MediaTime{startTicks});
            
            auto startTime = clock.GetStartTime();
            Assert::AreEqual(startTicks, startTime.ticks);
        }

        TEST_METHOD(Advance_IncrementsSampleCount)
        {
            MediaClock clock(MediaClock::SampleRate{48000});
            
            clock.Advance(480); // 10ms at 48kHz
            Assert::AreEqual(480LL, clock.GetTotalSamplesCaptured());
            
            clock.Advance(480);
            Assert::AreEqual(960LL, clock.GetTotalSamplesCaptured());
        }

        TEST_METHOD(CurrentTime_CalculatesCorrectTime)
        {
            MediaClock clock(MediaClock::SampleRate{48000});
            
            // Advance by 48000 samples = 1 second
            clock.Advance(48000);
            
            auto currentTime = clock.CurrentTime();
            // 1 second = 10,000,000 ticks (100ns units)
            Assert::AreEqual(10'000'000LL, currentTime.ticks);
        }

        TEST_METHOD(CurrentTime_CalculatesWithStartTime)
        {
            const int64_t startTicks = 5'000'000LL; // 0.5 second
            MediaClock clock(MediaClock::SampleRate{48000}, MediaClock::MediaTime{startTicks});
            
            // Advance by 24000 samples = 0.5 second
            clock.Advance(24000);
            
            auto currentTime = clock.CurrentTime();
            // 0.5 + 0.5 = 1 second = 10,000,000 ticks
            Assert::AreEqual(10'000'000LL, currentTime.ticks);
        }

        TEST_METHOD(CurrentTime_HandlesMultipleAdvances)
        {
            MediaClock clock(MediaClock::SampleRate{48000});
            
            // Simulate typical audio buffer advances (480 samples = 10ms at 48kHz)
            for (int i = 0; i < 100; i++) {
                clock.Advance(480);
            }
            
            auto currentTime = clock.CurrentTime();
            // 100 buffers * 10ms = 1 second = 10,000,000 ticks
            Assert::AreEqual(10'000'000LL, currentTime.ticks);
            Assert::AreEqual(48000LL, clock.GetTotalSamplesCaptured());
        }

        TEST_METHOD(CurrentTime_MonotonicallyIncreases)
        {
            MediaClock clock(MediaClock::SampleRate{48000});
            
            int64_t previousTime = 0;
            for (int i = 0; i < 100; i++) {
                clock.Advance(480);
                auto currentTime = clock.CurrentTime();
                Assert::IsTrue(currentTime.ticks >= previousTime, L"Time must be monotonically increasing");
                previousTime = currentTime.ticks;
            }
        }

        TEST_METHOD(CurrentTime_DifferentSampleRates_44100Hz)
        {
            MediaClock clock(MediaClock::SampleRate{44100});
            
            // Advance by 44100 samples = 1 second at 44.1kHz
            clock.Advance(44100);
            
            auto currentTime = clock.CurrentTime();
            Assert::AreEqual(10'000'000LL, currentTime.ticks);
        }

        TEST_METHOD(CurrentTime_DifferentSampleRates_96000Hz)
        {
            MediaClock clock(MediaClock::SampleRate{96000});
            
            // Advance by 96000 samples = 1 second at 96kHz
            clock.Advance(96000);
            
            auto currentTime = clock.CurrentTime();
            Assert::AreEqual(10'000'000LL, currentTime.ticks);
        }

        TEST_METHOD(CurrentTime_PrecisionWithSmallIncrements)
        {
            MediaClock clock(MediaClock::SampleRate{48000});
            
            // Advance by 1 sample (~20.83 microseconds at 48kHz)
            clock.Advance(1);
            
            auto currentTime = clock.CurrentTime();
            // 1 sample at 48kHz = 10,000,000 / 48,000 = 208.33... ticks
            // Should be truncated to 208
            Assert::AreEqual(208LL, currentTime.ticks);
        }

        TEST_METHOD(ThreadSafety_ConcurrentAdvanceAndRead)
        {
            MediaClock clock(MediaClock::SampleRate{48000});
            std::atomic<bool> stopFlag{false};
            
            // Thread that advances the clock
            std::thread advanceThread([&clock, &stopFlag]() {
                while (!stopFlag) {
                    clock.Advance(480); // 10ms buffer
                    std::this_thread::sleep_for(std::chrono::microseconds(100));
                }
            });
            
            // Thread that reads from the clock
            std::thread readThread([&clock, &stopFlag]() {
                int64_t previousTime = 0;
                while (!stopFlag) {
                    auto currentTime = clock.CurrentTime();
                    auto samples = clock.GetTotalSamplesCaptured();
                    
                    // Verify monotonicity
                    Assert::IsTrue(currentTime.ticks >= previousTime, L"Time must be monotonically increasing");
                    Assert::IsTrue(samples >= 0, L"Samples must be non-negative");
                    
                    previousTime = currentTime.ticks;
                    std::this_thread::sleep_for(std::chrono::microseconds(50));
                }
            });
            
            // Let threads run for a short time
            std::this_thread::sleep_for(std::chrono::milliseconds(100));
            stopFlag = true;
            
            advanceThread.join();
            readThread.join();
            
            // Verify clock advanced significantly
            auto finalTime = clock.CurrentTime();
            Assert::IsTrue(finalTime.ticks > 0, L"Clock should have advanced");
        }

        TEST_METHOD(ThreadSafety_MultipleReaders)
        {
            MediaClock clock(MediaClock::SampleRate{48000});
            std::atomic<bool> stopFlag{false};
            
            // Advance clock in main thread
            std::thread advanceThread([&clock, &stopFlag]() {
                while (!stopFlag) {
                    clock.Advance(480);
                    std::this_thread::sleep_for(std::chrono::microseconds(100));
                }
            });
            
            // Multiple reader threads
            std::vector<std::thread> readers;
            for (int i = 0; i < 5; i++) {
                readers.emplace_back([&clock, &stopFlag]() {
                    while (!stopFlag) {
                        auto time = clock.CurrentTime();
                        auto samples = clock.GetTotalSamplesCaptured();
                        auto rate = clock.GetSampleRate();
                        
                        // Just verify no crashes and values are sane
                        Assert::IsTrue(time.ticks >= 0);
                        Assert::IsTrue(samples >= 0);
                        Assert::AreEqual(48000u, rate.Hz);
                        
                        std::this_thread::sleep_for(std::chrono::microseconds(50));
                    }
                });
            }
            
            std::this_thread::sleep_for(std::chrono::milliseconds(100));
            stopFlag = true;
            
            advanceThread.join();
            for (auto& thread : readers) {
                thread.join();
            }
        }

        TEST_METHOD(LongRunning_NoOverflow)
        {
            MediaClock clock(MediaClock::SampleRate{48000});
            
            // Simulate 1 hour of audio capture
            // 1 hour = 3600 seconds * 48000 samples/sec = 172,800,000 samples
            // Advance in typical 10ms chunks (480 samples)
            const int64_t totalSamples = 172'800'000LL;
            const int64_t chunkSize = 480;
            const int64_t numChunks = totalSamples / chunkSize;
            
            for (int64_t i = 0; i < numChunks; i++) {
                clock.Advance(static_cast<uint32_t>(chunkSize));
            }
            
            auto currentTime = clock.CurrentTime();
            auto samples = clock.GetTotalSamplesCaptured();
            
            // 1 hour = 36,000,000,000,000 ticks
            int64_t expectedTicks = 36'000'000'000'000LL;
            Assert::AreEqual(expectedTicks, currentTime.ticks);
            Assert::AreEqual(totalSamples, samples);
        }

        TEST_METHOD(ZeroSamples_NoChange)
        {
            MediaClock clock(MediaClock::SampleRate{48000});
            
            clock.Advance(480);
            auto time1 = clock.CurrentTime();
            
            clock.Advance(0);
            auto time2 = clock.CurrentTime();
            
            Assert::AreEqual(time1.ticks, time2.ticks);
            Assert::AreEqual(480LL, clock.GetTotalSamplesCaptured());
        }
    };
}
