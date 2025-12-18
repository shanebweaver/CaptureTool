#pragma once
#include "IMuxer.h"
#include <mfapi.h>
#include <mfidl.h>
#include <mfreadwrite.h>
#include <vector>
#include <mutex>
#include <string>

// Forward declarations
struct ID3D11Device;
struct ID3D11Texture2D;
struct IMFSinkWriter;
struct IMFMediaBuffer;

namespace CaptureInterop
{
    /// <summary>
    /// MP4 muxer implementation using Media Foundation.
    /// Implements the IMuxer interface for MP4 container format.
    /// Provides improved interleaving algorithm for multi-track synchronization.
    /// </summary>
    class MP4Muxer : public IMuxer
    {
    public:
        MP4Muxer();
        virtual ~MP4Muxer();

        // IMuxer interface implementation
        HRESULT Initialize(const MuxerConfig& config) override;
        HRESULT Start() override;
        HRESULT Stop() override;
        HRESULT Finalize() override;
        bool IsRunning() const override;
        
        HRESULT AddTrack(const TrackInfo& trackInfo, uint32_t* pTrackIndex) override;
        HRESULT RemoveTrack(uint32_t trackIndex) override;
        uint32_t GetTrackCount() const override;
        
        HRESULT WriteSample(uint32_t trackIndex, IMFSample* pSample) override;
        
        HRESULT SetMaxInterleaveDelta(int64_t deltaMicroseconds) override;
        int64_t GetMaxInterleaveDelta() const override;
        
        HRESULT SetMetadata(const wchar_t* key, const wchar_t* value) override;
        HRESULT GetMetadata(const wchar_t* key, std::wstring* pValue) const override;
        
        uint64_t GetTotalBytesWritten() const override;
        uint64_t GetSamplesWritten(uint32_t trackIndex) const override;
        int64_t GetDuration() const override;
        
        uint32_t AddRef() override;
        uint32_t Release() override;

        // MP4-specific methods
        HRESULT SetD3DDevice(ID3D11Device* pDevice);
        HRESULT WriteVideoFrame(ID3D11Texture2D* pTexture, LONGLONG timestamp);

    private:
        // Internal structure for tracking
        struct TrackData
        {
            TrackInfo info;
            DWORD streamIndex;          // Media Foundation stream index
            uint64_t samplesWritten;
            int64_t lastTimestamp;      // Last timestamp written (for interleaving)
            bool isEnabled;
            
            TrackData()
                : streamIndex(0)
                , samplesWritten(0)
                , lastTimestamp(0)
                , isEnabled(true)
            {}
        };

        // Helper methods
        HRESULT CreateSinkWriter();
        HRESULT ConfigureVideoStream(const TrackInfo& trackInfo, DWORD* pStreamIndex);
        HRESULT ConfigureAudioStream(const TrackInfo& trackInfo, DWORD* pStreamIndex);
        HRESULT ConvertTextureToSample(ID3D11Texture2D* pTexture, IMFSample** ppSample);
        bool CheckInterleaving(uint32_t trackIndex, int64_t timestamp);
        void UpdateStatistics(uint32_t trackIndex, int64_t timestamp);

        // Member variables
        std::mutex m_mutex;
        uint32_t m_refCount;
        bool m_isInitialized;
        bool m_isRunning;
        
        MuxerConfig m_config;
        IMFSinkWriter* m_pSinkWriter;
        ID3D11Device* m_pD3DDevice;
        
        std::vector<TrackData> m_tracks;
        uint32_t m_videoTrackCount;
        uint32_t m_audioTrackCount;
        
        // Interleaving control
        int64_t m_maxInterleaveDelta;   // Maximum timestamp delta in microseconds
        int64_t m_startTimestamp;       // Recording start timestamp
        int64_t m_duration;             // Total duration
        uint64_t m_totalBytesWritten;
        
        // Metadata storage
        std::map<std::wstring, std::wstring> m_metadata;

        // Constants
        static constexpr uint32_t MAX_VIDEO_TRACKS = 1;     // MP4 typically 1 video track
        static constexpr uint32_t MAX_AUDIO_TRACKS = 6;     // Support up to 6 audio tracks
        static constexpr int64_t DEFAULT_INTERLEAVE_DELTA = 1000000; // 1 second default
    };
}
