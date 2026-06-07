#include "pch.h"
#include "CppUnitTest.h"
#include "V2/Output/MediaFoundationFileSink.h"

#include <algorithm>
#include <chrono>
#include <condition_variable>
#include <initializer_list>
#include <memory>
#include <mutex>
#include <optional>
#include <string>
#include <thread>
#include <utility>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using namespace CaptureInterop::V2;
using namespace CaptureInterop::V2::Output;

namespace
{
    VideoMediaType CreatePlannedVideoMediaType(
        uint32_t width = 1920,
        uint32_t height = 1080,
        Rational frameRate = Rational::From(60, 1),
        VideoPixelFormat pixelFormat = VideoPixelFormat::Bgra8)
    {
        VideoMediaType mediaType;
        mediaType.width = width;
        mediaType.height = height;
        mediaType.frameRate = frameRate;
        mediaType.pixelFormat = pixelFormat;
        return mediaType;
    }

    class FakeMediaFoundationRuntimeApi final : public IMediaFoundationRuntimeApi
    {
    public:
        explicit FakeMediaFoundationRuntimeApi(long startupResult = S_OK) noexcept
            : m_startupResult(startupResult)
        {
        }

        [[nodiscard]] long Startup() noexcept override
        {
            ++startupCalls;
            return m_startupResult;
        }

        void Shutdown() noexcept override
        {
            ++shutdownCalls;
        }

        int startupCalls{ 0 };
        int shutdownCalls{ 0 };

    private:
        long m_startupResult{ S_OK };
    };

    class FakeSinkWriterSession final : public IMediaFoundationSinkWriterSession
    {
    public:
        [[nodiscard]] MediaFoundationStreamConfigurationResult ConfigureH264VideoStream(
            const MediaFoundationH264VideoStreamConfig& config) noexcept override
        {
            ++configureVideoCalls;
            lastVideoConfig = config;
            if (configureVideoResult.IsFailure())
            {
                return MediaFoundationStreamConfigurationResult{ configureVideoResult, 0 };
            }

            return MediaFoundationStreamConfigurationResult{
                OperationResult::Success(),
                nextVideoStreamIndex
            };
        }

        [[nodiscard]] MediaFoundationStreamConfigurationResult ConfigureAacAudioStream(
            const MediaFoundationAacAudioStreamConfig& config) noexcept override
        {
            ++configureAudioCalls;
            lastAudioConfig = config;
            if (configureAudioResult.IsFailure())
            {
                return MediaFoundationStreamConfigurationResult{ configureAudioResult, 0 };
            }

            return MediaFoundationStreamConfigurationResult{
                OperationResult::Success(),
                nextAudioStreamIndex
            };
        }

        [[nodiscard]] OperationResult WriteVideoSample(
            uint32_t sinkStreamIndex,
            const VideoSample& sample) noexcept override
        {
            EnterWriterCall();
            ++writeVideoCalls;
            lastWriteVideoStreamIndex = sinkStreamIndex;
            lastVideoSample = sample;
            const OperationResult result = writeVideoResult;
            LeaveWriterCall();
            return result;
        }

        [[nodiscard]] OperationResult WriteAudioSample(
            uint32_t sinkStreamIndex,
            const AudioSample& sample) noexcept override
        {
            EnterWriterCall();
            ++writeAudioCalls;
            lastWriteAudioStreamIndex = sinkStreamIndex;
            lastAudioSample = sample;
            const OperationResult result = writeAudioResult;
            LeaveWriterCall();
            return result;
        }

        [[nodiscard]] OperationResult BeginWriting() noexcept override
        {
            ++beginWritingCalls;
            return beginWritingResult;
        }

        [[nodiscard]] OperationResult Finalize() noexcept override
        {
            ++finalizeCalls;
            return finalizeResult;
        }

        void BlockWriterCalls()
        {
            std::lock_guard lock(writeBlockMutex);
            blockWriterCalls = true;
            writerCallEntered = false;
        }

        bool WaitForWriterCallEntered(std::chrono::milliseconds timeout)
        {
            std::unique_lock lock(writeBlockMutex);
            return writeBlockCondition.wait_for(
                lock,
                timeout,
                [&] { return writerCallEntered; });
        }

        void ReleaseWriterCalls()
        {
            {
                std::lock_guard lock(writeBlockMutex);
                blockWriterCalls = false;
            }

            writeBlockCondition.notify_all();
        }

        int configureVideoCalls{ 0 };
        int configureAudioCalls{ 0 };
        int beginWritingCalls{ 0 };
        int writeVideoCalls{ 0 };
        int writeAudioCalls{ 0 };
        int finalizeCalls{ 0 };
        int activeWriterCalls{ 0 };
        int maxConcurrentWriterCalls{ 0 };
        uint32_t nextVideoStreamIndex{ 42 };
        uint32_t nextAudioStreamIndex{ 77 };
        uint32_t lastWriteVideoStreamIndex{ 0 };
        uint32_t lastWriteAudioStreamIndex{ 0 };
        std::optional<MediaFoundationH264VideoStreamConfig> lastVideoConfig;
        std::optional<MediaFoundationAacAudioStreamConfig> lastAudioConfig;
        std::optional<VideoSample> lastVideoSample;
        std::optional<AudioSample> lastAudioSample;
        OperationResult configureVideoResult{ OperationResult::Success() };
        OperationResult configureAudioResult{ OperationResult::Success() };
        OperationResult beginWritingResult{ OperationResult::Success() };
        OperationResult writeVideoResult{ OperationResult::Success() };
        OperationResult writeAudioResult{ OperationResult::Success() };
        OperationResult finalizeResult{ OperationResult::Success() };

    private:
        void EnterWriterCall() noexcept
        {
            std::unique_lock lock(writeBlockMutex);
            ++activeWriterCalls;
            if (activeWriterCalls > maxConcurrentWriterCalls)
            {
                maxConcurrentWriterCalls = activeWriterCalls;
            }
            writerCallEntered = true;
            writeBlockCondition.notify_all();
            writeBlockCondition.wait(lock, [&] { return !blockWriterCalls; });
        }

        void LeaveWriterCall() noexcept
        {
            std::lock_guard lock(writeBlockMutex);
            --activeWriterCalls;
        }

        std::mutex writeBlockMutex;
        std::condition_variable writeBlockCondition;
        bool blockWriterCalls{ false };
        bool writerCallEntered{ false };
    };

    class FakeSinkWriterFactory final : public IMediaFoundationSinkWriterFactory
    {
    public:
        FakeSinkWriterFactory()
            : session(std::make_shared<FakeSinkWriterSession>())
        {
        }

        [[nodiscard]] MediaFoundationSinkWriterCreationResult CreateFileSinkWriter(
            const std::wstring& outputPath,
            const MediaFoundationSinkWriterFactoryOptions& options) noexcept override
        {
            ++createCalls;
            lastOutputPath = outputPath;
            lastOptions = options;
            if (nextResult.IsFailure())
            {
                return MediaFoundationSinkWriterCreationResult{
                    nextResult,
                    {},
                    attributesConfigured,
                    false
                };
            }

            return MediaFoundationSinkWriterCreationResult{
                OperationResult::Success(),
                writerCreated ? session : nullptr,
                attributesConfigured,
                writerCreated
            };
        }

        int createCalls{ 0 };
        std::wstring lastOutputPath;
        MediaFoundationSinkWriterFactoryOptions lastOptions;
        bool attributesConfigured{ true };
        bool writerCreated{ true };
        OperationResult nextResult{ OperationResult::Success() };
        std::shared_ptr<FakeSinkWriterSession> session;
    };

