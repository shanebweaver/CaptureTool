#pragma once
#include "IAudioCaptureSource.h"
#include "IMediaClockAdvancer.h"
#include "AudioCaptureHandler.h"
#include <memory>
#include <mmreg.h>
#include <string>
#include <Windows.h>

// Forward declaration
class IMediaClockReader;

/// <summary>
/// Audio input source that captures system audio using WASAPI loopback mode.
/// Implements IAudioCaptureSource to provide system-wide audio capture.
/// 
/// Ownership model:
/// - WindowsLocalAudioCaptureSource owns AudioCaptureHandler via unique_ptr
/// - Handler lifetime equals source lifetime
/// - All operations delegate to handler (simple wrapper pattern)
/// 
/// Threading model:
/// - Control methods (Initialize, Start, Stop) are called from session thread
/// - Capture callback is invoked from dedicated audio capture thread
/// - Thread safety is handled by AudioCaptureHandler implementation
/// </summary>
class WindowsLocalAudioCaptureSource : public IAudioCaptureSource
{
public:
    explicit WindowsLocalAudioCaptureSource(IMediaClockReader* clockReader, std::wstring inputDeviceId = L"");
    ~WindowsLocalAudioCaptureSource() override;

    // IAudioCaptureSource implementation
    bool Initialize(HRESULT* outHr = nullptr) override;
    bool Start(HRESULT* outHr = nullptr) override;
    void Stop() override;
    WAVEFORMATEX* GetFormat() const override;
    void SetAudioSampleReadyCallback(AudioSampleReadyCallback callback) override;
    void SetEnabled(bool enabled) override;
    bool IsEnabled() const override;
    void SetVolume(uint32_t volumePercentage) override;
    bool IsRunning() const override;
    bool SetInputDeviceId(const wchar_t* sourceId, HRESULT* outHr = nullptr) override;

    // IMediaClockAdvancer implementation
    void SetClockWriter(IMediaClockWriter* clockWriter) override;

private:
    std::unique_ptr<AudioCaptureHandler> m_handler;
    std::wstring m_inputDeviceId;
};
