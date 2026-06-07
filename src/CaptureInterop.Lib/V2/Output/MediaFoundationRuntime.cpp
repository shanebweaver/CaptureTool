#include "pch.h"
#include "V2/Output/MediaFoundationRuntime.h"

#include <mfapi.h>

#include <utility>

namespace CaptureInterop::V2::Output
{
    long WindowsMediaFoundationRuntimeApi::Startup() noexcept
    {
        return MFStartup(MF_VERSION);
    }

    void WindowsMediaFoundationRuntimeApi::Shutdown() noexcept
    {
        [[maybe_unused]] HRESULT shutdownHr = MFShutdown();
    }

    MediaFoundationRuntimeLease::MediaFoundationRuntimeLease(MediaFoundationRuntimeLease&& other) noexcept
        : m_runtime(std::exchange(other.m_runtime, nullptr))
    {
    }

    MediaFoundationRuntimeLease& MediaFoundationRuntimeLease::operator=(
        MediaFoundationRuntimeLease&& other) noexcept
    {
        if (this != &other)
        {
            Release();
            m_runtime = std::exchange(other.m_runtime, nullptr);
        }

        return *this;
    }

    MediaFoundationRuntimeLease::~MediaFoundationRuntimeLease()
    {
        Release();
    }

    void MediaFoundationRuntimeLease::Release() noexcept
    {
        if (m_runtime == nullptr)
        {
            return;
        }

        MediaFoundationRuntime* runtime = std::exchange(m_runtime, nullptr);
        runtime->ReleaseLease();
    }

    MediaFoundationRuntime::MediaFoundationRuntime(
        std::shared_ptr<IMediaFoundationRuntimeApi> api)
        : m_api(std::move(api))
    {
    }

    MediaFoundationRuntimeLeaseResult MediaFoundationRuntime::Acquire()
    {
        std::lock_guard lock(m_mutex);
        if (!m_api)
        {
            return MediaFoundationRuntimeLeaseResult{
                OperationResult::Failure(
                    CoreResultCode::InvalidState,
                    "MediaFoundationRuntime",
                    "Acquire",
                    "Media Foundation runtime API is not configured"),
                MediaFoundationRuntimeLease{}
            };
        }

        if (m_activeLeases == 0)
        {
            const long startupResult = m_api->Startup();
            if (FAILED(startupResult))
            {
                return MediaFoundationRuntimeLeaseResult{
                    OperationResult::Failure(
                        CoreResultCode::NativeFailure,
                        "MediaFoundationRuntime",
                        "Acquire",
                        "Media Foundation startup failed",
                        startupResult),
                    MediaFoundationRuntimeLease{}
                };
            }
        }

        ++m_activeLeases;
        return MediaFoundationRuntimeLeaseResult{
            OperationResult::Success(),
            MediaFoundationRuntimeLease(*this)
        };
    }

    uint32_t MediaFoundationRuntime::ActiveLeaseCount() const noexcept
    {
        std::lock_guard lock(m_mutex);
        return m_activeLeases;
    }

    void MediaFoundationRuntime::ReleaseLease() noexcept
    {
        std::lock_guard lock(m_mutex);
        if (m_activeLeases == 0)
        {
            return;
        }

        --m_activeLeases;
        if (m_activeLeases == 0)
        {
            m_api->Shutdown();
        }
    }
}
