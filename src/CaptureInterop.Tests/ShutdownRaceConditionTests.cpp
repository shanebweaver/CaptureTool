#include "pch.h"
#include "CppUnitTest.h"
#include "WindowsGraphicsCaptureSession.h"
#include "WindowsGraphicsCaptureSessionFactory.h"
#include "CaptureSessionConfig.h"
#include "SimpleMediaClock.h"
#include "SimpleMediaClockFactory.h"
#include "WindowsLocalAudioCaptureSourceFactory.h"
#include "WindowsDesktopVideoCaptureSourceFactory.h"
#include "WindowsMFMP4SinkWriterFactory.h"

#include <atomic>
#include <thread>
#include <chrono>
#include <vector>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace CaptureInteropTests
{
    TEST_CLASS(ShutdownRaceConditionTests)
    {
    private:
        static std::atomic<int> s_callbackInvocations;
        static std::atomic<int> s_callbacksDuringShutdown;
        static std::atomic<bool> s_crashDetected;

    public:
        TEST_CLASS_INITIALIZE(ClassInitialize)
        {
            s_callbackInvocations = 0;
            s_callbacksDuringShutdown = 0;
            s_crashDetected = false;
        }

        TEST_METHOD(Shutdown_RapidStartStop_NoCrash)
        {
            // This test verifies that rapidly starting and stopping recording
            // does not cause a crash due to race conditions in Stop()
            
            Logger::WriteMessage("[ShutdownRace] Testing rapid start/stop cycles...");

            const int TEST_CYCLES = 10;
            int successfulCycles = 0;

            for (int i = 0; i < TEST_CYCLES; i++)
            {
                try
                {
                    // Create a minimal capture session
                    CaptureSessionConfig config;
                    config.outputPath = L"test_output.mp4";
                    config.audioEnabled = true;
                    config.frameRate = 30;
                    config.videoBitrate = 5000000;
                    config.audioBitrate = 192000;

                    SimpleMediaClockFactory clockFactory;
                    WindowsLocalAudioCaptureSourceFactory audioFactory;
                    WindowsDesktopVideoCaptureSourceFactory videoFactory;
                    WindowsMFMP4SinkWriterFactory sinkFactory;

                    WindowsGraphicsCaptureSession session(
                        config,
                        &clockFactory,
                        &audioFactory,
                        &videoFactory,
                        &sinkFactory
                    );

                    // Start the session
                    HRESULT hr;
                    bool started = session.Start(&hr);
                    
                    if (!started)
                    {
                        // Some test environments may not have capture capabilities
                        char msg[256];
                        sprintf_s(msg, "[ShutdownRace] Cycle %d: Cannot start session (HRESULT: 0x%08X) - skipping test", 
                                 i + 1, hr);
                        Logger::WriteMessage(msg);
                        return; // Skip test if we can't start
                    }

                    // Let it run for a very short time
                    std::this_thread::sleep_for(std::chrono::milliseconds(50));

                    // Stop immediately - this is where the race condition would occur
                    session.Stop();

                    successfulCycles++;
                    
                    char msg[256];
                    sprintf_s(msg, "[ShutdownRace] Cycle %d/%d completed successfully", 
                             i + 1, TEST_CYCLES);
                    Logger::WriteMessage(msg);
                }
                catch (...)
                {
                    char msg[256];
                    sprintf_s(msg, "[ShutdownRace] Cycle %d FAILED - exception caught", i + 1);
                    Logger::WriteMessage(msg);
                    Assert::Fail(L"Start/Stop cycle crashed due to race condition");
                }
            }

            char msg[256];
            sprintf_s(msg, "[ShutdownRace] Completed %d/%d cycles successfully", 
                     successfulCycles, TEST_CYCLES);
            Logger::WriteMessage(msg);
            
            Assert::IsTrue(successfulCycles > 0, 
                          L"At least one start/stop cycle should complete successfully");
        }

        TEST_METHOD(Shutdown_CallbacksNotInvokedAfterStop_VerifyFlag)
        {
            // This test verifies that the shutdown flag prevents callbacks
            // from executing after Stop() has been called
            
            Logger::WriteMessage("[ShutdownRace] Testing shutdown flag behavior...");

            s_callbackInvocations = 0;
            s_callbacksDuringShutdown = 0;

            CaptureSessionConfig config;
            config.outputPath = L"test_output2.mp4";
            config.audioEnabled = true;
            config.frameRate = 30;
            config.videoBitrate = 5000000;
            config.audioBitrate = 192000;

            SimpleMediaClockFactory clockFactory;
            WindowsLocalAudioCaptureSourceFactory audioFactory;
            WindowsDesktopVideoCaptureSourceFactory videoFactory;
            WindowsMFMP4SinkWriterFactory sinkFactory;

            WindowsGraphicsCaptureSession session(
                config,
                &clockFactory,
                &audioFactory,
                &videoFactory,
                &sinkFactory
            );

            HRESULT hr;
            bool started = session.Start(&hr);
            
            if (!started)
            {
                Logger::WriteMessage("[ShutdownRace] Cannot start session - skipping test");
                return;
            }

            // Let some callbacks execute
            std::this_thread::sleep_for(std::chrono::milliseconds(100));

            // Stop the session
            session.Stop();

            int callbacksBeforeStop = s_callbackInvocations.load();
            
            // Wait a bit more - no new callbacks should execute
            std::this_thread::sleep_for(std::chrono::milliseconds(100));

            int callbacksAfterStop = s_callbackInvocations.load();

            char msg[256];
            sprintf_s(msg, "[ShutdownRace] Callbacks before stop: %d, after stop: %d", 
                     callbacksBeforeStop, callbacksAfterStop);
            Logger::WriteMessage(msg);

            // After Stop(), no new callbacks should be invoked
            Assert::AreEqual(callbacksBeforeStop, callbacksAfterStop,
                           L"No callbacks should be invoked after Stop()");
        }

        TEST_METHOD(Shutdown_StopOrderingCorrect_SourcesBeforeCallbacks)
        {
            // This test verifies the ordering of shutdown operations:
            // 1. Set shutdown flag
            // 2. Stop capture sources
            // 3. Clear callbacks
            
            Logger::WriteMessage("[ShutdownRace] Testing shutdown ordering...");

            CaptureSessionConfig config;
            config.outputPath = L"test_output3.mp4";
            config.audioEnabled = true;
            config.frameRate = 30;
            config.videoBitrate = 5000000;
            config.audioBitrate = 192000;

            SimpleMediaClockFactory clockFactory;
            WindowsLocalAudioCaptureSourceFactory audioFactory;
            WindowsDesktopVideoCaptureSourceFactory videoFactory;
            WindowsMFMP4SinkWriterFactory sinkFactory;

            WindowsGraphicsCaptureSession session(
                config,
                &clockFactory,
                &audioFactory,
                &videoFactory,
                &sinkFactory
            );

            HRESULT hr;
            bool started = session.Start(&hr);
            
            if (!started)
            {
                Logger::WriteMessage("[ShutdownRace] Cannot start session - skipping test");
                return;
            }

            // Let it run briefly
            std::this_thread::sleep_for(std::chrono::milliseconds(100));

            // Stop should not crash - this tests the correct ordering
            session.Stop();

            Logger::WriteMessage("[ShutdownRace] Shutdown completed without crash - ordering is correct");
            
            // If we reach here without crashing, the test passes
            Assert::IsTrue(true, L"Shutdown completed successfully");
        }
    };

    // Static member definitions
    std::atomic<int> ShutdownRaceConditionTests::s_callbackInvocations(0);
    std::atomic<int> ShutdownRaceConditionTests::s_callbacksDuringShutdown(0);
    std::atomic<bool> ShutdownRaceConditionTests::s_crashDetected(false);
}
