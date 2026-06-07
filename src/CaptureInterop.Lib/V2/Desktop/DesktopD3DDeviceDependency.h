#pragma once

#include "V2/Core/ResultTypes.h"

#include <d3d11.h>
#include <string>
#include <utility>
#include <wil/com.h>

namespace CaptureInterop::V2::Desktop
{
    class IDesktopD3DDeviceDependency
    {
    public:
        virtual ~IDesktopD3DDeviceDependency() = default;

        [[nodiscard]] virtual std::string Name() const = 0;
        [[nodiscard]] virtual ID3D11Device* Device() const noexcept = 0;
        [[nodiscard]] virtual ID3D11DeviceContext* ImmediateContext() const noexcept = 0;
        [[nodiscard]] virtual OperationResult CheckDeviceHealth() const noexcept = 0;
    };

    class DesktopD3DDeviceDependency final : public IDesktopD3DDeviceDependency
    {
    public:
        DesktopD3DDeviceDependency(
            wil::com_ptr<ID3D11Device> device,
            wil::com_ptr<ID3D11DeviceContext> immediateContext,
            std::string name = "DesktopD3DDeviceDependency")
            : m_device(std::move(device)),
              m_immediateContext(std::move(immediateContext)),
              m_name(std::move(name))
        {
        }

        [[nodiscard]] std::string Name() const override
        {
            return m_name;
        }

        [[nodiscard]] ID3D11Device* Device() const noexcept override
        {
            return m_device.get();
        }

        [[nodiscard]] ID3D11DeviceContext* ImmediateContext() const noexcept override
        {
            return m_immediateContext.get();
        }

        [[nodiscard]] OperationResult CheckDeviceHealth() const noexcept override
        {
            if (!m_device || !m_immediateContext)
            {
                return OperationResult::Failure(
                    CoreResultCode::InvalidState,
                    m_name,
                    "CheckDeviceHealth",
                    "D3D device and immediate context are required");
            }

            const HRESULT hr = m_device->GetDeviceRemovedReason();
            if (FAILED(hr))
            {
                return OperationResult::Failure(
                    CoreResultCode::NativeFailure,
                    m_name,
                    "CheckDeviceHealth",
                    "D3D device was removed or reset",
                    static_cast<int64_t>(hr));
            }

            return OperationResult::Success();
        }

    private:
        wil::com_ptr<ID3D11Device> m_device;
        wil::com_ptr<ID3D11DeviceContext> m_immediateContext;
        std::string m_name;
    };
}
