#pragma once

#include "IWasapiLoopbackAudioProvider.h"

#include <utility>

namespace CaptureInterop::V2::Audio
{
    class FakeWasapiLoopbackAudioProvider final : public IWasapiLoopbackAudioProvider
    {
    public:
        explicit FakeWasapiLoopbackAudioProvider(
            AudioMediaType mediaType,
            std::string providerName = "FakeWasapiLoopbackAudioProvider")
            : m_mediaType(mediaType),
              m_providerName(std::move(providerName))
        {
        }

        [[nodiscard]] std::string ProviderName() const override
        {
            return m_providerName;
        }

        [[nodiscard]] AudioMediaType CurrentMediaType() const override
        {
            return m_mediaType;
        }

    private:
        AudioMediaType m_mediaType;
        std::string m_providerName;
    };
}
