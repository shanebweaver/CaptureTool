#pragma once

#include "ResultTypes.h"

namespace CaptureInterop::V2
{
    enum class PipelineState
    {
        Created = 0,
        Prepared,
        Recording,
        Paused,
        Stopping,
        Finalized,
        Failing,
        Failed
    };

    enum class PipelineOperation
    {
        Start = 0,
        Pause,
        Resume,
        Stop,
        SetAudioMuted,
        SetAudioGain,
        Fail
    };

    class PipelineStateMachine
    {
    public:
        [[nodiscard]] PipelineState State() const noexcept
        {
            return m_state;
        }

        [[nodiscard]] bool IsActive() const noexcept
        {
            return m_state == PipelineState::Recording || m_state == PipelineState::Paused;
        }

        [[nodiscard]] bool IsTerminal() const noexcept
        {
            return m_state == PipelineState::Finalized || m_state == PipelineState::Failed;
        }

        [[nodiscard]] bool CanApply(PipelineOperation operation) const noexcept
        {
            switch (operation)
            {
            case PipelineOperation::Start:
                return m_state == PipelineState::Created;
            case PipelineOperation::Pause:
                return m_state == PipelineState::Recording;
            case PipelineOperation::Resume:
                return m_state == PipelineState::Paused;
            case PipelineOperation::Stop:
                return m_state == PipelineState::Recording || m_state == PipelineState::Paused;
            case PipelineOperation::SetAudioMuted:
            case PipelineOperation::SetAudioGain:
                return m_state == PipelineState::Recording || m_state == PipelineState::Paused;
            case PipelineOperation::Fail:
                return !IsTerminal() && m_state != PipelineState::Failed;
            default:
                return false;
            }
        }

        [[nodiscard]] OperationResult Start()
        {
            if (!CanApply(PipelineOperation::Start))
            {
                return InvalidTransition(PipelineOperation::Start);
            }

            m_state = PipelineState::Prepared;
            m_state = PipelineState::Recording;
            return OperationResult::Success();
        }

        [[nodiscard]] OperationResult Pause()
        {
            if (!CanApply(PipelineOperation::Pause))
            {
                return InvalidTransition(PipelineOperation::Pause);
            }

            m_state = PipelineState::Paused;
            return OperationResult::Success();
        }

        [[nodiscard]] OperationResult Resume()
        {
            if (!CanApply(PipelineOperation::Resume))
            {
                return InvalidTransition(PipelineOperation::Resume);
            }

            m_state = PipelineState::Recording;
            return OperationResult::Success();
        }

        [[nodiscard]] OperationResult Stop()
        {
            if (!CanApply(PipelineOperation::Stop))
            {
                return InvalidTransition(PipelineOperation::Stop);
            }

            m_state = PipelineState::Stopping;
            m_state = PipelineState::Finalized;
            return OperationResult::Success();
        }

        [[nodiscard]] OperationResult SetAudioMuted()
        {
            if (!CanApply(PipelineOperation::SetAudioMuted))
            {
                return InvalidTransition(PipelineOperation::SetAudioMuted);
            }

            return OperationResult::Success();
        }

        [[nodiscard]] OperationResult SetAudioGain()
        {
            if (!CanApply(PipelineOperation::SetAudioGain))
            {
                return InvalidTransition(PipelineOperation::SetAudioGain);
            }

            return OperationResult::Success();
        }

        [[nodiscard]] OperationResult Fail()
        {
            if (!CanApply(PipelineOperation::Fail))
            {
                return InvalidTransition(PipelineOperation::Fail);
            }

            m_state = PipelineState::Failing;
            m_state = PipelineState::Failed;
            return OperationResult::Success();
        }

        [[nodiscard]] static const char* OperationName(PipelineOperation operation) noexcept
        {
            switch (operation)
            {
            case PipelineOperation::Start:
                return "Start";
            case PipelineOperation::Pause:
                return "Pause";
            case PipelineOperation::Resume:
                return "Resume";
            case PipelineOperation::Stop:
                return "Stop";
            case PipelineOperation::SetAudioMuted:
                return "SetAudioMuted";
            case PipelineOperation::SetAudioGain:
                return "SetAudioGain";
            case PipelineOperation::Fail:
                return "Fail";
            default:
                return "Unknown";
            }
        }

    private:
        [[nodiscard]] OperationResult InvalidTransition(PipelineOperation operation) const
        {
            return OperationResult::Failure(
                CoreResultCode::InvalidState,
                "PipelineStateMachine",
                OperationName(operation),
                "Operation is not valid for the current pipeline state");
        }

        PipelineState m_state{ PipelineState::Created };
    };
}
