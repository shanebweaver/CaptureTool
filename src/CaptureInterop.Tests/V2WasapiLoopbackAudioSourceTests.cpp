#include "pch.h"
#include "CppUnitTest.h"
#include "V2/Audio/FakeWasapiLoopbackAudioProvider.h"
#include "V2/Audio/FakeWasapiLoopbackPacketProvider.h"
#include "V2/Audio/WasapiLoopbackAudioSource.h"
#include "V2/Audio/WindowsWasapiLoopbackPacketProvider.h"

#include <atomic>
#include <chrono>
#include <condition_variable>
#include <functional>
#include <mutex>
#include <optional>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using namespace CaptureInterop::V2;
using namespace CaptureInterop::V2::Audio;

namespace
{
    AudioMediaType CreateMediaType(
        uint32_t sampleRate = 48000,
        uint16_t channels = 2)
    {
        return AudioMediaType{
            sampleRate,
            channels,
            16,
            static_cast<uint16_t>(channels * 2),
            AudioSampleFormat::Pcm16
        };
    }

    SystemAudioSourceConfig CreateCoreConfig(
        bool armed = true,
        bool initiallyMuted = true)
    {
        SystemAudioSourceConfig source;
        source.id = SourceId::FromValue(22);
        source.name = "System mix";
        source.armed = armed;
        source.controls.initiallyMuted = initiallyMuted;
        source.controls.initialGain.gainDb = -3.0f;
        return source;
    }

    WasapiLoopbackAudioSourceConfig CreateConfig(
        bool armed = true,
        bool initiallyMuted = true)
    {
        return MapWasapiLoopbackAudioSourceConfig(
            CreateCoreConfig(armed, initiallyMuted),
            CreateMediaType(),
            StreamId::FromValue(44));
    }

    AudioSample CreateSample(uint8_t firstByte = 7)
    {
        AudioSample sample;
        sample.sourceId = SourceId::FromValue(22);
        sample.streamId = StreamId::FromValue(44);
        sample.timestamp = MediaTime::FromTicks(123);
        sample.duration = MediaDuration::FromTicks(456);
        sample.frameCount = 1;
        sample.mediaType = CreateMediaType();
        sample.pcmData = { firstByte, 0, 0, 0 };
        sample.sourceTiming.timestampSource = AudioTimestampSource::GeneratedContinuity;
        return sample;
    }

    bool WaitFor(
        const std::function<bool()>& predicate,
        std::chrono::milliseconds timeout = std::chrono::milliseconds(500))
    {
        const auto deadline = std::chrono::steady_clock::now() + timeout;
        while (std::chrono::steady_clock::now() < deadline)
        {
            if (predicate())
            {
                return true;
            }

            Sleep(1);
        }

        return predicate();
    }

    bool IsWasapiLoopbackProbeEnabled()
    {
        wchar_t value[8]{};
        const DWORD length = GetEnvironmentVariableW(
            L"CAPTURETOOL_V2_WASAPI_LOOPBACK_PROBE",
            value,
            ARRAYSIZE(value));
        return length > 0 && value[0] == L'1';
    }
}

