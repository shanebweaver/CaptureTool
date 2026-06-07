#define CTCAPTUREV2_BUILD
#include "CaptureInteropV2Exports.h"
#include "CaptureInteropV2Validation.h"

#include <Windows.h>
#include <ks.h>
#include <mfapi.h>
#include <mfobjects.h>
#include <mfidl.h>
#include <mfreadwrite.h>
#include <mmdeviceapi.h>
#include <audioclient.h>
#include <mmreg.h>

#include "V2/Core/CapturePipelineSession.h"
#include "V2/Core/ProductionCapturePipelineFactories.h"

#include <algorithm>
#include <chrono>
#include <memory>
#include <mutex>
#include <optional>
#include <string>
#include <unordered_set>
#include <vector>

namespace
{
    enum class RecorderState : int32_t
    {
        Idle = 0,
        Recording = 1,
        Paused = 2
    };

    struct CopiedSourceConfig
    {
        uint32_t sourceId{ 0 };
        int32_t sourceKind{ CtCaptureV2_SourceKind_Unknown };
        uint8_t enabled{ 0 };
    };

    struct CopiedAudioGainConfig
    {
        uint32_t sourceId{ 0 };
        float gainDb{ 0.0F };
    };

    struct CopiedRecorderConfig
    {
        std::vector<CopiedSourceConfig> sources;
        std::u16string outputPath;
        int32_t containerFormat{ CtCaptureV2_ContainerFormat_Unknown };
        int32_t videoCodec{ CtCaptureV2_VideoCodec_None };
        int32_t audioCodec{ CtCaptureV2_AudioCodec_None };
        uint8_t startMuted{ 0 };
        std::vector<CopiedAudioGainConfig> audioGains;
    };

    struct LastErrorState
    {
        int32_t resultCode{ CtCaptureV2_ResultCode_Success };
        int32_t errorCode{ 0 };
        int32_t nativeStatus{ 0 };
        int32_t stage{ 0 };
        std::string component;
        std::string operation;
        std::u16string message;
    };

    struct CopiedCallback
    {
        CtCaptureV2_EventCallback callback{ nullptr };
        void* userData{ nullptr };
    };

    class IRecorderSession
    {
    public:
        virtual ~IRecorderSession() = default;

        [[nodiscard]] virtual int32_t Start() noexcept = 0;
        [[nodiscard]] virtual int32_t Pause() noexcept = 0;
        [[nodiscard]] virtual int32_t Resume() noexcept = 0;
        [[nodiscard]] virtual int32_t SetAudioMuted(uint32_t sourceId, bool muted) noexcept = 0;
        [[nodiscard]] virtual int32_t SetAudioGain(uint32_t sourceId, float gainDb) noexcept = 0;
        [[nodiscard]] virtual CtCaptureV2_StopResult Stop() noexcept = 0;
        [[nodiscard]] virtual const CaptureInterop::V2::CoreDiagnostic* LastDiagnostic() const noexcept = 0;
    };

    class SystemClockTimeProvider final : public CaptureInterop::V2::IClockTimeProvider
    {
    public:
        [[nodiscard]] CaptureInterop::V2::MediaTime Now() const noexcept override
        {
            const auto now = std::chrono::steady_clock::now().time_since_epoch();
            const auto ticks = std::chrono::duration_cast<std::chrono::duration<int64_t, std::ratio<1, 10'000'000>>>(now);
            return CaptureInterop::V2::MediaTime::FromTicks(ticks.count());
        }
    };
}

struct CtCaptureV2_CallbackRegistration_t
{
    CtCaptureV2_RecorderHandle recorder{ nullptr };
    CtCaptureV2_EventCallback callback{ nullptr };
    void* userData{ nullptr };
    uint64_t eventMask{ 0 };
};

struct CtCaptureV2_Recorder_t
{
    RecorderState state{ RecorderState::Idle };
    std::optional<CopiedRecorderConfig> activeConfig;
    std::vector<CopiedAudioGainConfig> runtimeGains;
    std::vector<uint32_t> mutedAudioSources;
    std::vector<CtCaptureV2_CallbackRegistrationHandle> callbackRegistrations;
    std::unique_ptr<IRecorderSession> activeSession;
    LastErrorState lastError;
};

namespace
{
    constexpr uint32_t ApiVersionDtoVersion = 1;
    constexpr uint32_t ApiVersionMajor = 2;
    constexpr uint32_t ApiVersionMinor = 0;
    constexpr uint32_t ApiVersionPatch = 0;

    std::mutex RecorderRegistryMutex;
    std::unordered_set<CtCaptureV2_RecorderHandle> RecorderRegistry;
    std::unordered_set<CtCaptureV2_CallbackRegistrationHandle> CallbackRegistrationRegistry;
    constexpr const char* RecorderComponent = "CaptureInteropV2Recorder";

    CtCaptureV2_Recorder_t* FindRecorder(CtCaptureV2_RecorderHandle handle) noexcept
    {
        if (handle == nullptr)
        {
            return nullptr;
        }

        std::lock_guard lock(RecorderRegistryMutex);
        return RecorderRegistry.find(handle) == RecorderRegistry.end() ? nullptr : handle;
    }

    bool IsEventTypeEnabled(uint64_t eventMask, int32_t eventType) noexcept
    {
        if (eventType <= CtCaptureV2_EventType_Unknown || eventType >= 64)
        {
            return false;
        }

        return (eventMask & (1ULL << eventType)) != 0;
    }

    void RemoveRegistrationFromRecorder(
        CtCaptureV2_Recorder_t& recorder,
        CtCaptureV2_CallbackRegistrationHandle registration)
    {
        recorder.callbackRegistrations.erase(
            std::remove(
                recorder.callbackRegistrations.begin(),
                recorder.callbackRegistrations.end(),
                registration),
            recorder.callbackRegistrations.end());
    }

    void DeleteRegistrations(std::vector<CtCaptureV2_CallbackRegistrationHandle>& registrations) noexcept
    {
        for (CtCaptureV2_CallbackRegistrationHandle registration : registrations)
        {
            delete registration;
        }

        registrations.clear();
    }

    void UnregisterAllCallbacksLocked(
        CtCaptureV2_Recorder_t& recorder,
        std::vector<CtCaptureV2_CallbackRegistrationHandle>& registrationsToDelete)
    {
        registrationsToDelete.insert(
            registrationsToDelete.end(),
            recorder.callbackRegistrations.begin(),
            recorder.callbackRegistrations.end());

        for (CtCaptureV2_CallbackRegistrationHandle registration : recorder.callbackRegistrations)
        {
            CallbackRegistrationRegistry.erase(registration);
        }

        recorder.callbackRegistrations.clear();
    }

