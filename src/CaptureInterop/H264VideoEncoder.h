#pragma once
#include "IVideoEncoder.h"
#include "TextureConverter.h"
#include <mfapi.h>
#include <mfidl.h>
#include <mfreadwrite.h>
#include <mutex>
#include <vector>

namespace CaptureInterop
{
    // H.264 video encoder implementation using Media Foundation
    class H264VideoEncoder : public IVideoEncoder
    {
    public:
        H264VideoEncoder();
        
        // IMediaSource interface
        MediaSourceType GetSourceType() const override { return MediaSourceType::Video; }
        bool Initialize() override;
        bool Start() override;
        void Stop() override;
        bool IsRunning() const override;
        
        // Reference counting
        ULONG AddRef() override;
        ULONG Release() override;
        
        // IVideoEncoder interface
        HRESULT Configure(const VideoEncoderConfig& config) override;
        HRESULT GetConfiguration(VideoEncoderConfig* pConfig) const override;
        HRESULT GetCapabilities(VideoEncoderCapabilities* pCapabilities) const override;
        bool SupportsCodec(VideoCodec codec) const override;
        
        // Encoding
        HRESULT EncodeFrame(ID3D11Texture2D* pTexture, int64_t timestamp, IMFSample** ppSample) override;
        HRESULT Flush(IMFSample** ppSample) override;
        
        // Stats
        uint64_t GetEncodedFrameCount() const override;
        uint64_t GetDroppedFrameCount() const override;
        double GetAverageEncodingTimeMs() const override;
        
    protected:
        virtual ~H264VideoEncoder();
        
    private:
        // Initialization helpers
        HRESULT DetectHardwareEncoder();
        HRESULT CreateSoftwareEncoder();
        HRESULT CreateHardwareEncoder();
        HRESULT ConfigureEncoder();
        HRESULT CreateInputMediaType(IMFMediaType** ppMediaType);
        HRESULT CreateOutputMediaType(IMFMediaType** ppMediaType);
        
        // Encoding helpers
        HRESULT ConvertTextureToSample(ID3D11Texture2D* pTexture, IMFSample** ppSample);
        HRESULT ProcessEncoderOutput(IMFSample** ppSample);
        
        // Stats helpers
        void UpdateEncodingStats(double encodingTimeMs);
        
        // Member variables
        std::atomic<ULONG> m_refCount;
        mutable std::mutex m_mutex;
        bool m_initialized;
        bool m_running;
        
        // Configuration
        VideoEncoderConfig m_config;
        VideoEncoderCapabilities m_capabilities;
        bool m_hardwareEncoderAvailable;
        bool m_usingHardwareEncoder;
        
        // Media Foundation objects
        IMFTransform* m_pEncoder;
        IMFMediaType* m_pInputType;
        IMFMediaType* m_pOutputType;
        
        // D3D11 support for hardware encoding
        ID3D11Device* m_pD3DDevice;
        ID3D11DeviceContext* m_pD3DContext;
        
        // Texture converter for D3D11 → Media Foundation
        TextureConverter* m_pTextureConverter;
        
        // Stats
        uint64_t m_encodedFrameCount;
        uint64_t m_droppedFrameCount;
        double m_totalEncodingTimeMs;
        std::vector<double> m_recentEncodingTimes;
        static constexpr size_t MAX_RECENT_SAMPLES = 60;
    };
}
