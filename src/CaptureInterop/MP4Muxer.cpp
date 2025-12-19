#include "pch.h"
#include "MP4Muxer.h"
#include <mferror.h>
#include <wrl/client.h>
#include <d3d11.h>
#include <algorithm>

using namespace CaptureInterop;
using Microsoft::WRL::ComPtr;

MP4Muxer::MP4Muxer()
    : m_refCount(1)
    , m_isInitialized(false)
    , m_isRunning(false)
    , m_pSinkWriter(nullptr)
    , m_pD3DDevice(nullptr)
    , m_videoTrackCount(0)
    , m_audioTrackCount(0)
    , m_maxInterleaveDelta(DEFAULT_INTERLEAVE_DELTA)
    , m_startTimestamp(0)
    , m_duration(0)
    , m_totalBytesWritten(0)
{
}

MP4Muxer::~MP4Muxer()
{
    Finalize();
    
    if (m_pSinkWriter)
    {
        m_pSinkWriter->Release();
        m_pSinkWriter = nullptr;
    }
    
    if (m_pD3DDevice)
    {
        m_pD3DDevice->Release();
        m_pD3DDevice = nullptr;
    }
}

HRESULT MP4Muxer::Initialize(const MuxerConfig& config)
{
    std::lock_guard<std::mutex> lock(m_mutex);
    
    if (m_isInitialized)
    {
        return E_UNEXPECTED;
    }
    
    if (config.format != ContainerFormat::MP4)
    {
        return E_INVALIDARG; // Only MP4 supported in this implementation
    }
    
    m_config = config;
    
    // Create sink writer will be deferred until we have at least one track
    m_isInitialized = true;
    
    return S_OK;
}

HRESULT MP4Muxer::Start()
{
    std::lock_guard<std::mutex> lock(m_mutex);
    
    if (!m_isInitialized)
    {
        return E_UNEXPECTED;
    }
    
    if (m_isRunning)
    {
        return S_OK;
    }
    
    // Create sink writer if not already created
    if (!m_pSinkWriter)
    {
        HRESULT hr = CreateSinkWriter();
        if (FAILED(hr))
        {
            return hr;
        }
    }
    
    // Start writing
    HRESULT hr = m_pSinkWriter->BeginWriting();
    if (FAILED(hr))
    {
        return hr;
    }
    
    m_isRunning = true;
    m_startTimestamp = 0;
    
    return S_OK;
}

HRESULT MP4Muxer::Stop()
{
    std::lock_guard<std::mutex> lock(m_mutex);
    
    if (!m_isRunning)
    {
        return S_OK;
    }
    
    m_isRunning = false;
    
    return S_OK;
}

HRESULT MP4Muxer::Finalize()
{
    std::lock_guard<std::mutex> lock(m_mutex);
    
    if (!m_isInitialized)
    {
        return S_OK;
    }
    
    HRESULT hr = S_OK;
    
    if (m_pSinkWriter && m_isRunning)
    {
        hr = m_pSinkWriter->Finalize();
    }
    
    m_isRunning = false;
    m_isInitialized = false;
    
    return hr;
}

bool MP4Muxer::IsRunning() const
{
    return m_isRunning;
}

HRESULT MP4Muxer::AddTrack(const TrackInfo& trackInfo, uint32_t* pTrackIndex)
{
    std::lock_guard<std::mutex> lock(m_mutex);
    
    if (!m_isInitialized || m_isRunning)
    {
        return E_UNEXPECTED;
    }
    
    // Validate track limits
    if (trackInfo.type == TrackType::Video && m_videoTrackCount >= MAX_VIDEO_TRACKS)
    {
        return E_INVALIDARG;
    }
    
    if (trackInfo.type == TrackType::Audio && m_audioTrackCount >= MAX_AUDIO_TRACKS)
    {
        return E_INVALIDARG;
    }
    
    // Create sink writer if not created yet
    if (!m_pSinkWriter)
    {
        HRESULT hr = CreateSinkWriter();
        if (FAILED(hr))
        {
            return hr;
        }
    }
    
    // Configure stream based on type
    TrackData trackData;
    trackData.info = trackInfo;
    
    HRESULT hr;
    if (trackInfo.type == TrackType::Video)
    {
        hr = ConfigureVideoStream(trackInfo, &trackData.streamIndex);
        if (SUCCEEDED(hr))
        {
            m_videoTrackCount++;
        }
    }
    else if (trackInfo.type == TrackType::Audio)
    {
        hr = ConfigureAudioStream(trackInfo, &trackData.streamIndex);
        if (SUCCEEDED(hr))
        {
            m_audioTrackCount++;
        }
    }
    else
    {
        return E_NOTIMPL; // Subtitle tracks not yet supported
    }
    
    if (FAILED(hr))
    {
        return hr;
    }
    
    // Add to track list
    uint32_t trackIndex = static_cast<uint32_t>(m_tracks.size());
    trackData.info.trackIndex = trackIndex;
    m_tracks.push_back(trackData);
    
    if (pTrackIndex)
    {
        *pTrackIndex = trackIndex;
    }
    
    return S_OK;
}

