#pragma once

#include "DesktopVideoSourceConfig.h"

#include <optional>
#include <string>
#include <utility>
#include <vector>

namespace CaptureInterop::V2::Desktop
{
    struct DesktopMonitorBounds
    {
        int32_t x{ 0 };
        int32_t y{ 0 };
        uint32_t width{ 0 };
        uint32_t height{ 0 };

        [[nodiscard]] bool IsValid() const noexcept
        {
            return width != 0 && height != 0;
        }
    };

    struct DesktopMonitorInfo
    {
        DesktopMonitorTarget target;
        DesktopMonitorBounds bounds;
        std::string displayName;
    };

    class IDesktopMonitorResolver
    {
    public:
        virtual ~IDesktopMonitorResolver() = default;

        [[nodiscard]] virtual std::optional<DesktopMonitorInfo> Resolve(
            const DesktopMonitorTarget& target) const = 0;
    };

    class FakeDesktopMonitorResolver final : public IDesktopMonitorResolver
    {
    public:
        void AddMonitor(DesktopMonitorInfo monitor)
        {
            m_monitors.push_back(std::move(monitor));
        }

        [[nodiscard]] std::optional<DesktopMonitorInfo> Resolve(
            const DesktopMonitorTarget& target) const override
        {
            for (const DesktopMonitorInfo& monitor : m_monitors)
            {
                if (Matches(monitor.target, target))
                {
                    return monitor;
                }
            }

            return std::nullopt;
        }

    private:
        static bool Matches(const DesktopMonitorTarget& candidate, const DesktopMonitorTarget& requested)
        {
            if (requested.monitorHandle != 0 && candidate.monitorHandle == requested.monitorHandle)
            {
                return true;
            }

            if (!requested.displayId.empty() && candidate.displayId == requested.displayId)
            {
                return true;
            }

            if (!requested.deviceName.empty() && candidate.deviceName == requested.deviceName)
            {
                return true;
            }

            return false;
        }

        std::vector<DesktopMonitorInfo> m_monitors;
    };
}
