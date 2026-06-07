#include "pch.h"
#include "CppUnitTest.h"
#include "V2/Audio/WasapiAudioClientFactory.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using namespace CaptureInterop::V2;
using namespace CaptureInterop::V2::Audio;

namespace
{
    bool IsWasapiEndpointProbeEnabled()
    {
        wchar_t value[8]{};
        const DWORD length = GetEnvironmentVariableW(
            L"CAPTURETOOL_V2_WASAPI_ENDPOINT_PROBE",
            value,
            ARRAYSIZE(value));
        return length > 0 && value[0] == L'1';
    }
}

namespace CaptureInteropTests
{
    TEST_CLASS(V2WasapiAudioClientFactoryTests)
    {
    public:
        TEST_METHOD(FakeProvider_ReportsDefaultEndpointInfo)
        {
            FakeWasapiAudioClientFactory factory;

            const WasapiEndpointActivationResult result =
                factory.ActivateDefaultRenderEndpoint(WasapiEndpointRole::Console);

            Assert::IsTrue(result.IsSuccess());
            Assert::AreEqual(1, factory.ActivationAttempts());
            Assert::AreEqual(L"fake-default-render", result.endpoint->Info().endpointId.c_str());
            Assert::AreEqual(L"Fake default render endpoint", result.endpoint->Info().friendlyName.c_str());
            Assert::AreEqual(
                static_cast<int>(WasapiEndpointRole::Console),
                static_cast<int>(result.endpoint->Info().role));
            Assert::AreEqual(48000u, result.endpoint->Info().mixFormat.sampleRate);
            Assert::AreEqual(
                static_cast<int>(AudioSampleFormat::Float32),
                static_cast<int>(result.endpoint->Info().mixFormat.sampleFormat));
        }

        TEST_METHOD(FakeProvider_NoDefaultEndpointReturnsNotFound)
        {
            FakeWasapiAudioClientFactory factory;
            factory.SimulateNoDefaultEndpoint();

            const WasapiEndpointActivationResult result =
                factory.ActivateDefaultRenderEndpoint(WasapiEndpointRole::Console);

            Assert::IsTrue(result.IsFailure());
            Assert::AreEqual(
                static_cast<uint32_t>(CoreResultCode::NotFound),
                static_cast<uint32_t>(result.result.code));
            Assert::IsNull(result.endpoint.get());
        }

        TEST_METHOD(FakeProvider_UnsupportedEndpointRoleFailsValidation)
        {
            FakeWasapiAudioClientFactory factory;

            const WasapiEndpointActivationResult result =
                factory.ActivateDefaultRenderEndpoint(static_cast<WasapiEndpointRole>(99));

            Assert::IsTrue(result.IsFailure());
            Assert::AreEqual(
                static_cast<uint32_t>(CoreResultCode::ValidationFailure),
                static_cast<uint32_t>(result.result.code));
        }

        TEST_METHOD(FakeProvider_PartialActivationCleanupIsCounted)
        {
            FakeWasapiAudioClientFactory factory;
            factory.SimulatePartialActivationFailure();

            const WasapiEndpointActivationResult result =
                factory.ActivateDefaultRenderEndpoint(WasapiEndpointRole::Console);

            Assert::IsTrue(result.IsFailure());
            Assert::AreEqual(1, factory.PartialActivationCleanupCount());
            Assert::AreEqual(0, factory.EndpointReleaseCount());
        }

        TEST_METHOD(FakeEndpoint_ReleaseIsObservedWhenActivationHandleIsDropped)
        {
            FakeWasapiAudioClientFactory factory;
            {
                const WasapiEndpointActivationResult result =
                    factory.ActivateDefaultRenderEndpoint(WasapiEndpointRole::Multimedia);
                Assert::IsTrue(result.IsSuccess());
                Assert::AreEqual(0, factory.EndpointReleaseCount());
            }

            Assert::AreEqual(1, factory.EndpointReleaseCount());
        }

        TEST_METHOD(LocalProbe_DefaultRenderEndpointDiscovery)
        {
            if (!IsWasapiEndpointProbeEnabled())
            {
                Logger::WriteMessage("Skipping local WASAPI endpoint probe. Set CAPTURETOOL_V2_WASAPI_ENDPOINT_PROBE=1 to enable.");
                return;
            }

            WindowsWasapiAudioClientFactory factory;
            const WasapiEndpointActivationResult result =
                factory.ActivateDefaultRenderEndpoint(WasapiEndpointRole::Console);

            if (result.IsFailure())
            {
                Logger::WriteMessage("Local WASAPI endpoint probe did not find an activatable endpoint.");
                return;
            }

            Logger::WriteMessage(result.endpoint->Info().endpointId.c_str());
            Logger::WriteMessage(result.endpoint->Info().friendlyName.c_str());
            Assert::IsTrue(result.endpoint->Info().mixFormat.IsValid());
            Assert::IsNotNull(result.endpoint->AudioClient());
            Assert::IsNotNull(result.endpoint->MixFormat());
        }
    };
}