    std::u16string CopyNullTerminatedString(const char16_t* value)
    {
        return value == nullptr ? std::u16string{} : std::u16string{ value };
    }

    CopiedRecorderConfig CopyConfig(const CtCaptureV2_Config& config)
    {
        CopiedRecorderConfig copy;
        copy.outputPath = CopyNullTerminatedString(config.output.outputPath);
        copy.containerFormat = config.output.containerFormat;
        copy.videoCodec = config.output.video.codec;
        copy.audioCodec = config.output.audio.codec;
        copy.startMuted = config.controls.startMuted;
        copy.sources.reserve(config.sourceCount);
        for (uint32_t index = 0; index < config.sourceCount; ++index)
        {
            copy.sources.push_back(CopiedSourceConfig{
                config.sources[index].sourceId,
                config.sources[index].sourceKind,
                config.sources[index].enabled
            });
        }

        copy.audioGains.reserve(config.controls.audioGainCount);
        for (uint32_t index = 0; index < config.controls.audioGainCount; ++index)
        {
            copy.audioGains.push_back(CopiedAudioGainConfig{
                config.controls.audioGains[index].sourceId,
                config.controls.audioGains[index].gainDb
            });
        }

        return copy;
    }

    bool HasArmedAudioSource(const CtCaptureV2_Recorder_t& recorder, uint32_t sourceId) noexcept
    {
        if (!recorder.activeConfig.has_value())
        {
            return false;
        }

        for (const CopiedSourceConfig& source : recorder.activeConfig->sources)
        {
            if (source.sourceId == sourceId
                && source.sourceKind == CtCaptureV2_SourceKind_SystemAudio
                && source.enabled != 0)
            {
                return true;
            }
        }

        return false;
    }

    void SetRuntimeGain(CtCaptureV2_Recorder_t& recorder, uint32_t sourceId, float gainDb)
    {
        for (CopiedAudioGainConfig& gain : recorder.runtimeGains)
        {
            if (gain.sourceId == sourceId)
            {
                gain.gainDb = gainDb;
                return;
            }
        }

        recorder.runtimeGains.push_back(CopiedAudioGainConfig{ sourceId, gainDb });
    }

    void SetRuntimeMuted(CtCaptureV2_Recorder_t& recorder, uint32_t sourceId, bool muted)
    {
        for (auto iterator = recorder.mutedAudioSources.begin(); iterator != recorder.mutedAudioSources.end(); ++iterator)
        {
            if (*iterator == sourceId)
            {
                if (!muted)
                {
                    recorder.mutedAudioSources.erase(iterator);
                }

                return;
            }
        }

        if (muted)
        {
            recorder.mutedAudioSources.push_back(sourceId);
        }
    }

    CtCaptureV2_StopResult MakeStopResult(int32_t resultCode, RecorderState finalState) noexcept
    {
        CtCaptureV2_StopResult result;
        CtCaptureV2_InitStopResult(&result);
        result.resultCode = resultCode;
        result.finalState = static_cast<int32_t>(finalState);
        return result;
    }

    void ClearLastError(CtCaptureV2_Recorder_t& recorder)
    {
        recorder.lastError = LastErrorState{};
    }

    int32_t RecordFailure(
        CtCaptureV2_Recorder_t& recorder,
        int32_t resultCode,
        const char* operation,
        const char16_t* message,
        int32_t nativeStatus = 0,
        int32_t stage = 0)
    {
        recorder.lastError.resultCode = resultCode;
        recorder.lastError.errorCode = resultCode;
        recorder.lastError.nativeStatus = nativeStatus;
        recorder.lastError.stage = stage;
        recorder.lastError.component = RecorderComponent;
        recorder.lastError.operation = operation == nullptr ? "" : operation;
        recorder.lastError.message = message == nullptr ? u"" : message;
        return resultCode;
    }

    int32_t RecordNativeFailure(
        CtCaptureV2_Recorder_t* recorder,
        const char* operation) noexcept
    {
        if (recorder != nullptr)
        {
            return RecordFailure(
                *recorder,
                CtCaptureV2_ResultCode_NativeFailure,
                operation,
                u"An unexpected native failure occurred.");
        }

        return CtCaptureV2_ResultCode_NativeFailure;
    }

    uint32_t RequiredMessageLength(const std::u16string& message) noexcept
    {
        return static_cast<uint32_t>(message.size() + 1);
    }

    void CopyMessage(const std::u16string& message, char16_t* buffer, uint32_t bufferLength) noexcept
    {
        if (buffer == nullptr || bufferLength == 0)
        {
            return;
        }

        uint32_t index = 0;
        for (; index < message.size() && index + 1 < bufferLength; ++index)
        {
            buffer[index] = message[index];
        }

        buffer[index] = u'\0';
    }

    std::u16string ToUtf16(const std::string& value)
    {
        std::u16string converted;
        converted.reserve(value.size());
        for (const char character : value)
        {
            converted.push_back(static_cast<char16_t>(static_cast<unsigned char>(character)));
        }

        return converted;
    }

    std::wstring ToWideString(const char16_t* value)
    {
        std::wstring converted;
        if (value == nullptr)
        {
            return converted;
        }

        while (*value != u'\0')
        {
            converted.push_back(static_cast<wchar_t>(*value));
            ++value;
        }

        return converted;
    }

    int32_t ToApiResultCode(CaptureInterop::V2::CoreResultCode code) noexcept
    {
        using CaptureInterop::V2::CoreResultCode;
        switch (code)
        {
        case CoreResultCode::Success:
            return CtCaptureV2_ResultCode_Success;
        case CoreResultCode::ValidationFailure:
            return CtCaptureV2_ResultCode_ValidationFailed;
        case CoreResultCode::InvalidState:
            return CtCaptureV2_ResultCode_InvalidState;
        case CoreResultCode::UnsupportedOperation:
            return CtCaptureV2_ResultCode_UnsupportedOperation;
        case CoreResultCode::NotFound:
            return CtCaptureV2_ResultCode_NotFound;
        case CoreResultCode::RangeError:
            return CtCaptureV2_ResultCode_ValidationFailed;
        case CoreResultCode::NativeFailure:
        default:
            return CtCaptureV2_ResultCode_NativeFailure;
        }
    }

    int32_t ToApiResultCode(const CaptureInterop::V2::OperationResult& result) noexcept
    {
        return ToApiResultCode(result.code);
    }