HRESULT MP4Muxer::RemoveTrack(uint32_t trackIndex)
{
    std::lock_guard<std::mutex> lock(m_mutex);
    
    if (trackIndex >= m_tracks.size())
    {
        return E_INVALIDARG;
    }
    
    if (m_isRunning)
    {
        return E_UNEXPECTED; // Cannot remove tracks while running
    }
    
    // Mark as disabled (actual removal not supported by Media Foundation once added)
    m_tracks[trackIndex].isEnabled = false;
    
    return S_OK;
}

uint32_t MP4Muxer::GetTrackCount() const
{
    return static_cast<uint32_t>(m_tracks.size());
}

HRESULT MP4Muxer::WriteSample(uint32_t trackIndex, IMFSample* pSample)
{
    std::lock_guard<std::mutex> lock(m_mutex);
    
    if (!m_isRunning)
    {
        return E_UNEXPECTED;
    }
    
    if (trackIndex >= m_tracks.size())
    {
        return E_INVALIDARG;
    }
    
    if (!m_tracks[trackIndex].isEnabled)
    {
        return E_INVALIDARG;
    }
    
    if (!pSample)
    {
        return E_POINTER;
    }
    
    // Get timestamp for interleaving check
    LONGLONG timestamp = 0;
    pSample->GetSampleTime(&timestamp);
    
    // Check interleaving constraints
    if (!CheckInterleaving(trackIndex, timestamp))
    {
        // Sample out of order - may need buffering logic here
        // For now, we'll still write it
    }
    
    // Write sample to sink writer
    DWORD streamIndex = m_tracks[trackIndex].streamIndex;
    HRESULT hr = m_pSinkWriter->WriteSample(streamIndex, pSample);
    
    if (SUCCEEDED(hr))
    {
        UpdateStatistics(trackIndex, timestamp);
    }
    
    return hr;
}

HRESULT MP4Muxer::SetMaxInterleaveDelta(int64_t deltaMicroseconds)
{
    std::lock_guard<std::mutex> lock(m_mutex);
    
    if (deltaMicroseconds < 0)
    {
        return E_INVALIDARG;
    }
    
    m_maxInterleaveDelta = deltaMicroseconds;
    
    return S_OK;
}

int64_t MP4Muxer::GetMaxInterleaveDelta() const
{
    return m_maxInterleaveDelta;
}

HRESULT MP4Muxer::SetMetadata(const wchar_t* key, const wchar_t* value)
{
    std::lock_guard<std::mutex> lock(m_mutex);
    
    if (!key || !value)
    {
        return E_POINTER;
    }
    
    m_metadata[key] = value;
    
    // Note: MP4 metadata should be set before creating the sink writer
    // Media Foundation doesn't provide a simple API to set arbitrary metadata
    // after the sink writer is created. The metadata constants like MF_PD_TITLE
    // are for reading metadata from media sources, not for setting on sink writers.
    // 
    // For proper MP4 metadata support, we would need to:
    // 1. Use IMFMetadata interface (if available)
    // 2. Or use IMFAttributes on the sink writer before adding streams
    // 3. Or write custom atoms to the MP4 file post-processing
    //
    // For now, we just store it in our map for potential future use
    
    return S_OK;
}

