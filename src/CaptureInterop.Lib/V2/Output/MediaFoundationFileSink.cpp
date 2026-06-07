#include "pch.h"
#include "V2/Output/MediaFoundationFileSink.h"

#include <algorithm>
#include <utility>

namespace CaptureInterop::V2::Output
{
    MediaFoundationFileSink::MediaFoundationFileSink(
        MediaFoundationSinkProfileValidator profileValidator)
        : m_profileValidator(std::move(profileValidator))
    {
    }

    OperationResult MediaFoundationFileSink::Open(const OutputPlan& plan) noexcept
    {
        std::lock_guard lock(m_mutex);
        if (m_state != MediaFoundationFileSinkState::Created)
        {
            return Failure(
                CoreResultCode::InvalidState,
                "Open",
                "Media Foundation file sink can only be opened once");
        }

        OperationResult validation = ValidateOpenPlan(plan);
        if (validation.IsFailure())
        {
            m_state = MediaFoundationFileSinkState::Failed;
            return validation;
        }

        std::vector<MediaFoundationSinkStreamMapping> mappings;
        mappings.reserve(plan.streams.size());
        for (const OutputStreamPlan& stream : plan.streams)
        {
            mappings.push_back(MediaFoundationSinkStreamMapping{
                stream.streamId,
                stream.kind,
                static_cast<uint32_t>(mappings.size())
            });
        }

        m_streamMappings = std::move(mappings);
        m_state = MediaFoundationFileSinkState::Opened;
        return OperationResult::Success();
    }

    OperationResult MediaFoundationFileSink::WriteSample(const MediaSample& sample) noexcept
    {
        std::lock_guard lock(m_mutex);
        if (m_state == MediaFoundationFileSinkState::Created)
        {
            return Failure(
                CoreResultCode::InvalidState,
                "WriteSample",
                "Media Foundation file sink is not open");
        }

        if (m_state == MediaFoundationFileSinkState::Finalizing
            || m_state == MediaFoundationFileSinkState::Finalized
            || m_state == MediaFoundationFileSinkState::Failed)
        {
            return Failure(
                CoreResultCode::InvalidState,
                "WriteSample",
                "Media Foundation file sink is not accepting samples");
        }

        const auto mapping = std::find_if(
            m_streamMappings.begin(),
            m_streamMappings.end(),
            [&](const MediaFoundationSinkStreamMapping& candidate)
            {
                return candidate.streamId == sample.Stream();
            });

        if (mapping == m_streamMappings.end())
        {
            return Failure(
                CoreResultCode::NotFound,
                "WriteSample",
                "Sample stream is not mapped by the Media Foundation file sink");
        }

        if (mapping->kind != sample.Kind())
        {
            return Failure(
                CoreResultCode::ValidationFailure,
                "WriteSample",
                "Sample media kind does not match the negotiated output stream");
        }

        return Failure(
            CoreResultCode::UnsupportedOperation,
            "WriteSample",
            "Media Foundation sample writing is not implemented in this PRD slice");
    }

    OperationResult MediaFoundationFileSink::Finalize() noexcept
    {
        std::lock_guard lock(m_mutex);
        if (m_state == MediaFoundationFileSinkState::Finalized)
        {
            return OperationResult::Success();
        }

        if (m_state == MediaFoundationFileSinkState::Created)
        {
            return Failure(
                CoreResultCode::InvalidState,
                "Finalize",
                "Media Foundation file sink cannot finalize before open");
        }

        if (m_state == MediaFoundationFileSinkState::Failed)
        {
            return Failure(
                CoreResultCode::InvalidState,
                "Finalize",
                "Media Foundation file sink cannot finalize after failure");
        }

        m_state = MediaFoundationFileSinkState::Finalizing;
        m_state = MediaFoundationFileSinkState::Finalized;
        return OperationResult::Success();
    }

    MediaFoundationFileSinkState MediaFoundationFileSink::State() const noexcept
    {
        std::lock_guard lock(m_mutex);
        return m_state;
    }

    std::vector<MediaFoundationSinkStreamMapping> MediaFoundationFileSink::StreamMappings() const
    {
        std::lock_guard lock(m_mutex);
        return m_streamMappings;
    }

    std::optional<MediaFoundationSinkStreamMapping> MediaFoundationFileSink::FindStream(
        StreamId streamId) const
    {
        std::lock_guard lock(m_mutex);
        const auto mapping = std::find_if(
            m_streamMappings.begin(),
            m_streamMappings.end(),
            [&](const MediaFoundationSinkStreamMapping& candidate)
            {
                return candidate.streamId == streamId;
            });

        if (mapping == m_streamMappings.end())
        {
            return std::nullopt;
        }

        return *mapping;
    }

    OperationResult MediaFoundationFileSink::ValidateOpenPlan(const OutputPlan& plan) const
    {
        const MediaFoundationProfileValidationResult profileResult = m_profileValidator.Validate(plan);
        if (!profileResult.IsSuccess())
        {
            return profileResult.diagnostics.ToOperationResult();
        }

        for (const OutputStreamPlan& stream : plan.streams)
        {
            OperationResult streamResult = ValidateStreamShape(stream);
            if (streamResult.IsFailure())
            {
                return streamResult;
            }
        }

        return OperationResult::Success();
    }

    OperationResult MediaFoundationFileSink::ValidateStreamShape(const OutputStreamPlan& stream)
    {
        if (!stream.streamId.IsValid())
        {
            return Failure(
                CoreResultCode::ValidationFailure,
                "Open",
                "Output stream id is required");
        }

        if (!stream.sourceId.IsValid())
        {
            return Failure(
                CoreResultCode::ValidationFailure,
                "Open",
                "Output stream source id is required");
        }

        if (stream.kind == MediaKind::Video)
        {
            if (!stream.video.has_value())
            {
                return Failure(
                    CoreResultCode::ValidationFailure,
                    "Open",
                    "Video output stream is missing encoding settings");
            }

            if (stream.video->bitrate == 0 || !stream.video->frameRate.IsValid())
            {
                return Failure(
                    CoreResultCode::ValidationFailure,
                    "Open",
                    "Video output stream is missing required media fields");
            }

            return OperationResult::Success();
        }

        if (stream.kind == MediaKind::Audio)
        {
            if (!stream.audio.has_value())
            {
                return Failure(
                    CoreResultCode::ValidationFailure,
                    "Open",
                    "Audio output stream is missing encoding settings");
            }

            if (stream.audio->bitrate == 0
                || stream.audio->sampleRate == 0
                || stream.audio->channels == 0)
            {
                return Failure(
                    CoreResultCode::ValidationFailure,
                    "Open",
                    "Audio output stream is missing required media fields");
            }

            return OperationResult::Success();
        }

        return Failure(
            CoreResultCode::UnsupportedOperation,
            "Open",
            "Output stream kind is not supported");
    }

    OperationResult MediaFoundationFileSink::Failure(
        CoreResultCode code,
        const char* operation,
        const char* message) noexcept
    {
        return OperationResult::Failure(code, Component, operation, message);
    }
}