    int32_t ToApiFinalState(CaptureInterop::V2::PipelineState state) noexcept
    {
        using CaptureInterop::V2::PipelineState;
        switch (state)
        {
        case PipelineState::Recording:
            return static_cast<int32_t>(RecorderState::Recording);
        case PipelineState::Paused:
            return static_cast<int32_t>(RecorderState::Paused);
        default:
            return static_cast<int32_t>(RecorderState::Idle);
        }
    }

    CaptureInterop::V2::ContainerFormat ToDomainContainer(int32_t value) noexcept
    {
        return value == CtCaptureV2_ContainerFormat_Mp4
            ? CaptureInterop::V2::ContainerFormat::Mp4
            : CaptureInterop::V2::ContainerFormat::Mp4;
    }

    CaptureInterop::V2::VideoCodec ToDomainVideoCodec(int32_t value) noexcept
    {
        return value == CtCaptureV2_VideoCodec_H264
            ? CaptureInterop::V2::VideoCodec::H264
            : CaptureInterop::V2::VideoCodec::None;
    }

    CaptureInterop::V2::AudioCodec ToDomainAudioCodec(int32_t value) noexcept
    {
        return value == CtCaptureV2_AudioCodec_Aac
            ? CaptureInterop::V2::AudioCodec::Aac
            : CaptureInterop::V2::AudioCodec::None;
    }

    CaptureInterop::V2::HdrPolicy ToDomainHdrPolicy(int32_t value) noexcept
    {
        using CaptureInterop::V2::HdrPolicy;
        switch (value)
        {
        case CtCaptureV2_HdrPolicy_Preserve:
            return HdrPolicy::Preserve;
        case CtCaptureV2_HdrPolicy_MapToSdr:
            return HdrPolicy::MapToSdr;
        case CtCaptureV2_HdrPolicy_MatchDisplay:
            return HdrPolicy::MatchDisplay;
        case CtCaptureV2_HdrPolicy_ForceSdr:
            return HdrPolicy::ForceSdr;
        case CtCaptureV2_HdrPolicy_Auto:
        default:
            return HdrPolicy::Auto;
        }
    }

    std::optional<CaptureInterop::V2::VideoMediaType> ResolveMonitorVideoMediaType(
        void* platformHandle,
        CaptureInterop::V2::Rational frameRate) noexcept
    {
        HMONITOR monitor = reinterpret_cast<HMONITOR>(platformHandle);
        if (monitor == nullptr)
        {
            monitor = MonitorFromPoint(POINT{ 0, 0 }, MONITOR_DEFAULTTOPRIMARY);
        }

        if (monitor == nullptr)
        {
            return std::nullopt;
        }

        MONITORINFO monitorInfo{};
        monitorInfo.cbSize = sizeof(monitorInfo);
        if (!GetMonitorInfoW(monitor, &monitorInfo))
        {
            return std::nullopt;
        }

        const LONG width = monitorInfo.rcMonitor.right - monitorInfo.rcMonitor.left;
        const LONG height = monitorInfo.rcMonitor.bottom - monitorInfo.rcMonitor.top;
        if (width <= 0 || height <= 0 || !frameRate.IsValid())
        {
            return std::nullopt;
        }

        return CaptureInterop::V2::VideoMediaType{
            static_cast<uint32_t>(width),
            static_cast<uint32_t>(height),
            frameRate,
            CaptureInterop::V2::VideoPixelFormat::Bgra8,
            CaptureInterop::V2::ColorPrimaries::Srgb,
            CaptureInterop::V2::TransferFunction::Srgb,
            CaptureInterop::V2::ColorRange::Full
        };
    }

    float InitialGainForSource(const CtCaptureV2_Config& config, uint32_t sourceId) noexcept
    {
        for (uint32_t index = 0; index < config.controls.audioGainCount; ++index)
        {
            const CtCaptureV2_AudioGainConfig& gain = config.controls.audioGains[index];
            if (gain.sourceId == sourceId)
            {
                return gain.gainDb;
            }
        }

        return CaptureInterop::V2::AudioGainSettings::DefaultGainDb;
    }

    CaptureInterop::V2::CapturePipelineConfig MapConfigToPipelineConfig(const CtCaptureV2_Config& config)
    {
        CaptureInterop::V2::CapturePipelineConfig mapped;

        mapped.output.container = ToDomainContainer(config.output.containerFormat);
        mapped.output.outputPath = ToWideString(config.output.outputPath);
        if (config.output.video.codec != CtCaptureV2_VideoCodec_None)
        {
            mapped.output.video = CaptureInterop::V2::VideoEncodingSettings{
                ToDomainVideoCodec(config.output.video.codec),
                config.output.video.bitrate,
                CaptureInterop::V2::Rational::From(
                    config.output.video.frameRateNumerator,
                    config.output.video.frameRateDenominator),
                config.output.video.gopLength,
                config.output.video.hardwareAccelerationPreferred != 0
            };
        }

        if (config.output.audio.codec != CtCaptureV2_AudioCodec_None)
        {
            mapped.output.audio = CaptureInterop::V2::AudioEncodingSettings{
                ToDomainAudioCodec(config.output.audio.codec),
                config.output.audio.bitrate,
                config.output.audio.sampleRate,
                config.output.audio.channels
            };

            mapped.audioMixer.normalizedSampleRate = config.output.audio.sampleRate;
            mapped.audioMixer.normalizedChannels = config.output.audio.channels;
            mapped.audioMixer.normalizedSampleFormat = CaptureInterop::V2::AudioSampleFormat::Float32;
        }

        mapped.toneMapping.policy = ToDomainHdrPolicy(config.toneMapping.hdrPolicy);
        mapped.toneMapping.targetNits = config.toneMapping.targetNits;
        mapped.toneMapping.preserveMetadataWhenPossible =
            config.toneMapping.preserveMetadataWhenPossible != 0;
        mapped.controls.pauseResumeEnabled = true;
        mapped.controls.runtimeAudioMuteEnabled = true;
        mapped.controls.runtimeAudioGainEnabled = true;

        for (uint32_t index = 0; index < config.sourceCount; ++index)
        {
            const CtCaptureV2_SourceConfig& source = config.sources[index];
            if (source.enabled == 0)
            {
                continue;
            }

            if (source.sourceKind == CtCaptureV2_SourceKind_Desktop)
            {
                CaptureInterop::V2::DesktopSourceConfig desktop;
                desktop.id = CaptureInterop::V2::SourceId::FromValue(source.sourceId);
                desktop.videoStreamId = CaptureInterop::V2::StreamId::FromValue(source.sourceId);
                desktop.name = "Desktop";
                desktop.monitorHandle = reinterpret_cast<uintptr_t>(source.platformHandle);
                desktop.frameRate = mapped.output.video.has_value()
                    ? mapped.output.video->frameRate
                    : CaptureInterop::V2::Rational::From(60, 1);

                if (source.captureRect.width > 0 && source.captureRect.height > 0)
                {
                    desktop.captureArea = CaptureInterop::V2::CaptureRectangle{
                        source.captureRect.x,
                        source.captureRect.y,
                        static_cast<uint32_t>(source.captureRect.width),
                        static_cast<uint32_t>(source.captureRect.height)
                    };
                }
                else
                {
                    desktop.resolvedVideoMediaType =
                        ResolveMonitorVideoMediaType(source.platformHandle, desktop.frameRate);
                }

                mapped.sources.push_back(CaptureInterop::V2::SourceConfig::Desktop(std::move(desktop)));
                continue;
            }

            if (source.sourceKind == CtCaptureV2_SourceKind_SystemAudio)
            {
                CaptureInterop::V2::SystemAudioSourceConfig audio;
                audio.id = CaptureInterop::V2::SourceId::FromValue(source.sourceId);
                audio.name = "System audio";
                audio.armed = true;
                audio.controls.initiallyMuted = config.controls.startMuted != 0;
                audio.controls.initialGain.gainDb = InitialGainForSource(config, source.sourceId);
                mapped.sources.push_back(CaptureInterop::V2::SourceConfig::SystemAudio(std::move(audio)));
            }
        }

        return mapped;
    }

