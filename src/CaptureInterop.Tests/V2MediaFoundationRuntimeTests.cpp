#include "pch.h"
#include "CppUnitTest.h"
#include "V2/Output/MediaFoundationRuntime.h"

#include <memory>
#include <stdexcept>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using namespace CaptureInterop::V2;
using namespace CaptureInterop::V2::Output;

namespace
{
    class FakeMediaFoundationRuntimeApi final : public IMediaFoundationRuntimeApi
    {
    public:
        explicit FakeMediaFoundationRuntimeApi(long startupResult = S_OK) noexcept
            : m_startupResult(startupResult)
        {
        }

        [[nodiscard]] long Startup() noexcept override
        {
            ++startupCalls;
            return m_startupResult;
        }

        void Shutdown() noexcept override
        {
            ++shutdownCalls;
        }

        int startupCalls{ 0 };
        int shutdownCalls{ 0 };

    private:
        long m_startupResult{ S_OK };
    };
}

namespace CaptureInteropTests
{
    TEST_CLASS(V2MediaFoundationRuntimeTests)
    {
    public:
        TEST_METHOD(SingleLease_AcquiresAndReleasesRuntime)
        {
            auto api = std::make_shared<FakeMediaFoundationRuntimeApi>();
            MediaFoundationRuntime runtime(api);

            {
                MediaFoundationRuntimeLeaseResult lease = runtime.Acquire();

                Assert::IsTrue(lease.IsSuccess());
                Assert::AreEqual(1, api->startupCalls);
                Assert::AreEqual(0, api->shutdownCalls);
                Assert::AreEqual(1u, runtime.ActiveLeaseCount());
            }

            Assert::AreEqual(0u, runtime.ActiveLeaseCount());
            Assert::AreEqual(1, api->shutdownCalls);
        }

        TEST_METHOD(NestedLeases_StartOnceAndShutdownAfterFinalRelease)
        {
            auto api = std::make_shared<FakeMediaFoundationRuntimeApi>();
            MediaFoundationRuntime runtime(api);

            MediaFoundationRuntimeLeaseResult outer = runtime.Acquire();
            {
                MediaFoundationRuntimeLeaseResult inner = runtime.Acquire();

                Assert::IsTrue(outer.IsSuccess());
                Assert::IsTrue(inner.IsSuccess());
                Assert::AreEqual(1, api->startupCalls);
                Assert::AreEqual(2u, runtime.ActiveLeaseCount());
            }

            Assert::AreEqual(1u, runtime.ActiveLeaseCount());
            Assert::AreEqual(0, api->shutdownCalls);
            outer.lease.Release();

            Assert::AreEqual(0u, runtime.ActiveLeaseCount());
            Assert::AreEqual(1, api->shutdownCalls);
        }

        TEST_METHOD(StartupFailure_ReturnsStructuredNativeFailure)
        {
            auto api = std::make_shared<FakeMediaFoundationRuntimeApi>(E_FAIL);
            MediaFoundationRuntime runtime(api);

            MediaFoundationRuntimeLeaseResult lease = runtime.Acquire();

            Assert::IsFalse(lease.IsSuccess());
            Assert::IsTrue(lease.result.IsFailure());
            Assert::AreEqual(
                static_cast<uint32_t>(CoreResultCode::NativeFailure),
                static_cast<uint32_t>(lease.result.code));
            Assert::AreEqual("MediaFoundationRuntime", lease.result.diagnostic->component.c_str());
            Assert::AreEqual("Acquire", lease.result.diagnostic->operation.c_str());
            Assert::AreEqual(static_cast<int64_t>(E_FAIL), lease.result.diagnostic->nativeStatus.value());
            Assert::AreEqual(0u, runtime.ActiveLeaseCount());
            Assert::AreEqual(0, api->shutdownCalls);
        }

        TEST_METHOD(MissingRuntimeApi_ReturnsStructuredFailure)
        {
            MediaFoundationRuntime runtime(nullptr);

            MediaFoundationRuntimeLeaseResult lease = runtime.Acquire();

            Assert::IsFalse(lease.IsSuccess());
            Assert::IsTrue(lease.result.IsFailure());
            Assert::AreEqual(
                static_cast<uint32_t>(CoreResultCode::InvalidState),
                static_cast<uint32_t>(lease.result.code));
            Assert::AreEqual("MediaFoundationRuntime", lease.result.diagnostic->component.c_str());
            Assert::AreEqual("Acquire", lease.result.diagnostic->operation.c_str());
            Assert::AreEqual(0u, runtime.ActiveLeaseCount());
        }

        TEST_METHOD(MovedLease_ReleasesExactlyOnce)
        {
            auto api = std::make_shared<FakeMediaFoundationRuntimeApi>();
            MediaFoundationRuntime runtime(api);

            MediaFoundationRuntimeLeaseResult lease = runtime.Acquire();
            MediaFoundationRuntimeLease moved = std::move(lease.lease);

            Assert::IsFalse(lease.lease.IsValid());
            Assert::IsTrue(moved.IsValid());
            moved.Release();

            Assert::AreEqual(0u, runtime.ActiveLeaseCount());
            Assert::AreEqual(1, api->shutdownCalls);
        }

        TEST_METHOD(LeaseRelease_IsSafeDuringExceptionUnwinding)
        {
            auto api = std::make_shared<FakeMediaFoundationRuntimeApi>();
            MediaFoundationRuntime runtime(api);

            try
            {
                MediaFoundationRuntimeLeaseResult lease = runtime.Acquire();
                Assert::IsTrue(lease.IsSuccess());
                throw std::runtime_error("simulated failure");
            }
            catch (const std::runtime_error&)
            {
            }

            Assert::AreEqual(0u, runtime.ActiveLeaseCount());
            Assert::AreEqual(1, api->shutdownCalls);
        }
    };
}
