#pragma once

#include "IAudioEncoder.h"
#include <mfapi.h>
#include <mfidl.h>
#include <mfreadwrite.h>
#include <Mferror.h>
#include <codecapi.h>
#include <mutex>

using namespace CaptureInterop;

// AAC Audio Encoder Implementation
// Uses Media Foundation to encode PCM audio to AAC format
class AACEncoder : public IAudioEncoder
{
public:
    AACEncoder();
    virtual ~AACEncoder();

    // IAudioEncoder interface implementation
    HRESULT Configure(const AudioEncoderConfig& config) override;
    HRESULT GetConfiguration(AudioEncoderConfig* pConfig) const override;
    
    HRESULT GetCapabilities(AudioEncoderCapabilities* pCapabilities) const override;
    bool SupportsCodec(AudioCodec codec) const override;
    
    HRESULT EncodeAudio(const uint8_t* pData, uint32_t dataSize, int64_t timestamp, IMFSample** ppSample) override;
    HRESULT Flush(IMFSample** ppSample) override;
    
    uint64_t GetEncodedSampleCount() const override;
    double GetAverageEncodingTimeMs() const override;
    
    // IMediaSource interface (inherited)
    MediaSourceType GetSourceType() const override { return MediaSourceType::Audio; }
    bool Initialize() override { return true; }
    bool Start() override { return true; }
    void Stop() override {}
    bool IsRunning() const override { return m_isInitialized; }

    // Statistics
    UINT32 GetDroppedSampleCount() const { return m_droppedSamples; }

    // Reference counting (COM-style)
    ULONG AddRef() override;
    ULONG Release() override;

private:
    HRESULT CreateEncoder();
    HRESULT ConfigureEncoder();
    HRESULT CreateInputMediaType();
    HRESULT CreateOutputMediaType();
    HRESULT ProcessInput(const BYTE* pData, DWORD dataSize, LONGLONG timestamp);
    HRESULT ProcessOutput(IMFSample** ppSample);
    
    // Calculate bitrate based on config
    UINT32 CalculateBitrate() const;
    
    // Get quality value for encoder (0-100)
    UINT32 GetQualityValue() const;

private:
    ULONG m_refCount;
    AudioEncoderConfig m_config;
    bool m_isInitialized;
    bool m_isHardwareAccelerated;
    
    // Media Foundation objects
    IMFTransform* m_pEncoder;
    IMFMediaType* m_pInputType;
    IMFMediaType* m_pOutputType;
    
    // Input buffer for partial samples
    std::vector<BYTE> m_inputBuffer;
    UINT32 m_samplesPerFrame;  // AAC frame size (typically 1024 samples)
    
    // Statistics
    uint64_t m_encodedSamples;
    UINT32 m_droppedSamples;
    double m_totalEncodingTime;
    
    // Thread safety
    mutable std::mutex m_mutex;
};
