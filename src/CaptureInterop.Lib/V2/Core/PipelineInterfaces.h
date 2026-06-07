#pragma once

#include "CapturePipelineConfig.h"
#include "MediaSamples.h"
#include "OutputProfileResolver.h"
#include "ResultTypes.h"

#include <functional>
#include <memory>
#include <utility>
#include <vector>

namespace CaptureInterop::V2
{
    using VideoSampleHandler = std::function<void(const VideoSample&)>;
    using AudioSampleHandler = std::function<void(const AudioSample&)>;
    using MediaSampleHandler = std::function<void(const MediaSample&)>;

    // Destroying a callback registration synchronously unregisters the callback from
    // the publisher. Handlers must not assume sample references remain valid after
    // the callback returns unless they copy the sample data.
    class ICallbackRegistration
    {
    public:
        virtual ~ICallbackRegistration() = default;
    };

    using CallbackRegistrationToken = std::unique_ptr<ICallbackRegistration>;

    class IMediaSource
    {
    public:
        virtual ~IMediaSource() = default;

        [[nodiscard]] virtual SourceDescriptor Describe() const = 0;
        [[nodiscard]] virtual std::vector<StreamDescriptor> Streams() const = 0;
        [[nodiscard]] virtual OperationResult Start() noexcept = 0;
        [[nodiscard]] virtual OperationResult Stop() noexcept = 0;
    };

    class IVideoCaptureSource : public IMediaSource
    {
    public:
        [[nodiscard]] virtual CallbackRegistrationToken RegisterFrameArrivedHandler(VideoSampleHandler handler) = 0;
    };

    class IAudioCaptureSource : public IMediaSource
    {
    public:
        [[nodiscard]] virtual CallbackRegistrationToken RegisterSampleArrivedHandler(AudioSampleHandler handler) = 0;
    };

    class ISourcePauseControl
    {
    public:
        virtual ~ISourcePauseControl() = default;

        [[nodiscard]] virtual OperationResult SetPaused(bool paused) noexcept = 0;
    };

    class IMediaProcessor
    {
    public:
        virtual ~IMediaProcessor() = default;

        [[nodiscard]] virtual MediaKind Kind() const noexcept = 0;
        [[nodiscard]] virtual OperationResult Configure(const MediaType& input, const MediaType& output) noexcept = 0;
        [[nodiscard]] virtual OperationResult Process(const MediaSample& sample) noexcept = 0;
        [[nodiscard]] virtual CallbackRegistrationToken RegisterOutputHandler(MediaSampleHandler handler) = 0;
    };

    class IOutputSink
    {
    public:
        virtual ~IOutputSink() = default;

        [[nodiscard]] virtual OperationResult Open(const OutputPlan& plan) noexcept = 0;
        [[nodiscard]] virtual OperationResult WriteSample(const MediaSample& sample) noexcept = 0;
        [[nodiscard]] virtual OperationResult Finalize() noexcept = 0;
    };

    class IMediaSourceFactory
    {
    public:
        virtual ~IMediaSourceFactory() = default;

        [[nodiscard]] virtual std::vector<std::unique_ptr<IMediaSource>> CreateSources(
            const CapturePipelineConfig& config) = 0;
    };

    class IMediaProcessorFactory
    {
    public:
        virtual ~IMediaProcessorFactory() = default;

        [[nodiscard]] virtual std::vector<std::unique_ptr<IMediaProcessor>> CreateProcessors(
            const OutputPlan& plan) = 0;
    };

    class IOutputSinkFactory
    {
    public:
        virtual ~IOutputSinkFactory() = default;

        [[nodiscard]] virtual std::unique_ptr<IOutputSink> CreateSink(const OutputPlan& plan) = 0;
    };
}