    struct SinkHarness
    {
        explicit SinkHarness(long startupResult = S_OK)
            : runtimeApi(std::make_shared<FakeMediaFoundationRuntimeApi>(startupResult)),
              runtime(std::make_shared<MediaFoundationRuntime>(runtimeApi)),
              sinkWriterFactory(std::make_shared<FakeSinkWriterFactory>()),
              sink(MediaFoundationSinkProfileValidator{}, runtime, sinkWriterFactory)
        {
        }

        std::shared_ptr<FakeMediaFoundationRuntimeApi> runtimeApi;
        std::shared_ptr<MediaFoundationRuntime> runtime;
        std::shared_ptr<FakeSinkWriterFactory> sinkWriterFactory;
        MediaFoundationFileSink sink;
    };

    OutputStreamPlan CreateVideoStream(
        uint32_t streamId = 1,
        VideoCodec codec = VideoCodec::H264,
        Rational frameRate = Rational::From(60, 1),
        uint32_t bitrate = 8'000'000)
    {
        return OutputStreamPlan{
            StreamId::FromValue(streamId),
            SourceId::FromValue(10),
            MediaKind::Video,
            VideoEncodingSettings{ codec, bitrate, frameRate, 120, true },
            std::nullopt,
            CreatePlannedVideoMediaType(2, 2, frameRate)
        };
    }

    OutputStreamPlan CreateAudioStream(
        uint32_t streamId = 2,
        AudioCodec codec = AudioCodec::Aac,
        uint32_t sampleRate = 48000,
        uint16_t channels = 2,
        uint32_t bitrate = 192000)
    {
        return OutputStreamPlan{
            StreamId::FromValue(streamId),
            SourceId::FromValue(20),
            MediaKind::Audio,
            std::nullopt,
            AudioEncodingSettings{ codec, bitrate, sampleRate, channels },
            std::nullopt
        };
    }

    OutputPlan CreatePlan(ContainerFormat container, std::initializer_list<OutputStreamPlan> streams)
    {
        OutputPlan plan;
        plan.container = container;
        plan.outputPath = container == ContainerFormat::Mp3
            ? L"C:\\Temp\\capture.mp3"
            : L"C:\\Temp\\capture.mp4";
        plan.streams.assign(streams.begin(), streams.end());
        return plan;
    }

    VideoSample CreateVideoSample(
        StreamId streamId = StreamId::FromValue(1),
        uint32_t width = 2,
        uint32_t height = 2)
    {
        VideoMediaType mediaType;
        mediaType.width = width;
        mediaType.height = height;
        mediaType.frameRate = Rational::From(60, 1);
        mediaType.pixelFormat = VideoPixelFormat::Bgra8;

        std::vector<uint8_t> pixelData(static_cast<size_t>(width) * height * 4);
        for (size_t i = 0; i < pixelData.size(); ++i)
        {
            pixelData[i] = static_cast<uint8_t>((i % 251) + 1);
        }

        return VideoSample{
            SourceId::FromValue(10),
            streamId,
            MediaTime::Zero(),
            MediaDuration::FromMilliseconds(16),
            mediaType,
            std::move(pixelData),
            1,
            VideoFrameDimensions{ width, height }
        };
    }

    AudioSample CreateAudioSample(
        StreamId streamId = StreamId::FromValue(2),
        uint32_t frameCount = 2)
    {
        AudioMediaType mediaType;
        mediaType.sampleRate = 48000;
        mediaType.channels = 2;
        mediaType.bitsPerSample = 32;
        mediaType.blockAlign = 8;
        mediaType.sampleFormat = AudioSampleFormat::Float32;

        std::vector<uint8_t> pcmData(static_cast<size_t>(frameCount) * mediaType.blockAlign);
        for (size_t i = 0; i < pcmData.size(); ++i)
        {
            pcmData[i] = static_cast<uint8_t>((i % 127) + 1);
        }

        return AudioSample{
            SourceId::FromValue(20),
            streamId,
            MediaTime::Zero(),
            MediaDuration::FromMilliseconds(10),
            mediaType,
            std::move(pcmData),
            frameCount
        };
    }

    const MediaFoundationFileSinkStreamDiagnostics* FindDiagnosticsStream(
        const MediaFoundationFileSinkDiagnostics& diagnostics,
        StreamId streamId)
    {
        const auto found = std::find_if(
            diagnostics.streams.begin(),
            diagnostics.streams.end(),
            [&](const MediaFoundationFileSinkStreamDiagnostics& stream)
            {
                return stream.streamId == streamId;
            });

        return found == diagnostics.streams.end() ? nullptr : &*found;
    }
}

