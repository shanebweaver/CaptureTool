#pragma once

#include "MediaPrimitives.h"
#include "ResultTypes.h"

namespace CaptureInterop::V2
{
    class IClockTimeProvider
    {
    public:
        virtual ~IClockTimeProvider() = default;

        [[nodiscard]] virtual MediaTime Now() const noexcept = 0;
    };

    class IRecordingClock
    {
    public:
        virtual ~IRecordingClock() = default;

        [[nodiscard]] virtual OperationResult Start() noexcept = 0;
        [[nodiscard]] virtual OperationResult Pause() noexcept = 0;
        [[nodiscard]] virtual OperationResult Resume() noexcept = 0;
        [[nodiscard]] virtual MediaTime CurrentTime() const noexcept = 0;
        [[nodiscard]] virtual bool IsStarted() const noexcept = 0;
        [[nodiscard]] virtual bool IsPaused() const noexcept = 0;
    };

    class RecordingClock final : public IRecordingClock
    {
    public:
        explicit RecordingClock(const IClockTimeProvider& timeProvider) noexcept
            : m_timeProvider(timeProvider)
        {
        }

        [[nodiscard]] OperationResult Start() noexcept override
        {
            if (m_started)
            {
                return InvalidState("Start", "Recording clock has already started");
            }

            m_started = true;
            m_paused = false;
            m_startWallTime = m_timeProvider.Now();
            m_pauseStartWallTime = MediaTime::Zero();
            m_accumulatedPausedDuration = MediaDuration::Zero();
            m_lastReturnedTime = MediaTime::Zero();

            return OperationResult::Success();
        }

        [[nodiscard]] OperationResult Pause() noexcept override
        {
            if (!m_started)
            {
                return InvalidState("Pause", "Recording clock has not started");
            }

            if (m_paused)
            {
                return InvalidState("Pause", "Recording clock is already paused");
            }

            m_pauseStartWallTime = m_timeProvider.Now();
            m_paused = true;

            return OperationResult::Success();
        }

        [[nodiscard]] OperationResult Resume() noexcept override
        {
            if (!m_started)
            {
                return InvalidState("Resume", "Recording clock has not started");
            }

            if (!m_paused)
            {
                return InvalidState("Resume", "Recording clock is not paused");
            }

            MediaDuration pausedDuration = m_timeProvider.Now() - m_pauseStartWallTime;
            if (pausedDuration.IsNegative())
            {
                pausedDuration = MediaDuration::Zero();
            }

            m_accumulatedPausedDuration = m_accumulatedPausedDuration + pausedDuration;
            m_pauseStartWallTime = MediaTime::Zero();
            m_paused = false;

            return OperationResult::Success();
        }

        [[nodiscard]] MediaTime CurrentTime() const noexcept override
        {
            if (!m_started)
            {
                return MediaTime::Zero();
            }

            const MediaTime effectiveWallTime = m_paused ? m_pauseStartWallTime : m_timeProvider.Now();
            MediaDuration elapsed = effectiveWallTime - m_startWallTime - m_accumulatedPausedDuration;
            if (elapsed.IsNegative())
            {
                elapsed = MediaDuration::Zero();
            }

            MediaTime currentTime = MediaTime::Zero() + elapsed;
            if (currentTime < m_lastReturnedTime)
            {
                currentTime = m_lastReturnedTime;
            }

            m_lastReturnedTime = currentTime;
            return currentTime;
        }

        [[nodiscard]] bool IsStarted() const noexcept override
        {
            return m_started;
        }

        [[nodiscard]] bool IsPaused() const noexcept override
        {
            return m_paused;
        }

    private:
        static OperationResult InvalidState(const char* operation, const char* message)
        {
            return OperationResult::Failure(
                CoreResultCode::InvalidState,
                "RecordingClock",
                operation,
                message);
        }

        const IClockTimeProvider& m_timeProvider;
        bool m_started{ false };
        bool m_paused{ false };
        MediaTime m_startWallTime;
        MediaTime m_pauseStartWallTime;
        MediaDuration m_accumulatedPausedDuration;
        mutable MediaTime m_lastReturnedTime;
    };
}