HRESULT MP4Muxer::GetMetadata(const wchar_t* key, std::wstring* pValue) const
{
    if (!key || !pValue)
    {
        return E_POINTER;
    }
    
    auto it = m_metadata.find(key);
    if (it == m_metadata.end())
    {
        return E_NOT_SET;
    }
    
    *pValue = it->second;
    
    return S_OK;
}

uint64_t MP4Muxer::GetTotalBytesWritten() const
{
    return m_totalBytesWritten;
}

uint64_t MP4Muxer::GetSamplesWritten(uint32_t trackIndex) const
{
    if (trackIndex >= m_tracks.size())
    {
        return 0;
    }
    
    return m_tracks[trackIndex].samplesWritten;
}

int64_t MP4Muxer::GetDuration() const
{
    return m_duration;
}

uint32_t MP4Muxer::AddRef()
{
    return ++m_refCount;
}

uint32_t MP4Muxer::Release()
{
    uint32_t count = --m_refCount;
    if (count == 0)
    {
        delete this;
    }
    return count;
}

HRESULT MP4Muxer::SetD3DDevice(ID3D11Device* pDevice)
{
    std::lock_guard<std::mutex> lock(m_mutex);
    
    if (m_isRunning)
    {
        return E_UNEXPECTED;
    }
    
    if (m_pD3DDevice)
    {
        m_pD3DDevice->Release();
    }
    
    m_pD3DDevice = pDevice;
    
    if (m_pD3DDevice)
    {
        m_pD3DDevice->AddRef();
    }
    
    return S_OK;
}

HRESULT MP4Muxer::WriteVideoFrame(ID3D11Texture2D* pTexture, LONGLONG timestamp)
{
    if (!pTexture)
    {
        return E_POINTER;
    }
    
    // Convert texture to IMFSample
    ComPtr<IMFSample> spSample;
    HRESULT hr = ConvertTextureToSample(pTexture, &spSample);
    if (FAILED(hr))
    {
        return hr;
    }
    
    // Set timestamp
    hr = spSample->SetSampleTime(timestamp);
    if (FAILED(hr))
    {
        return hr;
    }
    
    // Find video track
    for (uint32_t i = 0; i < m_tracks.size(); i++)
    {
        if (m_tracks[i].info.type == TrackType::Video)
        {
            return WriteSample(i, spSample.Get());
        }
    }
    
    return E_UNEXPECTED; // No video track found
}

// Private helper methods

HRESULT MP4Muxer::CreateSinkWriter()
{
    ComPtr<IMFSinkWriter> spSinkWriter;
    ComPtr<IMFAttributes> spAttributes;
    
    // Create attributes
    HRESULT hr = MFCreateAttributes(&spAttributes, 1);
    if (FAILED(hr))
    {
        return hr;
    }
    
    // Set D3D manager if available
    if (m_pD3DDevice)
    {
        ComPtr<IMFDXGIDeviceManager> spDeviceManager;
        UINT resetToken = 0;
        
        hr = MFCreateDXGIDeviceManager(&resetToken, &spDeviceManager);
        if (SUCCEEDED(hr))
        {
            hr = spDeviceManager->ResetDevice(m_pD3DDevice, resetToken);
            if (SUCCEEDED(hr))
            {
                spAttributes->SetUnknown(MF_SINK_WRITER_D3D_MANAGER, spDeviceManager.Get());
            }
        }
    }
    
    // Create sink writer
    hr = MFCreateSinkWriterFromURL(
        m_config.outputPath.c_str(),
        nullptr,
        spAttributes.Get(),
        &spSinkWriter
    );
    
    if (FAILED(hr))
    {
        return hr;
    }
    
    m_pSinkWriter = spSinkWriter.Detach();
    
    return S_OK;
}

