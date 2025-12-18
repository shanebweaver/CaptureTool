#pragma once
#include <Windows.h>
#include <d3d11.h>
#include <mfapi.h>
#include <mfidl.h>
#include <string>
#include <mutex>
#include <vector>
#include "IVideoEncoder.h"
#include "IAudioEncoder.h"
#include "IMuxer.h"
#include "EncoderPresets.h"

// Forward declarations
class H264VideoEncoder;
class AACEncoder;
class MP4Muxer;

// Configuration structure for encoder pipeline
struct EncoderPipelineConfig
{
    // Video configuration
    UINT32 videoWidth;
    UINT32 videoHeight;
    UINT32 videoFPS;
    EncoderPreset videoPreset;
    bool useHardwareEncoding;
    
    // Audio configuration
    UINT32 audioSampleRate;
    UINT32 audioChannels;
    UINT32 audioBitsPerSample;
    UINT32 audioTrackCount;
    AudioQuality audioQuality;
    
    // Output configuration
    std::wstring outputPath;
    ID3D11Device* d3dDevice;  // Required for video encoding
    
    // Constructor with defaults
    EncoderPipelineConfig()
        : videoWidth(1920)
        , videoHeight(1080)
        , videoFPS(30)
        , videoPreset(EncoderPreset::Balanced)
        , useHardwareEncoding(true)
        , audioSampleRate(48000)
        , audioChannels(2)
        , audioBitsPerSample(16)
        , audioTrackCount(1)
        , audioQuality(AudioQuality::High)
        , d3dDevice(nullptr)
    {
    }
};

// Pipeline statistics
struct PipelineStatistics
{
    // Video stats
    UINT64 videoFramesProcessed;
    UINT64 videoFramesEncoded;
    UINT64 videoFramesDropped;
    UINT64 videoBytesize;
    double averageVideoEncodingTimeMs;
    
    // Audio stats (per track)
    std::vector<UINT64> audioSamplesProcessed;
    std::vector<UINT64> audioSamplesEncoded;
    std::vector<UINT64> audioSamplesDropped;
    std::vector<UINT64> audioByteSize;
    std::vector<double> averageAudioEncodingTimeMs;
    
    // Synchronization
    INT64 maxTimestampDeltaMs;
    
    // Overall
    bool isRunning;
    UINT64 totalDurationMs;
    
    PipelineStatistics()
        : videoFramesProcessed(0)
        , videoFramesEncoded(0)
        , videoFramesDropped(0)
        , videoBytesize(0)
        , averageVideoEncodingTimeMs(0.0)
        , maxTimestampDeltaMs(0)
        , isRunning(false)
        , totalDurationMs(0)
    {
    }
};

// EncoderPipeline: Coordinates video/audio encoders and muxer
// Replaces the monolithic MP4SinkWriter with modular encoder+muxer architecture
class EncoderPipeline
{
public:
    EncoderPipeline();
    ~EncoderPipeline();
    
    // Lifecycle management
    HRESULT Initialize(const EncoderPipelineConfig& config);
    HRESULT Start();
    HRESULT Stop();
    bool IsRunning() const { return m_isRunning; }
    
    // Video processing
    HRESULT ProcessVideoFrame(ID3D11Texture2D* texture, UINT64 timestamp);
    
    // Audio processing (per-track)
    HRESULT ProcessAudioSamples(const BYTE* data, UINT32 length, UINT64 timestamp, UINT32 trackIndex);
    
    // Audio track management
    HRESULT InitializeAudioTrack(UINT32 trackIndex, const WAVEFORMATEX* format, const wchar_t* trackName);
    
    // Statistics and monitoring
    void GetStatistics(PipelineStatistics* stats) const;
    void ResetStatistics();
    
    // Configuration access
    const EncoderPipelineConfig& GetConfig() const { return m_config; }
    
    // Reference counting (for consistency with other components)
    ULONG AddRef();
    ULONG Release();
    
private:
    // Disable copy
    EncoderPipeline(const EncoderPipeline&) = delete;
    EncoderPipeline& operator=(const EncoderPipeline&) = delete;
    
    // Internal initialization helpers
    HRESULT InitializeVideoEncoder();
    HRESULT InitializeAudioEncoders();
    HRESULT InitializeMuxer();
    
    // Internal cleanup
    void Cleanup();
    
    // Callback handlers for encoded data
    static void OnVideoDataEncoded(const BYTE* data, UINT32 size, UINT64 timestamp, bool isKeyFrame, void* context);
    static void OnAudioDataEncoded(const BYTE* data, UINT32 size, UINT64 timestamp, UINT32 trackIndex, void* context);
    
    // Member variables
    std::mutex m_mutex;
    ULONG m_refCount;
    bool m_isInitialized;
    bool m_isRunning;
    
    // Configuration
    EncoderPipelineConfig m_config;
    
    // Encoders and muxer
    H264VideoEncoder* m_videoEncoder;
    std::vector<AACEncoder*> m_audioEncoders;  // One per track
    MP4Muxer* m_muxer;
    
    // Track mapping
    std::map<UINT32, UINT32> m_trackToEncoderMap;  // AudioMixer track index → encoder index
    UINT32 m_videoTrackIndex;
    
    // Statistics
    PipelineStatistics m_stats;
    UINT64 m_startTime;
    
    // Timing for statistics
    std::vector<double> m_videoEncodingTimes;
    std::vector<std::vector<double>> m_audioEncodingTimes;  // Per track
};
