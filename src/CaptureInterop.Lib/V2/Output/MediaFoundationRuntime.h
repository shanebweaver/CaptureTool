#pragma once

#include "V2/Core/ResultTypes.h"

#include <cstdint>
#include <memory>
#include <mutex>

namespace CaptureInterop::V2::Output
{
    class IMediaFoundationRuntimeApi
    {
    public:
        virtual ~IMediaFoundationRuntimeApi() = default;

        [[nodiscard]] virtual long Startup() noexcept = 0;
        virtual void Shutdown() noexcept = 0;
    };

    class WindowsMediaFoundationRuntimeApi final : public IMediaFoundationRuntimeApi
    {
    public:
        [[nodiscard]] long Startup() noexcept override;
        void Shutdown() noexcept override;
    };

    class MediaFoundationRuntime;

    class MediaFoundationRuntimeLease final
    {
    public:
        MediaFoundationRuntimeLease() noexcept = default;
        MediaFoundationRuntimeLease(const MediaFoundationRuntimeLease&) = delete;
        MediaFoundationRuntimeLease& operator=(const MediaFoundationRuntimeLease&) = delete;

        MediaFoundationRuntimeLease(MediaFoundationRuntimeLease&& other) noexcept;
        MediaFoundationRuntimeLease& operator=(MediaFoundationRuntimeLease&& other) noexcept;

        ~MediaFoundationRuntimeLease();

        [[nodiscard]] bool IsValid() const noexcept
        {
            return m_runtime != nullptr;
        }

        void Release() noexcept;

    private:
        friend class MediaFoundationRuntime;

        explicit MediaFoundationRuntimeLease(MediaFoundationRuntime& runtime) noexcept
            : m_runtime(&runtime)
        {
        }

        MediaFoundationRuntime* m_runtime{ nullptr };
    };

    struct MediaFoundationRuntimeLeaseResult
    {
        OperationResult result;
        MediaFoundationRuntimeLease lease;

        [[nodiscard]] bool IsSuccess() const noexcept
        {
            return result.IsSuccess() && lease.IsValid();
        }
    };

    class MediaFoundationRuntime final
    {
    public:
        explicit MediaFoundationRuntime(
            std::shared_ptr<IMediaFoundationRuntimeApi> api =
                std::make_shared<WindowsMediaFoundationRuntimeApi>());

        MediaFoundationRuntime(const MediaFoundationRuntime&) = delete;
        MediaFoundationRuntime& operator=(const MediaFoundationRuntime&) = delete;

        [[nodiscard]] MediaFoundationRuntimeLeaseResult Acquire();

        [[nodiscard]] uint32_t ActiveLeaseCount() const noexcept;

    private:
        friend class MediaFoundationRuntimeLease;

        void ReleaseLease() noexcept;

        mutable std::mutex m_mutex;
        std::shared_ptr<IMediaFoundationRuntimeApi> m_api;
        uint32_t m_activeLeases{ 0 };
    };
}
