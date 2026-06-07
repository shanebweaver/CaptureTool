#pragma once

#include "AudioControlProcessors.h"
#include "OutputProfileResolver.h"
#include "PipelineInterfaces.h"
#include "PipelineStateMachine.h"
#include "RecordingClock.h"

#include <memory>
#include <optional>
#include <utility>
#include <vector>

namespace CaptureInterop::V2
{
    struct CapturePipelineStopResult
    {
        OperationResult result = OperationResult::Success();
        PipelineState finalState{ PipelineState::Created };
        TeardownStage failureStage{ TeardownStage::None };
        bool alreadyStopped{ false };
    };

    class CapturePipelineSession
    {
    public:
        CapturePipelineSession(
            CapturePipelineConfig config,
            IMediaSourceFactory& sourceFactory,
            IMediaProcessorFactory& processorFactory,
            IOutputSinkFactory& sinkFactory,
            std::unique_ptr<IRecordingClock> clock)
            : m_config(std::move(config)),
              m_sourceFactory(sourceFactory),
              m_processorFactory(processorFactory),
              m_sinkFactory(sinkFactory),
              m_clock(std::move(clock))
        {
        }

        [[nodiscard]] PipelineState State() const noexcept
        {
            return m_stateMachine.State();
        }

        [[nodiscard]] const std::vector<TeardownStage>& TeardownStages() const noexcept
        {
            return m_teardownStages;
        }

        [[nodiscard]] OperationResult Start()
        {
            if (!m_stateMachine.CanApply(PipelineOperation::Start))
            {
                return OperationResult::Failure(
                    CoreResultCode::InvalidState,
                    "CapturePipelineSession",
                    "Start",
                    "Session cannot be started from the current state");
            }

            const OutputProfileResolutionResult resolution = m_outputProfileResolver.Resolve(m_config);
            if (!resolution.IsSuccess())
            {
                (void)m_stateMachine.Fail();
                return resolution.diagnostics.ToOperationResult();
            }

            m_outputPlan = resolution.plan;
            m_sources = m_sourceFactory.CreateSources(m_config);
            m_processors = m_processorFactory.CreateProcessors(*m_outputPlan);
            m_sink = m_sinkFactory.CreateSink(*m_outputPlan);

            if (m_clock == nullptr || m_sink == nullptr)
            {
                (void)m_stateMachine.Fail();
                return OperationResult::Failure(
                    CoreResultCode::ValidationFailure,
                    "CapturePipelineSession",
                    "Start",
                    "Session graph factory returned a missing required component");
            }

            if (OperationResult openResult = m_sink->Open(*m_outputPlan); openResult.IsFailure())
            {
                return FailStartAndTeardown(std::move(openResult));
            }

            if (OperationResult clockResult = m_clock->Start(); clockResult.IsFailure())
            {
                return FailStartAndTeardown(std::move(clockResult));
            }

            RegisterGraphCallbacks();

            if (OperationResult stateResult = m_stateMachine.Start(); stateResult.IsFailure())
            {
                return FailStartAndTeardown(std::move(stateResult));
            }

            for (const std::unique_ptr<IMediaSource>& source : m_sources)
            {
                if (source == nullptr)
                {
                    return FailStartAndTeardown(OperationResult::Failure(
                        CoreResultCode::ValidationFailure,
                        "CapturePipelineSession",
                        "Start",
                        "Session graph factory returned a null source"));
                }

                if (OperationResult startResult = source->Start(); startResult.IsFailure())
                {
                    return FailStartAndTeardown(std::move(startResult));
                }
            }

            return OperationResult::Success();
        }

        [[nodiscard]] OperationResult Pause()
        {
            if (!m_stateMachine.CanApply(PipelineOperation::Pause))
            {
                return OperationResult::Failure(
                    CoreResultCode::InvalidState,
                    "CapturePipelineSession",
                    "Pause",
                    "Session cannot be paused from the current state");
            }

            if (m_clock == nullptr)
            {
                return OperationResult::Failure(
                    CoreResultCode::InvalidState,
                    "CapturePipelineSession",
                    "Pause",
                    "Session clock is not available");
            }

            if (OperationResult clockResult = m_clock->Pause(); clockResult.IsFailure())
            {
                return clockResult;
            }

            return m_stateMachine.Pause();
        }

        [[nodiscard]] OperationResult Resume()
        {
            if (!m_stateMachine.CanApply(PipelineOperation::Resume))
            {
                return OperationResult::Failure(
                    CoreResultCode::InvalidState,
                    "CapturePipelineSession",
                    "Resume",
                    "Session cannot be resumed from the current state");
            }

            if (m_clock == nullptr)
            {
                return OperationResult::Failure(
                    CoreResultCode::InvalidState,
                    "CapturePipelineSession",
                    "Resume",
                    "Session clock is not available");
            }

            if (OperationResult clockResult = m_clock->Resume(); clockResult.IsFailure())
            {
                return clockResult;
            }

            return m_stateMachine.Resume();
        }

