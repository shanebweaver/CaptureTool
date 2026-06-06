#pragma once

#include "V2/Core/PipelineInterfaces.h"

#include <functional>
#include <memory>
#include <utility>
#include <vector>

namespace CaptureInterop::V2::Testing
{
    class CallbackRegistration final : public ICallbackRegistration
    {
    public:
        explicit CallbackRegistration(std::function<void()> unregister)
            : m_unregister(std::move(unregister))
        {
        }

        ~CallbackRegistration() override
        {
            if (m_unregister)
            {
                m_unregister();
            }
        }

    private:
        std::function<void()> m_unregister;
    };

    struct SampleBuilder
    {
        static VideoMediaType VideoType()
        {
            VideoMediaType mediaType;
            mediaType.width = 2;
            mediaType.height = 2;
            mediaType.frameRate = Rational::From(60, 1);
            mediaType.pixelFormat = VideoPixelFormat::Bgra8;
            return mediaType;
        }

        static AudioMediaType AudioType()
        {
            AudioMediaType mediaType;
            mediaType.sampleRate = 48000;
            mediaType.channels = 2;
            mediaType.bitsPerSample = 16;
            mediaType.blockAlign = 4;
            mediaType.sampleFormat = AudioSampleFormat::Pcm16;
            return mediaType;
        }

        static VideoSample Video(
            MediaTime timestamp = MediaTime::Zero(),
            MediaDuration duration = MediaDuration::FromMilliseconds(16))
        {
            return VideoSample{
                SourceId::FromValue(1),
                StreamId::FromValue(1),
                timestamp,
                duration,
                VideoType(),
                std::vector<uint8_t>{ 1, 2, 3, 4 }
            };
        }

        static AudioSample Audio(
            MediaTime timestamp = MediaTime::Zero(),
            MediaDuration duration = MediaDuration::FromMilliseconds(10))
        {
            return AudioSample{
                SourceId::FromValue(2),
                StreamId::FromValue(2),
                timestamp,
                duration,
                AudioType(),
                std::vector<uint8_t>{ 5, 6, 7, 8 }
            };
        }
    };

    class FakeVideoSource final : public IVideoCaptureSource
    {
    public:
        SourceDescriptor Describe() const override
        {
            return SourceDescriptor{ SourceId::FromValue(1), SourceKind::Desktop, "Fake video source" };
        }

        std::vector<StreamDescriptor> Streams() const override
        {
            return { StreamDescriptor{ StreamId::FromValue(1), SourceId::FromValue(1), MediaKind::Video, "Fake video stream" } };
        }

        OperationResult Start() noexcept override
        {
            if (m_startResult.IsFailure())
            {
                return m_startResult;
            }

            m_started = true;
            return OperationResult::Success();
        }

        OperationResult Stop() noexcept override
        {
            if (m_stopResult.IsFailure())
            {
                return m_stopResult;
            }

            m_stopped = true;
            return OperationResult::Success();
        }

        CallbackRegistrationToken RegisterFrameArrivedHandler(VideoSampleHandler handler) override
        {
            m_state->handler = std::move(handler);
            std::weak_ptr<State> weakState = m_state;
            return std::make_unique<CallbackRegistration>(
                [weakState]()
                {
                    if (const std::shared_ptr<State> state = weakState.lock())
                    {
                        state->handler = nullptr;
                    }
                });
        }

        OperationResult Emit(const VideoSample& sample)
        {
            if (m_state->handler)
            {
                m_state->handler(sample);
            }

            return OperationResult::Success();
        }

        void SetStartResult(OperationResult result)
        {
            m_startResult = std::move(result);
        }

        void SetStopResult(OperationResult result)
        {
            m_stopResult = std::move(result);
        }

        [[nodiscard]] bool Started() const noexcept { return m_started; }
        [[nodiscard]] bool Stopped() const noexcept { return m_stopped; }

    private:
        struct State
        {
            VideoSampleHandler handler;
        };

        std::shared_ptr<State> m_state = std::make_shared<State>();
        OperationResult m_startResult = OperationResult::Success();
        OperationResult m_stopResult = OperationResult::Success();
        bool m_started{ false };
        bool m_stopped{ false };
    };

    class FakeAudioSource final : public IAudioCaptureSource
    {
    public:
        SourceDescriptor Describe() const override
        {
            return SourceDescriptor{ SourceId::FromValue(2), SourceKind::SystemAudio, "Fake audio source" };
        }