    class RealRecorderSession final : public IRecorderSession
    {
    public:
        explicit RealRecorderSession(CaptureInterop::V2::CapturePipelineConfig config)
            : m_config(std::move(config)),
              m_clockProvider(std::make_unique<SystemClockTimeProvider>())
        {
        }

        [[nodiscard]] int32_t Start() noexcept override
        {
            try
            {
                m_session = std::make_unique<CaptureInterop::V2::CapturePipelineSession>(
                    std::move(m_config),
                    m_sourceFactory,
                    m_processorFactory,
                    m_sinkFactory,
                    std::make_unique<CaptureInterop::V2::RecordingClock>(*m_clockProvider));

                CaptureInterop::V2::OperationResult result = m_session->Start();
                CaptureLastDiagnostic(result);
                return ToApiResultCode(result);
            }
            catch (...)
            {
                m_lastDiagnostic = CaptureInterop::V2::CoreDiagnostic::Error(
                    CaptureInterop::V2::CoreResultCode::NativeFailure,
                    RecorderComponent,
                    "Start",
                    "An unexpected native failure occurred while starting the V2 session");
                return CtCaptureV2_ResultCode_NativeFailure;
            }
        }

        [[nodiscard]] int32_t Pause() noexcept override
        {
            return ApplyCommand("Pause", [this] { return m_session->Pause(); });
        }

        [[nodiscard]] int32_t Resume() noexcept override
        {
            return ApplyCommand("Resume", [this] { return m_session->Resume(); });
        }

        [[nodiscard]] int32_t SetAudioMuted(uint32_t sourceId, bool muted) noexcept override
        {
            return ApplyCommand(
                "SetAudioMuted",
                [this, sourceId, muted]
                {
                    return m_session->SetAudioMuted(CaptureInterop::V2::SourceId::FromValue(sourceId), muted);
                });
        }

        [[nodiscard]] int32_t SetAudioGain(uint32_t sourceId, float gainDb) noexcept override
        {
            return ApplyCommand(
                "SetAudioGain",
                [this, sourceId, gainDb]
                {
                    return m_session->SetAudioGain(CaptureInterop::V2::SourceId::FromValue(sourceId), gainDb);
                });
        }

        [[nodiscard]] CtCaptureV2_StopResult Stop() noexcept override
        {
            CtCaptureV2_StopResult result;
            CtCaptureV2_InitStopResult(&result);

            try
            {
                if (!m_session)
                {
                    result.resultCode = CtCaptureV2_ResultCode_AlreadyStopped;
                    result.finalState = static_cast<int32_t>(RecorderState::Idle);
                    return result;
                }

                CaptureInterop::V2::CapturePipelineStopResult stopResult = m_session->Stop();
                CaptureLastDiagnostic(stopResult.result);
                result.resultCode = stopResult.alreadyStopped
                    ? CtCaptureV2_ResultCode_AlreadyStopped
                    : ToApiResultCode(stopResult.result);
                result.finalState = ToApiFinalState(stopResult.finalState);
                result.failureStage = static_cast<int32_t>(stopResult.failureStage);
                result.droppedVideoFrames = m_session->Counters().droppedVideoFrames;
                result.audioDiscontinuities = m_session->Counters().audioDiscontinuities;
                result.lateSamples = m_session->Counters().lateSamples;
                result.unsupportedCommands = m_session->Counters().unsupportedCommands;
                result.validationWarnings = m_session->Counters().validationWarnings;
                m_session.reset();
                return result;
            }
            catch (...)
            {
                m_lastDiagnostic = CaptureInterop::V2::CoreDiagnostic::Error(
                    CaptureInterop::V2::CoreResultCode::NativeFailure,
                    RecorderComponent,
                    "Stop",
                    "An unexpected native failure occurred while stopping the V2 session");
                result.resultCode = CtCaptureV2_ResultCode_NativeFailure;
                result.finalState = static_cast<int32_t>(RecorderState::Idle);
                return result;
            }
        }

        [[nodiscard]] const CaptureInterop::V2::CoreDiagnostic* LastDiagnostic() const noexcept override
        {
            return m_lastDiagnostic.has_value() ? &*m_lastDiagnostic : nullptr;
        }

    private:
        template <typename TCommand>
        [[nodiscard]] int32_t ApplyCommand(const char* operation, TCommand command) noexcept
        {
            try
            {
                if (!m_session)
                {
                    m_lastDiagnostic = CaptureInterop::V2::CoreDiagnostic::Error(
                        CaptureInterop::V2::CoreResultCode::InvalidState,
                        RecorderComponent,
                        operation,
                        "V2 session is not active");
                    return CtCaptureV2_ResultCode_InvalidState;
                }

                CaptureInterop::V2::OperationResult result = command();
                CaptureLastDiagnostic(result);
                return ToApiResultCode(result);
            }
            catch (...)
            {
                m_lastDiagnostic = CaptureInterop::V2::CoreDiagnostic::Error(
                    CaptureInterop::V2::CoreResultCode::NativeFailure,
                    RecorderComponent,
                    operation,
                    "An unexpected native failure occurred while applying a V2 session command");
                return CtCaptureV2_ResultCode_NativeFailure;
            }
        }

