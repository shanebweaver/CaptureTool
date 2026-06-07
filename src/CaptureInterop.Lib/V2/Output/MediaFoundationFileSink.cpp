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
        [[nodiscard]] GUID InputSubtype(VideoPixelFormat pixelFormat) noexcept
        {
            switch (pixelFormat)
            {
            case VideoPixelFormat::Bgra8:
                return MFVideoFormat_RGB32;
            default:
                return GUID_NULL;
            }
        }

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

        [[nodiscard]] OperationResult SetGuidAttribute(
            IMFMediaType& mediaType,
            const GUID& key,
            const GUID& value,
            const char* operation,
            const char* message) noexcept
        {
            const HRESULT hr = mediaType.SetGUID(key, value);
            return SUCCEEDED(hr) ? OperationResult::Success() : NativeFailure(operation, message, hr);
        }

        [[nodiscard]] OperationResult SetUint32Attribute(
            IMFMediaType& mediaType,
            const GUID& key,
            uint32_t value,
            const char* operation,
            const char* message) noexcept
        {
            const HRESULT hr = mediaType.SetUINT32(key, value);
            return SUCCEEDED(hr) ? OperationResult::Success() : NativeFailure(operation, message, hr);
        }

        [[nodiscard]] OperationResult SetSizeAttribute(
            IMFMediaType& mediaType,
            const GUID& key,
            uint32_t width,
            uint32_t height,
            const char* operation,
            const char* message) noexcept
        {
            const HRESULT hr = MFSetAttributeSize(&mediaType, key, width, height);
            return SUCCEEDED(hr) ? OperationResult::Success() : NativeFailure(operation, message, hr);
        }

        [[nodiscard]] OperationResult SetRatioAttribute(
            IMFMediaType& mediaType,
            const GUID& key,
            uint32_t numerator,
            uint32_t denominator,
            const char* operation,
            const char* message) noexcept
        {
            const HRESULT hr = MFSetAttributeRatio(&mediaType, key, numerator, denominator);
            return SUCCEEDED(hr) ? OperationResult::Success() : NativeFailure(operation, message, hr);
        }

        class WindowsMediaFoundationSinkWriterSession final : public IMediaFoundationSinkWriterSession
        {
        public:
            explicit WindowsMediaFoundationSinkWriterSession(wil::com_ptr<IMFSinkWriter> sinkWriter)
                : m_sinkWriter(std::move(sinkWriter))
            {
            }

            [[nodiscard]] MediaFoundationStreamConfigurationResult ConfigureH264VideoStream(
                const MediaFoundationH264VideoStreamConfig& config) noexcept override
            {
                if (!m_sinkWriter)
                {
                    return MediaFoundationStreamConfigurationResult{
                        OperationResult::Failure(
                            CoreResultCode::InvalidState,
                            "MediaFoundationFileSink",
                            "ConfigureH264VideoStream",
                            "Media Foundation sink writer is not available"),
                        0
                    };
                }

                auto outputTypeResult = CreateH264OutputType(config);
                if (outputTypeResult.result.IsFailure())
                {
                    return MediaFoundationStreamConfigurationResult{ outputTypeResult.result, 0 };
                }

                DWORD sinkStreamIndex = 0;
                HRESULT hr = m_sinkWriter->AddStream(outputTypeResult.mediaType.get(), &sinkStreamIndex);
                if (FAILED(hr))
                {
                    return MediaFoundationStreamConfigurationResult{
                        NativeFailure("AddVideoStream", "Failed to add H.264 video output stream", hr),
                        0
                    };
                }

                auto inputTypeResult = CreateVideoInputType(config);
                if (inputTypeResult.result.IsFailure())
                {
                    return MediaFoundationStreamConfigurationResult{ inputTypeResult.result, sinkStreamIndex };
                }

                hr = m_sinkWriter->SetInputMediaType(
                    sinkStreamIndex,
                    inputTypeResult.mediaType.get(),
                    nullptr);
                if (FAILED(hr))
                {
                    return MediaFoundationStreamConfigurationResult{
                        NativeFailure("SetVideoInputMediaType", "Failed to set H.264 video input media type", hr),
                        sinkStreamIndex
                    };
                }

                return MediaFoundationStreamConfigurationResult{
                    OperationResult::Success(),
                    sinkStreamIndex
                };
            }

            [[nodiscard]] OperationResult BeginWriting() noexcept override
            {
                if (!m_sinkWriter)
                {
                    return OperationResult::Failure(
                        CoreResultCode::InvalidState,
                        "MediaFoundationFileSink",
                        "BeginWriting",
                        "Media Foundation sink writer is not available");
                }

                const HRESULT hr = m_sinkWriter->BeginWriting();
                return SUCCEEDED(hr)
                    ? OperationResult::Success()
                    : NativeFailure("BeginWriting", "Failed to begin Media Foundation sink writing", hr);
            }

        private:
            struct MediaTypeCreationResult
            {
                OperationResult result;
                wil::com_ptr<IMFMediaType> mediaType;
            };

            [[nodiscard]] static MediaTypeCreationResult CreateMediaType(const char* operation) noexcept
            {
                wil::com_ptr<IMFMediaType> mediaType;
                const HRESULT hr = MFCreateMediaType(mediaType.put());
                if (FAILED(hr))
                {
                    return MediaTypeCreationResult{
                        NativeFailure(operation, "Failed to create Media Foundation media type", hr),
                        {}
                    };
                }

                return MediaTypeCreationResult{ OperationResult::Success(), std::move(mediaType) };
            }

            [[nodiscard]] static MediaTypeCreationResult CreateH264OutputType(
                const MediaFoundationH264VideoStreamConfig& config) noexcept
            {
                MediaTypeCreationResult created = CreateMediaType("CreateH264OutputType");
                if (created.result.IsFailure())
                {
                    return created;
                }

                IMFMediaType& mediaType = *created.mediaType;
                OperationResult result = SetGuidAttribute(
                    mediaType,
                    MF_MT_MAJOR_TYPE,
                    MFMediaType_Video,
                    "CreateH264OutputType",
                    "Failed to set H.264 output major type");
                if (result.IsFailure()) return MediaTypeCreationResult{ result, {} };

                result = SetGuidAttribute(
                    mediaType,
                    MF_MT_SUBTYPE,
                    MFVideoFormat_H264,
                    "CreateH264OutputType",
                    "Failed to set H.264 output subtype");
                if (result.IsFailure()) return MediaTypeCreationResult{ result, {} };

                result = SetUint32Attribute(
                    mediaType,
                    MF_MT_AVG_BITRATE,
                    config.bitrate,
                    "CreateH264OutputType",
                    "Failed to set H.264 output bitrate");
                if (result.IsFailure()) return MediaTypeCreationResult{ result, {} };

                result = SetUint32Attribute(
                    mediaType,
                    MF_MT_INTERLACE_MODE,
                    MFVideoInterlace_Progressive,
                    "CreateH264OutputType",
                    "Failed to set H.264 output interlace mode");
                if (result.IsFailure()) return MediaTypeCreationResult{ result, {} };

                result = SetSizeAttribute(
                    mediaType,
                    MF_MT_FRAME_SIZE,
                    config.width,
                    config.height,
                    "CreateH264OutputType",
                    "Failed to set H.264 output frame size");
                if (result.IsFailure()) return MediaTypeCreationResult{ result, {} };

                result = SetRatioAttribute(
                    mediaType,
                    MF_MT_FRAME_RATE,
                    config.frameRate.numerator,
                    config.frameRate.denominator,
                    "CreateH264OutputType",
                    "Failed to set H.264 output frame rate");
                if (result.IsFailure()) return MediaTypeCreationResult{ result, {} };

                result = SetRatioAttribute(
                    mediaType,
                    MF_MT_PIXEL_ASPECT_RATIO,
                    config.pixelAspectRatioNumerator,
                    config.pixelAspectRatioDenominator,
                    "CreateH264OutputType",
                    "Failed to set H.264 output pixel aspect ratio");
                if (result.IsFailure()) return MediaTypeCreationResult{ result, {} };

                return created;
            }

            [[nodiscard]] static MediaTypeCreationResult CreateVideoInputType(
                const MediaFoundationH264VideoStreamConfig& config) noexcept
            {
                const GUID inputSubtype = InputSubtype(config.inputPixelFormat);
                if (IsEqualGUID(inputSubtype, GUID_NULL))
                {
                    return MediaTypeCreationResult{
                        OperationResult::Failure(
                            CoreResultCode::UnsupportedOperation,
                            "MediaFoundationFileSink",
                            "CreateVideoInputType",
                            "Video input pixel format is not supported by the H.264 sink path"),
                        {}
                    };
                }

                MediaTypeCreationResult created = CreateMediaType("CreateVideoInputType");
                if (created.result.IsFailure())
                {
                    return created;
                }

                IMFMediaType& mediaType = *created.mediaType;
                OperationResult result = SetGuidAttribute(
                    mediaType,
                    MF_MT_MAJOR_TYPE,
                    MFMediaType_Video,
                    "CreateVideoInputType",
                    "Failed to set video input major type");
                if (result.IsFailure()) return MediaTypeCreationResult{ result, {} };

                result = SetGuidAttribute(
                    mediaType,
                    MF_MT_SUBTYPE,
                    inputSubtype,
                    "CreateVideoInputType",
                    "Failed to set video input subtype");
                if (result.IsFailure()) return MediaTypeCreationResult{ result, {} };

                result = SetSizeAttribute(
                    mediaType,
                    MF_MT_FRAME_SIZE,
                    config.width,
                    config.height,
                    "CreateVideoInputType",
                    "Failed to set video input frame size");
                if (result.IsFailure()) return MediaTypeCreationResult{ result, {} };

                result = SetRatioAttribute(
                    mediaType,
                    MF_MT_FRAME_RATE,
                    config.frameRate.numerator,
                    config.frameRate.denominator,
                    "CreateVideoInputType",
                    "Failed to set video input frame rate");
                if (result.IsFailure()) return MediaTypeCreationResult{ result, {} };

                result = SetRatioAttribute(
                    mediaType,
                    MF_MT_PIXEL_ASPECT_RATIO,
                    config.pixelAspectRatioNumerator,
                    config.pixelAspectRatioDenominator,
                    "CreateVideoInputType",
                    "Failed to set video input pixel aspect ratio");
                if (result.IsFailure()) return MediaTypeCreationResult{ result, {} };

                result = SetUint32Attribute(
                    mediaType,
                    MF_MT_DEFAULT_STRIDE,
                    config.width * 4,
                    "CreateVideoInputType",
                    "Failed to set video input default stride");
                if (result.IsFailure()) return MediaTypeCreationResult{ result, {} };

                return created;
            }

            wil::com_ptr<IMFSinkWriter> m_sinkWriter;
        };
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
            std::make_shared<WindowsMediaFoundationSinkWriterSession>(std::move(sinkWriter)),
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
        if (plan.container == ContainerFormat::Mp4)
        {
            OperationResult configureResult = ConfigureMp4Streams(plan, mappings);
            if (configureResult.IsFailure())
            {
                ReleaseWriterResources();
                m_state = MediaFoundationFileSinkState::Failed;
                return configureResult;
            }
        }
        else
        {
            for (const OutputStreamPlan& stream : plan.streams)
            {
                mappings.push_back(MediaFoundationSinkStreamMapping{
                    stream.streamId,
                    stream.kind,
                    static_cast<uint32_t>(mappings.size())
                });
            }
        }

        m_streamMappings = std::move(mappings);
        m_state = plan.container == ContainerFormat::Mp4 && plan.HasVideoStream() && !plan.HasAudioStream()
            ? MediaFoundationFileSinkState::WritingReady
            : MediaFoundationFileSinkState::Opened;
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

            if (!stream.videoMediaType.has_value() || !stream.videoMediaType->IsValid())
            {
                return Failure(
                    CoreResultCode::ValidationFailure,
                    "Open",
                    "Video output stream is missing required media type fields");
            }

            if (stream.videoMediaType->width == 0 || stream.videoMediaType->height == 0)
            {
                return Failure(
                    CoreResultCode::ValidationFailure,
                    "Open",
                    "Video output stream is missing width or height");
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

    OperationResult MediaFoundationFileSink::ConfigureMp4Streams(
        const OutputPlan& plan,
        std::vector<MediaFoundationSinkStreamMapping>& mappings) noexcept
    {
        if (!m_sinkWriter)
        {
            return Failure(
                CoreResultCode::InvalidState,
                "ConfigureMp4Streams",
                "Media Foundation sink writer is not available");
        }

        for (const OutputStreamPlan& stream : plan.streams)
        {
            if (stream.kind == MediaKind::Video)
            {
                MediaFoundationStreamConfigurationResult streamResult =
                    m_sinkWriter->ConfigureH264VideoStream(BuildH264VideoStreamConfig(stream));
                if (streamResult.result.IsFailure())
                {
                    return streamResult.result;
                }

                mappings.push_back(MediaFoundationSinkStreamMapping{
                    stream.streamId,
                    stream.kind,
                    streamResult.sinkStreamIndex
                });
                continue;
            }

            mappings.push_back(MediaFoundationSinkStreamMapping{
                stream.streamId,
                stream.kind,
                static_cast<uint32_t>(mappings.size())
            });
        }

        if (plan.HasVideoStream() && !plan.HasAudioStream())
        {
            OperationResult beginResult = m_sinkWriter->BeginWriting();
            if (beginResult.IsFailure())
            {
                return beginResult;
            }
        }

        return OperationResult::Success();
    }

    MediaFoundationH264VideoStreamConfig MediaFoundationFileSink::BuildH264VideoStreamConfig(
        const OutputStreamPlan& stream) noexcept
    {
        return MediaFoundationH264VideoStreamConfig{
            stream.streamId,
            stream.videoMediaType->width,
            stream.videoMediaType->height,
            stream.video->bitrate,
            stream.video->frameRate,
            1,
            1,
            stream.videoMediaType->pixelFormat
        };
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
