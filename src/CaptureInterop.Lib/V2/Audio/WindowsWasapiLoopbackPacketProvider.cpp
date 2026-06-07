#include "pch.h"
#include "V2/Audio/WindowsWasapiLoopbackPacketProvider.h"
#include "V2/Audio/OwnedAudioSampleBuilder.h"

namespace CaptureInterop::V2::Audio
{
    namespace
    {
        constexpr REFERENCE_TIME DefaultBufferDuration100Ns = 1'000'000;

        [[nodiscard]] OperationResult NativeFailure(
            const char* operation,
            const char* message,
            HRESULT hr)
        {
            return OperationResult::Failure(
                CoreResultCode::NativeFailure,
                "WindowsWasapiLoopbackPacketProvider",
                operation,
                message,
                hr);
        }
    }

    WindowsWasapiLoopbackPacketProvider::WindowsWasapiLoopbackPacketProvider(
        std::shared_ptr<IWasapiAudioClientFactory> factory)
        : m_factory(std::move(factory))
    {
    }

    WindowsWasapiLoopbackPacketProvider::~WindowsWasapiLoopbackPacketProvider()
    {
        [[maybe_unused]] OperationResult stopResult = Stop();
    }

    OperationResult WindowsWasapiLoopbackPacketProvider::Initialize(
        const WasapiLoopbackAudioSourceConfig& config)
    {
        std::lock_guard lock(m_mutex);
        if (m_initialized)
        {
            return OperationResult::Failure(
                CoreResultCode::InvalidState,
                "WindowsWasapiLoopbackPacketProvider",
                "Initialize",
                "WASAPI loopback packet provider is already initialized");
        }

        m_config = config;
        OperationResult eventDrivenResult = ActivateAndInitialize(true);
        if (eventDrivenResult.IsSuccess())
        {
            m_initialized = true;
            m_diagnostics.eventDrivenCapture = true;
            return OperationResult::Success();
        }

        ReleaseResourcesNoLock();
        OperationResult pollingResult = ActivateAndInitialize(false);
        if (pollingResult.IsFailure())
        {
            ReleaseResourcesNoLock();
            return pollingResult;
        }

        m_initialized = true;
        m_diagnostics.eventDrivenCapture = false;
        m_diagnostics.pollingFallbackUsed = true;
        return OperationResult::Success();
    }

    OperationResult WindowsWasapiLoopbackPacketProvider::Start()
    {
        std::lock_guard lock(m_mutex);
        if (!m_initialized || !m_endpoint || !m_endpoint->AudioClient())
        {
            return OperationResult::Failure(
                CoreResultCode::InvalidState,
                "WindowsWasapiLoopbackPacketProvider",
                "Start",
                "WASAPI loopback packet provider is not initialized");
        }

        HRESULT hr = m_endpoint->AudioClient()->Start();
        if (FAILED(hr))
        {
            return NativeFailure(
                "Start",
                "WASAPI loopback audio client could not be started",
                hr);
        }

        m_started = true;
        return OperationResult::Success();
    }

    OperationResult WindowsWasapiLoopbackPacketProvider::Stop() noexcept
    {
        std::lock_guard lock(m_mutex);
        if (m_endpoint && m_endpoint->AudioClient() && m_started)
        {
            [[maybe_unused]] HRESULT stopHr = m_endpoint->AudioClient()->Stop();
        }

        m_started = false;
        m_initialized = false;
        ReleaseResourcesNoLock();
        return OperationResult::Success();
    }

    std::optional<AudioSample> WindowsWasapiLoopbackPacketProvider::TryReadPacket()
    {
        std::lock_guard lock(m_mutex);
        if (!m_started || !m_captureClient)
        {
            return std::nullopt;
        }

        UINT32 packetFrames = 0;
        HRESULT hr = m_captureClient->GetNextPacketSize(&packetFrames);
        if (FAILED(hr))
        {
            return std::nullopt;
        }

        if (packetFrames == 0 && m_captureEvent)
        {
            WaitForSingleObject(m_captureEvent.get(), 1);
            hr = m_captureClient->GetNextPacketSize(&packetFrames);
            if (FAILED(hr) || packetFrames == 0)
            {
                return std::nullopt;
            }
        }

        if (packetFrames == 0)
        {
            return std::nullopt;
        }

        BYTE* data = nullptr;
        UINT32 framesAvailable = 0;
        DWORD flags = 0;
        UINT64 devicePosition = 0;
        UINT64 qpcPosition = 0;
        hr = m_captureClient->GetBuffer(
            &data,
            &framesAvailable,
            &flags,
            &devicePosition,
            &qpcPosition);
        if (FAILED(hr))
        {
            return std::nullopt;
        }

        const bool silentPacket = (flags & AUDCLNT_BUFFERFLAGS_SILENT) != 0 || data == nullptr;
        AudioSample sample = BuildOwnedAudioSampleFromWasapiPacket(WasapiAudioPacketView{
            m_config.sourceId,
            m_config.audioStreamId,
            MediaTime{},
            m_config.mediaType,
            data,
            framesAvailable,
            silentPacket
        });

        if (silentPacket)
        {
            ++m_diagnostics.silentPackets;
        }

        if ((flags & AUDCLNT_BUFFERFLAGS_DATA_DISCONTINUITY) != 0)
        {
            ++m_diagnostics.discontinuities;
        }

        ++m_diagnostics.packetsRead;
        m_diagnostics.framesRead += framesAvailable;

        [[maybe_unused]] HRESULT releaseHr = m_captureClient->ReleaseBuffer(framesAvailable);
        return sample;
    }

