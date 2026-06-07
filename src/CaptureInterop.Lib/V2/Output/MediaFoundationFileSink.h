#pragma once

#include "V2/Core/MediaFoundationSinkCapabilities.h"
#include "V2/Core/PipelineInterfaces.h"
#include "V2/Output/MediaFoundationRuntime.h"

#include <cstdint>
#include <mfreadwrite.h>
#include <memory>
#include <mutex>
#include <optional>
#include <string>
#include <utility>
#include <vector>
#include <wil/com.h>

namespace CaptureInterop::V2::Output
{
    enum class MediaFoundationFileSinkState
    {
        Created = 0,
        Opened,
        WritingReady,
        Finalizing,
        Finalized,
        Failed
    };

    struct MediaFoundationSinkStreamMapping
    {
        StreamId streamId;
        MediaKind kind{ MediaKind::Unknown };
        uint32_t sinkStreamIndex{ 0 };
        std::optional<VideoMediaType> videoMediaType;
        std::optional<AudioMediaType> audioMediaType;
    };

    struct MediaFoundationFileSinkWriteDiagnostics
    {
        uint64_t acceptedWrites{ 0 };
        uint64_t completedWrites{ 0 };
        uint64_t failedWrites{ 0 };
        uint64_t rejectedWrites{ 0 };
        uint32_t writeDepthHighWaterMark{ 0 };
    };

    struct MediaFoundationFileSinkStreamDiagnostics
    {
        StreamId streamId;
        MediaKind kind{ MediaKind::Unknown };
        bool accepted{ false };
        bool rejected{ false };
        uint32_t sinkStreamIndex{ 0 };
        std::string configuredMediaTypeSummary;
        uint64_t samplesWritten{ 0 };
        uint64_t timestampValidationFailures{ 0 };
        uint64_t textureSamplesConverted{ 0 };
    };

    struct MediaFoundationFileSinkDiagnostics
    {
        std::wstring outputPath;
        ContainerFormat selectedProfile{ ContainerFormat::Mp4 };
        std::string selectedProfileName;
        std::vector<MediaFoundationFileSinkStreamDiagnostics> streams;
        MediaFoundationFileSinkWriteDiagnostics writes;
        std::vector<std::string> encoderSettingDiagnostics;
        uint64_t timestampValidationFailures{ 0 };
        std::optional<CoreDiagnostic> lastWriteFailure;
        std::string setupStage;
        std::optional<CoreDiagnostic> setupFailure;
        std::string finalizeStage;
        std::optional<CoreDiagnostic> finalizeFailure;
        bool finalized{ false };
        bool failedFinalizeOutputDeleteAttempted{ false };
        bool failedFinalizeOutputDeleted{ false };
    };

    struct MediaFoundationH264VideoStreamConfig
    {
        StreamId streamId;
        uint32_t width{ 0 };
        uint32_t height{ 0 };
        uint32_t bitrate{ 0 };
        Rational frameRate;
        uint32_t pixelAspectRatioNumerator{ 1 };
        uint32_t pixelAspectRatioDenominator{ 1 };
        VideoPixelFormat inputPixelFormat{ VideoPixelFormat::Bgra8 };
        uint32_t gopLength{ 0 };
        bool hardwareAccelerationPreferred{ true };
    };

    struct MediaFoundationAacAudioStreamConfig
    {
        StreamId streamId;
        uint32_t sampleRate{ 0 };
        uint16_t channels{ 0 };
        uint32_t bitrate{ 0 };
        AudioSampleFormat inputSampleFormat{ AudioSampleFormat::Float32 };
        uint16_t inputBitsPerSample{ 32 };
        uint16_t inputBlockAlign{ 0 };
    };

    struct MediaFoundationStreamConfigurationResult
    {
        OperationResult result;
        uint32_t sinkStreamIndex{ 0 };

        [[nodiscard]] bool IsSuccess() const noexcept
        {
            return result.IsSuccess();
        }
    };

