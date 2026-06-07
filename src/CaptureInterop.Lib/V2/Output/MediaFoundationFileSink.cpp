#include "pch.h"
#include "V2/Output/MediaFoundationFileSink.h"

#include <algorithm>
#include <cstring>
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

        [[nodiscard]] GUID AudioInputSubtype(AudioSampleFormat sampleFormat) noexcept
        {
            switch (sampleFormat)
            {
            case AudioSampleFormat::Float32:
                return MFAudioFormat_Float;
            case AudioSampleFormat::Pcm16:
            case AudioSampleFormat::Pcm24:
            case AudioSampleFormat::Pcm32:
                return MFAudioFormat_PCM;
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

            [[nodiscard]] MediaFoundationStreamConfigurationResult ConfigureAacAudioStream(
                const MediaFoundationAacAudioStreamConfig& config) noexcept override
            {
                if (!m_sinkWriter)
                {
                    return MediaFoundationStreamConfigurationResult{
                        OperationResult::Failure(
                            CoreResultCode::InvalidState,
                            "MediaFoundationFileSink",
                            "ConfigureAacAudioStream",
                            "Media Foundation sink writer is not available"),
                        0
                    };
                }

                auto outputTypeResult = CreateAacOutputType(config);
                if (outputTypeResult.result.IsFailure())
                {
                    return MediaFoundationStreamConfigurationResult{ outputTypeResult.result, 0 };
                }

                DWORD sinkStreamIndex = 0;
                HRESULT hr = m_sinkWriter->AddStream(outputTypeResult.mediaType.get(), &sinkStreamIndex);
                if (FAILED(hr))
                {
                    return MediaFoundationStreamConfigurationResult{
                        NativeFailure("AddAudioStream", "Failed to add AAC audio output stream", hr),
                        0
                    };
                }

                auto inputTypeResult = CreateAudioInputType(config);
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
                        NativeFailure("SetAudioInputMediaType", "Failed to set AAC audio input media type", hr),
                        sinkStreamIndex
                    };
                }

                return MediaFoundationStreamConfigurationResult{
                    OperationResult::Success(),
                    sinkStreamIndex
                };
            }

            [[nodiscard]] OperationResult WriteVideoSample(
                uint32_t sinkStreamIndex,
                const VideoSample& sample) noexcept override
            {
                if (!m_sinkWriter)
                {
                    return OperationResult::Failure(
                        CoreResultCode::InvalidState,
                        "MediaFoundationFileSink",
                        "WriteVideoSample",
                        "Media Foundation sink writer is not available");
                }

                if (sample.pixelData.empty())
                {
                    return OperationResult::Failure(
                        CoreResultCode::ValidationFailure,
                        "MediaFoundationFileSink",
                        "WriteVideoSample",
                        "Video sample does not contain CPU pixel data for the current H.264 input path");
                }

                wil::com_ptr<IMFMediaBuffer> buffer;
                HRESULT hr = MFCreateMemoryBuffer(
                    static_cast<DWORD>(sample.pixelData.size()),
                    buffer.put());
                if (FAILED(hr))
                {
                    return NativeFailure("CreateVideoSampleBuffer", "Failed to create Media Foundation video sample buffer", hr);
                }

                BYTE* destination = nullptr;
                DWORD maxLength = 0;
                DWORD currentLength = 0;
                hr = buffer->Lock(&destination, &maxLength, &currentLength);
                if (FAILED(hr))
                {
                    return NativeFailure("LockVideoSampleBuffer", "Failed to lock Media Foundation video sample buffer", hr);
                }

                if (maxLength < sample.pixelData.size())
                {
                    buffer->Unlock();
                    return OperationResult::Failure(
                        CoreResultCode::ValidationFailure,
                        "MediaFoundationFileSink",
                        "WriteVideoSample",
                        "Media Foundation video sample buffer is smaller than the source sample");
                }

                std::memcpy(destination, sample.pixelData.data(), sample.pixelData.size());
                hr = buffer->Unlock();
                if (FAILED(hr))
                {
                    return NativeFailure("UnlockVideoSampleBuffer", "Failed to unlock Media Foundation video sample buffer", hr);
                }

                hr = buffer->SetCurrentLength(static_cast<DWORD>(sample.pixelData.size()));
                if (FAILED(hr))
                {
                    return NativeFailure("SetVideoSampleBufferLength", "Failed to set Media Foundation video sample buffer length", hr);
                }

                wil::com_ptr<IMFSample> mfSample;
                hr = MFCreateSample(mfSample.put());
                if (FAILED(hr))
                {
                    return NativeFailure("CreateVideoSample", "Failed to create Media Foundation video sample", hr);
                }

                hr = mfSample->AddBuffer(buffer.get());
                if (FAILED(hr))
                {
                    return NativeFailure("AddVideoSampleBuffer", "Failed to attach video buffer to Media Foundation sample", hr);
                }

                hr = mfSample->SetSampleTime(sample.timestamp.ticks100ns);
                if (FAILED(hr))
                {
                    return NativeFailure("SetVideoSampleTime", "Failed to set Media Foundation video sample time", hr);
                }

                if (sample.duration.IsPositive())
                {
                    hr = mfSample->SetSampleDuration(sample.duration.ticks100ns);
                    if (FAILED(hr))
                    {
                        return NativeFailure("SetVideoSampleDuration", "Failed to set Media Foundation video sample duration", hr);
                    }
                }

                hr = m_sinkWriter->WriteSample(sinkStreamIndex, mfSample.get());
                return SUCCEEDED(hr)
                    ? OperationResult::Success()
                    : NativeFailure("WriteVideoSample", "Failed to write Media Foundation video sample", hr);
            }

            [[nodiscard]] OperationResult WriteAudioSample(
                uint32_t sinkStreamIndex,
                const AudioSample& sample) noexcept override
            {
                if (!m_sinkWriter)
                {
                    return OperationResult::Failure(
                        CoreResultCode::InvalidState,
                        "MediaFoundationFileSink",
                        "WriteAudioSample",
                        "Media Foundation sink writer is not available");
                }

                if (sample.pcmData.empty())
                {
                    return OperationResult::Failure(
                        CoreResultCode::ValidationFailure,
                        "MediaFoundationFileSink",
                        "WriteAudioSample",
                        "Audio sample does not contain PCM data for the current AAC input path");
                }

                wil::com_ptr<IMFMediaBuffer> buffer;
                HRESULT hr = MFCreateMemoryBuffer(
                    static_cast<DWORD>(sample.pcmData.size()),
                    buffer.put());
                if (FAILED(hr))
                {
                    return NativeFailure("CreateAudioSampleBuffer", "Failed to create Media Foundation audio sample buffer", hr);
                }

                BYTE* destination = nullptr;
                DWORD maxLength = 0;
                DWORD currentLength = 0;
                hr = buffer->Lock(&destination, &maxLength, &currentLength);
                if (FAILED(hr))
                {
                    return NativeFailure("LockAudioSampleBuffer", "Failed to lock Media Foundation audio sample buffer", hr);
                }

                if (maxLength < sample.pcmData.size())
                {
                    buffer->Unlock();
                    return OperationResult::Failure(
                        CoreResultCode::ValidationFailure,
                        "MediaFoundationFileSink",
                        "WriteAudioSample",
                        "Media Foundation audio sample buffer is smaller than the source sample");
                }

                std::memcpy(destination, sample.pcmData.data(), sample.pcmData.size());
                hr = buffer->Unlock();
                if (FAILED(hr))
                {
                    return NativeFailure("UnlockAudioSampleBuffer", "Failed to unlock Media Foundation audio sample buffer", hr);
                }

                hr = buffer->SetCurrentLength(static_cast<DWORD>(sample.pcmData.size()));
                if (FAILED(hr))
                {
                    return NativeFailure("SetAudioSampleBufferLength", "Failed to set Media Foundation audio sample buffer length", hr);
                }

                wil::com_ptr<IMFSample> mfSample;
                hr = MFCreateSample(mfSample.put());
                if (FAILED(hr))
                {
                    return NativeFailure("CreateAudioSample", "Failed to create Media Foundation audio sample", hr);
                }

                hr = mfSample->AddBuffer(buffer.get());
                if (FAILED(hr))
                {
                    return NativeFailure("AddAudioSampleBuffer", "Failed to attach audio buffer to Media Foundation sample", hr);
                }

                hr = mfSample->SetSampleTime(sample.timestamp.ticks100ns);
                if (FAILED(hr))
                {
                    return NativeFailure("SetAudioSampleTime", "Failed to set Media Foundation audio sample time", hr);
                }

                if (sample.duration.IsPositive())
                {
                    hr = mfSample->SetSampleDuration(sample.duration.ticks100ns);
                    if (FAILED(hr))
                    {
                        return NativeFailure("SetAudioSampleDuration", "Failed to set Media Foundation audio sample duration", hr);
                    }
                }

                hr = m_sinkWriter->WriteSample(sinkStreamIndex, mfSample.get());
                return SUCCEEDED(hr)
                    ? OperationResult::Success()
                    : NativeFailure("WriteAudioSample", "Failed to write Media Foundation audio sample", hr);
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

            [[nodiscard]] OperationResult Finalize() noexcept override
            {
                if (!m_sinkWriter)
                {
                    return OperationResult::Success();
                }

                const HRESULT hr = m_sinkWriter->Finalize();
                m_sinkWriter.reset();
                return SUCCEEDED(hr)
                    ? OperationResult::Success()
                    : NativeFailure("Finalize", "Failed to finalize Media Foundation sink writer", hr);
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

            [[nodiscard]] static MediaTypeCreationResult CreateAacOutputType(
                const MediaFoundationAacAudioStreamConfig& config) noexcept
            {
                MediaTypeCreationResult created = CreateMediaType("CreateAacOutputType");
                if (created.result.IsFailure())
                {
                    return created;
                }

                IMFMediaType& mediaType = *created.mediaType;
                OperationResult result = SetGuidAttribute(
                    mediaType,
                    MF_MT_MAJOR_TYPE,
                    MFMediaType_Audio,
                    "CreateAacOutputType",
                    "Failed to set AAC output major type");
                if (result.IsFailure()) return MediaTypeCreationResult{ result, {} };

                result = SetGuidAttribute(
                    mediaType,
                    MF_MT_SUBTYPE,
                    MFAudioFormat_AAC,
                    "CreateAacOutputType",
                    "Failed to set AAC output subtype");
                if (result.IsFailure()) return MediaTypeCreationResult{ result, {} };

                result = SetUint32Attribute(
                    mediaType,
                    MF_MT_AUDIO_SAMPLES_PER_SECOND,
                    config.sampleRate,
                    "CreateAacOutputType",
                    "Failed to set AAC output sample rate");
                if (result.IsFailure()) return MediaTypeCreationResult{ result, {} };

                result = SetUint32Attribute(
                    mediaType,
                    MF_MT_AUDIO_NUM_CHANNELS,
                    config.channels,
                    "CreateAacOutputType",
                    "Failed to set AAC output channel count");
                if (result.IsFailure()) return MediaTypeCreationResult{ result, {} };

                result = SetUint32Attribute(
                    mediaType,
                    MF_MT_AUDIO_AVG_BYTES_PER_SECOND,
                    config.bitrate / 8,
                    "CreateAacOutputType",
                    "Failed to set AAC output bitrate");
                if (result.IsFailure()) return MediaTypeCreationResult{ result, {} };

                result = SetUint32Attribute(
                    mediaType,
                    MF_MT_AUDIO_BITS_PER_SAMPLE,
                    16,
                    "CreateAacOutputType",
                    "Failed to set AAC output bits per sample");
                if (result.IsFailure()) return MediaTypeCreationResult{ result, {} };

                return created;
            }

            [[nodiscard]] static MediaTypeCreationResult CreateAudioInputType(
                const MediaFoundationAacAudioStreamConfig& config) noexcept
            {
                const GUID inputSubtype = AudioInputSubtype(config.inputSampleFormat);
                if (IsEqualGUID(inputSubtype, GUID_NULL))
                {
                    return MediaTypeCreationResult{
                        OperationResult::Failure(
                            CoreResultCode::UnsupportedOperation,
                            "MediaFoundationFileSink",
                            "CreateAudioInputType",
                            "Audio input sample format is not supported by the AAC sink path"),
                        {}
                    };
                }

                MediaTypeCreationResult created = CreateMediaType("CreateAudioInputType");
                if (created.result.IsFailure())
                {
                    return created;
                }

                IMFMediaType& mediaType = *created.mediaType;
                OperationResult result = SetGuidAttribute(
                    mediaType,
                    MF_MT_MAJOR_TYPE,
                    MFMediaType_Audio,
                    "CreateAudioInputType",
                    "Failed to set audio input major type");
                if (result.IsFailure()) return MediaTypeCreationResult{ result, {} };

                result = SetGuidAttribute(
                    mediaType,
                    MF_MT_SUBTYPE,
                    inputSubtype,
                    "CreateAudioInputType",
                    "Failed to set audio input subtype");
                if (result.IsFailure()) return MediaTypeCreationResult{ result, {} };

                result = SetUint32Attribute(
                    mediaType,
                    MF_MT_AUDIO_SAMPLES_PER_SECOND,
                    config.sampleRate,
                    "CreateAudioInputType",
                    "Failed to set audio input sample rate");
                if (result.IsFailure()) return MediaTypeCreationResult{ result, {} };

                result = SetUint32Attribute(
                    mediaType,
                    MF_MT_AUDIO_NUM_CHANNELS,
                    config.channels,
                    "CreateAudioInputType",
                    "Failed to set audio input channel count");
                if (result.IsFailure()) return MediaTypeCreationResult{ result, {} };

                result = SetUint32Attribute(
                    mediaType,
                    MF_MT_AUDIO_BITS_PER_SAMPLE,
                    config.inputBitsPerSample,
                    "CreateAudioInputType",
                    "Failed to set audio input bits per sample");
                if (result.IsFailure()) return MediaTypeCreationResult{ result, {} };

                result = SetUint32Attribute(
                    mediaType,
                    MF_MT_AUDIO_BLOCK_ALIGNMENT,
                    config.inputBlockAlign,
                    "CreateAudioInputType",
                    "Failed to set audio input block alignment");
                if (result.IsFailure()) return MediaTypeCreationResult{ result, {} };

                result = SetUint32Attribute(
                    mediaType,
                    MF_MT_AUDIO_AVG_BYTES_PER_SECOND,
                    config.sampleRate * config.inputBlockAlign,
                    "CreateAudioInputType",
                    "Failed to set audio input average bytes per second");
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
        const std::wstring& outputPath,
        const MediaFoundationSinkWriterFactoryOptions& options) noexcept
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

        hr = attributes->SetUINT32(
            MF_READWRITE_ENABLE_HARDWARE_TRANSFORMS,
            options.hardwareTransformsEnabled ? TRUE : FALSE);
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
        if (m_state == MediaFoundationFileSinkState::Opened
            || m_state == MediaFoundationFileSinkState::WritingReady)
        {
            (void)FinalizeCore();
            return;
        }

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

        InitializeDiagnostics(plan);
        OperationResult validation = ValidateOpenPlan(plan);
        if (validation.IsFailure())
        {
            RecordSetupFailure("ValidateOpenPlan", validation);
            m_state = MediaFoundationFileSinkState::Failed;
            return validation;
        }

        if (plan.container == ContainerFormat::Mp4)
        {
            if (!m_runtime)
            {
                OperationResult result = Failure(
                    CoreResultCode::InvalidState,
                    "Open",
                    "Media Foundation runtime is not configured");
                RecordSetupFailure("ValidateRuntime", result);
                m_state = MediaFoundationFileSinkState::Failed;
                return result;
            }

            if (!m_sinkWriterFactory)
            {
                OperationResult result = Failure(
                    CoreResultCode::InvalidState,
                    "Open",
                    "Media Foundation sink writer factory is not configured");
                RecordSetupFailure("ValidateSinkWriterFactory", result);
                m_state = MediaFoundationFileSinkState::Failed;
                return result;
            }

            MediaFoundationRuntimeLeaseResult runtimeLeaseResult = m_runtime->Acquire();
            if (!runtimeLeaseResult.IsSuccess())
            {
                RecordSetupFailure("AcquireRuntime", runtimeLeaseResult.result);
                m_state = MediaFoundationFileSinkState::Failed;
                return runtimeLeaseResult.result;
            }

            MediaFoundationSinkWriterFactoryOptions writerOptions = BuildSinkWriterOptions(plan);
            m_diagnostics.encoderSettingDiagnostics.push_back(
                writerOptions.hardwareTransformsEnabled
                    ? "Hardware transform preference applied: enabled"
                    : "Hardware transform preference applied: disabled");

            MediaFoundationSinkWriterCreationResult writerResult =
                m_sinkWriterFactory->CreateFileSinkWriter(plan.outputPath, writerOptions);
            if (!writerResult.IsSuccess())
            {
                runtimeLeaseResult.lease.Release();
                m_state = MediaFoundationFileSinkState::Failed;
                OperationResult result = writerResult.result.IsFailure()
                    ? writerResult.result
                    : Failure(
                        CoreResultCode::InvalidState,
                        "CreateFileSinkWriter",
                        "Media Foundation sink writer factory did not create a writer");
                RecordSetupFailure("CreateFileSinkWriter", result);
                return result;
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
                RecordSetupFailure("ConfigureMp4Streams", configureResult);
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

        const bool hasConfiguredMp4Streams = plan.container == ContainerFormat::Mp4 && !mappings.empty();
        m_streamMappings = std::move(mappings);
        for (const MediaFoundationSinkStreamMapping& mapping : m_streamMappings)
        {
            MarkAcceptedStream(mapping);
        }
        m_state = hasConfiguredMp4Streams
            ? MediaFoundationFileSinkState::WritingReady
            : MediaFoundationFileSinkState::Opened;
        return OperationResult::Success();
    }

    OperationResult MediaFoundationFileSink::WriteSample(const MediaSample& sample) noexcept
    {
        std::lock_guard lock(m_mutex);
        if (m_state == MediaFoundationFileSinkState::Created)
        {
            RecordRejectedWrite();
            return Failure(
                CoreResultCode::InvalidState,
                "WriteSample",
                "Media Foundation file sink is not open");
        }

        if (m_diagnostics.selectedProfile == ContainerFormat::Mp3)
        {
            RecordRejectedWrite();
            return Failure(
                CoreResultCode::UnsupportedOperation,
                "WriteSample",
                sample.Kind() == MediaKind::Video
                    ? "MP3 output does not accept video samples"
                    : "Media Foundation MP3 sample writing is not implemented in this PRD slice");
        }

        if (m_state == MediaFoundationFileSinkState::Opened)
        {
            RecordRejectedWrite();
            return Failure(
                CoreResultCode::InvalidState,
                "WriteSample",
                "Media Foundation file sink is not ready for sample writing");
        }

        if (m_state == MediaFoundationFileSinkState::Finalizing
            || m_state == MediaFoundationFileSinkState::Finalized
            || m_state == MediaFoundationFileSinkState::Failed)
        {
            RecordRejectedWrite();
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
            RecordRejectedWrite();
            return Failure(
                CoreResultCode::NotFound,
                "WriteSample",
                "Sample stream is not mapped by the Media Foundation file sink");
        }

        if (mapping->kind != sample.Kind())
        {
            RecordRejectedWrite();
            return Failure(
                CoreResultCode::ValidationFailure,
                "WriteSample",
                "Sample media kind does not match the negotiated output stream");
        }

        if (sample.Kind() == MediaKind::Video)
        {
            if (!m_sinkWriter)
            {
                RecordRejectedWrite();
                return Failure(
                    CoreResultCode::InvalidState,
                    "WriteSample",
                    "Media Foundation sink writer is not available");
            }

            const VideoSample& video = std::get<VideoSample>(sample.data);
            OperationResult validation = ValidateVideoSample(*mapping, video);
            if (validation.IsFailure())
            {
                if (IsTimestampValidationFailure(validation))
                {
                    RecordTimestampValidationFailure(mapping->streamId);
                }
                RecordRejectedWrite();
                return validation;
            }

            RecordAcceptedWriteStart();
            OperationResult writeResult = m_sinkWriter->WriteVideoSample(mapping->sinkStreamIndex, video);
            RecordAcceptedWriteCompletion(writeResult);
            if (writeResult.IsFailure())
            {
                return writeResult;
            }

            RecordWrittenTimestamp(video.streamId, video.timestamp);
            RecordSampleWritten(video.streamId);
            return OperationResult::Success();
        }

        if (sample.Kind() == MediaKind::Audio)
        {
            if (!m_sinkWriter)
            {
                RecordRejectedWrite();
                return Failure(
                    CoreResultCode::InvalidState,
                    "WriteSample",
                    "Media Foundation sink writer is not available");
            }

            const AudioSample& audio = std::get<AudioSample>(sample.data);
            OperationResult validation = ValidateAudioSample(*mapping, audio);
            if (validation.IsFailure())
            {
                if (IsTimestampValidationFailure(validation))
                {
                    RecordTimestampValidationFailure(mapping->streamId);
                }
                RecordRejectedWrite();
                return validation;
            }

            RecordAcceptedWriteStart();
            OperationResult writeResult = m_sinkWriter->WriteAudioSample(mapping->sinkStreamIndex, audio);
            RecordAcceptedWriteCompletion(writeResult);
            if (writeResult.IsFailure())
            {
                return writeResult;
            }

            RecordWrittenTimestamp(audio.streamId, audio.timestamp);
            RecordSampleWritten(audio.streamId);
            return OperationResult::Success();
        }

        RecordRejectedWrite();
        return Failure(
            CoreResultCode::UnsupportedOperation,
            "WriteSample",
            "Media sample kind is not supported by the Media Foundation file sink");
    }

    OperationResult MediaFoundationFileSink::Finalize() noexcept
    {
        std::lock_guard lock(m_mutex);
        if (m_state == MediaFoundationFileSinkState::Finalized)
        {
            return m_finalizationResult.value_or(OperationResult::Success());
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

        return FinalizeCore();
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

    MediaFoundationFileSinkWriteDiagnostics MediaFoundationFileSink::WriteDiagnostics() const noexcept
    {
        std::lock_guard lock(m_mutex);
        return m_writeDiagnostics;
    }

    MediaFoundationFileSinkDiagnostics MediaFoundationFileSink::Diagnostics() const
    {
        std::lock_guard lock(m_mutex);
        MediaFoundationFileSinkDiagnostics diagnostics = m_diagnostics;
        diagnostics.writes = m_writeDiagnostics;
        return diagnostics;
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

            if (stream.video->bitrate < 100'000 || stream.video->bitrate > 100'000'000)
            {
                return Failure(
                    CoreResultCode::RangeError,
                    "Open",
                    "Video output bitrate is outside the supported Media Foundation range");
            }

            if (stream.video->frameRate.numerator < stream.video->frameRate.denominator
                || stream.video->frameRate.numerator > stream.video->frameRate.denominator * 240)
            {
                return Failure(
                    CoreResultCode::RangeError,
                    "Open",
                    "Video output frame rate is outside the supported Media Foundation range");
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
                if (stream.video->gopLength != 0)
                {
                    m_diagnostics.encoderSettingDiagnostics.push_back(
                        "GOP length "
                        + std::to_string(stream.video->gopLength)
                        + " ignored: direct Media Foundation GOP mapping is deferred");
                }

                MediaFoundationStreamConfigurationResult streamResult =
                    m_sinkWriter->ConfigureH264VideoStream(BuildH264VideoStreamConfig(stream));
                if (streamResult.result.IsFailure())
                {
                    return streamResult.result;
                }

                mappings.push_back(MediaFoundationSinkStreamMapping{
                    stream.streamId,
                    stream.kind,
                    streamResult.sinkStreamIndex,
                    stream.videoMediaType
                });
                continue;
            }

            if (stream.kind == MediaKind::Audio)
            {
                const MediaFoundationAacAudioStreamConfig config = BuildAacAudioStreamConfig(stream);
                MediaFoundationStreamConfigurationResult streamResult =
                    m_sinkWriter->ConfigureAacAudioStream(config);
                if (streamResult.result.IsFailure())
                {
                    return streamResult.result;
                }

                mappings.push_back(MediaFoundationSinkStreamMapping{
                    stream.streamId,
                    stream.kind,
                    streamResult.sinkStreamIndex,
                    std::nullopt,
                    BuildAudioInputMediaType(config)
                });
                continue;
            }

            mappings.push_back(MediaFoundationSinkStreamMapping{
                stream.streamId,
                stream.kind,
                static_cast<uint32_t>(mappings.size()),
                std::nullopt
            });
        }

        if (!mappings.empty())
        {
            OperationResult beginResult = m_sinkWriter->BeginWriting();
            if (beginResult.IsFailure())
            {
                return beginResult;
            }

            m_hasBegunWriting = true;
        }

        return OperationResult::Success();
    }

    MediaFoundationSinkWriterFactoryOptions MediaFoundationFileSink::BuildSinkWriterOptions(
        const OutputPlan& plan) noexcept
    {
        for (const OutputStreamPlan& stream : plan.streams)
        {
            if (stream.IsVideo() && stream.video.has_value())
            {
                return MediaFoundationSinkWriterFactoryOptions{
                    stream.video->hardwareAccelerationPreferred
                };
            }
        }

        return {};
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
            stream.videoMediaType->pixelFormat,
            stream.video->gopLength,
            stream.video->hardwareAccelerationPreferred
        };
    }

    MediaFoundationAacAudioStreamConfig MediaFoundationFileSink::BuildAacAudioStreamConfig(
        const OutputStreamPlan& stream) noexcept
    {
        const uint16_t inputBitsPerSample = 32;
        const uint16_t inputBlockAlign =
            static_cast<uint16_t>(stream.audio->channels * (inputBitsPerSample / 8));

        return MediaFoundationAacAudioStreamConfig{
            stream.streamId,
            stream.audio->sampleRate,
            stream.audio->channels,
            stream.audio->bitrate,
            AudioSampleFormat::Float32,
            inputBitsPerSample,
            inputBlockAlign
        };
    }

    AudioMediaType MediaFoundationFileSink::BuildAudioInputMediaType(
        const MediaFoundationAacAudioStreamConfig& config) noexcept
    {
        return AudioMediaType{
            config.sampleRate,
            config.channels,
            config.inputBitsPerSample,
            config.inputBlockAlign,
            config.inputSampleFormat
        };
    }

    OperationResult MediaFoundationFileSink::ValidateVideoSample(
        const MediaFoundationSinkStreamMapping& mapping,
        const VideoSample& sample) const noexcept
    {
        if (!mapping.videoMediaType.has_value())
        {
            return Failure(
                CoreResultCode::InvalidState,
                "WriteSample",
                "Video stream does not have a negotiated media type");
        }

        if (!(sample.mediaType == mapping.videoMediaType.value()))
        {
            return Failure(
                CoreResultCode::ValidationFailure,
                "WriteSample",
                "Video sample media type does not match the negotiated output stream");
        }

        if (sample.timestamp.IsNegative())
        {
            return Failure(
                CoreResultCode::ValidationFailure,
                "WriteSample",
                "Video sample timestamp must be recording-relative");
        }

        if (sample.mediaType.pixelFormat == VideoPixelFormat::Bgra8)
        {
            const uint64_t expectedBytes =
                static_cast<uint64_t>(sample.mediaType.width) * sample.mediaType.height * 4;
            if (sample.pixelData.size() != expectedBytes)
            {
                return Failure(
                    CoreResultCode::ValidationFailure,
                    "WriteSample",
                    "Video sample buffer size does not match the negotiated media type");
            }
        }

        if (HasRegressingTimestamp(mapping, sample.timestamp))
        {
            return Failure(
                CoreResultCode::ValidationFailure,
                "WriteSample",
                "Video sample timestamp regressed for the negotiated stream");
        }

        return OperationResult::Success();
    }

    OperationResult MediaFoundationFileSink::ValidateAudioSample(
        const MediaFoundationSinkStreamMapping& mapping,
        const AudioSample& sample) const noexcept
    {
        if (!mapping.audioMediaType.has_value())
        {
            return Failure(
                CoreResultCode::InvalidState,
                "WriteSample",
                "Audio stream does not have a negotiated media type");
        }

        if (!(sample.mediaType == mapping.audioMediaType.value()))
        {
            return Failure(
                CoreResultCode::ValidationFailure,
                "WriteSample",
                "Audio sample media type does not match the negotiated output stream");
        }

        if (sample.timestamp.IsNegative())
        {
            return Failure(
                CoreResultCode::ValidationFailure,
                "WriteSample",
                "Audio sample timestamp must be recording-relative");
        }

        if (sample.pcmData.empty() || sample.pcmData.size() % sample.mediaType.blockAlign != 0)
        {
            return Failure(
                CoreResultCode::ValidationFailure,
                "WriteSample",
                "Audio sample buffer size does not match the negotiated media type");
        }

        if (sample.frameCount != 0
            && sample.pcmData.size() != static_cast<size_t>(sample.frameCount) * sample.mediaType.blockAlign)
        {
            return Failure(
                CoreResultCode::ValidationFailure,
                "WriteSample",
                "Audio sample frame count does not match the buffer size");
        }

        if (HasRegressingTimestamp(mapping, sample.timestamp))
        {
            return Failure(
                CoreResultCode::ValidationFailure,
                "WriteSample",
                "Audio sample timestamp regressed for the negotiated stream");
        }

        return OperationResult::Success();
    }

    bool MediaFoundationFileSink::HasRegressingTimestamp(
        const MediaFoundationSinkStreamMapping& mapping,
        const MediaTime& timestamp) const noexcept
    {
        for (const auto& entry : m_lastWrittenTimestamps)
        {
            if (entry.first == mapping.streamId)
            {
                return timestamp < entry.second;
            }
        }

        return false;
    }

    void MediaFoundationFileSink::RecordWrittenTimestamp(StreamId streamId, MediaTime timestamp) noexcept
    {
        for (auto& entry : m_lastWrittenTimestamps)
        {
            if (entry.first == streamId)
            {
                entry.second = timestamp;
                return;
            }
        }

        m_lastWrittenTimestamps.emplace_back(streamId, timestamp);
    }

    OperationResult MediaFoundationFileSink::FinalizeCore() noexcept
    {
        m_state = MediaFoundationFileSinkState::Finalizing;

        OperationResult result = OperationResult::Success();
        if (m_sinkWriter && m_hasBegunWriting)
        {
            result = m_sinkWriter->Finalize();
        }

        RecordFinalizeResult(result);
        ReleaseWriterResources();
        m_finalizationResult = result;
        m_state = MediaFoundationFileSinkState::Finalized;
        return result;
    }

    void MediaFoundationFileSink::InitializeDiagnostics(const OutputPlan& plan)
    {
        m_diagnostics = MediaFoundationFileSinkDiagnostics{};
        m_writeDiagnostics = MediaFoundationFileSinkWriteDiagnostics{};
        m_activeWriteCount = 0;
        m_diagnostics.outputPath = plan.outputPath;
        m_diagnostics.selectedProfile = plan.container;
        m_diagnostics.selectedProfileName = ContainerFormatName(plan.container);
        m_diagnostics.setupStage = "Open";
        m_diagnostics.streams.reserve(plan.streams.size());

        for (const OutputStreamPlan& stream : plan.streams)
        {
            MediaFoundationFileSinkStreamDiagnostics streamDiagnostics;
            streamDiagnostics.streamId = stream.streamId;
            streamDiagnostics.kind = stream.kind;
            streamDiagnostics.rejected = true;
            if (stream.videoMediaType.has_value())
            {
                streamDiagnostics.configuredMediaTypeSummary =
                    "video planned "
                    + std::to_string(stream.videoMediaType->width)
                    + "x"
                    + std::to_string(stream.videoMediaType->height);
            }
            else if (stream.audio.has_value())
            {
                streamDiagnostics.configuredMediaTypeSummary =
                    "audio planned "
                    + std::to_string(stream.audio->sampleRate)
                    + "Hz "
                    + std::to_string(stream.audio->channels)
                    + "ch";
            }

            m_diagnostics.streams.push_back(std::move(streamDiagnostics));
        }
    }

    void MediaFoundationFileSink::MarkAcceptedStream(const MediaFoundationSinkStreamMapping& mapping)
    {
        MediaFoundationFileSinkStreamDiagnostics* diagnostics = FindStreamDiagnostics(mapping.streamId);
        if (!diagnostics)
        {
            m_diagnostics.streams.push_back(MediaFoundationFileSinkStreamDiagnostics{
                mapping.streamId,
                mapping.kind
            });
            diagnostics = &m_diagnostics.streams.back();
        }

        diagnostics->kind = mapping.kind;
        diagnostics->accepted = true;
        diagnostics->rejected = false;
        diagnostics->sinkStreamIndex = mapping.sinkStreamIndex;
        diagnostics->configuredMediaTypeSummary = BuildStreamMediaTypeSummary(mapping);
    }

    void MediaFoundationFileSink::RecordSetupFailure(std::string stage, const OperationResult& result)
    {
        m_diagnostics.setupStage = std::move(stage);
        if (result.diagnostic.has_value())
        {
            m_diagnostics.setupFailure = result.diagnostic;
        }
    }

    void MediaFoundationFileSink::RecordFinalizeResult(const OperationResult& result)
    {
        m_diagnostics.finalized = true;
        m_diagnostics.finalizeStage = "Finalize";
        if (result.IsFailure() && result.diagnostic.has_value())
        {
            m_diagnostics.finalizeFailure = result.diagnostic;
        }
    }

    void MediaFoundationFileSink::RecordRejectedWrite() noexcept
    {
        ++m_writeDiagnostics.rejectedWrites;
    }

    void MediaFoundationFileSink::RecordAcceptedWriteStart() noexcept
    {
        ++m_writeDiagnostics.acceptedWrites;
        ++m_activeWriteCount;
        m_writeDiagnostics.writeDepthHighWaterMark =
            std::max(m_writeDiagnostics.writeDepthHighWaterMark, m_activeWriteCount);
    }

    void MediaFoundationFileSink::RecordAcceptedWriteCompletion(const OperationResult& result) noexcept
    {
        if (m_activeWriteCount > 0)
        {
            --m_activeWriteCount;
        }

        if (result.IsSuccess())
        {
            ++m_writeDiagnostics.completedWrites;
        }
        else
        {
            ++m_writeDiagnostics.failedWrites;
        }
    }

    void MediaFoundationFileSink::RecordSampleWritten(StreamId streamId) noexcept
    {
        if (MediaFoundationFileSinkStreamDiagnostics* diagnostics = FindStreamDiagnostics(streamId))
        {
            ++diagnostics->samplesWritten;
        }
    }

    void MediaFoundationFileSink::RecordTimestampValidationFailure(StreamId streamId) noexcept
    {
        ++m_diagnostics.timestampValidationFailures;
        if (MediaFoundationFileSinkStreamDiagnostics* diagnostics = FindStreamDiagnostics(streamId))
        {
            ++diagnostics->timestampValidationFailures;
        }
    }

    MediaFoundationFileSinkStreamDiagnostics* MediaFoundationFileSink::FindStreamDiagnostics(
        StreamId streamId) noexcept
    {
        const auto found = std::find_if(
            m_diagnostics.streams.begin(),
            m_diagnostics.streams.end(),
            [&](const MediaFoundationFileSinkStreamDiagnostics& candidate)
            {
                return candidate.streamId == streamId;
            });

        return found == m_diagnostics.streams.end() ? nullptr : &*found;
    }

    bool MediaFoundationFileSink::IsTimestampValidationFailure(const OperationResult& result) noexcept
    {
        return result.IsFailure()
            && result.diagnostic.has_value()
            && result.diagnostic->operation == "WriteSample"
            && result.diagnostic->message.find("timestamp") != std::string::npos;
    }

    std::string MediaFoundationFileSink::ContainerFormatName(ContainerFormat container)
    {
        switch (container)
        {
        case ContainerFormat::Mp4:
            return "mp4";
        case ContainerFormat::Mp3:
            return "mp3";
        case ContainerFormat::Wav:
            return "wav";
        default:
            return "unknown";
        }
    }

    std::string MediaFoundationFileSink::BuildStreamMediaTypeSummary(
        const MediaFoundationSinkStreamMapping& mapping)
    {
        if (mapping.videoMediaType.has_value())
        {
            const VideoMediaType& mediaType = mapping.videoMediaType.value();
            return "video "
                + std::to_string(mediaType.width)
                + "x"
                + std::to_string(mediaType.height)
                + " @ "
                + std::to_string(mediaType.frameRate.numerator)
                + "/"
                + std::to_string(mediaType.frameRate.denominator)
                + " fps";
        }

        if (mapping.audioMediaType.has_value())
        {
            const AudioMediaType& mediaType = mapping.audioMediaType.value();
            return "audio "
                + std::to_string(mediaType.sampleRate)
                + "Hz "
                + std::to_string(mediaType.channels)
                + "ch "
                + std::to_string(mediaType.bitsPerSample)
                + "bit";
        }

        return "unconfigured";
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
        m_hasBegunWriting = false;
        m_lastWrittenTimestamps.clear();
        m_runtimeLease.Release();
    }
}
