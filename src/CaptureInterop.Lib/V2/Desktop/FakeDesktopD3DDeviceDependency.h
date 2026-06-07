#pragma once

#include "DesktopD3DDeviceDependency.h"

#include <memory>
#include <optional>
#include <string>
#include <utility>
#include <vector>

namespace CaptureInterop::V2::Desktop
{
    class FakeDesktopD3DDeviceDependency final : public IDesktopD3DDeviceDependency
    {
    public:
        explicit FakeDesktopD3DDeviceDependency(
            std::shared_ptr<std::vector<std::string>> lifecycleEvents = nullptr,
            std::string name = "FakeDesktopD3DDeviceDependency")
            : m_lifecycleEvents(std::move(lifecycleEvents)),
              m_name(std::move(name))
        {
        }

        ~FakeDesktopD3DDeviceDependency() override
        {
            if (m_lifecycleEvents)
            {
                m_lifecycleEvents->push_back("dependency-destroyed");
            }
        }

        [[nodiscard]] std::string Name() const override
        {
            return m_name;
        }

        [[nodiscard]] ID3D11Device* Device() const noexcept override
        {
            return nullptr;
        }

        [[nodiscard]] ID3D11DeviceContext* ImmediateContext() const noexcept override
        {
            return nullptr;
        }

        [[nodiscard]] OperationResult CheckDeviceHealth() const noexcept override
        {
            if (m_healthFailure.has_value())
            {
                return *m_healthFailure;
            }

            return OperationResult::Success();
        }

        void SetHealthFailure(OperationResult failure)
        {
            m_healthFailure = std::move(failure);
        }

    private:
        std::shared_ptr<std::vector<std::string>> m_lifecycleEvents;
        std::string m_name;
        std::optional<OperationResult> m_healthFailure;
    };
}
