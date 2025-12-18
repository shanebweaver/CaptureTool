#pragma once
#include <cstdint>
#include <string>

// Forward declarations
struct IMFSample;
struct IMFMediaType;

namespace CaptureInterop
{
    // Container format types
    enum class ContainerFormat
    {
        MP4,            // MP4 (MPEG-4 Part 14)
        MKV,            // Matroska (future)
        AVI             // AVI (future, legacy support)
    };

    // Track types
    enum class TrackType
    {
        Video,
        Audio,
        Subtitle
    };

    // Track information
    struct TrackInfo
    {
        uint32_t trackIndex;        // Track index
        TrackType type;             // Track type
        std::wstring name;          // Track name (metadata)
        IMFMediaType* pMediaType;   // Media type (format)
        
        TrackInfo()
            : trackIndex(0)
            , type(TrackType::Video)
            , pMediaType(nullptr)
        {}
    };

    // Muxer configuration
    struct MuxerConfig
    {
        ContainerFormat format;     // Container format
        std::wstring outputPath;    // Output file path
        bool fastStart;             // Enable fast-start (moov at beginning)
        bool fragmentedMP4;         // Enable fragmented MP4 (future)
        
        MuxerConfig()
            : format(ContainerFormat::MP4)
            , fastStart(true)
            , fragmentedMP4(false)
        {}
    };

    // Muxer interface
    class IMuxer
    {
    public:
        // Lifecycle
        virtual HRESULT Initialize(const MuxerConfig& config) = 0;
        virtual HRESULT Start() = 0;
        virtual HRESULT Stop() = 0;
        virtual HRESULT Finalize() = 0;
        virtual bool IsRunning() const = 0;
        
        // Track management
        virtual HRESULT AddTrack(const TrackInfo& trackInfo, uint32_t* pTrackIndex) = 0;
        virtual HRESULT RemoveTrack(uint32_t trackIndex) = 0;
        virtual uint32_t GetTrackCount() const = 0;
        
        // Writing
        virtual HRESULT WriteSample(uint32_t trackIndex, IMFSample* pSample) = 0;
        
        // Interleaving control
        virtual HRESULT SetMaxInterleaveDelta(int64_t deltaMicroseconds) = 0;
        virtual int64_t GetMaxInterleaveDelta() const = 0;
        
        // Metadata
        virtual HRESULT SetMetadata(const wchar_t* key, const wchar_t* value) = 0;
        virtual HRESULT GetMetadata(const wchar_t* key, std::wstring* pValue) const = 0;
        
        // Stats
        virtual uint64_t GetTotalBytesWritten() const = 0;
        virtual uint64_t GetSamplesWritten(uint32_t trackIndex) const = 0;
        virtual int64_t GetDuration() const = 0;
        
        // Reference counting
        virtual uint32_t AddRef() = 0;
        virtual uint32_t Release() = 0;
        
    protected:
        virtual ~IMuxer() = default;
    };
}
