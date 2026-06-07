#include "pch.h"
#include "V2/Audio/WasapiAudioClientFactory.h"

#include <functiondiscoverykeys_devpkey.h>

namespace CaptureInterop::V2::Audio
{
    namespace
    {
        class ComApartment final
        {
        public:
            ComApartment()
                : m_hr(CoInitializeEx(nullptr, COINIT_MULTITHREADED))
            {
                m_shouldUninitialize = m_hr == S_OK || m_hr == S_FALSE;
            }

            ComApartment(const ComApartment&) = delete;
            ComApartment& operator=(const ComApartment&) = delete;

            ComApartment(ComApartment&& other) noexcept
                : m_hr(other.m_hr),
                  m_shouldUninitialize(other.m_shouldUninitialize)
            {
                other.m_shouldUninitialize = false;
            }

            ComApartment& operator=(ComApartment&& other) noexcept
            {
                if (this != &other)
                {
                    if (m_shouldUninitialize)
                    {
                        CoUninitialize();
                    }

                    m_hr = other.m_hr;
                    m_shouldUninitialize = other.m_shouldUninitialize;
                    other.m_shouldUninitialize = false;
                }

                return *this;
            }

            ~ComApartment()
            {
                if (m_shouldUninitialize)
                {
                    CoUninitialize();
                }
            }

            [[nodiscard]] HRESULT Result() const noexcept
            {
                return m_hr;
            }

            [[nodiscard]] bool IsUsable() const noexcept
            {
                return SUCCEEDED(m_hr) || m_hr == RPC_E_CHANGED_MODE;
            }

        private:
            HRESULT m_hr{ S_OK };
            bool m_shouldUninitialize{ false };
        };

        class WindowsWasapiActivatedEndpoint final : public IWasapiActivatedEndpoint
        {
        public:
            WindowsWasapiActivatedEndpoint(
                ComApartment apartment,
                WasapiEndpointInfo info,
                wil::com_ptr<IMMDevice> device,
                wil::com_ptr<IAudioClient> audioClient,
                wil::unique_cotaskmem_ptr<WAVEFORMATEX> mixFormat)
                : m_apartment(std::move(apartment)),
                  m_info(std::move(info)),
                  m_device(std::move(device)),
                  m_audioClient(std::move(audioClient)),
                  m_mixFormat(std::move(mixFormat))
            {
            }

            [[nodiscard]] const WasapiEndpointInfo& Info() const noexcept override
            {
                return m_info;
            }

            [[nodiscard]] IAudioClient* AudioClient() const noexcept override
            {
                return m_audioClient.get();
            }

            [[nodiscard]] WAVEFORMATEX* MixFormat() const noexcept override
            {
                return m_mixFormat.get();
            }

        private:
            ComApartment m_apartment;
            WasapiEndpointInfo m_info;
            wil::com_ptr<IMMDevice> m_device;
            wil::com_ptr<IAudioClient> m_audioClient;
            wil::unique_cotaskmem_ptr<WAVEFORMATEX> m_mixFormat;
        };

        [[nodiscard]] ERole ToWindowsRole(WasapiEndpointRole role) noexcept
        {
            switch (role)
            {
            case WasapiEndpointRole::Console:
                return eConsole;
            case WasapiEndpointRole::Multimedia:
                return eMultimedia;
            case WasapiEndpointRole::Communications:
                return eCommunications;
            default:
                return eConsole;
            }
        }

        [[nodiscard]] bool IsSupportedRole(WasapiEndpointRole role) noexcept
        {
            return role == WasapiEndpointRole::Console
                || role == WasapiEndpointRole::Multimedia
                || role == WasapiEndpointRole::Communications;
        }

        [[nodiscard]] std::wstring ReadEndpointId(IMMDevice& device)
        {
            wil::unique_cotaskmem_string endpointId;
            if (SUCCEEDED(device.GetId(&endpointId)))
            {
                return endpointId.get();
            }

            return {};
        }

