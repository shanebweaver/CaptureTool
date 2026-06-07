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
    };

    struct MediaFoundationSinkWriterCreationResult
    {
        OperationResult result;
        wil::com_ptr<IMFSinkWriter> sinkWriter;
        bool attributesConfigured{ false };
        bool writerCreated{ false };

        [[nodiscard]] bool IsSuccess() const noexcept
        {
            return result.IsSuccess() && writerCreated;
        }
    };

    class IMediaFoundationSinkWriterFactory
    {
    public:
        virtual ~IMediaFoundationSinkWriterFactory() = default;

        [[nodiscard]] virtual MediaFoundationSinkWriterCreationResult CreateFileSinkWriter(
            const std::wstring& outputPath) noexcept = 0;
    };

    class WindowsMediaFoundationSinkWriterFactory final : public IMediaFoundationSinkWriterFactory
    {
    public:
        [[nodiscard]] MediaFoundationSinkWriterCreationResult CreateFileSinkWriter(
            const std::wstring& outputPath) noexcept override;
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
        [[nodiscard]] bool HasSinkWriter() const noexcept;

    private:
        static constexpr const char* Component = "MediaFoundationFileSink";

        [[nodiscard]] OperationResult ValidateOpenPlan(const OutputPlan& plan) const;
        [[nodiscard]] static OperationResult ValidateStreamShape(const OutputStreamPlan& stream);
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
        MediaFoundationRuntimeLease m_runtimeLease;
        wil::com_ptr<IMFSinkWriter> m_sinkWriter;
        bool m_sinkWriterCreated{ false };
    };
}
