#include "pch.h"
#include "CppUnitTest.h"
#include "V2/Audio/FakeWasapiLoopbackAudioProvider.h"
#include "V2/Audio/WasapiLoopbackAudioSource.h"

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

    SystemAudioSourceConfig CreateCoreConfig(bool armed = true)
    {
        SystemAudioSourceConfig source;
        source.id = SourceId::FromValue(22);
        source.name = "System mix";
        source.armed = armed;
        source.controls.initiallyMuted = true;
        source.controls.initialGain.gainDb = -3.0f;
        return source;
    }

    WasapiLoopbackAudioSourceConfig CreateConfig(bool armed = true)
    {
        return MapWasapiLoopbackAudioSourceConfig(
            CreateCoreConfig(armed),
            CreateMediaType(),
            StreamId::FromValue(44));
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
    };
}