        [[nodiscard]] std::wstring ReadFriendlyName(IMMDevice& device)
        {
            wil::com_ptr<IPropertyStore> propertyStore;
            HRESULT hr = device.OpenPropertyStore(STGM_READ, propertyStore.put());
            if (FAILED(hr))
            {
                return {};
            }

            PROPVARIANT value;
            PropVariantInit(&value);
            hr = propertyStore->GetValue(PKEY_Device_FriendlyName, &value);
            std::wstring friendlyName;
            if (SUCCEEDED(hr) && value.vt == VT_LPWSTR && value.pwszVal != nullptr)
            {
                friendlyName = value.pwszVal;
            }

            PropVariantClear(&value);
            return friendlyName;
        }

        [[nodiscard]] WasapiEndpointActivationResult FailureResult(
            CoreResultCode code,
            const char* message,
            HRESULT hr)
        {
            return WasapiEndpointActivationResult{
                nullptr,
                OperationResult::Failure(
                    code,
                    "WindowsWasapiAudioClientFactory",
                    "ActivateDefaultRenderEndpoint",
                    message,
                    hr)
            };
        }
    }

    WasapiEndpointActivationResult WindowsWasapiAudioClientFactory::ActivateDefaultRenderEndpoint(
        WasapiEndpointRole role)
    {
        if (!IsSupportedRole(role))
        {
            return FailureResult(
                CoreResultCode::ValidationFailure,
                "Unsupported endpoint role",
                E_INVALIDARG);
        }

        ComApartment apartment;
        if (!apartment.IsUsable())
        {
            return FailureResult(
                CoreResultCode::NativeFailure,
                "COM could not be initialized for WASAPI endpoint activation",
                apartment.Result());
        }

        wil::com_ptr<IMMDeviceEnumerator> enumerator;
        HRESULT hr = CoCreateInstance(
            __uuidof(MMDeviceEnumerator),
            nullptr,
            CLSCTX_ALL,
            __uuidof(IMMDeviceEnumerator),
            enumerator.put_void());
        if (FAILED(hr))
        {
            return FailureResult(
                CoreResultCode::NativeFailure,
                "WASAPI device enumerator could not be created",
                hr);
        }

        wil::com_ptr<IMMDevice> endpoint;
        hr = enumerator->GetDefaultAudioEndpoint(eRender, ToWindowsRole(role), endpoint.put());
        if (FAILED(hr))
        {
            return FailureResult(
                hr == E_NOTFOUND ? CoreResultCode::NotFound : CoreResultCode::NativeFailure,
                hr == E_NOTFOUND
                    ? "Default render endpoint was not found"
                    : "Default render endpoint lookup failed",
                hr);
        }

        wil::com_ptr<IAudioClient> audioClient;
        hr = endpoint->Activate(
            __uuidof(IAudioClient),
            CLSCTX_ALL,
            nullptr,
            audioClient.put_void());
        if (FAILED(hr))
        {
            return FailureResult(
                CoreResultCode::NativeFailure,
                "Default render endpoint audio client could not be activated",
                hr);
        }

        WAVEFORMATEX* rawMixFormat = nullptr;
        hr = audioClient->GetMixFormat(&rawMixFormat);
        if (FAILED(hr))
        {
            return FailureResult(
                CoreResultCode::NativeFailure,
                "Default render endpoint mix format could not be read",
                hr);
        }

        wil::unique_cotaskmem_ptr<WAVEFORMATEX> mixFormat;
        mixFormat.reset(rawMixFormat);
        WasapiAudioFormatMappingResult formatResult =
            MapWasapiMixFormatToAudioMediaType(*mixFormat);
        if (formatResult.IsFailure())
        {
            return WasapiEndpointActivationResult{ nullptr, std::move(formatResult.result) };
        }

        WasapiEndpointInfo info;
        info.endpointId = ReadEndpointId(*endpoint);
        info.friendlyName = ReadFriendlyName(*endpoint);
        info.role = role;
        info.mixFormat = formatResult.mediaType;
        info.formatDiagnostics = formatResult.diagnostics;

        return WasapiEndpointActivationResult{
            std::make_shared<WindowsWasapiActivatedEndpoint>(
                std::move(apartment),
                std::move(info),
                std::move(endpoint),
                std::move(audioClient),
                std::move(mixFormat)),
            OperationResult::Success()
        };
    }
}
