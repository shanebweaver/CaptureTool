#include "pch.h"
#include "V2/Output/MediaFoundationFileSink.h"

#include <algorithm>
#include <mfapi.h>
#include <mfreadwrite.h>
#include <utility>

namespace CaptureInterop::V2::Output
{
    namespace
    {
        [[nodiscard]] OperationResult NativeFailure(
            const char* operation,
            const char* message,
            HRESULT hr)
        {
            return OperationResult::Failure(
                CoreResultCode::NativeFailure,
                "MediaFoundationFileSink",
                operation,
                message,
                hr);
        }
    }

    MediaFoundationSinkWriterCreationResult WindowsMediaFoundationSinkWriterFactory::CreateFileSinkWriter(
        const std::wstring& outputPath) noexcept
    {
        wil::com_ptr<IMFAttributes> attributes;
        HRESULT hr = MFCreateAttributes(attributes.put(), 1);
        if (FAILED(hr))
        {
            return MediaFoundationSinkWriterCreationResult{
                NativeFailure("CreateSinkWriterAttributes", "Failed to create Media Foundation sink writer attributes", hr),
                {},
                false,
                false
            };
        }

        hr = attributes->SetUINT32(MF_READWRITE_ENABLE_HARDWARE_TRANSFORMS, TRUE);
        if (FAILED(hr))
        {
            return MediaFoundationSinkWriterCreationResult{
                NativeFailure("ConfigureSinkWriterAttributes", "Failed to configure Media Foundation sink writer attributes", hr),
                {},
                false,
                false
            };
        }

        wil::com_ptr<IMFSinkWriter> sinkWriter;
        hr = MFCreateSinkWriterFromURL(outputPath.c_str(), nullptr, attributes.get(), sinkWriter.put());
        if (FAILED(hr))
        {
            return MediaFoundationSinkWriterCreationResult{
                NativeFailure("CreateFileSinkWriter", "Failed to create Media Foundation file sink writer", hr),
                {},
                true,
                false
            };
        }

        return MediaFoundationSinkWriterCreationResult{
            OperationResult::Success(),
            std::move(sinkWriter),
            true,
            true
        };
    }

    MediaFoundationFileSink::MediaFoundationFileSink(
        MediaFoundationSinkProfileValidator profileValidator,
        std::shared_ptr<MediaFoundationRuntime> runtime,
        std::shared_ptr<IMediaFoundationSinkWriterFactory> sinkWriterFactory)
        : m_profileValidator(std::move(profileValidator)),
          m_runtime(std::move(runtime)),
          m_sinkWriterFactory(std::move(sinkWriterFactory))
    {
    }

    MediaFoundationFileSink::~MediaFoundationFileSink()
    {
        std::lock_guard lock(m_mutex);
        ReleaseWriterResources();
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

        if (plan.container == ContainerFormat::Mp4)
        {
            if (!m_runtime)
            {
                m_state = MediaFoundationFileSinkState::Failed;
                return Failure(
                    CoreResultCode::InvalidState,
                    "Open",
                    "Media Foundation runtime is not configured");
            }

            if (!m_sinkWriterFactory)
            {
                m_state = MediaFoundationFileSinkState::Failed;
                return Failure(
                    CoreResultCode::InvalidState,
                    "Open",
                    "Media Foundation sink writer factory is not configured");
            }

            MediaFoundationRuntimeLeaseResult runtimeLeaseResult = m_runtime->Acquire();
            if (!runtimeLeaseResult.IsSuccess())
            {
                m_state = MediaFoundationFileSinkState::Failed;
                return runtimeLeaseResult.result;
            }

            MediaFoundationSinkWriterCreationResult writerResult =
                m_sinkWriterFactory->CreateFileSinkWriter(plan.outputPath);
            if (!writerResult.IsSuccess())
            {
                runtimeLeaseResult.lease.Release();
                m_state = MediaFoundationFileSinkState::Failed;
                return writerResult.result.IsFailure()
                    ? writerResult.result
                    : Failure(
                        CoreResultCode::InvalidState,
                        "CreateFileSinkWriter",
                        "Media Foundation sink writer factory did not create a writer");
            }

            m_runtimeLease = std::move(runtimeLeaseResult.lease);
            m_sinkWriter = std::move(writerResult.sinkWriter);
            m_sinkWriterCreated = writerResult.writerCreated;
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
        ReleaseWriterResources();
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

    bool MediaFoundationFileSink::HasSinkWriter() const noexcept
    {
        std::lock_guard lock(m_mutex);
        return m_sinkWriterCreated;
    }

    OperationResult MediaFoundationFileSink::ValidateOpenPlan(const OutputPlan& plan) const
    {
        if (plan.outputPath.empty())
        {
            return Failure(
                CoreResultCode::ValidationFailure,
                "Open",
                "Output path is required");
        }

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

    void MediaFoundationFileSink::ReleaseWriterResources() noexcept
    {
        m_sinkWriter.reset();
        m_sinkWriterCreated = false;
        m_runtimeLease.Release();
    }
}