namespace CaptureInteropTests
{
    TEST_CLASS(V2MediaFoundationFileSinkTests)
    {
    public:
        TEST_METHOD(Open_Mp4VideoOnlyPlan_BuildsStreamMap)
        {
            SinkHarness harness;
            const OutputPlan plan = CreatePlan(ContainerFormat::Mp4, { CreateVideoStream() });

            const OperationResult result = harness.sink.Open(plan);

            Assert::IsTrue(result.IsSuccess());
            Assert::AreEqual(
                static_cast<int>(MediaFoundationFileSinkState::WritingReady),
                static_cast<int>(harness.sink.State()));
            const std::vector<MediaFoundationSinkStreamMapping> mappings = harness.sink.StreamMappings();
            Assert::AreEqual(static_cast<size_t>(1), mappings.size());
            Assert::AreEqual(1u, mappings[0].streamId.value);
            Assert::AreEqual(42u, mappings[0].sinkStreamIndex);
            Assert::AreEqual(static_cast<int>(MediaKind::Video), static_cast<int>(mappings[0].kind));
            Assert::IsTrue(harness.sink.HasSinkWriter());
            Assert::AreEqual(1, harness.runtimeApi->startupCalls);
            Assert::AreEqual(1, harness.sinkWriterFactory->createCalls);
            Assert::AreEqual(1, harness.sinkWriterFactory->session->configureVideoCalls);
            Assert::AreEqual(1, harness.sinkWriterFactory->session->beginWritingCalls);

            const auto& videoConfig = harness.sinkWriterFactory->session->lastVideoConfig;
            Assert::IsTrue(videoConfig.has_value());
            Assert::AreEqual(2u, videoConfig->width);
            Assert::AreEqual(2u, videoConfig->height);
            Assert::AreEqual(8'000'000u, videoConfig->bitrate);
            Assert::AreEqual(60u, videoConfig->frameRate.numerator);
            Assert::AreEqual(1u, videoConfig->frameRate.denominator);
            Assert::AreEqual(1u, videoConfig->pixelAspectRatioNumerator);
            Assert::AreEqual(1u, videoConfig->pixelAspectRatioDenominator);
            Assert::AreEqual(
                static_cast<int>(VideoPixelFormat::Bgra8),
                static_cast<int>(videoConfig->inputPixelFormat));
            Assert::AreEqual(120u, videoConfig->gopLength);
            Assert::IsTrue(videoConfig->hardwareAccelerationPreferred);
            Assert::IsTrue(harness.sinkWriterFactory->lastOptions.hardwareTransformsEnabled);

            const MediaFoundationFileSinkDiagnostics diagnostics = harness.sink.Diagnostics();
            Assert::IsTrue(std::find(
                diagnostics.encoderSettingDiagnostics.begin(),
                diagnostics.encoderSettingDiagnostics.end(),
                "Hardware transform preference applied: enabled") != diagnostics.encoderSettingDiagnostics.end());
            Assert::IsTrue(std::find(
                diagnostics.encoderSettingDiagnostics.begin(),
                diagnostics.encoderSettingDiagnostics.end(),
                "GOP length 120 ignored: direct Media Foundation GOP mapping is deferred")
                != diagnostics.encoderSettingDiagnostics.end());
        }

        TEST_METHOD(Open_Mp4VideoHardwarePreferenceDisabled_ConfiguresWriterAttributeAndDiagnostics)
        {
            SinkHarness harness;
            OutputStreamPlan stream = CreateVideoStream();
            stream.video->hardwareAccelerationPreferred = false;
            const OutputPlan plan = CreatePlan(ContainerFormat::Mp4, { stream });

            const OperationResult result = harness.sink.Open(plan);

            Assert::IsTrue(result.IsSuccess());
            Assert::IsFalse(harness.sinkWriterFactory->lastOptions.hardwareTransformsEnabled);
            Assert::IsTrue(harness.sinkWriterFactory->session->lastVideoConfig.has_value());
            Assert::IsFalse(harness.sinkWriterFactory->session->lastVideoConfig->hardwareAccelerationPreferred);
            const MediaFoundationFileSinkDiagnostics diagnostics = harness.sink.Diagnostics();
            Assert::IsTrue(std::find(
                diagnostics.encoderSettingDiagnostics.begin(),
                diagnostics.encoderSettingDiagnostics.end(),
                "Hardware transform preference applied: disabled") != diagnostics.encoderSettingDiagnostics.end());
        }

        TEST_METHOD(Open_Mp4VideoPlusAudioPlan_MapsBothStreamsInOrder)
        {
            SinkHarness harness;
            const OutputPlan plan = CreatePlan(
                ContainerFormat::Mp4,
                { CreateVideoStream(7), CreateAudioStream(8) });

            const OperationResult result = harness.sink.Open(plan);

            Assert::IsTrue(result.IsSuccess());
            const auto video = harness.sink.FindStream(StreamId::FromValue(7));
            const auto audio = harness.sink.FindStream(StreamId::FromValue(8));
            Assert::IsTrue(video.has_value());
            Assert::IsTrue(audio.has_value());
            Assert::AreEqual(42u, video->sinkStreamIndex);
            Assert::AreEqual(77u, audio->sinkStreamIndex);
            Assert::AreEqual(static_cast<int>(MediaKind::Video), static_cast<int>(video->kind));
            Assert::AreEqual(static_cast<int>(MediaKind::Audio), static_cast<int>(audio->kind));
            Assert::AreEqual(1, harness.sinkWriterFactory->session->configureVideoCalls);
            Assert::AreEqual(1, harness.sinkWriterFactory->session->configureAudioCalls);
            Assert::AreEqual(1, harness.sinkWriterFactory->session->beginWritingCalls);
            Assert::AreEqual(
                static_cast<int>(MediaFoundationFileSinkState::WritingReady),
                static_cast<int>(harness.sink.State()));

            const auto& audioConfig = harness.sinkWriterFactory->session->lastAudioConfig;
            Assert::IsTrue(audioConfig.has_value());
            Assert::AreEqual(8u, audioConfig->streamId.value);
            Assert::AreEqual(48000u, audioConfig->sampleRate);
            Assert::AreEqual(2u, static_cast<uint32_t>(audioConfig->channels));
            Assert::AreEqual(192000u, audioConfig->bitrate);
            Assert::AreEqual(
                static_cast<int>(AudioSampleFormat::Float32),
                static_cast<int>(audioConfig->inputSampleFormat));
            Assert::AreEqual(32u, static_cast<uint32_t>(audioConfig->inputBitsPerSample));
            Assert::AreEqual(8u, static_cast<uint32_t>(audioConfig->inputBlockAlign));

            const MediaFoundationFileSinkDiagnostics diagnostics = harness.sink.Diagnostics();
            Assert::AreEqual(static_cast<size_t>(2), diagnostics.streams.size());
            const MediaFoundationFileSinkStreamDiagnostics* videoDiagnostics =
                FindDiagnosticsStream(diagnostics, StreamId::FromValue(7));
            const MediaFoundationFileSinkStreamDiagnostics* audioDiagnostics =
                FindDiagnosticsStream(diagnostics, StreamId::FromValue(8));
            Assert::IsNotNull(videoDiagnostics);
            Assert::IsNotNull(audioDiagnostics);
            Assert::IsTrue(videoDiagnostics->accepted);
            Assert::IsFalse(videoDiagnostics->rejected);
            Assert::IsTrue(audioDiagnostics->accepted);
            Assert::IsFalse(audioDiagnostics->rejected);
            Assert::IsTrue(audioDiagnostics->configuredMediaTypeSummary.find("audio 48000Hz 2ch 32bit")
                != std::string::npos);
        }

        TEST_METHOD(Open_Mp3PlanWithVideoStream_FailsDefensively)
        {
            SinkHarness harness;
            const OutputPlan plan = CreatePlan(
                ContainerFormat::Mp3,
                { CreateVideoStream(), CreateAudioStream(2, AudioCodec::Mp3) });

            const OperationResult result = harness.sink.Open(plan);

            Assert::IsTrue(result.IsFailure());
            Assert::AreEqual(
                static_cast<uint32_t>(CoreResultCode::UnsupportedOperation),
                static_cast<uint32_t>(result.code));
            Assert::AreEqual(
                static_cast<int>(MediaFoundationFileSinkState::Failed),
                static_cast<int>(harness.sink.State()));
            Assert::AreEqual(0, harness.runtimeApi->startupCalls);
            Assert::AreEqual(0, harness.sinkWriterFactory->createCalls);
        }

        TEST_METHOD(Open_Mp3AudioOnlyPlan_ReachesExplicitShellWithoutMediaFoundationWriter)
        {
            SinkHarness harness;
            const OutputPlan plan = CreatePlan(
                ContainerFormat::Mp3,
                { CreateAudioStream(2, AudioCodec::Mp3) });

            const OperationResult result = harness.sink.Open(plan);

            Assert::IsTrue(result.IsSuccess());
            Assert::AreEqual(
                static_cast<int>(MediaFoundationFileSinkState::Opened),
                static_cast<int>(harness.sink.State()));
            Assert::AreEqual(0, harness.runtimeApi->startupCalls);
            Assert::AreEqual(0, harness.sinkWriterFactory->createCalls);
            const MediaFoundationFileSinkDiagnostics diagnostics = harness.sink.Diagnostics();
            Assert::AreEqual("mp3", diagnostics.selectedProfileName.c_str());
            Assert::AreEqual(static_cast<size_t>(1), diagnostics.streams.size());
            Assert::IsTrue(diagnostics.streams[0].accepted);
        }

        TEST_METHOD(WriteSample_Mp3AudioOnlyPlan_ReturnsExplicitNotImplemented)
        {
            SinkHarness harness;
            Assert::IsTrue(harness.sink.Open(CreatePlan(
                ContainerFormat::Mp3,
                { CreateAudioStream(2, AudioCodec::Mp3) })).IsSuccess());

            const OperationResult result =
                harness.sink.WriteSample(MediaSample{ CreateAudioSample(StreamId::FromValue(2)) });

            Assert::IsTrue(result.IsFailure());
            Assert::AreEqual(
                static_cast<uint32_t>(CoreResultCode::UnsupportedOperation),
                static_cast<uint32_t>(result.code));
            Assert::AreEqual(
                "Media Foundation MP3 sample writing is not implemented in this PRD slice",
                result.diagnostic->message.c_str());
            Assert::AreEqual(1ull, harness.sink.Diagnostics().writes.rejectedWrites);
        }

        TEST_METHOD(WriteSample_Mp3ProfileRejectsVideoSampleDefensively)
        {
            SinkHarness harness;
            Assert::IsTrue(harness.sink.Open(CreatePlan(
                ContainerFormat::Mp3,
                { CreateAudioStream(2, AudioCodec::Mp3) })).IsSuccess());

            const OperationResult result =
                harness.sink.WriteSample(MediaSample{ CreateVideoSample(StreamId::FromValue(2)) });

            Assert::IsTrue(result.IsFailure());
            Assert::AreEqual(
                static_cast<uint32_t>(CoreResultCode::UnsupportedOperation),
                static_cast<uint32_t>(result.code));
            Assert::AreEqual("MP3 output does not accept video samples", result.diagnostic->message.c_str());
            Assert::AreEqual(1ull, harness.sink.Diagnostics().writes.rejectedWrites);
        }

        TEST_METHOD(Open_DuplicateStreamIds_Fails)
        {
            SinkHarness harness;
            const OutputPlan plan = CreatePlan(
                ContainerFormat::Mp4,
                { CreateVideoStream(4), CreateAudioStream(4) });

            const OperationResult result = harness.sink.Open(plan);

            Assert::IsTrue(result.IsFailure());
            Assert::AreEqual("Duplicate output stream id", result.diagnostic->message.c_str());
            Assert::AreEqual(0, harness.runtimeApi->startupCalls);
            Assert::AreEqual(0, harness.sinkWriterFactory->createCalls);
        }

        TEST_METHOD(Open_MissingVideoFields_Fails)
        {
            SinkHarness harness;
            const OutputPlan plan = CreatePlan(
                ContainerFormat::Mp4,
                { CreateVideoStream(1, VideoCodec::H264, Rational::Invalid(), 0) });

            const OperationResult result = harness.sink.Open(plan);

            Assert::IsTrue(result.IsFailure());
            Assert::AreEqual(
                "Video output stream is missing required media fields",
                result.diagnostic->message.c_str());
        }

        TEST_METHOD(Open_UnsupportedVideoBitrate_FailsBeforeRuntimeLease)
        {
            SinkHarness harness;
            const OutputPlan plan = CreatePlan(
                ContainerFormat::Mp4,
                { CreateVideoStream(1, VideoCodec::H264, Rational::From(60, 1), 50'000) });

            const OperationResult result = harness.sink.Open(plan);

            Assert::IsTrue(result.IsFailure());
            Assert::AreEqual(
                static_cast<uint32_t>(CoreResultCode::RangeError),
                static_cast<uint32_t>(result.code));
            Assert::AreEqual(
                "Video output bitrate is outside the supported Media Foundation range",
                result.diagnostic->message.c_str());
            Assert::AreEqual(0, harness.runtimeApi->startupCalls);
            Assert::AreEqual(0, harness.sinkWriterFactory->createCalls);
        }

        TEST_METHOD(Open_UnsupportedVideoFrameRate_FailsBeforeRuntimeLease)
        {
            SinkHarness harness;
            const OutputPlan plan = CreatePlan(
                ContainerFormat::Mp4,
                { CreateVideoStream(1, VideoCodec::H264, Rational::From(241, 1)) });

            const OperationResult result = harness.sink.Open(plan);

            Assert::IsTrue(result.IsFailure());
            Assert::AreEqual(
                static_cast<uint32_t>(CoreResultCode::RangeError),
                static_cast<uint32_t>(result.code));
            Assert::AreEqual(
                "Video output frame rate is outside the supported Media Foundation range",
                result.diagnostic->message.c_str());
            Assert::AreEqual(0, harness.runtimeApi->startupCalls);
            Assert::AreEqual(0, harness.sinkWriterFactory->createCalls);
        }

        TEST_METHOD(Open_MissingVideoMediaTypeFields_FailsBeforeRuntimeLease)
        {
            OutputStreamPlan stream = CreateVideoStream();
            stream.videoMediaType = CreatePlannedVideoMediaType(0, 1080);

            SinkHarness harness;
            const OutputPlan plan = CreatePlan(ContainerFormat::Mp4, { stream });

            const OperationResult result = harness.sink.Open(plan);

            Assert::IsTrue(result.IsFailure());
            Assert::AreEqual(
                "Video output stream is missing required media type fields",
                result.diagnostic->message.c_str());
            Assert::AreEqual(0, harness.runtimeApi->startupCalls);
            Assert::AreEqual(0, harness.sinkWriterFactory->createCalls);
        }

        TEST_METHOD(Open_InvalidAudioSampleRate_FailsBeforeRuntimeLease)
        {
            SinkHarness harness;
            const OutputPlan plan = CreatePlan(
                ContainerFormat::Mp4,
                { CreateVideoStream(1), CreateAudioStream(2, AudioCodec::Aac, 0) });

            const OperationResult result = harness.sink.Open(plan);

            Assert::IsTrue(result.IsFailure());
            Assert::AreEqual(
                static_cast<uint32_t>(CoreResultCode::ValidationFailure),
                static_cast<uint32_t>(result.code));
            Assert::AreEqual("Audio output stream is missing required media fields", result.diagnostic->message.c_str());
            Assert::AreEqual(0, harness.runtimeApi->startupCalls);
            Assert::AreEqual(0, harness.sinkWriterFactory->createCalls);
        }

        TEST_METHOD(Open_InvalidAudioChannelCount_FailsBeforeRuntimeLease)
        {
            SinkHarness harness;
            const OutputPlan plan = CreatePlan(
                ContainerFormat::Mp4,
                { CreateVideoStream(1), CreateAudioStream(2, AudioCodec::Aac, 48000, 0) });

            const OperationResult result = harness.sink.Open(plan);

            Assert::IsTrue(result.IsFailure());
            Assert::AreEqual(
                static_cast<uint32_t>(CoreResultCode::ValidationFailure),
                static_cast<uint32_t>(result.code));
            Assert::AreEqual("Audio output stream is missing required media fields", result.diagnostic->message.c_str());
            Assert::AreEqual(0, harness.runtimeApi->startupCalls);
            Assert::AreEqual(0, harness.sinkWriterFactory->createCalls);
        }

        TEST_METHOD(Open_UnknownStreamKind_Fails)
        {
            OutputStreamPlan unknown = CreateVideoStream();
            unknown.kind = MediaKind::Unknown;

            SinkHarness harness;
            const OutputPlan plan = CreatePlan(ContainerFormat::Mp4, { unknown });

            const OperationResult result = harness.sink.Open(plan);

            Assert::IsTrue(result.IsFailure());
            Assert::AreEqual("Output stream kind is not supported", result.diagnostic->message.c_str());
        }

        TEST_METHOD(FindStream_UnknownStream_ReturnsEmpty)
        {
            SinkHarness harness;
            Assert::IsTrue(harness.sink.Open(CreatePlan(ContainerFormat::Mp4, { CreateVideoStream() })).IsSuccess());

            const std::optional<MediaFoundationSinkStreamMapping> mapping =
                harness.sink.FindStream(StreamId::FromValue(99));

            Assert::IsFalse(mapping.has_value());
        }

        TEST_METHOD(WriteSample_BeforeOpen_ReturnsNotReady)
        {
            SinkHarness harness;

            const OperationResult result = harness.sink.WriteSample(MediaSample{ CreateVideoSample() });

            Assert::IsTrue(result.IsFailure());
            Assert::AreEqual(
                static_cast<uint32_t>(CoreResultCode::InvalidState),
                static_cast<uint32_t>(result.code));
        }

        TEST_METHOD(WriteSample_ForMappedVideoStream_WritesThroughSession)
        {
            SinkHarness harness;
            Assert::IsTrue(harness.sink.Open(CreatePlan(ContainerFormat::Mp4, { CreateVideoStream() })).IsSuccess());

            VideoSample sample = CreateVideoSample();
            sample.timestamp = MediaTime::FromTicks(100);
            const OperationResult result = harness.sink.WriteSample(MediaSample{ sample });

            Assert::IsTrue(result.IsSuccess());
            Assert::AreEqual(1, harness.sinkWriterFactory->session->writeVideoCalls);
            Assert::AreEqual(42u, harness.sinkWriterFactory->session->lastWriteVideoStreamIndex);
            Assert::IsTrue(harness.sinkWriterFactory->session->lastVideoSample.has_value());
            Assert::AreEqual(100LL, harness.sinkWriterFactory->session->lastVideoSample->timestamp.ticks100ns);

            const MediaFoundationFileSinkDiagnostics diagnostics = harness.sink.Diagnostics();
            Assert::AreEqual(L"C:\\Temp\\capture.mp4", diagnostics.outputPath.c_str());
            Assert::AreEqual("mp4", diagnostics.selectedProfileName.c_str());
            Assert::AreEqual(1u, diagnostics.writes.writeDepthHighWaterMark);
            const MediaFoundationFileSinkStreamDiagnostics* stream =
                FindDiagnosticsStream(diagnostics, StreamId::FromValue(1));
            Assert::IsNotNull(stream);
            Assert::IsTrue(stream->accepted);
            Assert::IsFalse(stream->rejected);
            Assert::AreEqual(42u, stream->sinkStreamIndex);
            Assert::AreEqual(1ull, stream->samplesWritten);
            Assert::IsTrue(stream->configuredMediaTypeSummary.find("video 2x2") != std::string::npos);
        }

        TEST_METHOD(WriteSample_UnknownStream_ReturnsNotFound)
        {
            SinkHarness harness;
            Assert::IsTrue(harness.sink.Open(CreatePlan(ContainerFormat::Mp4, { CreateVideoStream() })).IsSuccess());

            const OperationResult result =
                harness.sink.WriteSample(MediaSample{ CreateVideoSample(StreamId::FromValue(99)) });

            Assert::IsTrue(result.IsFailure());
            Assert::AreEqual(
                static_cast<uint32_t>(CoreResultCode::NotFound),
                static_cast<uint32_t>(result.code));
            Assert::AreEqual(1ull, harness.sink.Diagnostics().writes.rejectedWrites);
        }

        TEST_METHOD(WriteSample_AudioSentToVideoStream_ReturnsValidationFailure)
        {
            SinkHarness harness;
            Assert::IsTrue(harness.sink.Open(CreatePlan(ContainerFormat::Mp4, { CreateVideoStream() })).IsSuccess());

            const OperationResult result =
                harness.sink.WriteSample(MediaSample{ CreateAudioSample(StreamId::FromValue(1)) });

            Assert::IsTrue(result.IsFailure());
            Assert::AreEqual(
                static_cast<uint32_t>(CoreResultCode::ValidationFailure),
                static_cast<uint32_t>(result.code));
        }

        TEST_METHOD(WriteSample_VideoSentToAudioStream_ReturnsValidationFailure)
        {
            SinkHarness harness;
            Assert::IsTrue(harness.sink.Open(CreatePlan(
                ContainerFormat::Mp4,
                { CreateVideoStream(1), CreateAudioStream(2) })).IsSuccess());

            const OperationResult result =
                harness.sink.WriteSample(MediaSample{ CreateVideoSample(StreamId::FromValue(2)) });

            Assert::IsTrue(result.IsFailure());
            Assert::AreEqual(
                static_cast<uint32_t>(CoreResultCode::ValidationFailure),
                static_cast<uint32_t>(result.code));
            Assert::AreEqual(0, harness.sinkWriterFactory->session->writeVideoCalls);
            Assert::AreEqual(0, harness.sinkWriterFactory->session->writeAudioCalls);
        }

        TEST_METHOD(WriteSample_ForMappedAudioStream_WritesThroughSession)
        {
            SinkHarness harness;
            Assert::IsTrue(harness.sink.Open(CreatePlan(
                ContainerFormat::Mp4,
                { CreateVideoStream(1), CreateAudioStream(2) })).IsSuccess());

            AudioSample sample = CreateAudioSample(StreamId::FromValue(2));
            sample.timestamp = MediaTime::FromTicks(1000);
            const OperationResult result =
                harness.sink.WriteSample(MediaSample{ sample });

            Assert::IsTrue(result.IsSuccess());
            Assert::AreEqual(0, harness.sinkWriterFactory->session->writeVideoCalls);
            Assert::AreEqual(1, harness.sinkWriterFactory->session->writeAudioCalls);
            Assert::AreEqual(77u, harness.sinkWriterFactory->session->lastWriteAudioStreamIndex);
            Assert::IsTrue(harness.sinkWriterFactory->session->lastAudioSample.has_value());
            Assert::AreEqual(1000LL, harness.sinkWriterFactory->session->lastAudioSample->timestamp.ticks100ns);
        }

        TEST_METHOD(WriteSample_SilentAudioSample_WritesSuccessfully)
        {
            SinkHarness harness;
            Assert::IsTrue(harness.sink.Open(CreatePlan(
                ContainerFormat::Mp4,
                { CreateVideoStream(1), CreateAudioStream(2) })).IsSuccess());

            AudioSample sample = CreateAudioSample(StreamId::FromValue(2));
            sample.sourceTiming.silent = true;
            std::fill(sample.pcmData.begin(), sample.pcmData.end(), 0);

            const OperationResult result = harness.sink.WriteSample(MediaSample{ sample });

            Assert::IsTrue(result.IsSuccess());
            Assert::AreEqual(1, harness.sinkWriterFactory->session->writeAudioCalls);
            Assert::IsTrue(harness.sinkWriterFactory->session->lastAudioSample->sourceTiming.silent);
        }

        TEST_METHOD(WriteSample_SyntheticVideoPlusAudioMp4_WritesAndFinalizes)
        {
            wchar_t tempDirectory[MAX_PATH] = {};
            Assert::IsTrue(GetTempPathW(MAX_PATH, tempDirectory) > 0);

            const std::wstring outputPath = std::wstring(tempDirectory) + L"capturetool-v2-pr005-09.mp4";
            DeleteFileW(outputPath.c_str());

            OutputStreamPlan videoStream = CreateVideoStream(1);
            videoStream.videoMediaType = CreatePlannedVideoMediaType(320, 180);
            OutputPlan plan = CreatePlan(
                ContainerFormat::Mp4,
                { videoStream, CreateAudioStream(2) });
            plan.outputPath = outputPath;

            MediaFoundationFileSink sink;
            const OperationResult openResult = sink.Open(plan);
            if (openResult.IsFailure() && openResult.diagnostic.has_value())
            {
                Logger::WriteMessage(openResult.diagnostic->message.c_str());
            }
            Assert::IsTrue(openResult.IsSuccess());

            VideoSample videoSample = CreateVideoSample(StreamId::FromValue(1), 320, 180);
            AudioSample audioSample = CreateAudioSample(StreamId::FromValue(2), 480);
            audioSample.sourceTiming.silent = true;
            std::fill(audioSample.pcmData.begin(), audioSample.pcmData.end(), 0);

            Assert::IsTrue(sink.WriteSample(MediaSample{ videoSample }).IsSuccess());
            Assert::IsTrue(sink.WriteSample(MediaSample{ audioSample }).IsSuccess());
            Assert::IsTrue(sink.Finalize().IsSuccess());

            WIN32_FILE_ATTRIBUTE_DATA attributes = {};
            Assert::IsTrue(GetFileAttributesExW(
                outputPath.c_str(),
                GetFileExInfoStandard,
                &attributes));
            const uint64_t fileSize =
                (static_cast<uint64_t>(attributes.nFileSizeHigh) << 32) | attributes.nFileSizeLow;
            Assert::IsTrue(fileSize > 0);

            DeleteFileW(outputPath.c_str());
        }

        TEST_METHOD(WriteSample_ConcurrentVideoAndAudioWrites_AreSerialized)
        {
            SinkHarness harness;
            Assert::IsTrue(harness.sink.Open(CreatePlan(
                ContainerFormat::Mp4,
                { CreateVideoStream(1), CreateAudioStream(2) })).IsSuccess());

            harness.sinkWriterFactory->session->BlockWriterCalls();
            OperationResult firstResult = OperationResult::Success();
            OperationResult secondResult = OperationResult::Success();

            std::thread firstWrite([&]
            {
                firstResult = harness.sink.WriteSample(MediaSample{ CreateVideoSample(StreamId::FromValue(1)) });
            });
            const bool firstEntered =
                harness.sinkWriterFactory->session->WaitForWriterCallEntered(std::chrono::seconds(2));

            std::thread secondWrite([&]
            {
                secondResult = harness.sink.WriteSample(MediaSample{ CreateAudioSample(StreamId::FromValue(2)) });
            });
            std::this_thread::sleep_for(std::chrono::milliseconds(50));
            const int activeWriterCallsWhileBlocked =
                harness.sinkWriterFactory->session->activeWriterCalls;

            harness.sinkWriterFactory->session->ReleaseWriterCalls();
            firstWrite.join();
            secondWrite.join();

            Assert::IsTrue(firstEntered);
            Assert::AreEqual(1, activeWriterCallsWhileBlocked);
            Assert::IsTrue(firstResult.IsSuccess());
            Assert::IsTrue(secondResult.IsSuccess());
            Assert::AreEqual(1, harness.sinkWriterFactory->session->maxConcurrentWriterCalls);
            Assert::AreEqual(1, harness.sinkWriterFactory->session->writeVideoCalls);
            Assert::AreEqual(1, harness.sinkWriterFactory->session->writeAudioCalls);

            const MediaFoundationFileSinkWriteDiagnostics diagnostics = harness.sink.WriteDiagnostics();
            Assert::AreEqual(2ull, diagnostics.acceptedWrites);
            Assert::AreEqual(2ull, diagnostics.completedWrites);
            Assert::AreEqual(0ull, diagnostics.failedWrites);
            Assert::AreEqual(1u, diagnostics.writeDepthHighWaterMark);
        }

        TEST_METHOD(Finalize_DuringAcceptedWrite_WaitsForWriteCompletion)
        {
            SinkHarness harness;
            Assert::IsTrue(harness.sink.Open(CreatePlan(ContainerFormat::Mp4, { CreateVideoStream() })).IsSuccess());

            harness.sinkWriterFactory->session->BlockWriterCalls();
            OperationResult writeResult = OperationResult::Success();
            OperationResult finalizeResult = OperationResult::Success();

            std::thread writeThread([&]
            {
                writeResult = harness.sink.WriteSample(MediaSample{ CreateVideoSample() });
            });
            const bool writeEntered =
                harness.sinkWriterFactory->session->WaitForWriterCallEntered(std::chrono::seconds(2));

            std::thread finalizeThread([&]
            {
                finalizeResult = harness.sink.Finalize();
            });
            std::this_thread::sleep_for(std::chrono::milliseconds(50));
            const int finalizeCallsWhileWriteBlocked = harness.sinkWriterFactory->session->finalizeCalls;

            harness.sinkWriterFactory->session->ReleaseWriterCalls();
            writeThread.join();
            finalizeThread.join();

            Assert::IsTrue(writeEntered);
            Assert::AreEqual(0, finalizeCallsWhileWriteBlocked);
            Assert::IsTrue(writeResult.IsSuccess());
            Assert::IsTrue(finalizeResult.IsSuccess());
            Assert::AreEqual(1, harness.sinkWriterFactory->session->finalizeCalls);
            Assert::AreEqual(1ull, harness.sink.WriteDiagnostics().completedWrites);
        }

        TEST_METHOD(WriteSample_RegressingAudioTimestamp_ReturnsValidationFailure)
        {
            SinkHarness harness;
            Assert::IsTrue(harness.sink.Open(CreatePlan(
                ContainerFormat::Mp4,
                { CreateVideoStream(1), CreateAudioStream(2) })).IsSuccess());

            AudioSample first = CreateAudioSample(StreamId::FromValue(2));
            first.timestamp = MediaTime::FromTicks(200);
            Assert::IsTrue(harness.sink.WriteSample(MediaSample{ first }).IsSuccess());

            AudioSample second = CreateAudioSample(StreamId::FromValue(2));
            second.timestamp = MediaTime::FromTicks(100);
            const OperationResult result = harness.sink.WriteSample(MediaSample{ second });

            Assert::IsTrue(result.IsFailure());
            Assert::AreEqual(
                "Audio sample timestamp regressed for the negotiated stream",
                result.diagnostic->message.c_str());
            Assert::AreEqual(1, harness.sinkWriterFactory->session->writeAudioCalls);
            const MediaFoundationFileSinkDiagnostics diagnostics = harness.sink.Diagnostics();
            Assert::AreEqual(1ull, diagnostics.timestampValidationFailures);
            const MediaFoundationFileSinkStreamDiagnostics* stream =
                FindDiagnosticsStream(diagnostics, StreamId::FromValue(2));
            Assert::IsNotNull(stream);
            Assert::AreEqual(1ull, stream->timestampValidationFailures);
        }

        TEST_METHOD(WriteSample_InvalidAudioBufferLength_ReturnsValidationFailure)
        {
            SinkHarness harness;
            Assert::IsTrue(harness.sink.Open(CreatePlan(
                ContainerFormat::Mp4,
                { CreateVideoStream(1), CreateAudioStream(2) })).IsSuccess());

            AudioSample sample = CreateAudioSample(StreamId::FromValue(2));
            sample.pcmData.pop_back();

            const OperationResult result = harness.sink.WriteSample(MediaSample{ sample });

            Assert::IsTrue(result.IsFailure());
            Assert::AreEqual(
                "Audio sample buffer size does not match the negotiated media type",
                result.diagnostic->message.c_str());
            Assert::AreEqual(0, harness.sinkWriterFactory->session->writeAudioCalls);
        }

        TEST_METHOD(WriteSample_RegressingVideoTimestamp_ReturnsValidationFailure)
        {
            SinkHarness harness;
            Assert::IsTrue(harness.sink.Open(CreatePlan(ContainerFormat::Mp4, { CreateVideoStream() })).IsSuccess());

            VideoSample first = CreateVideoSample();
            first.timestamp = MediaTime::FromTicks(200);
            Assert::IsTrue(harness.sink.WriteSample(MediaSample{ first }).IsSuccess());

            VideoSample second = CreateVideoSample();
            second.timestamp = MediaTime::FromTicks(100);
            const OperationResult result = harness.sink.WriteSample(MediaSample{ second });

            Assert::IsTrue(result.IsFailure());
            Assert::AreEqual(
                "Video sample timestamp regressed for the negotiated stream",
                result.diagnostic->message.c_str());
            Assert::AreEqual(1, harness.sinkWriterFactory->session->writeVideoCalls);
        }

        TEST_METHOD(WriteSample_ChangedVideoMediaType_ReturnsValidationFailure)
        {
            SinkHarness harness;
            Assert::IsTrue(harness.sink.Open(CreatePlan(ContainerFormat::Mp4, { CreateVideoStream() })).IsSuccess());

            VideoSample sample = CreateVideoSample();
            sample.mediaType.width = 4;

            const OperationResult result = harness.sink.WriteSample(MediaSample{ sample });

            Assert::IsTrue(result.IsFailure());
            Assert::AreEqual(
                "Video sample media type does not match the negotiated output stream",
                result.diagnostic->message.c_str());
            Assert::AreEqual(0, harness.sinkWriterFactory->session->writeVideoCalls);
        }

        TEST_METHOD(WriteSample_InvalidVideoBufferLength_ReturnsValidationFailure)
        {
            SinkHarness harness;
            Assert::IsTrue(harness.sink.Open(CreatePlan(ContainerFormat::Mp4, { CreateVideoStream() })).IsSuccess());

            VideoSample sample = CreateVideoSample();
            sample.pixelData.pop_back();

            const OperationResult result = harness.sink.WriteSample(MediaSample{ sample });

            Assert::IsTrue(result.IsFailure());
            Assert::AreEqual(
                "Video sample buffer size does not match the negotiated media type",
                result.diagnostic->message.c_str());
            Assert::AreEqual(0, harness.sinkWriterFactory->session->writeVideoCalls);
        }

        TEST_METHOD(WriteSample_AfterFinalize_ReturnsInvalidState)
        {
            SinkHarness harness;
            Assert::IsTrue(harness.sink.Open(CreatePlan(ContainerFormat::Mp4, { CreateVideoStream() })).IsSuccess());
            Assert::IsTrue(harness.sink.Finalize().IsSuccess());

            const OperationResult result = harness.sink.WriteSample(MediaSample{ CreateVideoSample() });

            Assert::IsTrue(result.IsFailure());
            Assert::AreEqual(
                static_cast<uint32_t>(CoreResultCode::InvalidState),
                static_cast<uint32_t>(result.code));
            Assert::AreEqual(0, harness.sinkWriterFactory->session->writeVideoCalls);
            Assert::AreEqual(1ull, harness.sink.WriteDiagnostics().rejectedWrites);
        }

        TEST_METHOD(WriteSample_SessionFailure_DoesNotAdvanceTimestamp)
        {
            SinkHarness harness;
            harness.sinkWriterFactory->session->writeVideoResult = OperationResult::Failure(
                CoreResultCode::NativeFailure,
                "MediaFoundationFileSink",
                "WriteVideoSample",
                "simulated write failure",
                E_FAIL);
            Assert::IsTrue(harness.sink.Open(CreatePlan(ContainerFormat::Mp4, { CreateVideoStream() })).IsSuccess());

            VideoSample first = CreateVideoSample();
            first.timestamp = MediaTime::FromTicks(200);
            const OperationResult firstResult = harness.sink.WriteSample(MediaSample{ first });

            harness.sinkWriterFactory->session->writeVideoResult = OperationResult::Success();
            VideoSample second = CreateVideoSample();
            second.timestamp = MediaTime::FromTicks(100);
            const OperationResult secondResult = harness.sink.WriteSample(MediaSample{ second });

            Assert::IsTrue(firstResult.IsFailure());
            Assert::IsTrue(secondResult.IsSuccess());
            Assert::AreEqual(2, harness.sinkWriterFactory->session->writeVideoCalls);
            const MediaFoundationFileSinkWriteDiagnostics diagnostics = harness.sink.WriteDiagnostics();
            Assert::AreEqual(2ull, diagnostics.acceptedWrites);
            Assert::AreEqual(1ull, diagnostics.failedWrites);
            Assert::AreEqual(1ull, diagnostics.completedWrites);
        }

        TEST_METHOD(Finalize_AfterOpen_MovesToFinalized)
        {
            SinkHarness harness;
            Assert::IsTrue(harness.sink.Open(CreatePlan(ContainerFormat::Mp4, { CreateVideoStream() })).IsSuccess());
            Assert::AreEqual(1u, harness.runtime->ActiveLeaseCount());

            const OperationResult result = harness.sink.Finalize();

            Assert::IsTrue(result.IsSuccess());
            Assert::AreEqual(
                static_cast<int>(MediaFoundationFileSinkState::Finalized),
                static_cast<int>(harness.sink.State()));
            Assert::IsFalse(harness.sink.HasSinkWriter());
            Assert::AreEqual(0u, harness.runtime->ActiveLeaseCount());
            Assert::AreEqual(1, harness.runtimeApi->shutdownCalls);
            Assert::AreEqual(1, harness.sinkWriterFactory->session->finalizeCalls);
            Assert::IsTrue(harness.sink.Finalize().IsSuccess());
            Assert::AreEqual(1, harness.sinkWriterFactory->session->finalizeCalls);
        }

        TEST_METHOD(Finalize_FailureIsStableAndReleasesResources)
        {
            SinkHarness harness;
            harness.sinkWriterFactory->session->finalizeResult = OperationResult::Failure(
                CoreResultCode::NativeFailure,
                "MediaFoundationFileSink",
                "Finalize",
                "simulated finalize failure",
                E_FAIL);
            Assert::IsTrue(harness.sink.Open(CreatePlan(ContainerFormat::Mp4, { CreateVideoStream() })).IsSuccess());

            const OperationResult first = harness.sink.Finalize();
            const OperationResult second = harness.sink.Finalize();

            Assert::IsTrue(first.IsFailure());
            Assert::IsTrue(second.IsFailure());
            Assert::AreEqual("simulated finalize failure", second.diagnostic->message.c_str());
            Assert::AreEqual(1, harness.sinkWriterFactory->session->finalizeCalls);
            Assert::AreEqual(0u, harness.runtime->ActiveLeaseCount());
            Assert::AreEqual(1, harness.runtimeApi->shutdownCalls);
            Assert::IsFalse(harness.sink.HasSinkWriter());
            Assert::AreEqual(
                static_cast<int>(MediaFoundationFileSinkState::Finalized),
                static_cast<int>(harness.sink.State()));
            const MediaFoundationFileSinkDiagnostics diagnostics = harness.sink.Diagnostics();
            Assert::IsTrue(diagnostics.finalized);
            Assert::AreEqual("Finalize", diagnostics.finalizeStage.c_str());
            Assert::IsTrue(diagnostics.finalizeFailure.has_value());
            Assert::AreEqual("simulated finalize failure", diagnostics.finalizeFailure->message.c_str());
            Assert::IsTrue(diagnostics.finalizeFailure->nativeStatus.has_value());
            Assert::AreEqual(static_cast<int64_t>(E_FAIL), diagnostics.finalizeFailure->nativeStatus.value());
        }

        TEST_METHOD(Finalize_VideoPlusAudio_FinalizesAfterBeginWriting)
        {
            SinkHarness harness;
            Assert::IsTrue(harness.sink.Open(CreatePlan(
                ContainerFormat::Mp4,
                { CreateVideoStream(1), CreateAudioStream(2) })).IsSuccess());

            const OperationResult result = harness.sink.Finalize();

            Assert::IsTrue(result.IsSuccess());
            Assert::AreEqual(1, harness.sinkWriterFactory->session->beginWritingCalls);
            Assert::AreEqual(1, harness.sinkWriterFactory->session->finalizeCalls);
            Assert::AreEqual(0u, harness.runtime->ActiveLeaseCount());
            Assert::AreEqual(1, harness.runtimeApi->shutdownCalls);
        }

        TEST_METHOD(Destructor_FinalizesUnfinalizedWritingSink)
        {
            auto runtimeApi = std::make_shared<FakeMediaFoundationRuntimeApi>();
            auto runtime = std::make_shared<MediaFoundationRuntime>(runtimeApi);
            auto sinkWriterFactory = std::make_shared<FakeSinkWriterFactory>();

            {
                MediaFoundationFileSink sink(
                    MediaFoundationSinkProfileValidator{},
                    runtime,
                    sinkWriterFactory);
                Assert::IsTrue(sink.Open(CreatePlan(ContainerFormat::Mp4, { CreateVideoStream() })).IsSuccess());
                Assert::AreEqual(1u, runtime->ActiveLeaseCount());
            }

            Assert::AreEqual(1, sinkWriterFactory->session->finalizeCalls);
            Assert::AreEqual(0u, runtime->ActiveLeaseCount());
            Assert::AreEqual(1, runtimeApi->shutdownCalls);
        }

        TEST_METHOD(Open_EmptyOutputPath_FailsBeforeRuntimeLease)
        {
            SinkHarness harness;
            OutputPlan plan = CreatePlan(ContainerFormat::Mp4, { CreateVideoStream() });
            plan.outputPath.clear();

            const OperationResult result = harness.sink.Open(plan);

            Assert::IsTrue(result.IsFailure());
            Assert::AreEqual("Output path is required", result.diagnostic->message.c_str());
            Assert::AreEqual(0, harness.runtimeApi->startupCalls);
            Assert::AreEqual(0, harness.sinkWriterFactory->createCalls);
        }

        TEST_METHOD(Open_RuntimeStartupFailure_DoesNotCreateSinkWriter)
        {
            SinkHarness harness(E_FAIL);

            const OperationResult result =
                harness.sink.Open(CreatePlan(ContainerFormat::Mp4, { CreateVideoStream() }));

            Assert::IsTrue(result.IsFailure());
            Assert::AreEqual(
                static_cast<uint32_t>(CoreResultCode::NativeFailure),
                static_cast<uint32_t>(result.code));
            Assert::AreEqual(1, harness.runtimeApi->startupCalls);
            Assert::AreEqual(0, harness.sinkWriterFactory->createCalls);
            Assert::AreEqual(0u, harness.runtime->ActiveLeaseCount());
        }

        TEST_METHOD(Open_SinkWriterCreationFailure_ReleasesRuntimeLease)
        {
            SinkHarness harness;
            harness.sinkWriterFactory->nextResult = OperationResult::Failure(
                CoreResultCode::NativeFailure,
                "MediaFoundationFileSink",
                "CreateFileSinkWriter",
                "simulated writer creation failure",
                E_FAIL);

            const OperationResult result =
                harness.sink.Open(CreatePlan(ContainerFormat::Mp4, { CreateVideoStream() }));

            Assert::IsTrue(result.IsFailure());
            Assert::AreEqual(1, harness.runtimeApi->startupCalls);
            Assert::AreEqual(1, harness.sinkWriterFactory->createCalls);
            Assert::AreEqual(0u, harness.runtime->ActiveLeaseCount());
            Assert::AreEqual(1, harness.runtimeApi->shutdownCalls);
            Assert::IsFalse(harness.sink.HasSinkWriter());
            const MediaFoundationFileSinkDiagnostics diagnostics = harness.sink.Diagnostics();
            Assert::AreEqual("CreateFileSinkWriter", diagnostics.setupStage.c_str());
            Assert::IsTrue(diagnostics.setupFailure.has_value());
            Assert::AreEqual("simulated writer creation failure", diagnostics.setupFailure->message.c_str());
            Assert::IsTrue(diagnostics.setupFailure->nativeStatus.has_value());
            Assert::AreEqual(static_cast<int64_t>(E_FAIL), diagnostics.setupFailure->nativeStatus.value());
        }

        TEST_METHOD(Open_SinkWriterFactorySuccessWithoutWriter_FailsAndReleasesRuntimeLease)
        {
            SinkHarness harness;
            harness.sinkWriterFactory->writerCreated = false;

            const OperationResult result =
                harness.sink.Open(CreatePlan(ContainerFormat::Mp4, { CreateVideoStream() }));

            Assert::IsTrue(result.IsFailure());
            Assert::AreEqual(
                static_cast<uint32_t>(CoreResultCode::InvalidState),
                static_cast<uint32_t>(result.code));
            Assert::AreEqual(1, harness.sinkWriterFactory->createCalls);
            Assert::AreEqual(0u, harness.runtime->ActiveLeaseCount());
            Assert::AreEqual(1, harness.runtimeApi->shutdownCalls);
        }

        TEST_METHOD(Open_VideoStreamConfigurationFailure_ReleasesRuntimeLease)
        {
            SinkHarness harness;
            harness.sinkWriterFactory->session->configureVideoResult = OperationResult::Failure(
                CoreResultCode::NativeFailure,
                "MediaFoundationFileSink",
                "AddVideoStream",
                "simulated video stream setup failure",
                E_FAIL);

            const OperationResult result =
                harness.sink.Open(CreatePlan(ContainerFormat::Mp4, { CreateVideoStream() }));

            Assert::IsTrue(result.IsFailure());
            Assert::AreEqual(1, harness.sinkWriterFactory->session->configureVideoCalls);
            Assert::AreEqual(0, harness.sinkWriterFactory->session->beginWritingCalls);
            Assert::AreEqual(0u, harness.runtime->ActiveLeaseCount());
            Assert::AreEqual(1, harness.runtimeApi->shutdownCalls);
            Assert::IsFalse(harness.sink.HasSinkWriter());
        }

        TEST_METHOD(Open_AudioStreamConfigurationFailure_ReleasesRuntimeLease)
        {
            SinkHarness harness;
            harness.sinkWriterFactory->session->configureAudioResult = OperationResult::Failure(
                CoreResultCode::NativeFailure,
                "MediaFoundationFileSink",
                "AddAudioStream",
                "simulated audio stream setup failure",
                E_FAIL);

            const OperationResult result = harness.sink.Open(CreatePlan(
                ContainerFormat::Mp4,
                { CreateVideoStream(1), CreateAudioStream(2) }));

            Assert::IsTrue(result.IsFailure());
            Assert::AreEqual(1, harness.sinkWriterFactory->session->configureVideoCalls);
            Assert::AreEqual(1, harness.sinkWriterFactory->session->configureAudioCalls);
            Assert::AreEqual(0, harness.sinkWriterFactory->session->beginWritingCalls);
            Assert::AreEqual(0u, harness.runtime->ActiveLeaseCount());
            Assert::AreEqual(1, harness.runtimeApi->shutdownCalls);
            Assert::IsFalse(harness.sink.HasSinkWriter());
        }

        TEST_METHOD(Open_BeginWritingFailure_ReleasesRuntimeLease)
        {
            SinkHarness harness;
            harness.sinkWriterFactory->session->beginWritingResult = OperationResult::Failure(
                CoreResultCode::NativeFailure,
                "MediaFoundationFileSink",
                "BeginWriting",
                "simulated begin writing failure",
                E_FAIL);

            const OperationResult result =
                harness.sink.Open(CreatePlan(ContainerFormat::Mp4, { CreateVideoStream() }));

            Assert::IsTrue(result.IsFailure());
            Assert::AreEqual(1, harness.sinkWriterFactory->session->configureVideoCalls);
            Assert::AreEqual(1, harness.sinkWriterFactory->session->beginWritingCalls);
            Assert::AreEqual(0u, harness.runtime->ActiveLeaseCount());
            Assert::AreEqual(1, harness.runtimeApi->shutdownCalls);
            Assert::IsFalse(harness.sink.HasSinkWriter());
        }
    };
}
