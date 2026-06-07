#pragma once

#include "PipelineInterfaces.h"
#include "V2/Audio/WasapiLoopbackAudioSource.h"
#include "V2/Audio/WindowsWasapiLoopbackPacketProvider.h"
#include "V2/Desktop/DesktopD3DDeviceDependency.h"
#include "V2/Desktop/WindowsDesktopVideoSource.h"
#include "V2/Desktop/WindowsGraphicsCaptureProvider.h"
#include "V2/Output/MediaFoundationFileSink.h"

#include <d3d11.h>
#include <functional>
#include <memory>
#include <utility>
#include <vector>

namespace CaptureInterop::V2
{
    class ProductionMediaSourceFactory final : public IMediaSourceFactory
    {
    public:
        using DesktopProviderFactory = std::function<std::shared_ptr<Desktop::IDesktopCaptureProvider>(
            const Desktop::DesktopVideoSourceConfig&)>;
        using DesktopD3DDeviceFactory = std::function<std::shared_ptr<Desktop::IDesktopD3DDeviceDependency>()>;
        using WasapiLoopbackPacketProviderFactory =
            std::function<std::shared_ptr<Audio::IWasapiLoopbackPacketProvider>()>;

        ProductionMediaSourceFactory() = default;

        ProductionMediaSourceFactory(
            DesktopProviderFactory desktopProviderFactory,
            DesktopD3DDeviceFactory desktopD3DDeviceFactory,
            WasapiLoopbackPacketProviderFactory wasapiLoopbackPacketProviderFactory,
            std::shared_ptr<Desktop::IDesktopMonitorResolver> desktopMonitorResolver = nullptr)
            : m_desktopProviderFactory(std::move(desktopProviderFactory)),
              m_desktopD3DDeviceFactory(std::move(desktopD3DDeviceFactory)),
              m_wasapiLoopbackPacketProviderFactory(std::move(wasapiLoopbackPacketProviderFactory)),
              m_desktopMonitorResolver(std::move(desktopMonitorResolver))
        {
        }

        [[nodiscard]] std::vector<std::unique_ptr<IMediaSource>> CreateSources(
            const CapturePipelineConfig& config) override
        {
            std::vector<std::unique_ptr<IMediaSource>> sources;
            for (const SourceConfig& source : config.sources)
            {
                if (const DesktopSourceConfig* desktop = source.AsDesktop())
                {
                    sources.push_back(CreateDesktopSource(*desktop));
                    continue;
                }

                if (const SystemAudioSourceConfig* systemAudio = source.AsSystemAudio())
                {
                    if (systemAudio->armed)
                    {
                        sources.push_back(CreateSystemAudioSource(*systemAudio, BuildSystemAudioMediaType(config)));
                    }
                }
            }

            return sources;
        }

        [[nodiscard]] static AudioMediaType BuildSystemAudioMediaType(const CapturePipelineConfig& config) noexcept
        {
            const uint16_t bitsPerSample = ResolveBitsPerSample(config.audioMixer.normalizedSampleFormat);
            return AudioMediaType{
                config.audioMixer.normalizedSampleRate,
                config.audioMixer.normalizedChannels,
                bitsPerSample,
                static_cast<uint16_t>((config.audioMixer.normalizedChannels * bitsPerSample) / 8),
                config.audioMixer.normalizedSampleFormat
            };
        }

        [[nodiscard]] static std::shared_ptr<Desktop::IDesktopD3DDeviceDependency> CreateDefaultDesktopD3DDevice()
        {
            wil::com_ptr<ID3D11Device> device;
            wil::com_ptr<ID3D11DeviceContext> immediateContext;

            const D3D_FEATURE_LEVEL requestedFeatureLevels[] = {
                D3D_FEATURE_LEVEL_11_1,
                D3D_FEATURE_LEVEL_11_0,
                D3D_FEATURE_LEVEL_10_1,
                D3D_FEATURE_LEVEL_10_0
            };

            D3D_FEATURE_LEVEL selectedFeatureLevel{};
            const HRESULT hr = D3D11CreateDevice(
                nullptr,
                D3D_DRIVER_TYPE_HARDWARE,
                nullptr,
                D3D11_CREATE_DEVICE_BGRA_SUPPORT,
                requestedFeatureLevels,
                static_cast<UINT>(sizeof(requestedFeatureLevels) / sizeof(requestedFeatureLevels[0])),
                D3D11_SDK_VERSION,
                device.put(),
                &selectedFeatureLevel,
                immediateContext.put());

            if (FAILED(hr))
            {
                return nullptr;
            }

            return std::make_shared<Desktop::DesktopD3DDeviceDependency>(
                std::move(device),
                std::move(immediateContext));
        }

