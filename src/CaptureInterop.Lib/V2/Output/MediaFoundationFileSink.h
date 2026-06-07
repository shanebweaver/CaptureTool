#pragma once

#include "V2/Core/MediaFoundationSinkCapabilities.h"
#include "V2/Core/PipelineInterfaces.h"

#include <cstdint>
#include <mutex>
#include <optional>
#include <vector>

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

    class MediaFoundationFileSink final : public IOutputSink
    {
    public:
        explicit MediaFoundationFileSink(
            MediaFoundationSinkProfileValidator profileValidator = MediaFoundationSinkProfileValidator{});

        [[nodiscard]] OperationResult Open(const OutputPlan& plan) noexcept override;
        [[nodiscard]] OperationResult WriteSample(const MediaSample& sample) noexcept override;
        [[nodiscard]] OperationResult Finalize() noexcept override;

        [[nodiscard]] MediaFoundationFileSinkState State() const noexcept;
        [[nodiscard]] std::vector<MediaFoundationSinkStreamMapping> StreamMappings() const;
        [[nodiscard]] std::optional<MediaFoundationSinkStreamMapping> FindStream(StreamId streamId) const;

    private:
        static constexpr const char* Component = "MediaFoundationFileSink";

        [[nodiscard]] OperationResult ValidateOpenPlan(const OutputPlan& plan) const;
        [[nodiscard]] static OperationResult ValidateStreamShape(const OutputStreamPlan& stream);
        [[nodiscard]] static OperationResult Failure(
            CoreResultCode code,
            const char* operation,
            const char* message) noexcept;

        mutable std::mutex m_mutex;
        MediaFoundationSinkProfileValidator m_profileValidator;
        MediaFoundationFileSinkState m_state{ MediaFoundationFileSinkState::Created };
        std::vector<MediaFoundationSinkStreamMapping> m_streamMappings;
    };
}