    class IMediaFoundationSinkWriterSession
    {
    public:
        virtual ~IMediaFoundationSinkWriterSession() = default;

        [[nodiscard]] virtual MediaFoundationStreamConfigurationResult ConfigureH264VideoStream(
            const MediaFoundationH264VideoStreamConfig& config) noexcept = 0;
        [[nodiscard]] virtual MediaFoundationStreamConfigurationResult ConfigureAacAudioStream(
            const MediaFoundationAacAudioStreamConfig& config) noexcept = 0;
        [[nodiscard]] virtual OperationResult WriteVideoSample(
            uint32_t sinkStreamIndex,
            const VideoSample& sample) noexcept = 0;
        [[nodiscard]] virtual OperationResult WriteAudioSample(
            uint32_t sinkStreamIndex,
            const AudioSample& sample) noexcept = 0;
        [[nodiscard]] virtual OperationResult BeginWriting() noexcept = 0;
        [[nodiscard]] virtual OperationResult Finalize() noexcept = 0;
    };

    struct MediaFoundationSinkWriterCreationResult
    {
        OperationResult result;
        std::shared_ptr<IMediaFoundationSinkWriterSession> sinkWriter;
        bool attributesConfigured{ false };
        bool writerCreated{ false };

        [[nodiscard]] bool IsSuccess() const noexcept
        {
            return result.IsSuccess() && writerCreated && sinkWriter != nullptr;
        }
    };

    struct MediaFoundationSinkWriterFactoryOptions
    {
        bool hardwareTransformsEnabled{ true };
    };

    class IMediaFoundationSinkWriterFactory
    {
    public:
        virtual ~IMediaFoundationSinkWriterFactory() = default;

        [[nodiscard]] virtual MediaFoundationSinkWriterCreationResult CreateFileSinkWriter(
            const std::wstring& outputPath,
            const MediaFoundationSinkWriterFactoryOptions& options) noexcept = 0;
    };

    class WindowsMediaFoundationSinkWriterFactory final : public IMediaFoundationSinkWriterFactory
    {
    public:
        [[nodiscard]] MediaFoundationSinkWriterCreationResult CreateFileSinkWriter(
            const std::wstring& outputPath,
            const MediaFoundationSinkWriterFactoryOptions& options) noexcept override;
    };

    class MediaFoundationFileSink final : public IOutputSink
    {
    public:
        explicit MediaFoundationFileSink(
            MediaFoundationSinkProfileValidator profileValidator = MediaFoundationSinkProfileValidator{},
            std::shared_ptr<MediaFoundationRuntime> runtime = std::make_shared<MediaFoundationRuntime>(),
            std::shared_ptr<IMediaFoundationSinkWriterFactory> sinkWriterFactory =
                std::make_shared<WindowsMediaFoundationSinkWriterFactory>());

        ~MediaFoundationFileSink() override;

        [[nodiscard]] OperationResult Open(const OutputPlan& plan) noexcept override;
        [[nodiscard]] OperationResult WriteSample(const MediaSample& sample) noexcept override;
        [[nodiscard]] OperationResult Finalize() noexcept override;

        [[nodiscard]] MediaFoundationFileSinkState State() const noexcept;
        [[nodiscard]] std::vector<MediaFoundationSinkStreamMapping> StreamMappings() const;
        [[nodiscard]] std::optional<MediaFoundationSinkStreamMapping> FindStream(StreamId streamId) const;
        [[nodiscard]] MediaFoundationFileSinkWriteDiagnostics WriteDiagnostics() const noexcept;
        [[nodiscard]] MediaFoundationFileSinkDiagnostics Diagnostics() const;
        [[nodiscard]] bool HasSinkWriter() const noexcept;

    private:
        static constexpr const char* Component = "MediaFoundationFileSink";

