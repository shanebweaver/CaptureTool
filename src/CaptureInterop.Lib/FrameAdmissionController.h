#pragma once

#include "MediaTimeConstants.h"

class FrameAdmissionController
{
public:
    explicit FrameAdmissionController(int targetFrameRate = 30)
    {
        if (targetFrameRate > 0)
        {
            m_targetFrameDurationTicks = MediaTimeConstants::TicksPerSecond() / targetFrameRate;
        }
    }

    bool ShouldAccept(LONGLONG timestamp)
    {
        if (!m_hasAcceptedFrame)
        {
            m_lastAcceptedTimestamp = timestamp;
            m_hasAcceptedFrame = true;
            return true;
        }

        if (timestamp <= m_lastAcceptedTimestamp)
        {
            return false;
        }

        if ((timestamp - m_lastAcceptedTimestamp) < m_targetFrameDurationTicks)
        {
            return false;
        }

        m_lastAcceptedTimestamp = timestamp;
        return true;
    }

    LONGLONG GetLastAcceptedTimestamp() const { return m_lastAcceptedTimestamp; }
    LONGLONG GetTargetFrameDurationTicks() const { return m_targetFrameDurationTicks; }
    bool HasAcceptedFrame() const { return m_hasAcceptedFrame; }

private:
    LONGLONG m_targetFrameDurationTicks = MediaTimeConstants::TicksPerSecond() / 30;
    LONGLONG m_lastAcceptedTimestamp = 0;
    bool m_hasAcceptedFrame = false;
};