namespace CaptureInteropTests
{
    TEST_CLASS(V2WasapiLoopbackAudioSourceTests)
    {
    public:
        TEST_METHOD(MapConfig_PreservesSourceIdentityControlsAndDefaultEndpointSelection)
        {
            const WasapiLoopbackAudioSourceConfig config = CreateConfig();

            Assert::AreEqual(22u, config.sourceId.value);
            Assert::AreEqual(44u, config.audioStreamId.value);
            Assert::AreEqual("System mix", config.name.c_str());
            Assert::IsTrue(config.armed);
            Assert::IsTrue(config.controls.initiallyMuted);
            Assert::AreEqual(-3.0f, config.controls.initialGain.gainDb);
            Assert::AreEqual(
                static_cast<int>(AudioDeviceSelection::DefaultRenderEndpoint),
                static_cast<int>(config.deviceSelection));
        }

        TEST_METHOD(MapConfig_EndpointIdSelection_LeavesRoomForFutureDeviceSelection)
        {
            SystemAudioSourceConfig source = CreateCoreConfig();
            source.useDefaultDevice = false;
            source.deviceId = "endpoint-1";

            const WasapiLoopbackAudioSourceConfig config =
                MapWasapiLoopbackAudioSourceConfig(source, CreateMediaType());

            Assert::AreEqual("endpoint-1", config.endpointId.c_str());
            Assert::AreEqual(22u, config.audioStreamId.value);
            Assert::AreEqual(
                static_cast<int>(AudioDeviceSelection::EndpointId),
                static_cast<int>(config.deviceSelection));
        }

        TEST_METHOD(SourceDescriptor_IsStableSystemAudioIdentity)
        {
            WasapiLoopbackAudioSource source(CreateConfig());

            const SourceDescriptor first = source.Describe();
            const SourceDescriptor second = source.Describe();

            Assert::AreEqual(22u, first.id.value);
            Assert::AreEqual(static_cast<int>(SourceKind::SystemAudio), static_cast<int>(first.kind));
            Assert::AreEqual("System mix", first.name.c_str());
            Assert::AreEqual(first.id.value, second.id.value);
            Assert::AreEqual(first.name.c_str(), second.name.c_str());
        }

        TEST_METHOD(StreamDescriptor_IsStableAudioStream)
        {
            WasapiLoopbackAudioSource source(CreateConfig());

            const std::vector<StreamDescriptor> first = source.Streams();
            const std::vector<StreamDescriptor> second = source.Streams();

            Assert::AreEqual(static_cast<size_t>(1), first.size());
            Assert::AreEqual(44u, first[0].id.value);
            Assert::AreEqual(22u, first[0].sourceId.value);
            Assert::AreEqual(static_cast<int>(MediaKind::Audio), static_cast<int>(first[0].kind));
            Assert::AreEqual(first[0].id.value, second[0].id.value);
        }

        TEST_METHOD(FakeProvider_CanBeInjectedWithoutRealAudioHardware)
        {
            auto provider = std::make_shared<FakeWasapiLoopbackAudioProvider>(
                CreateMediaType(44100, 1),
                "InjectedProvider");
            WasapiLoopbackAudioSource source(CreateConfig(), provider);

            const AudioMediaType mediaType = source.CurrentMediaType();

            Assert::AreEqual("InjectedProvider", source.ProviderName().c_str());
            Assert::AreEqual(44100u, mediaType.sampleRate);
            Assert::AreEqual(1u, static_cast<uint32_t>(mediaType.channels));
            Assert::AreEqual(static_cast<int>(AudioSampleFormat::Pcm16), static_cast<int>(mediaType.sampleFormat));
        }

        TEST_METHOD(StartStop_ArmedShellTransitionsWithoutWasapiCalls)
        {
            WasapiLoopbackAudioSource source(CreateConfig());

            Assert::IsTrue(source.Start().IsSuccess());
            Assert::IsTrue(source.IsStarted());
            Assert::IsTrue(source.Stop().IsSuccess());
            Assert::IsFalse(source.IsStarted());
        }

        TEST_METHOD(Start_WithPacketProvider_StartsWorkerLifecycle)
        {
            auto packetProvider = std::make_shared<FakeWasapiLoopbackPacketProvider>();
            WasapiLoopbackAudioSource source(CreateConfig(), nullptr, packetProvider);

            Assert::IsTrue(source.Start().IsSuccess());

            Assert::IsTrue(source.IsStarted());
            Assert::AreEqual(1, packetProvider->InitializeCount());
            Assert::AreEqual(1, packetProvider->StartCount());
            Assert::IsTrue(packetProvider->IsStarted());
            Assert::IsTrue(source.Stop().IsSuccess());
        }

        TEST_METHOD(Stop_WithPacketProvider_StopsAndJoinsWorker)
        {
            auto packetProvider = std::make_shared<FakeWasapiLoopbackPacketProvider>();
            {
                WasapiLoopbackAudioSource source(CreateConfig(), nullptr, packetProvider);

                Assert::IsTrue(source.Start().IsSuccess());
                Assert::IsTrue(source.Stop().IsSuccess());

                Assert::IsFalse(source.IsStarted());
                Assert::IsFalse(packetProvider->IsStarted());
            }

            Assert::AreEqual(1, packetProvider->StopCount());
        }

        TEST_METHOD(PacketProvider_PublishesQueuedSamplesFromWorker)
        {
            auto packetProvider = std::make_shared<FakeWasapiLoopbackPacketProvider>();
            packetProvider->EnqueuePacket(CreateSample(42));
            WasapiLoopbackAudioSource source(CreateConfig(true, false), nullptr, packetProvider);

            std::mutex mutex;
            std::condition_variable condition;
            std::optional<AudioSample> receivedSample;
            CallbackRegistrationToken token = source.RegisterSampleArrivedHandler(
                [&](const AudioSample& sample)
                {
                    {
                        std::lock_guard lock(mutex);
                        receivedSample = sample;
                    }
                    condition.notify_one();
                });

            Assert::IsTrue(source.Start().IsSuccess());
            {
                std::unique_lock lock(mutex);
                condition.wait_for(
                    lock,
                    std::chrono::milliseconds(500),
                    [&]
                    {
                        return receivedSample.has_value();
                    });
            }

            Assert::IsTrue(receivedSample.has_value());
            Assert::AreEqual(42u, static_cast<uint32_t>(receivedSample->pcmData[0]));
            Assert::IsTrue(source.Stop().IsSuccess());
        }

        TEST_METHOD(InitiallyMutedSource_EmitsSilenceUntilUnmuted)
        {
            auto packetProvider = std::make_shared<FakeWasapiLoopbackPacketProvider>();
            packetProvider->EnqueuePacket(CreateSample(42));
            WasapiLoopbackAudioSource source(CreateConfig(true, true), nullptr, packetProvider);
            std::mutex mutex;
            std::optional<AudioSample> receivedSample;
            CallbackRegistrationToken token = source.RegisterSampleArrivedHandler(
                [&](const AudioSample& sample)
                {
                    std::lock_guard lock(mutex);
                    receivedSample = sample;
                });

            Assert::IsTrue(source.Start().IsSuccess());

            Assert::IsTrue(WaitFor(
                [&]
                {
                    std::lock_guard lock(mutex);
                    return receivedSample.has_value();
                }));
            AudioSample observedSample;
            {
                std::lock_guard lock(mutex);
                observedSample = receivedSample.value();
            }

            for (const uint8_t value : observedSample.pcmData)
            {
                Assert::AreEqual(0u, static_cast<uint32_t>(value));
            }
            Assert::AreEqual(1u, observedSample.frameCount);
            Assert::AreEqual(456ll, observedSample.duration.ticks100ns);
            Assert::IsTrue(source.Stop().IsSuccess());
        }

        TEST_METHOD(UnmutedSource_PreservesCapturedData)
        {
            auto packetProvider = std::make_shared<FakeWasapiLoopbackPacketProvider>();
            packetProvider->EnqueuePacket(CreateSample(55));
            WasapiLoopbackAudioSource source(CreateConfig(true, false), nullptr, packetProvider);
            std::mutex mutex;
            std::optional<AudioSample> receivedSample;
            CallbackRegistrationToken token = source.RegisterSampleArrivedHandler(
                [&](const AudioSample& sample)
                {
                    std::lock_guard lock(mutex);
                    receivedSample = sample;
                });

            Assert::IsTrue(source.Start().IsSuccess());

            Assert::IsTrue(WaitFor(
                [&]
                {
                    std::lock_guard lock(mutex);
                    return receivedSample.has_value();
                }));
            AudioSample observedSample;
            {
                std::lock_guard lock(mutex);
                observedSample = receivedSample.value();
            }

            Assert::AreEqual(55u, static_cast<uint32_t>(observedSample.pcmData[0]));
            Assert::IsTrue(source.Stop().IsSuccess());
        }

        TEST_METHOD(RuntimeMute_AffectsOnlyFutureSamplesWithoutRestart)
        {
            auto packetProvider = std::make_shared<FakeWasapiLoopbackPacketProvider>();
            packetProvider->EnqueuePacket(CreateSample(11));
            WasapiLoopbackAudioSource source(CreateConfig(true, false), nullptr, packetProvider);
            std::mutex mutex;
            std::vector<AudioSample> receivedSamples;
            CallbackRegistrationToken token = source.RegisterSampleArrivedHandler(
                [&](const AudioSample& sample)
                {
                    std::lock_guard lock(mutex);
                    receivedSamples.push_back(sample);
                });

            Assert::IsTrue(source.Start().IsSuccess());
            Assert::IsTrue(WaitFor(
                [&]
                {
                    std::lock_guard lock(mutex);
                    return receivedSamples.size() == 1;
                }));

            Assert::IsTrue(source.SetMuted(SourceId::FromValue(22), true).IsSuccess());
            packetProvider->EnqueuePacket(CreateSample(99));

            Assert::IsTrue(WaitFor(
                [&]
                {
                    std::lock_guard lock(mutex);
                    return receivedSamples.size() == 2;
                }));
            Assert::IsTrue(source.Stop().IsSuccess());

            Assert::AreEqual(1, packetProvider->StartCount());
            Assert::AreEqual(11u, static_cast<uint32_t>(receivedSamples[0].pcmData[0]));
            for (const uint8_t value : receivedSamples[1].pcmData)
            {
                Assert::AreEqual(0u, static_cast<uint32_t>(value));
            }
            Assert::AreEqual(receivedSamples[0].frameCount, receivedSamples[1].frameCount);
            Assert::AreEqual(receivedSamples[0].duration.ticks100ns, receivedSamples[1].duration.ticks100ns);
        }

        TEST_METHOD(PacketProvider_DrainsMultipleQueuedPacketsBeforeWaiting)
        {
            auto packetProvider = std::make_shared<FakeWasapiLoopbackPacketProvider>();
            packetProvider->EnqueuePacket(CreateSample(1));
            packetProvider->EnqueuePacket(CreateSample(2));
            packetProvider->EnqueuePacket(CreateSample(3));
            WasapiLoopbackAudioSource source(CreateConfig(true, false), nullptr, packetProvider);

            std::atomic_int callbackCount{ 0 };
            CallbackRegistrationToken token = source.RegisterSampleArrivedHandler(
                [&](const AudioSample&)
                {
                    ++callbackCount;
                });

            Assert::IsTrue(source.Start().IsSuccess());

            Assert::IsTrue(WaitFor([&] { return callbackCount.load() == 3; }));
            Assert::IsTrue(source.Stop().IsSuccess());
            Assert::AreEqual(3ull, packetProvider->Diagnostics().packetsRead);
        }

        TEST_METHOD(PacketProvider_SilentPacketIncrementsDiagnostics)
        {
            auto packetProvider = std::make_shared<FakeWasapiLoopbackPacketProvider>();
            packetProvider->EnqueuePacket(CreateSample(), true);
            WasapiLoopbackAudioSource source(CreateConfig(true, false), nullptr, packetProvider);

            Assert::IsTrue(source.Start().IsSuccess());

            Assert::IsTrue(WaitFor([&] { return packetProvider->Diagnostics().silentPackets == 1; }));
            Assert::IsTrue(source.Stop().IsSuccess());
        }

        TEST_METHOD(PacketProvider_DiscontinuityIncrementsDiagnostics)
        {
            auto packetProvider = std::make_shared<FakeWasapiLoopbackPacketProvider>();
            packetProvider->EnqueuePacket(CreateSample(), false, true);
            WasapiLoopbackAudioSource source(CreateConfig(true, false), nullptr, packetProvider);

            Assert::IsTrue(source.Start().IsSuccess());

            Assert::IsTrue(WaitFor([&] { return packetProvider->Diagnostics().discontinuities == 1; }));
            Assert::IsTrue(source.Stop().IsSuccess());
        }

        TEST_METHOD(PacketProvider_TracksLastTimestampSource)
        {
            auto packetProvider = std::make_shared<FakeWasapiLoopbackPacketProvider>();
            AudioSample sample = CreateSample();
            sample.sourceTiming.timestampSource = AudioTimestampSource::WasapiQpcPosition;
            packetProvider->EnqueuePacket(sample);
            WasapiLoopbackAudioSource source(CreateConfig(true, false), nullptr, packetProvider);

            Assert::IsTrue(source.Start().IsSuccess());

            Assert::IsTrue(WaitFor(
                [&]
                {
                    return packetProvider->Diagnostics().lastTimestampSource ==
                        AudioTimestampSource::WasapiQpcPosition;
                }));
            Assert::IsTrue(source.Stop().IsSuccess());
        }

        TEST_METHOD(PacketProvider_StopRecordsReleaseOrder)
        {
            auto packetProvider = std::make_shared<FakeWasapiLoopbackPacketProvider>();
            WasapiLoopbackAudioSource source(CreateConfig(), nullptr, packetProvider);

            Assert::IsTrue(source.Start().IsSuccess());
            Assert::IsTrue(source.Stop().IsSuccess());

            const WasapiLoopbackPacketProviderDiagnostics diagnostics = packetProvider->Diagnostics();
            Assert::AreEqual(static_cast<size_t>(1), diagnostics.releaseEvents.size());
            Assert::AreEqual("fake-packet-provider-stopped", diagnostics.releaseEvents[0].c_str());
        }

        TEST_METHOD(Stop_PreventsCallbacksAfterCompletion)
        {
            auto packetProvider = std::make_shared<FakeWasapiLoopbackPacketProvider>();
            WasapiLoopbackAudioSource source(CreateConfig(), nullptr, packetProvider);
            std::atomic_int callbackCount{ 0 };
            CallbackRegistrationToken token = source.RegisterSampleArrivedHandler(
                [&](const AudioSample&)
                {
                    ++callbackCount;
                });

            Assert::IsTrue(source.Start().IsSuccess());
            Assert::IsTrue(source.Stop().IsSuccess());
            packetProvider->EnqueuePacket(CreateSample());

            Sleep(25);
            Assert::AreEqual(0, callbackCount.load());
        }

        TEST_METHOD(Stop_AfterPacketProviderInitializeFailure_IsSafe)
        {
            auto packetProvider = std::make_shared<FakeWasapiLoopbackPacketProvider>();
            packetProvider->SimulateInitializeFailure();
            WasapiLoopbackAudioSource source(CreateConfig(), nullptr, packetProvider);

            const OperationResult startResult = source.Start();

            Assert::IsTrue(startResult.IsFailure());
            Assert::IsFalse(source.IsStarted());
            Assert::IsTrue(source.Stop().IsSuccess());
        }

        TEST_METHOD(PacketCallbacks_RunOutsideSourceStateLock)
        {
            auto packetProvider = std::make_shared<FakeWasapiLoopbackPacketProvider>();
            packetProvider->EnqueuePacket(CreateSample());
            WasapiLoopbackAudioSource source(CreateConfig(), nullptr, packetProvider);
            std::atomic_bool callbackCompleted{ false };
            CallbackRegistrationToken token = source.RegisterSampleArrivedHandler(
                [&](const AudioSample&)
                {
                    Assert::IsTrue(source.SetMuted(SourceId::FromValue(22), false).IsSuccess());
                    callbackCompleted.store(true);
                });

            Assert::IsTrue(source.Start().IsSuccess());

            Assert::IsTrue(WaitFor([&] { return callbackCompleted.load(); }));
            Assert::IsTrue(source.Stop().IsSuccess());
        }

        TEST_METHOD(Start_UnarmedSource_ReturnsUnsupportedOperation)
        {
            WasapiLoopbackAudioSource source(CreateConfig(false));

            const OperationResult result = source.Start();

            Assert::IsTrue(result.IsFailure());
            Assert::AreEqual(
                static_cast<uint32_t>(CoreResultCode::UnsupportedOperation),
                static_cast<uint32_t>(result.code));
        }

        TEST_METHOD(MuteCommand_TargetsConfiguredSourceId)
        {
            WasapiLoopbackAudioSource source(CreateConfig());

            Assert::IsTrue(source.IsMuted());
            Assert::IsTrue(source.SetMuted(SourceId::FromValue(22), false).IsSuccess());
            Assert::IsFalse(source.IsMuted());
        }

        TEST_METHOD(MuteCommand_MissingSourceIdReturnsNotFound)
        {
            WasapiLoopbackAudioSource source(CreateConfig());

            const OperationResult result = source.SetMuted(SourceId::FromValue(99), true);

            Assert::IsTrue(result.IsFailure());
            Assert::AreEqual(
                static_cast<uint32_t>(CoreResultCode::NotFound),
                static_cast<uint32_t>(result.code));
        }

        TEST_METHOD(MuteCommand_UnarmedSourceReturnsUnsupportedOperation)
        {
            WasapiLoopbackAudioSource source(CreateConfig(false));

            const OperationResult result = source.SetMuted(SourceId::FromValue(22), false);

            Assert::IsTrue(result.IsFailure());
            Assert::AreEqual(
                static_cast<uint32_t>(CoreResultCode::UnsupportedOperation),
                static_cast<uint32_t>(result.code));
        }

        TEST_METHOD(GainCommand_TargetsConfiguredSourceId)
        {
            WasapiLoopbackAudioSource source(CreateConfig());

            Assert::AreEqual(-3.0f, source.GainDb());
            Assert::IsTrue(source.SetGainDb(SourceId::FromValue(22), -6.0f).IsSuccess());
            Assert::AreEqual(-6.0f, source.GainDb());
        }

        TEST_METHOD(GainCommand_MissingSourceIdReturnsNotFound)
        {
            WasapiLoopbackAudioSource source(CreateConfig());

            const OperationResult result = source.SetGainDb(SourceId::FromValue(99), -6.0f);

            Assert::IsTrue(result.IsFailure());
            Assert::AreEqual(
                static_cast<uint32_t>(CoreResultCode::NotFound),
                static_cast<uint32_t>(result.code));
        }

        TEST_METHOD(GainCommand_UnarmedSourceReturnsUnsupportedOperation)
        {
            WasapiLoopbackAudioSource source(CreateConfig(false));

            const OperationResult result = source.SetGainDb(SourceId::FromValue(22), -6.0f);

            Assert::IsTrue(result.IsFailure());
            Assert::AreEqual(
                static_cast<uint32_t>(CoreResultCode::UnsupportedOperation),
                static_cast<uint32_t>(result.code));
        }

        TEST_METHOD(CallbackTokenCanOutliveSource)
        {
            CallbackRegistrationToken token;
            {
                WasapiLoopbackAudioSource source(CreateConfig());
                token = source.RegisterSampleArrivedHandler(
                    [](const AudioSample&)
                    {
                    });
            }

            token.reset();
        }

        TEST_METHOD(LocalProbe_DefaultRenderLoopbackStartStop)
        {
            if (!IsWasapiLoopbackProbeEnabled())
            {
                Logger::WriteMessage("Skipping local WASAPI loopback probe. Set CAPTURETOOL_V2_WASAPI_LOOPBACK_PROBE=1 to enable.");
                return;
            }

            auto packetProvider = std::make_shared<WindowsWasapiLoopbackPacketProvider>();
            WasapiLoopbackAudioSource source(CreateConfig(), nullptr, packetProvider);

            const OperationResult startResult = source.Start();
            if (startResult.IsFailure())
            {
                Logger::WriteMessage("Local WASAPI loopback probe could not start default render endpoint loopback.");
                return;
            }

            Sleep(50);
            Assert::IsTrue(source.Stop().IsSuccess());

            const WasapiLoopbackPacketProviderDiagnostics diagnostics = packetProvider->Diagnostics();
            Logger::WriteMessage(diagnostics.endpointId.c_str());
            Logger::WriteMessage(diagnostics.endpointName.c_str());
            Assert::IsFalse(source.IsStarted());
        }
    };
}