        std::vector<StreamDescriptor> Streams() const override
        {
            return { StreamDescriptor{ StreamId::FromValue(2), SourceId::FromValue(2), MediaKind::Audio, "Fake audio stream" } };
        }

        OperationResult Start() noexcept override
        {
            m_started = true;
            return OperationResult::Success();
        }

        OperationResult Stop() noexcept override
        {
            m_stopped = true;
            return OperationResult::Success();
        }

        CallbackRegistrationToken RegisterSampleArrivedHandler(AudioSampleHandler handler) override
        {
            m_state->handler = std::move(handler);
            std::weak_ptr<State> weakState = m_state;
            return std::make_unique<CallbackRegistration>(
                [weakState]()
                {
                    if (const std::shared_ptr<State> state = weakState.lock())
                    {
                        state->handler = nullptr;
                    }
                });
        }

        OperationResult Emit(const AudioSample& sample)
        {
            if (m_state->handler)
            {
                m_state->handler(sample);
            }

            return OperationResult::Success();
        }

        [[nodiscard]] bool Started() const noexcept { return m_started; }
        [[nodiscard]] bool Stopped() const noexcept { return m_stopped; }

    private:
        struct State
        {
            AudioSampleHandler handler;
        };

        std::shared_ptr<State> m_state = std::make_shared<State>();
        bool m_started{ false };
        bool m_stopped{ false };
    };

    class PassThroughProcessor final : public IMediaProcessor
    {
    public:
        explicit PassThroughProcessor(MediaKind kind) noexcept
            : m_kind(kind)
        {
        }

        MediaKind Kind() const noexcept override
        {
            return m_kind;
        }

        OperationResult Configure(const MediaType&, const MediaType&) noexcept override
        {
            m_configured = true;
            return OperationResult::Success();
        }

        OperationResult Process(const MediaSample& sample) noexcept override
        {
            if (m_processResult.IsFailure())
            {
                return m_processResult;
            }

            m_receivedSamples.push_back(sample);
            if (m_outputHandler)
            {
                m_outputHandler(sample);
            }

            return OperationResult::Success();
        }

        CallbackRegistrationToken RegisterOutputHandler(MediaSampleHandler handler) override
        {
            m_outputHandler = std::move(handler);
            return std::make_unique<CallbackRegistration>(
                [this]()
                {
                    m_outputHandler = nullptr;
                });
        }

        void SetProcessResult(OperationResult result)
        {
            m_processResult = std::move(result);
        }

        [[nodiscard]] bool Configured() const noexcept { return m_configured; }
        [[nodiscard]] const std::vector<MediaSample>& ReceivedSamples() const noexcept { return m_receivedSamples; }

    private:
        MediaKind m_kind{ MediaKind::Unknown };
        bool m_configured{ false };
        OperationResult m_processResult = OperationResult::Success();
        MediaSampleHandler m_outputHandler;
        std::vector<MediaSample> m_receivedSamples;
    };

    class NullOutputSink final : public IOutputSink
    {
    public:
        OperationResult Open(const OutputPlan& plan) noexcept override
        {
            m_opened = true;
            m_plan = plan;
            return OperationResult::Success();
        }

        OperationResult WriteSample(const MediaSample& sample) noexcept override
        {
            if (m_writeResult.IsFailure())
            {
                return m_writeResult;
            }

            m_receivedSamples.push_back(sample);
            return OperationResult::Success();
        }

        OperationResult Finalize() noexcept override
        {
            m_finalized = true;
            return OperationResult::Success();
        }

        void SetWriteResult(OperationResult result)
        {
            m_writeResult = std::move(result);
        }

        [[nodiscard]] bool Opened() const noexcept { return m_opened; }
        [[nodiscard]] bool Finalized() const noexcept { return m_finalized; }
        [[nodiscard]] const std::optional<OutputPlan>& Plan() const noexcept { return m_plan; }
        [[nodiscard]] const std::vector<MediaSample>& ReceivedSamples() const noexcept { return m_receivedSamples; }

    private:
        bool m_opened{ false };
        bool m_finalized{ false };
        std::optional<OutputPlan> m_plan;
        OperationResult m_writeResult = OperationResult::Success();
        std::vector<MediaSample> m_receivedSamples;
    };
}