        [[nodiscard]] OperationResult SetAudioMuted(SourceId sourceId, bool muted)
        {
            if (!m_stateMachine.CanApply(PipelineOperation::SetAudioMuted))
            {
                return OperationResult::Failure(
                    CoreResultCode::InvalidState,
                    "CapturePipelineSession",
                    "SetAudioMuted",
                    "Session cannot update audio mute from the current state");
            }

            IAudioMuteProcessor* processor = FindAudioMuteProcessor(sourceId);
            if (processor == nullptr)
            {
                return OperationResult::Failure(
                    CoreResultCode::NotFound,
                    "CapturePipelineSession",
                    "SetAudioMuted",
                    "No armed audio mute processor was found for the source");
            }

            return processor->SetMuted(muted);
        }

        [[nodiscard]] OperationResult SetAudioGain(SourceId sourceId, float gainDb)
        {
            if (!m_stateMachine.CanApply(PipelineOperation::SetAudioGain))
            {
                return OperationResult::Failure(
                    CoreResultCode::InvalidState,
                    "CapturePipelineSession",
                    "SetAudioGain",
                    "Session cannot update audio gain from the current state");
            }

            AudioGainSettings requestedGain;
            requestedGain.gainDb = gainDb;
            if (!requestedGain.IsInSupportedRange())
            {
                return OperationResult::Failure(
                    CoreResultCode::RangeError,
                    "CapturePipelineSession",
                    "SetAudioGain",
                    "Audio gain is outside the supported range");
            }

            IAudioGainProcessor* processor = FindAudioGainProcessor(sourceId);
            if (processor == nullptr)
            {
                return OperationResult::Failure(
                    CoreResultCode::NotFound,
                    "CapturePipelineSession",
                    "SetAudioGain",
                    "No armed audio gain processor was found for the source");
            }

            return processor->SetGainDb(gainDb);
        }

        [[nodiscard]] CapturePipelineStopResult Stop()
        {
            if (m_stateMachine.IsTerminal())
            {
                return CapturePipelineStopResult{
                    OperationResult::Success(),
                    m_stateMachine.State(),
                    TeardownStage::None,
                    true
                };
            }

            if (!m_stateMachine.CanApply(PipelineOperation::Stop))
            {
                return CapturePipelineStopResult{
                    OperationResult::Failure(
                        CoreResultCode::InvalidState,
                        "CapturePipelineSession",
                        "Stop",
                        "Session cannot be stopped from the current state"),
                    m_stateMachine.State(),
                    TeardownStage::None,
                    false
                };
            }

            const TeardownOutcome outcome = TeardownGraph();
            if (outcome.result.IsFailure())
            {
                (void)m_stateMachine.Fail();
            }
            else
            {
                (void)m_stateMachine.Stop();
            }

            return CapturePipelineStopResult{
                outcome.result,
                m_stateMachine.State(),
                outcome.failureStage,
                false
            };
        }

    private:
        struct TeardownOutcome
        {
            OperationResult result = OperationResult::Success();
            TeardownStage failureStage{ TeardownStage::None };
        };

        OperationResult FailStartAndTeardown(OperationResult failure)
        {
            TeardownGraph();
            (void)m_stateMachine.Fail();
            return failure;
        }

        void RegisterGraphCallbacks()
        {
            for (const std::unique_ptr<IMediaProcessor>& processor : m_processors)
            {
                if (processor != nullptr)
                {
                    m_callbackTokens.push_back(processor->RegisterOutputHandler(
                        [this](const MediaSample& sample)
                        {
                            WriteToSink(sample);
                        }));
                }
            }

            for (const std::unique_ptr<IMediaSource>& source : m_sources)
            {
                if (auto* videoSource = dynamic_cast<IVideoCaptureSource*>(source.get()))
                {
                    m_callbackTokens.push_back(videoSource->RegisterFrameArrivedHandler(
                        [this](const VideoSample& sample)
                        {
                            RouteSample(MediaSample{ sample });
                        }));
                }

                if (auto* audioSource = dynamic_cast<IAudioCaptureSource*>(source.get()))
                {
                    m_callbackTokens.push_back(audioSource->RegisterSampleArrivedHandler(
                        [this](const AudioSample& sample)
                        {
                            RouteSample(MediaSample{ sample });
                        }));
                }
            }
        }

