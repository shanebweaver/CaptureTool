#pragma once
#include "IAudioCaptureSource.h"
#include "AudioCaptureHandler.h"
#include <memory>

// Forward declaration
class IMediaClockReader;

/// <summary>
/// Audio input source that captures system audio using WASAPI loopback mode.
/// Implements IAudioCaptureSource to provide system-wide audio capture.
/// 
/// Implements Rust Principles:
/// - Principle #3 (No Nullable Pointers): Uses std::unique_ptr for handler ownership,
///   ensuring the handler is always valid after construction.
/// - Principle #5 (RAII Everything): Destructor calls Stop() to ensure audio resources
///   are released. Handler is automatically cleaned up via unique_ptr.
/// - Principle #6 (No Globals): Clock reader is passed via constructor, not accessed
///   as a singleton.
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
/// 
/// See docs/RUST_PRINCIPLES.md for more details.
/// </summary>
class WindowsLocalAudioCaptureSource : public IAudioCaptureSource
{
public:
    explicit WindowsLocalAudioCaptureSource(IMediaClockReader* clockReader);
    ~WindowsLocalAudioCaptureSource() override;

    // IAudioCaptureSource implementation
    bool Initialize(HRESULT* outHr = nullptr) override;
    bool Start(HRESULT* outHr = nullptr) override;
    void Stop() override;
    WAVEFORMATEX* GetFormat() const override;
    void SetAudioSampleReadyCallback(AudioSampleReadyCallback callback) override;
    void SetEnabled(bool enabled) override;
    bool IsEnabled() const override;
    bool IsRunning() const override;

    // IMediaClockAdvancer implementation
    void SetClockWriter(IMediaClockWriter* clockWriter) override;

private:
    std::unique_ptr<AudioCaptureHandler> m_handler;
};