HRESULT MP4Muxer::ConfigureVideoStream(const TrackInfo& trackInfo, DWORD* pStreamIndex)
{
    if (!m_pSinkWriter || !trackInfo.pMediaType)
    {
        return E_UNEXPECTED;
    }
    
    // Add stream to sink writer
    DWORD streamIndex = 0;
    HRESULT hr = m_pSinkWriter->AddStream(trackInfo.pMediaType, &streamIndex);
    if (FAILED(hr))
    {
        return hr;
    }
    
    // Set track name as metadata if provided
    if (!trackInfo.name.empty())
    {
        // Set stream name using IMFAttributes
        // Note: Stream names may not be widely supported in MP4 format
        // but we set it anyway for tools that do support it
        wil::com_ptr<IMFAttributes> pAttributes;
        HRESULT hrName = m_pSinkWriter->GetServiceForStream(
            streamIndex,
            GUID_NULL,
            __uuidof(IMFAttributes),
            pAttributes.put_void()
        );
        
        if (SUCCEEDED(hrName) && pAttributes)
        {
            hrName = pAttributes->SetString(MF_SD_STREAM_NAME, trackInfo.name.c_str());
        }
        // Ignore failures - stream names are optional
    }
    
    *pStreamIndex = streamIndex;
    
    return S_OK;
}

HRESULT MP4Muxer::ConfigureAudioStream(const TrackInfo& trackInfo, DWORD* pStreamIndex)
{
    if (!m_pSinkWriter || !trackInfo.pMediaType)
    {
        return E_UNEXPECTED;
    }
    
    // Add stream to sink writer
    DWORD streamIndex = 0;
    HRESULT hr = m_pSinkWriter->AddStream(trackInfo.pMediaType, &streamIndex);
    if (FAILED(hr))
    {
        return hr;
    }
    
    // Set track name as metadata if provided
    if (!trackInfo.name.empty())
    {
        // Set stream name using IMFAttributes
        // This helps professional tools identify audio tracks (e.g., "Desktop Audio", "Microphone")
        wil::com_ptr<IMFAttributes> pAttributes;
        HRESULT hrName = m_pSinkWriter->GetServiceForStream(
            streamIndex,
            GUID_NULL,
            __uuidof(IMFAttributes),
            pAttributes.put_void()
        );
        
        if (SUCCEEDED(hrName) && pAttributes)
        {
            hrName = pAttributes->SetString(MF_SD_STREAM_NAME, trackInfo.name.c_str());
        }
        // Ignore failures - stream names are optional
    }
    
    *pStreamIndex = streamIndex;
    
    return S_OK;
}

HRESULT MP4Muxer::ConvertTextureToSample(ID3D11Texture2D* pTexture, IMFSample** ppSample)
{
    // This is a placeholder - actual implementation would:
    // 1. Copy texture to staging texture
    // 2. Map staging texture and read pixel data
    // 3. Create IMFMediaBuffer with pixel data
    // 4. Create IMFSample and add buffer
    
    // For now, return not implemented
    // This will be completed during ScreenRecorder integration (Task 5)
    return E_NOTIMPL;
}

bool MP4Muxer::CheckInterleaving(uint32_t trackIndex, int64_t timestamp)
{
    // Check if timestamp is within acceptable interleave delta
    // This ensures samples from different tracks are properly interleaved
    
    if (m_tracks.empty())
    {
        return true;
    }
    
    // Find the maximum timestamp across all other tracks
    int64_t maxOtherTimestamp = 0;
    bool foundOther = false;
    
    for (uint32_t i = 0; i < m_tracks.size(); i++)
    {
        if (i != trackIndex && m_tracks[i].isEnabled && m_tracks[i].lastTimestamp > 0)
        {
            maxOtherTimestamp = std::max(maxOtherTimestamp, m_tracks[i].lastTimestamp);
            foundOther = true;
        }
    }
    
    if (!foundOther)
    {
        return true; // No other tracks to compare against
    }
    
    // Check if timestamp delta is within limits
    int64_t delta = std::abs(timestamp - maxOtherTimestamp);
    
    // Convert from 100-nanosecond units to microseconds
    int64_t deltaMicroseconds = delta / 10;
    
    return deltaMicroseconds <= m_maxInterleaveDelta;
}

void MP4Muxer::UpdateStatistics(uint32_t trackIndex, int64_t timestamp)
{
    if (trackIndex >= m_tracks.size())
    {
        return;
    }
    
    TrackData& track = m_tracks[trackIndex];
    
    track.samplesWritten++;
    track.lastTimestamp = timestamp;
    
    // Update duration (maximum timestamp across all tracks)
    if (timestamp > m_duration)
    {
        m_duration = timestamp;
    }
    
    // Note: Bytes written would need to be tracked from IMFSample
    // This is a simplified implementation
}