    private:
        [[nodiscard]] static uint16_t ResolveBitsPerSample(AudioSampleFormat sampleFormat) noexcept
        {
            switch (sampleFormat)
            {
            case AudioSampleFormat::Pcm16:
                return 16;
            case AudioSampleFormat::Pcm24:
                return 24;
            case AudioSampleFormat::Pcm32:
            case AudioSampleFormat::Float32:
                return 32;
            default:
                return 0;
            }
        }

        [[nodiscard]] std::unique_ptr<IMediaSource> CreateDesktopSource(
            const DesktopSourceConfig& desktop) const
        {
            Desktop::DesktopVideoSourceConfig sourceConfig =
                Desktop::MapDesktopVideoSourceConfig(desktop);
            return std::make_unique<Desktop::WindowsDesktopVideoSource>(
                sourceConfig,
                m_desktopProviderFactory(sourceConfig),
                m_desktopMonitorResolver,
                m_desktopD3DDeviceFactory());
        }

        [[nodiscard]] std::unique_ptr<IMediaSource> CreateSystemAudioSource(
            const SystemAudioSourceConfig& systemAudio,
            const AudioMediaType& mediaType) const
        {
            return std::make_unique<Audio::WasapiLoopbackAudioSource>(
                Audio::MapWasapiLoopbackAudioSourceConfig(systemAudio, mediaType),
                nullptr,
                m_wasapiLoopbackPacketProviderFactory());
        }

        DesktopProviderFactory m_desktopProviderFactory =
            [](const Desktop::DesktopVideoSourceConfig& sourceConfig)
            {
                return std::make_shared<Desktop::WindowsGraphicsCaptureProvider>(sourceConfig);
            };
        DesktopD3DDeviceFactory m_desktopD3DDeviceFactory = [] { return CreateDefaultDesktopD3DDevice(); };
        WasapiLoopbackPacketProviderFactory m_wasapiLoopbackPacketProviderFactory =
            [] { return std::make_shared<Audio::WindowsWasapiLoopbackPacketProvider>(); };
        std::shared_ptr<Desktop::IDesktopMonitorResolver> m_desktopMonitorResolver;
    };

    class ProductionMediaProcessorFactory final : public IMediaProcessorFactory
    {
    public:
        [[nodiscard]] std::vector<std::unique_ptr<IMediaProcessor>> CreateProcessors(
            const OutputPlan&) override
        {
            return {};
        }
    };

    class ProductionOutputSinkFactory final : public IOutputSinkFactory
    {
    public:
        explicit ProductionOutputSinkFactory(
            std::shared_ptr<Output::MediaFoundationRuntime> runtime = std::make_shared<Output::MediaFoundationRuntime>(),
            std::shared_ptr<Output::IMediaFoundationSinkWriterFactory> sinkWriterFactory =
                std::make_shared<Output::WindowsMediaFoundationSinkWriterFactory>())
            : m_runtime(std::move(runtime)),
              m_sinkWriterFactory(std::move(sinkWriterFactory))
        {
        }

        [[nodiscard]] std::unique_ptr<IOutputSink> CreateSink(const OutputPlan&) override
        {
            return std::make_unique<Output::MediaFoundationFileSink>(
                MediaFoundationSinkProfileValidator{},
                m_runtime,
                m_sinkWriterFactory);
        }

    private:
        std::shared_ptr<Output::MediaFoundationRuntime> m_runtime;
        std::shared_ptr<Output::IMediaFoundationSinkWriterFactory> m_sinkWriterFactory;
    };
}
