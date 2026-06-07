#pragma once

#include "IWasapiLoopbackPacketProvider.h"
#include "WasapiAudioClientFactory.h"

#include <wil/com.h>
#include <wil/resource.h>

#include <memory>
#include <mutex>

namespace CaptureInterop::V2::Audio
{
    class WindowsWasapiLoopbackPacketProvider final : public IWasapiLoopbackPacketProvider
    {
    public:
        explicit WindowsWasapiLoopbackPacketProvider(
            std::shared_ptr<IWasapiAudioClientFactory> factory =
                std::make_shared<WindowsWasapiAudioClientFactory>());

        ~WindowsWasapiLoopbackPacketProvider() override;

        [[nodiscard]] OperationResult Initialize(
            const WasapiLoopbackAudioSourceConfig& config) override;
        [[nodiscard]] OperationResult Start() override;
        [[nodiscard]] OperationResult Stop() noexcept override;
        [[nodiscard]] std::optional<AudioSample> TryReadPacket() override;
        [[nodiscard]] WasapiLoopbackPacketProviderDiagnostics Diagnostics() const override;

    private:
        [[nodiscard]] OperationResult ActivateAndInitialize(bool useEvent);
        [[nodiscard]] OperationResult InitializeAudioClient(bool useEvent);
        [[nodiscard]] OperationResult EnsureCaptureClient();
        void ReleaseResourcesNoLock() noexcept;
        void AddReleaseEventNoLock(std::string eventName);

        WasapiLoopbackAudioSourceConfig m_config;
        std::shared_ptr<IWasapiAudioClientFactory> m_factory;
        std::shared_ptr<IWasapiActivatedEndpoint> m_endpoint;
        wil::com_ptr<IAudioCaptureClient> m_captureClient;
        wil::unique_handle m_captureEvent;
        mutable std::mutex m_mutex;
        WasapiLoopbackPacketProviderDiagnostics m_diagnostics;
        bool m_initialized{ false };
        bool m_started{ false };
    };
}