        void CaptureLastDiagnostic(const CaptureInterop::V2::OperationResult& result) noexcept
        {
            if (result.diagnostic.has_value())
            {
                m_lastDiagnostic = result.diagnostic;
            }
            else if (result.IsSuccess())
            {
                m_lastDiagnostic.reset();
            }
        }

        CaptureInterop::V2::CapturePipelineConfig m_config;
        CaptureInterop::V2::ProductionMediaSourceFactory m_sourceFactory;
        CaptureInterop::V2::ProductionMediaProcessorFactory m_processorFactory;
        CaptureInterop::V2::ProductionOutputSinkFactory m_sinkFactory;
        std::unique_ptr<SystemClockTimeProvider> m_clockProvider;
        std::unique_ptr<CaptureInterop::V2::CapturePipelineSession> m_session;
        std::optional<CaptureInterop::V2::CoreDiagnostic> m_lastDiagnostic;
    };

    class FakeRecorderSession final : public IRecorderSession
    {
    public:
        explicit FakeRecorderSession(CopiedRecorderConfig config)
            : m_config(std::move(config))
        {
        }

        [[nodiscard]] int32_t Start() noexcept override
        {
            return CtCaptureV2_ResultCode_Success;
        }

        [[nodiscard]] int32_t Pause() noexcept override
        {
            m_paused = true;
            return CtCaptureV2_ResultCode_Success;
        }

        [[nodiscard]] int32_t Resume() noexcept override
        {
            m_paused = false;
            return CtCaptureV2_ResultCode_Success;
        }

        [[nodiscard]] int32_t SetAudioMuted(uint32_t sourceId, bool) noexcept override
        {
            return HasArmedAudioSource(sourceId)
                ? CtCaptureV2_ResultCode_Success
                : CtCaptureV2_ResultCode_NotFound;
        }

        [[nodiscard]] int32_t SetAudioGain(uint32_t sourceId, float gainDb) noexcept override
        {
            if (gainDb < CaptureInterop::V2::Api::MinAudioGainDb
                || gainDb > CaptureInterop::V2::Api::MaxAudioGainDb)
            {
                return CtCaptureV2_ResultCode_ValidationFailed;
            }

            return HasArmedAudioSource(sourceId)
                ? CtCaptureV2_ResultCode_Success
                : CtCaptureV2_ResultCode_NotFound;
        }

        [[nodiscard]] CtCaptureV2_StopResult Stop() noexcept override
        {
            CtCaptureV2_StopResult result;
            CtCaptureV2_InitStopResult(&result);
            result.resultCode = CtCaptureV2_ResultCode_Success;
            result.finalState = static_cast<int32_t>(RecorderState::Idle);
            return result;
        }

        [[nodiscard]] const CaptureInterop::V2::CoreDiagnostic* LastDiagnostic() const noexcept override
        {
            return nullptr;
        }

    private:
        [[nodiscard]] bool HasArmedAudioSource(uint32_t sourceId) const noexcept
        {
            for (const CopiedSourceConfig& source : m_config.sources)
            {
                if (source.sourceId == sourceId
                    && source.sourceKind == CtCaptureV2_SourceKind_SystemAudio
                    && source.enabled != 0)
                {
                    return true;
                }
            }

            return false;
        }

        CopiedRecorderConfig m_config;
        bool m_paused{ false };
    };

    bool ShouldUseFakeNativeSessionForTests() noexcept
    {
        wchar_t value[8]{};
        const DWORD length = GetEnvironmentVariableW(
            L"CAPTURETOOL_V2_FAKE_NATIVE_SESSION",
            value,
            static_cast<DWORD>(sizeof(value) / sizeof(value[0])));
        return length == 1 && value[0] == L'1';
    }

    std::unique_ptr<IRecorderSession> CreateRecorderSession(
        const CtCaptureV2_Config& config,
        const CopiedRecorderConfig& copiedConfig)
    {
        if (ShouldUseFakeNativeSessionForTests())
        {
            return std::make_unique<FakeRecorderSession>(copiedConfig);
        }

        return std::make_unique<RealRecorderSession>(MapConfigToPipelineConfig(config));
    }

    void RecordDiagnosticFailure(
        CtCaptureV2_Recorder_t& recorder,
        int32_t resultCode,
        const char* fallbackOperation,
        const char16_t* fallbackMessage,
        const CaptureInterop::V2::CoreDiagnostic* diagnostic)
    {
        if (diagnostic == nullptr)
        {
            (void)RecordFailure(recorder, resultCode, fallbackOperation, fallbackMessage);
            return;
        }

        recorder.lastError.resultCode = resultCode;
        recorder.lastError.errorCode = resultCode;
        recorder.lastError.nativeStatus = diagnostic->nativeStatus.has_value()
            ? static_cast<int32_t>(*diagnostic->nativeStatus)
            : 0;
        recorder.lastError.stage = 0;
        recorder.lastError.component = diagnostic->component;
        recorder.lastError.operation = diagnostic->operation;
        recorder.lastError.message = ToUtf16(diagnostic->message);
    }

    int32_t RecordSessionFailure(
        CtCaptureV2_Recorder_t& recorder,
        IRecorderSession* session,
        int32_t resultCode,
        const char* operation,
        const char16_t* message)
    {
        RecordDiagnosticFailure(
            recorder,
            resultCode,
            operation,
            message,
            session == nullptr ? nullptr : session->LastDiagnostic());
        return resultCode;
    }
}