        void RouteSample(MediaSample sample)
        {
            if (m_stateMachine.State() != PipelineState::Recording || m_clock == nullptr)
            {
                return;
            }

            sample = WithTimestamp(std::move(sample), m_clock->CurrentTime());

            if (IMediaProcessor* processor = FindProcessor(sample.Kind()))
            {
                if (OperationResult result = processor->Process(sample); result.IsFailure())
                {
                    (void)m_stateMachine.Fail();
                }

                return;
            }

            WriteToSink(sample);
        }

        void WriteToSink(const MediaSample& sample)
        {
            if (m_stateMachine.State() != PipelineState::Recording || m_sink == nullptr)
            {
                return;
            }

            if (OperationResult result = m_sink->WriteSample(sample); result.IsFailure())
            {
                (void)m_stateMachine.Fail();
            }
        }

        IMediaProcessor* FindProcessor(MediaKind kind) noexcept
        {
            for (const std::unique_ptr<IMediaProcessor>& processor : m_processors)
            {
                if (processor != nullptr && processor->Kind() == kind)
                {
                    return processor.get();
                }
            }

            return nullptr;
        }

        IAudioMuteProcessor* FindAudioMuteProcessor(SourceId sourceId) noexcept
        {
            for (const std::unique_ptr<IMediaProcessor>& processor : m_processors)
            {
                auto* muteProcessor = dynamic_cast<IAudioMuteProcessor*>(processor.get());
                if (muteProcessor != nullptr && muteProcessor->ControlledSource() == sourceId)
                {
                    return muteProcessor;
                }
            }

            return nullptr;
        }

        IAudioGainProcessor* FindAudioGainProcessor(SourceId sourceId) noexcept
        {
            for (const std::unique_ptr<IMediaProcessor>& processor : m_processors)
            {
                auto* gainProcessor = dynamic_cast<IAudioGainProcessor*>(processor.get());
                if (gainProcessor != nullptr && gainProcessor->ControlledSource() == sourceId)
                {
                    return gainProcessor;
                }
            }

            return nullptr;
        }

        static MediaSample WithTimestamp(MediaSample sample, MediaTime timestamp)
        {
            if (auto* video = std::get_if<VideoSample>(&sample.data))
            {
                video->timestamp = timestamp;
            }
            else
            {
                std::get<AudioSample>(sample.data).timestamp = timestamp;
            }

            return sample;
        }

        TeardownOutcome TeardownGraph()
        {
            TeardownOutcome outcome;
            m_teardownStages.clear();

            RecordStage(TeardownStage::StopAcceptingCallbacks);
            m_callbackTokens.clear();

            RecordStage(TeardownStage::StopSources);
            for (const std::unique_ptr<IMediaSource>& source : m_sources)
            {
                if (source != nullptr)
                {
                    CaptureFirstFailure(TeardownStage::StopSources, source->Stop(), outcome);
                }
            }

            RecordStage(TeardownStage::FlushProcessors);
            RecordStage(TeardownStage::FlushSink);

            RecordStage(TeardownStage::FinalizeSink);
            if (m_sink != nullptr)
            {
                CaptureFirstFailure(TeardownStage::FinalizeSink, m_sink->Finalize(), outcome);
            }

            RecordStage(TeardownStage::ReleaseSink);
            m_sink.reset();

            RecordStage(TeardownStage::ReleaseProcessors);
            m_processors.clear();

            RecordStage(TeardownStage::ReleaseSources);
            m_sources.clear();

            RecordStage(TeardownStage::ReleaseInfrastructure);
            m_clock.reset();

            return outcome;
        }

        static void CaptureFirstFailure(
            TeardownStage stage,
            OperationResult result,
            TeardownOutcome& outcome)
        {
            if (outcome.result.IsSuccess() && result.IsFailure())
            {
                outcome.result = std::move(result);
                outcome.failureStage = stage;
            }
        }

        void RecordStage(TeardownStage stage)
        {
            m_teardownStages.push_back(stage);
        }

        CapturePipelineConfig m_config;
        IMediaSourceFactory& m_sourceFactory;
        IMediaProcessorFactory& m_processorFactory;
        IOutputSinkFactory& m_sinkFactory;
        OutputProfileResolver m_outputProfileResolver;
        PipelineStateMachine m_stateMachine;
        std::optional<OutputPlan> m_outputPlan;
        std::vector<std::unique_ptr<IMediaSource>> m_sources;
        std::vector<std::unique_ptr<IMediaProcessor>> m_processors;
        std::unique_ptr<IOutputSink> m_sink;
        std::unique_ptr<IRecordingClock> m_clock;
        std::vector<CallbackRegistrationToken> m_callbackTokens;
        std::vector<TeardownStage> m_teardownStages;
    };
}