        [[nodiscard]] OperationResult ValidateOpenPlan(const OutputPlan& plan) const;
        [[nodiscard]] static OperationResult ValidateStreamShape(const OutputStreamPlan& stream);
        [[nodiscard]] OperationResult ConfigureMp4Streams(
            const OutputPlan& plan,
            std::vector<MediaFoundationSinkStreamMapping>& mappings) noexcept;
        [[nodiscard]] static MediaFoundationSinkWriterFactoryOptions BuildSinkWriterOptions(
            const OutputPlan& plan) noexcept;
        [[nodiscard]] static MediaFoundationH264VideoStreamConfig BuildH264VideoStreamConfig(
            const OutputStreamPlan& stream) noexcept;
        [[nodiscard]] static MediaFoundationAacAudioStreamConfig BuildAacAudioStreamConfig(
            const OutputStreamPlan& stream) noexcept;
        [[nodiscard]] static AudioMediaType BuildAudioInputMediaType(
            const MediaFoundationAacAudioStreamConfig& config) noexcept;
        [[nodiscard]] OperationResult ValidateVideoSample(
            const MediaFoundationSinkStreamMapping& mapping,
            const VideoSample& sample) const noexcept;
        [[nodiscard]] OperationResult ValidateAudioSample(
            const MediaFoundationSinkStreamMapping& mapping,
            const AudioSample& sample) const noexcept;
        [[nodiscard]] bool HasRegressingTimestamp(
            const MediaFoundationSinkStreamMapping& mapping,
            const MediaTime& timestamp) const noexcept;
        void RecordWrittenTimestamp(StreamId streamId, MediaTime timestamp) noexcept;
        [[nodiscard]] OperationResult FinalizeCore() noexcept;
        void InitializeDiagnostics(const OutputPlan& plan);
        void MarkAcceptedStream(const MediaFoundationSinkStreamMapping& mapping);
        void RecordSetupFailure(std::string stage, const OperationResult& result);
        void RecordFinalizeResult(const OperationResult& result);
        void DeleteFailedFinalizeOutput() noexcept;
        void RecordRejectedWrite() noexcept;
        OperationResult RejectWrite(OperationResult result) noexcept;
        void RecordAcceptedWriteStart() noexcept;
        void RecordAcceptedWriteCompletion(const OperationResult& result) noexcept;
        void RecordSampleWritten(StreamId streamId) noexcept;
        void RecordTextureSampleConverted(StreamId streamId) noexcept;
        void RecordTimestampValidationFailure(StreamId streamId) noexcept;
        [[nodiscard]] MediaFoundationFileSinkStreamDiagnostics* FindStreamDiagnostics(
            StreamId streamId) noexcept;
        [[nodiscard]] static bool IsTimestampValidationFailure(const OperationResult& result) noexcept;
        [[nodiscard]] static std::string ContainerFormatName(ContainerFormat container);
        [[nodiscard]] static std::string BuildStreamMediaTypeSummary(
            const MediaFoundationSinkStreamMapping& mapping);
        [[nodiscard]] static OperationResult Failure(
            CoreResultCode code,
            const char* operation,
            const char* message) noexcept;
        void ReleaseWriterResources() noexcept;

        mutable std::mutex m_mutex;
        MediaFoundationSinkProfileValidator m_profileValidator;
        std::shared_ptr<MediaFoundationRuntime> m_runtime;
        std::shared_ptr<IMediaFoundationSinkWriterFactory> m_sinkWriterFactory;
        MediaFoundationFileSinkState m_state{ MediaFoundationFileSinkState::Created };
        std::vector<MediaFoundationSinkStreamMapping> m_streamMappings;
        std::vector<std::pair<StreamId, MediaTime>> m_lastWrittenTimestamps;
        MediaFoundationFileSinkDiagnostics m_diagnostics;
        MediaFoundationFileSinkWriteDiagnostics m_writeDiagnostics;
        uint32_t m_activeWriteCount{ 0 };
        MediaFoundationRuntimeLease m_runtimeLease;
        std::shared_ptr<IMediaFoundationSinkWriterSession> m_sinkWriter;
        bool m_sinkWriterCreated{ false };
        bool m_hasBegunWriting{ false };
        std::optional<OperationResult> m_finalizationResult;
    };
}