extern "C"
{
    CTCAPTUREV2_API int32_t CTCAPTUREV2_CALL CtCaptureV2_GetApiVersion(
        CtCaptureV2_ApiVersion* outVersion) noexcept
    {
        if (outVersion == nullptr)
        {
            return CtCaptureV2_ResultCode_InvalidArgument;
        }

        *outVersion = CtCaptureV2_ApiVersion{
            sizeof(CtCaptureV2_ApiVersion),
            ApiVersionDtoVersion,
            ApiVersionMajor,
            ApiVersionMinor,
            ApiVersionPatch,
            0
        };

        return CtCaptureV2_ResultCode_Success;
    }

    CTCAPTUREV2_API int32_t CTCAPTUREV2_CALL CtCaptureV2_CreateRecorder(
        CtCaptureV2_RecorderHandle* outHandle) noexcept
    {
        if (outHandle == nullptr)
        {
            return CtCaptureV2_ResultCode_InvalidArgument;
        }

        *outHandle = nullptr;

        try
        {
            auto recorder = std::make_unique<CtCaptureV2_Recorder_t>();
            CtCaptureV2_RecorderHandle handle = recorder.get();

            {
                std::lock_guard lock(RecorderRegistryMutex);
                RecorderRegistry.insert(handle);
            }

            *outHandle = recorder.release();
            ClearLastError(**outHandle);
            return CtCaptureV2_ResultCode_Success;
        }
        catch (...)
        {
            return CtCaptureV2_ResultCode_NativeFailure;
        }
    }

    CTCAPTUREV2_API int32_t CTCAPTUREV2_CALL CtCaptureV2_DestroyRecorder(
        CtCaptureV2_RecorderHandle handle) noexcept
    {
        if (handle == nullptr)
        {
            return CtCaptureV2_ResultCode_Success;
        }

        try
        {
            std::vector<CtCaptureV2_CallbackRegistrationHandle> registrationsToDelete;

            {
                std::lock_guard lock(RecorderRegistryMutex);
                const auto found = RecorderRegistry.find(handle);
                if (found == RecorderRegistry.end())
                {
                    return CtCaptureV2_ResultCode_InvalidHandle;
                }

                RecorderRegistry.erase(found);
                UnregisterAllCallbacksLocked(*handle, registrationsToDelete);
            }

            if (handle->activeSession)
            {
                (void)handle->activeSession->Stop();
                handle->activeSession.reset();
            }

            DeleteRegistrations(registrationsToDelete);
            delete handle;
            return CtCaptureV2_ResultCode_Success;
        }
        catch (...)
        {
            return CtCaptureV2_ResultCode_NativeFailure;
        }
    }

    CTCAPTUREV2_API int32_t CTCAPTUREV2_CALL CtCaptureV2_Start(
        CtCaptureV2_RecorderHandle handle,
        const CtCaptureV2_Config* config) noexcept
    {
        try
        {
            CtCaptureV2_Recorder_t* recorder = FindRecorder(handle);
            if (recorder == nullptr)
            {
                return CtCaptureV2_ResultCode_InvalidHandle;
            }

            if (recorder->state != RecorderState::Idle)
            {
                return RecordFailure(
                    *recorder,
                    CtCaptureV2_ResultCode_AlreadyStarted,
                    "Start",
                    u"Recorder is already started.");
            }

            const int32_t validationResult = CaptureInterop::V2::Api::ValidateConfig(config);
            if (validationResult != CtCaptureV2_ResultCode_Success)
            {
                return RecordFailure(
                    *recorder,
                    validationResult,
                    "Start",
                    u"Recorder configuration validation failed.");
            }

            CopiedRecorderConfig copiedConfig = CopyConfig(*config);
            std::unique_ptr<IRecorderSession> session = CreateRecorderSession(*config, copiedConfig);
            const int32_t startResult = session->Start();
            if (startResult != CtCaptureV2_ResultCode_Success)
            {
                return RecordSessionFailure(
                    *recorder,
                    session.get(),
                    startResult,
                    "Start",
                    u"Recorder session failed to start.");
            }

            recorder->activeConfig = copiedConfig;
            recorder->runtimeGains = recorder->activeConfig->audioGains;
            recorder->mutedAudioSources.clear();
            if (recorder->activeConfig->startMuted != 0)
            {
                for (const CopiedSourceConfig& source : recorder->activeConfig->sources)
                {
                    if (source.sourceKind == CtCaptureV2_SourceKind_SystemAudio && source.enabled != 0)
                    {
                        recorder->mutedAudioSources.push_back(source.sourceId);
                    }
                }
            }

            recorder->activeSession = std::move(session);
            recorder->state = RecorderState::Recording;
            ClearLastError(*recorder);
            return CtCaptureV2_ResultCode_Success;
        }
        catch (...)
        {
            return RecordNativeFailure(FindRecorder(handle), "Start");
        }
    }

    CTCAPTUREV2_API int32_t CTCAPTUREV2_CALL CtCaptureV2_Pause(
        CtCaptureV2_RecorderHandle handle) noexcept
    {
        try
        {
            CtCaptureV2_Recorder_t* recorder = FindRecorder(handle);
            if (recorder == nullptr)
            {
                return CtCaptureV2_ResultCode_InvalidHandle;
            }

            if (recorder->state != RecorderState::Recording)
            {
                return RecordFailure(
                    *recorder,
                    CtCaptureV2_ResultCode_InvalidState,
                    "Pause",
                    u"Recorder cannot be paused from the current state.");
            }

            const int32_t pauseResult = recorder->activeSession
                ? recorder->activeSession->Pause()
                : CtCaptureV2_ResultCode_InvalidState;
            if (pauseResult != CtCaptureV2_ResultCode_Success)
            {
                return RecordSessionFailure(
                    *recorder,
                    recorder->activeSession.get(),
                    pauseResult,
                    "Pause",
                    u"Recorder session failed to pause.");
            }

            recorder->state = RecorderState::Paused;
            ClearLastError(*recorder);
            return CtCaptureV2_ResultCode_Success;
        }
        catch (...)
        {
            return RecordNativeFailure(FindRecorder(handle), "Pause");
        }
    }

    CTCAPTUREV2_API int32_t CTCAPTUREV2_CALL CtCaptureV2_Resume(
        CtCaptureV2_RecorderHandle handle) noexcept
    {
        try
        {
            CtCaptureV2_Recorder_t* recorder = FindRecorder(handle);
            if (recorder == nullptr)
            {
                return CtCaptureV2_ResultCode_InvalidHandle;
            }

            if (recorder->state != RecorderState::Paused)
            {
                return RecordFailure(
                    *recorder,
                    CtCaptureV2_ResultCode_InvalidState,
                    "Resume",
                    u"Recorder cannot be resumed from the current state.");
            }

            const int32_t resumeResult = recorder->activeSession
                ? recorder->activeSession->Resume()
                : CtCaptureV2_ResultCode_InvalidState;
            if (resumeResult != CtCaptureV2_ResultCode_Success)
            {
                return RecordSessionFailure(
                    *recorder,
                    recorder->activeSession.get(),
                    resumeResult,
                    "Resume",
                    u"Recorder session failed to resume.");
            }

            recorder->state = RecorderState::Recording;
            ClearLastError(*recorder);
            return CtCaptureV2_ResultCode_Success;
        }
        catch (...)
        {
            return RecordNativeFailure(FindRecorder(handle), "Resume");
        }
    }

    CTCAPTUREV2_API int32_t CTCAPTUREV2_CALL CtCaptureV2_SetAudioMuted(
        CtCaptureV2_RecorderHandle handle,
        uint32_t sourceId,
        uint8_t muted) noexcept
    {
        try
        {
            CtCaptureV2_Recorder_t* recorder = FindRecorder(handle);
            if (recorder == nullptr)
            {
                return CtCaptureV2_ResultCode_InvalidHandle;
            }

            if (muted != 0 && muted != 1)
            {
                return RecordFailure(
                    *recorder,
                    CtCaptureV2_ResultCode_InvalidArgument,
                    "SetAudioMuted",
                    u"Muted must be 0 or 1.");
            }

            if (recorder->state != RecorderState::Recording && recorder->state != RecorderState::Paused)
            {
                return RecordFailure(
                    *recorder,
                    CtCaptureV2_ResultCode_InvalidState,
                    "SetAudioMuted",
                    u"Recorder cannot update audio mute from the current state.");
            }

            const int32_t commandResult = recorder->activeSession
                ? recorder->activeSession->SetAudioMuted(sourceId, muted != 0)
                : CtCaptureV2_ResultCode_InvalidState;
            if (commandResult != CtCaptureV2_ResultCode_Success)
            {
                return RecordSessionFailure(
                    *recorder,
                    recorder->activeSession.get(),
                    commandResult,
                    "SetAudioMuted",
                    u"Recorder session failed to update audio mute.");
            }

            SetRuntimeMuted(*recorder, sourceId, muted != 0);
            ClearLastError(*recorder);
            return CtCaptureV2_ResultCode_Success;
        }
        catch (...)
        {
            return RecordNativeFailure(FindRecorder(handle), "SetAudioMuted");
        }
    }

    CTCAPTUREV2_API int32_t CTCAPTUREV2_CALL CtCaptureV2_SetAudioGain(
        CtCaptureV2_RecorderHandle handle,
        uint32_t sourceId,
        float gainDb) noexcept
    {
        try
        {
            CtCaptureV2_Recorder_t* recorder = FindRecorder(handle);
            if (recorder == nullptr)
            {
                return CtCaptureV2_ResultCode_InvalidHandle;
            }

            if (recorder->state != RecorderState::Recording && recorder->state != RecorderState::Paused)
            {
                return RecordFailure(
                    *recorder,
                    CtCaptureV2_ResultCode_InvalidState,
                    "SetAudioGain",
                    u"Recorder cannot update audio gain from the current state.");
            }

            if (gainDb < CaptureInterop::V2::Api::MinAudioGainDb
                || gainDb > CaptureInterop::V2::Api::MaxAudioGainDb)
            {
                return RecordFailure(
                    *recorder,
                    CtCaptureV2_ResultCode_ValidationFailed,
                    "SetAudioGain",
                    u"Audio gain is outside the supported range.");
            }

            const int32_t commandResult = recorder->activeSession
                ? recorder->activeSession->SetAudioGain(sourceId, gainDb)
                : CtCaptureV2_ResultCode_InvalidState;
            if (commandResult != CtCaptureV2_ResultCode_Success)
            {
                return RecordSessionFailure(
                    *recorder,
                    recorder->activeSession.get(),
                    commandResult,
                    "SetAudioGain",
                    u"Recorder session failed to update audio gain.");
            }

            SetRuntimeGain(*recorder, sourceId, gainDb);
            ClearLastError(*recorder);
            return CtCaptureV2_ResultCode_Success;
        }
        catch (...)
        {
            return RecordNativeFailure(FindRecorder(handle), "SetAudioGain");
        }
    }

    CTCAPTUREV2_API int32_t CTCAPTUREV2_CALL CtCaptureV2_Stop(
        CtCaptureV2_RecorderHandle handle,
        CtCaptureV2_StopResult* result) noexcept
    {
        try
        {
            if (result == nullptr)
            {
                return CtCaptureV2_ResultCode_InvalidArgument;
            }

            CtCaptureV2_Recorder_t* recorder = FindRecorder(handle);
            if (recorder == nullptr)
            {
                *result = MakeStopResult(CtCaptureV2_ResultCode_InvalidHandle, RecorderState::Idle);
                return CtCaptureV2_ResultCode_InvalidHandle;
            }

            std::vector<CtCaptureV2_CallbackRegistrationHandle> registrationsToDelete;

            if (recorder->state == RecorderState::Idle)
            {
                {
                    std::lock_guard lock(RecorderRegistryMutex);
                    UnregisterAllCallbacksLocked(*recorder, registrationsToDelete);
                }

                *result = MakeStopResult(CtCaptureV2_ResultCode_AlreadyStopped, RecorderState::Idle);
                const int32_t failure = RecordFailure(
                    *recorder,
                    CtCaptureV2_ResultCode_AlreadyStopped,
                    "Stop",
                    u"Recorder is already stopped.");
                DeleteRegistrations(registrationsToDelete);
                return failure;
            }

            IRecorderSession* activeSession = recorder->activeSession.get();
            CtCaptureV2_StopResult stopResult = activeSession != nullptr
                ? activeSession->Stop()
                : MakeStopResult(CtCaptureV2_ResultCode_AlreadyStopped, RecorderState::Idle);

            if (stopResult.resultCode == CtCaptureV2_ResultCode_Success)
            {
                ClearLastError(*recorder);
            }
            else
            {
                RecordDiagnosticFailure(
                    *recorder,
                    stopResult.resultCode,
                    "Stop",
                    u"Recorder session stop failed.",
                    activeSession == nullptr ? nullptr : activeSession->LastDiagnostic());
            }

            recorder->state = RecorderState::Idle;
            recorder->activeConfig.reset();
            recorder->activeSession.reset();
            recorder->runtimeGains.clear();
            recorder->mutedAudioSources.clear();
            {
                std::lock_guard lock(RecorderRegistryMutex);
                UnregisterAllCallbacksLocked(*recorder, registrationsToDelete);
            }

            *result = stopResult;
            DeleteRegistrations(registrationsToDelete);
            return stopResult.resultCode;
        }
        catch (...)
        {
            if (result != nullptr)
            {
                *result = MakeStopResult(CtCaptureV2_ResultCode_NativeFailure, RecorderState::Idle);
            }

            return RecordNativeFailure(FindRecorder(handle), "Stop");
        }
    }

    CTCAPTUREV2_API int32_t CTCAPTUREV2_CALL CtCaptureV2_GetLastError(
        CtCaptureV2_RecorderHandle handle,
        CtCaptureV2_ErrorInfo* errorInfo,
        char16_t* messageBuffer,
        uint32_t messageBufferLength,
        uint32_t* requiredMessageLength) noexcept
    {
        try
        {
            CtCaptureV2_Recorder_t* recorder = FindRecorder(handle);
            if (recorder == nullptr)
            {
                return CtCaptureV2_ResultCode_InvalidHandle;
            }

            if (errorInfo == nullptr || requiredMessageLength == nullptr)
            {
                return RecordFailure(
                    *recorder,
                    CtCaptureV2_ResultCode_InvalidArgument,
                    "GetLastError",
                    u"Error info and required message length outputs are required.");
            }

            CtCaptureV2_InitErrorInfo(errorInfo);
            errorInfo->resultCode = recorder->lastError.resultCode;
            errorInfo->errorCode = recorder->lastError.errorCode;
            errorInfo->nativeStatus = recorder->lastError.nativeStatus;
            errorInfo->stage = recorder->lastError.stage;
            errorInfo->component = recorder->lastError.component.c_str();
            errorInfo->operation = recorder->lastError.operation.c_str();

            const uint32_t requiredLength = RequiredMessageLength(recorder->lastError.message);
            *requiredMessageLength = requiredLength;

            if (messageBuffer == nullptr || messageBufferLength < requiredLength)
            {
                return CtCaptureV2_ResultCode_BufferTooSmall;
            }

            CopyMessage(recorder->lastError.message, messageBuffer, messageBufferLength);
            return CtCaptureV2_ResultCode_Success;
        }
        catch (...)
        {
            return CtCaptureV2_ResultCode_NativeFailure;
        }
    }

    CTCAPTUREV2_API int32_t CTCAPTUREV2_CALL CtCaptureV2_RegisterCallbacks(
        CtCaptureV2_RecorderHandle handle,
        const CtCaptureV2_CallbackConfig* config,
        CtCaptureV2_CallbackRegistrationHandle* outRegistration) noexcept
    {
        if (outRegistration == nullptr)
        {
            return CtCaptureV2_ResultCode_InvalidArgument;
        }

        *outRegistration = nullptr;

        try
        {
            if (config == nullptr || config->callback == nullptr)
            {
                return CtCaptureV2_ResultCode_InvalidArgument;
            }

            if (config->size != sizeof(CtCaptureV2_CallbackConfig))
            {
                return CtCaptureV2_ResultCode_InvalidArgument;
            }

            if (config->version != CtCaptureV2_DtoVersion)
            {
                return CtCaptureV2_ResultCode_UnsupportedVersion;
            }

            auto registration = std::make_unique<CtCaptureV2_CallbackRegistration_t>();
            registration->recorder = handle;
            registration->callback = config->callback;
            registration->userData = config->userData;
            registration->eventMask = config->eventMask;

            CtCaptureV2_RecorderHandle recorder = nullptr;
            {
                std::lock_guard lock(RecorderRegistryMutex);
                const auto recorderFound = RecorderRegistry.find(handle);
                if (recorderFound == RecorderRegistry.end())
                {
                    return CtCaptureV2_ResultCode_InvalidHandle;
                }

                CtCaptureV2_CallbackRegistrationHandle registrationHandle = registration.get();
                CallbackRegistrationRegistry.insert(registrationHandle);
                try
                {
                    (*recorderFound)->callbackRegistrations.push_back(registrationHandle);
                }
                catch (...)
                {
                    CallbackRegistrationRegistry.erase(registrationHandle);
                    throw;
                }

                *outRegistration = registration.release();
                recorder = *recorderFound;
            }

            ClearLastError(*recorder);
            return CtCaptureV2_ResultCode_Success;
        }
        catch (...)
        {
            return CtCaptureV2_ResultCode_CallbackRegistrationFailed;
        }
    }

    CTCAPTUREV2_API int32_t CTCAPTUREV2_CALL CtCaptureV2_UnregisterCallbacks(
        CtCaptureV2_CallbackRegistrationHandle registration) noexcept
    {
        if (registration == nullptr)
        {
            return CtCaptureV2_ResultCode_Success;
        }

        try
        {
            {
                std::lock_guard lock(RecorderRegistryMutex);
                const auto registrationFound = CallbackRegistrationRegistry.find(registration);
                if (registrationFound == CallbackRegistrationRegistry.end())
                {
                    return CtCaptureV2_ResultCode_InvalidHandle;
                }

                const auto recorderFound = RecorderRegistry.find(registration->recorder);
                if (recorderFound != RecorderRegistry.end())
                {
                    RemoveRegistrationFromRecorder(**recorderFound, registration);
                }

                CallbackRegistrationRegistry.erase(registrationFound);
            }

            delete registration;
            return CtCaptureV2_ResultCode_Success;
        }
        catch (...)
        {
            return CtCaptureV2_ResultCode_NativeFailure;
        }
    }

    CTCAPTUREV2_API int32_t CTCAPTUREV2_CALL CtCaptureV2_TestTriggerEvent(
        CtCaptureV2_RecorderHandle handle,
        const CtCaptureV2_Event* eventData) noexcept
    {
        try
        {
            if (eventData == nullptr)
            {
                return CtCaptureV2_ResultCode_InvalidArgument;
            }

            if (eventData->size != sizeof(CtCaptureV2_Event))
            {
                return CtCaptureV2_ResultCode_InvalidArgument;
            }

            if (eventData->version != CtCaptureV2_DtoVersion)
            {
                return CtCaptureV2_ResultCode_UnsupportedVersion;
            }

            std::vector<CopiedCallback> callbacks;
            {
                std::lock_guard lock(RecorderRegistryMutex);
                const auto recorderFound = RecorderRegistry.find(handle);
                if (recorderFound == RecorderRegistry.end())
                {
                    return CtCaptureV2_ResultCode_InvalidHandle;
                }

                const CtCaptureV2_Recorder_t& recorder = **recorderFound;
                callbacks.reserve(recorder.callbackRegistrations.size());
                for (CtCaptureV2_CallbackRegistrationHandle registration : recorder.callbackRegistrations)
                {
                    if (CallbackRegistrationRegistry.find(registration) != CallbackRegistrationRegistry.end()
                        && IsEventTypeEnabled(registration->eventMask, eventData->eventType))
                    {
                        callbacks.push_back(CopiedCallback{
                            registration->callback,
                            registration->userData
                        });
                    }
                }
            }

            for (const CopiedCallback& callback : callbacks)
            {
                callback.callback(eventData, callback.userData);
            }

            return CtCaptureV2_ResultCode_Success;
        }
        catch (...)
        {
            return CtCaptureV2_ResultCode_CallbackInvocationFailed;
        }
    }
}