    WasapiLoopbackPacketProviderDiagnostics WindowsWasapiLoopbackPacketProvider::Diagnostics() const
    {
        std::lock_guard lock(m_mutex);
        return m_diagnostics;
    }

    OperationResult WindowsWasapiLoopbackPacketProvider::ActivateAndInitialize(bool useEvent)
    {
        WasapiEndpointActivationResult endpointResult =
            m_factory->ActivateDefaultRenderEndpoint(WasapiEndpointRole::Console);
        if (endpointResult.IsFailure())
        {
            return endpointResult.result;
        }

        m_endpoint = std::move(endpointResult.endpoint);
        m_config.mediaType = m_endpoint->Info().mixFormat;
        m_diagnostics.endpointId = m_endpoint->Info().endpointId;
        m_diagnostics.endpointName = m_endpoint->Info().friendlyName;

        OperationResult initializeResult = InitializeAudioClient(useEvent);
        if (initializeResult.IsFailure())
        {
            return initializeResult;
        }

        return EnsureCaptureClient();
    }

    OperationResult WindowsWasapiLoopbackPacketProvider::InitializeAudioClient(bool useEvent)
    {
        if (!m_endpoint || !m_endpoint->AudioClient() || !m_endpoint->MixFormat())
        {
            return OperationResult::Failure(
                CoreResultCode::InvalidState,
                "WindowsWasapiLoopbackPacketProvider",
                "InitializeAudioClient",
                "Activated WASAPI endpoint is incomplete");
        }

        DWORD streamFlags = AUDCLNT_STREAMFLAGS_LOOPBACK;
        if (useEvent)
        {
            m_captureEvent.reset(CreateEventW(nullptr, FALSE, FALSE, nullptr));
            if (!m_captureEvent)
            {
                return NativeFailure(
                    "InitializeAudioClient",
                    "WASAPI loopback capture event could not be created",
                    HRESULT_FROM_WIN32(GetLastError()));
            }

            streamFlags |= AUDCLNT_STREAMFLAGS_EVENTCALLBACK;
        }

        HRESULT hr = m_endpoint->AudioClient()->Initialize(
            AUDCLNT_SHAREMODE_SHARED,
            streamFlags,
            DefaultBufferDuration100Ns,
            0,
            m_endpoint->MixFormat(),
            nullptr);
        if (FAILED(hr))
        {
            return NativeFailure(
                "InitializeAudioClient",
                "WASAPI loopback audio client could not be initialized",
                hr);
        }

        if (useEvent)
        {
            hr = m_endpoint->AudioClient()->SetEventHandle(m_captureEvent.get());
            if (FAILED(hr))
            {
                return NativeFailure(
                    "InitializeAudioClient",
                    "WASAPI loopback event handle could not be set",
                    hr);
            }
        }

        return OperationResult::Success();
    }

    OperationResult WindowsWasapiLoopbackPacketProvider::EnsureCaptureClient()
    {
        HRESULT hr = m_endpoint->AudioClient()->GetService(
            __uuidof(IAudioCaptureClient),
            m_captureClient.put_void());
        if (FAILED(hr))
        {
            return NativeFailure(
                "EnsureCaptureClient",
                "WASAPI loopback capture client could not be acquired",
                hr);
        }

        return OperationResult::Success();
    }

    void WindowsWasapiLoopbackPacketProvider::ReleaseResourcesNoLock() noexcept
    {
        if (m_captureClient)
        {
            m_captureClient.reset();
            AddReleaseEventNoLock("capture-client-released");
        }

        if (m_endpoint)
        {
            m_endpoint.reset();
            AddReleaseEventNoLock("audio-client-endpoint-released");
        }

        if (m_captureEvent)
        {
            m_captureEvent.reset();
            AddReleaseEventNoLock("capture-event-released");
        }
    }

    void WindowsWasapiLoopbackPacketProvider::AddReleaseEventNoLock(std::string eventName)
    {
        m_diagnostics.releaseEvents.push_back(std::move(eventName));
    }
}
