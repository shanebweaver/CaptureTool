#pragma once

#include "WasapiAudioFormatMapper.h"

#include <audioclient.h>
#include <mmdeviceapi.h>
#include <wil/com.h>
#include <wil/resource.h>

#include <memory>
#include <string>
#include <utility>

namespace CaptureInterop::V2::Audio
{
    enum class WasapiEndpointRole
    {
        Console = 0,
        Multimedia,
        Communications
    };

    struct WasapiEndpointInfo
    {
        std::wstring endpointId;
        std::wstring friendlyName;
        WasapiEndpointRole role{ WasapiEndpointRole::Console };
        AudioMediaType mixFormat;
        WasapiAudioFormatDiagnostics formatDiagnostics;
    };

    class IWasapiActivatedEndpoint
    {
    public:
        virtual ~IWasapiActivatedEndpoint() = default;

        [[nodiscard]] virtual const WasapiEndpointInfo& Info() const noexcept = 0;
        [[nodiscard]] virtual IAudioClient* AudioClient() const noexcept = 0;
        [[nodiscard]] virtual WAVEFORMATEX* MixFormat() const noexcept = 0;
    };

    struct WasapiEndpointActivationResult
    {
        std::shared_ptr<IWasapiActivatedEndpoint> endpoint;
        OperationResult result;

        [[nodiscard]] bool IsSuccess() const noexcept
        {
            return result.IsSuccess() && endpoint != nullptr;
        }

        [[nodiscard]] bool IsFailure() const noexcept
        {
            return !IsSuccess();
        }
    };

    class IWasapiAudioClientFactory
    {
    public:
        virtual ~IWasapiAudioClientFactory() = default;

        [[nodiscard]] virtual WasapiEndpointActivationResult ActivateDefaultRenderEndpoint(
            WasapiEndpointRole role) = 0;
    };

    class FakeWasapiActivatedEndpoint final : public IWasapiActivatedEndpoint
    {
    public:
        explicit FakeWasapiActivatedEndpoint(
            WasapiEndpointInfo info,
            std::shared_ptr<int> releaseCounter = nullptr)
            : m_info(std::move(info)),
              m_releaseCounter(std::move(releaseCounter))
        {
        }

        ~FakeWasapiActivatedEndpoint() override
        {
            if (m_releaseCounter)
            {
                ++(*m_releaseCounter);
            }
        }

        [[nodiscard]] const WasapiEndpointInfo& Info() const noexcept override
        {
            return m_info;
        }

        [[nodiscard]] IAudioClient* AudioClient() const noexcept override
        {
            return nullptr;
        }

        [[nodiscard]] WAVEFORMATEX* MixFormat() const noexcept override
        {
            return nullptr;
        }

    private:
        WasapiEndpointInfo m_info;
        std::shared_ptr<int> m_releaseCounter;
    };

    class FakeWasapiAudioClientFactory final : public IWasapiAudioClientFactory
    {
    public:
        FakeWasapiAudioClientFactory()
        {
            m_endpointInfo.endpointId = L"fake-default-render";
            m_endpointInfo.friendlyName = L"Fake default render endpoint";
            m_endpointInfo.role = WasapiEndpointRole::Console;
            m_endpointInfo.mixFormat = AudioMediaType{
                48000,
                2,
                32,
                8,
                AudioSampleFormat::Float32
            };
        }

        void SetEndpointInfo(WasapiEndpointInfo info)
        {
            m_endpointInfo = std::move(info);
        }

        void SimulateNoDefaultEndpoint()
        {
            m_failure = OperationResult::Failure(
                CoreResultCode::NotFound,
                "FakeWasapiAudioClientFactory",
                "ActivateDefaultRenderEndpoint",
                "Default render endpoint was not found");
        }

        void SimulatePartialActivationFailure()
        {
            m_simulatePartialActivationFailure = true;
        }

        [[nodiscard]] int ActivationAttempts() const noexcept
        {
            return m_activationAttempts;
        }

        [[nodiscard]] int PartialActivationCleanupCount() const noexcept
        {
            return m_partialActivationCleanupCount;
        }

        [[nodiscard]] int EndpointReleaseCount() const noexcept
        {
            return m_endpointReleaseCounter ? *m_endpointReleaseCounter : 0;
        }

        [[nodiscard]] WasapiEndpointActivationResult ActivateDefaultRenderEndpoint(
            WasapiEndpointRole role) override
        {
            ++m_activationAttempts;

            if (!IsSupportedRole(role))
            {
                return WasapiEndpointActivationResult{
                    nullptr,
                    OperationResult::Failure(
                        CoreResultCode::ValidationFailure,
                        "FakeWasapiAudioClientFactory",
                        "ActivateDefaultRenderEndpoint",
                        "Unsupported endpoint role")
                };
            }

            if (m_failure.IsFailure())
            {
                return WasapiEndpointActivationResult{ nullptr, m_failure };
            }

            if (m_simulatePartialActivationFailure)
            {
                ++m_partialActivationCleanupCount;
                return WasapiEndpointActivationResult{
                    nullptr,
                    OperationResult::Failure(
                        CoreResultCode::NativeFailure,
                        "FakeWasapiAudioClientFactory",
                        "ActivateDefaultRenderEndpoint",
                        "Simulated partial activation failure")
                };
            }

            WasapiEndpointInfo info = m_endpointInfo;
            info.role = role;
            return WasapiEndpointActivationResult{
                std::make_shared<FakeWasapiActivatedEndpoint>(std::move(info), m_endpointReleaseCounter),
                OperationResult::Success()
            };
        }

        [[nodiscard]] static bool IsSupportedRole(WasapiEndpointRole role) noexcept
        {
            return role == WasapiEndpointRole::Console
                || role == WasapiEndpointRole::Multimedia
                || role == WasapiEndpointRole::Communications;
        }

    private:
        WasapiEndpointInfo m_endpointInfo;
        OperationResult m_failure;
        std::shared_ptr<int> m_endpointReleaseCounter{ std::make_shared<int>(0) };
        int m_activationAttempts{ 0 };
        int m_partialActivationCleanupCount{ 0 };
        bool m_simulatePartialActivationFailure{ false };
    };

    class WindowsWasapiAudioClientFactory final : public IWasapiAudioClientFactory
    {
    public:
        [[nodiscard]] WasapiEndpointActivationResult ActivateDefaultRenderEndpoint(
            WasapiEndpointRole role) override;
    };
}
