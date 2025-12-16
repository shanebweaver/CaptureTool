#pragma once
#include "AudioCaptureDevice.h"
#include <thread>
#include <atomic>
#include <mutex>
#include <vector>

// Forward declaration
class MP4SinkWriter;

// Manages audio capture in a separate thread and buffers samples
class AudioCaptureHandler
{
public:
    AudioCaptureHandler();
    ~AudioCaptureHandler();

    // Initialize audio capture (loopback = true for system audio)
    bool Initialize(bool loopback, HRESULT* outHr = nullptr);
    
    // Start capturing audio
    bool Start(HRESULT* outHr = nullptr);
    
    // Stop capturing audio
    void Stop();
    
    // Get the audio format
    WAVEFORMATEX* GetFormat() const;
    
    // Set the sink writer to receive audio samples (will be used in Phase 2)
    void SetSinkWriter(MP4SinkWriter* sinkWriter) { m_sinkWriter = sinkWriter; }

private:
    void CaptureThreadProc();
    
    AudioCaptureDevice m_device;
    MP4SinkWriter* m_sinkWriter = nullptr;
    
    std::thread m_captureThread;
    std::atomic<bool> m_isRunning{false};
    
    LONGLONG m_startQpc = 0;
    LARGE_INTEGER m_qpcFrequency{};
    LONGLONG m_nextAudioTimestamp = 0;  // Track expected timestamp based on audio written
};
