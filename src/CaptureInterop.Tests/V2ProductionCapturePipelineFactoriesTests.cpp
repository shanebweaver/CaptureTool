#include "pch.h"
#include "CppUnitTest.h"
#include "V2/Core/ProductionCapturePipelineFactories.h"
#include "V2/Audio/FakeWasapiLoopbackPacketProvider.h"
#include "V2/Desktop/FakeDesktopCaptureProvider.h"
#include "V2/Desktop/FakeDesktopD3DDeviceDependency.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using namespace CaptureInterop::V2;

namespace CaptureInteropTests
{
    namespace
    {
        VideoMediaType CreateVideoMediaType()
        {
            return VideoMediaType{
                1920,
                1080,
                Rational::From(60, 1),
                VideoPixelFormat::Bgra8,
                ColorPrimaries::Srgb,
                TransferFunction::Srgb,
                ColorRange::Full
            };
        }

        CapturePipelineConfig CreateDesktopAndAudioConfig(bool audioArmed = true)
        {
            CapturePipelineConfig config;
            DesktopSourceConfig desktop;
            desktop.id = SourceId::FromValue(10);
            desktop.videoStreamId = StreamId::FromValue(11);
            desktop.name = "Primary monitor";
            desktop.monitorDeviceName = R"(\\.\DISPLAY1)";
            desktop.frameRate = Rational::From(60, 1);
            desktop.resolvedVideoMediaType = CreateVideoMediaType();
            config.sources.push_back(SourceConfig::Desktop(std::move(desktop)));

            SystemAudioSourceConfig systemAudio;
            systemAudio.id = SourceId::FromValue(20);
            systemAudio.name = "System audio";
            systemAudio.armed = audioArmed;
            systemAudio.controls.initiallyMuted = true;
            config.sources.push_back(SourceConfig::SystemAudio(std::move(systemAudio)));

            config.audioMixer.normalizedSampleRate = 48000;
            config.audioMixer.normalizedChannels = 2;
            config.audioMixer.normalizedSampleFormat = AudioSampleFormat::Float32;
            return config;
        }
    }

    TEST_CLASS(V2ProductionCapturePipelineFactoriesTests)
    {
    public:
        TEST_METHOD(MediaSourceFactory_CreatesDesktopAndArmedSystemAudioSources)
        {
            int desktopProviderCreations = 0;
            int d3dCreations = 0;
            int audioProviderCreations = 0;
            ProductionMediaSourceFactory factory(
                [&](const Desktop::DesktopVideoSourceConfig& sourceConfig)
                {
                    ++desktopProviderCreations;
                    return std::make_shared<Desktop::FakeDesktopCaptureProvider>(
                        sourceConfig.SourceDescriptor(),
                        Desktop::BuildDesktopVideoStreams(sourceConfig),
                        CreateVideoMediaType());
                },
                [&]
                {
                    ++d3dCreations;
                    return std::make_shared<Desktop::FakeDesktopD3DDeviceDependency>();
                },
                [&]
                {
                    ++audioProviderCreations;
                    return std::make_shared<Audio::FakeWasapiLoopbackPacketProvider>();
                });

            std::vector<std::unique_ptr<IMediaSource>> sources =
                factory.CreateSources(CreateDesktopAndAudioConfig());

            Assert::AreEqual(static_cast<size_t>(2), sources.size());
            Assert::AreEqual(1, desktopProviderCreations);
            Assert::AreEqual(1, d3dCreations);
            Assert::AreEqual(1, audioProviderCreations);

            SourceDescriptor desktop = sources[0]->Describe();
            Assert::AreEqual(10u, desktop.id.value);
            Assert::AreEqual(static_cast<int>(SourceKind::Desktop), static_cast<int>(desktop.kind));
            Assert::AreEqual("Primary monitor", desktop.name.c_str());

            std::vector<StreamDescriptor> desktopStreams = sources[0]->Streams();
            Assert::AreEqual(static_cast<size_t>(1), desktopStreams.size());
            Assert::AreEqual(11u, desktopStreams[0].id.value);
            Assert::AreEqual(10u, desktopStreams[0].sourceId.value);
            Assert::AreEqual(static_cast<int>(MediaKind::Video), static_cast<int>(desktopStreams[0].kind));

            SourceDescriptor audio = sources[1]->Describe();
            Assert::AreEqual(20u, audio.id.value);
            Assert::AreEqual(static_cast<int>(SourceKind::SystemAudio), static_cast<int>(audio.kind));
            Assert::AreEqual("System audio", audio.name.c_str());
        }

        TEST_METHOD(MediaSourceFactory_SkipsUnarmedSystemAudioSource)
        {
            ProductionMediaSourceFactory factory(
                [](const Desktop::DesktopVideoSourceConfig& sourceConfig)
                {
                    return std::make_shared<Desktop::FakeDesktopCaptureProvider>(
                        sourceConfig.SourceDescriptor(),
                        Desktop::BuildDesktopVideoStreams(sourceConfig),
                        CreateVideoMediaType());
                },
                [] { return std::make_shared<Desktop::FakeDesktopD3DDeviceDependency>(); },
                [] { return std::make_shared<Audio::FakeWasapiLoopbackPacketProvider>(); });

            std::vector<std::unique_ptr<IMediaSource>> sources =
                factory.CreateSources(CreateDesktopAndAudioConfig(false));

            Assert::AreEqual(static_cast<size_t>(1), sources.size());
            Assert::AreEqual(static_cast<int>(SourceKind::Desktop), static_cast<int>(sources[0]->Describe().kind));
        }

        TEST_METHOD(MediaSourceFactory_BuildSystemAudioMediaType_UsesMixerContract)
        {
            CapturePipelineConfig config = CreateDesktopAndAudioConfig();
            config.audioMixer.normalizedSampleRate = 44100;
            config.audioMixer.normalizedChannels = 6;
            config.audioMixer.normalizedSampleFormat = AudioSampleFormat::Pcm16;

            const AudioMediaType mediaType = ProductionMediaSourceFactory::BuildSystemAudioMediaType(config);

            Assert::AreEqual(44100u, mediaType.sampleRate);
            Assert::AreEqual(6u, static_cast<uint32_t>(mediaType.channels));
            Assert::AreEqual(16u, static_cast<uint32_t>(mediaType.bitsPerSample));
            Assert::AreEqual(12u, static_cast<uint32_t>(mediaType.blockAlign));
            Assert::AreEqual(static_cast<int>(AudioSampleFormat::Pcm16), static_cast<int>(mediaType.sampleFormat));
        }

        TEST_METHOD(ProductionMediaProcessorFactory_FirstWorkflowUsesDirectRouting)
        {
            ProductionMediaProcessorFactory factory;
            OutputPlan plan;

            std::vector<std::unique_ptr<IMediaProcessor>> processors = factory.CreateProcessors(plan);

            Assert::IsTrue(processors.empty());
        }

        TEST_METHOD(ProductionOutputSinkFactory_CreatesMediaFoundationFileSink)
        {
            ProductionOutputSinkFactory factory;
            OutputPlan plan;

            std::unique_ptr<IOutputSink> sink = factory.CreateSink(plan);

            Assert::IsNotNull(sink.get());
        }
    };
}
