#include "pch.h"
#include "WindowsGraphicsCaptureSession.h"
#include "FrameArrivedHandler.h"
#include "SystemAudioInputSource.h"
#include "MediaClock.h"
#include "WindowsGraphicsCaptureHelpers.h"

using namespace WindowsGraphicsCaptureHelpers;

WindowsGraphicsCaptureSession::WindowsGraphicsCaptureSession()
    : m_frameHandler(nullptr)
    , m_audioInputSource(std::make_unique<SystemAudioInputSource>())
    , m_isActive(false)
{
    m_frameArrivedEventToken.value = 0;
}

WindowsGraphicsCaptureSession::~WindowsGraphicsCaptureSession()
{
    Stop();
}

bool WindowsGraphicsCaptureSession::Start(const CaptureSessionConfig& config, HRESULT* outHr)
{
    // Delegate to existing Start method
    // TODO: Use config.frameRate, config.videoBitrate, and config.audioBitrate when implementing encoder settings
    return Start(config.hMonitor, config.outputPath, config.audioEnabled, outHr);
}

bool WindowsGraphicsCaptureSession::Start(HMONITOR hMonitor, const wchar_t* outputPath, bool audioEnabled, HRESULT* outHr)
{
    HRESULT hr = S_OK;

    // Get the graphics capture item
    wil::com_ptr<IGraphicsCaptureItemInterop> interop = GetGraphicsCaptureItemInterop(&hr);
    if (!interop)
    {
        if (outHr) *outHr = hr;
        return false;
    }

    wil::com_ptr<IGraphicsCaptureItem> captureItem = GetGraphicsCaptureItemForMonitor(hMonitor, interop, &hr);
    if (!captureItem)
    {
        if (outHr) *outHr = hr;
        return false;
    }

    // Initialize D3D device
    D3DDeviceAndContext d3d = InitializeD3D(&hr);
    if (FAILED(hr))
    {
        if (outHr) *outHr = hr;
        return false;
    }

    wil::com_ptr<ID3D11Device> device = d3d.device;
    wil::com_ptr<IDirect3DDevice> abiDevice = CreateDirect3DDevice(device, &hr);
    if (FAILED(hr))
    {
        if (outHr) *outHr = hr;
        return false;
    }

    // Create frame pool
    m_framePool = CreateCaptureFramePool(captureItem, abiDevice, &hr);
    if (FAILED(hr))
    {
        if (outHr) *outHr = hr;
        return false;
    }

    // Create capture session
    m_captureSession = CreateCaptureSession(m_framePool, captureItem, &hr);
    if (FAILED(hr))
    {
        if (outHr) *outHr = hr;
        return false;
    }

    // Get capture item size
    SizeInt32 size{};
    hr = captureItem->get_Size(&size);
    if (FAILED(hr))
    {
        if (outHr) *outHr = hr;
        return false;
    }
    
    // Initialize video sink writer
    if (!m_sinkWriter.Initialize(outputPath, device.get(), size.Width, size.Height, &hr))
    {
        if (outHr) *outHr = hr;
        return false;
    }
    
    // Initialize audio capture device (true = loopback mode for system audio)
    if (m_audioInputSource->Initialize(&hr))
    {
        // Initialize audio stream on sink writer
        WAVEFORMATEX* audioFormat = m_audioInputSource->GetFormat();
        if (audioFormat && m_sinkWriter.InitializeAudioStream(audioFormat, &hr))
        {
            // Set the sink writer on audio input source so it can write samples
            m_audioInputSource->SetSinkWriter(&m_sinkWriter);
                    
            // Start audio capture
            m_audioInputSource->SetEnabled(audioEnabled);
            hr = m_audioInputSource->Start(&hr);
            if (FAILED(hr))
            {
                if (outHr) *outHr = hr;
                return false;
            }
        }
    }
    
    // Register frame arrived handler
    wil::com_ptr<MP4SinkWriter> sinkWriterPtr(&m_sinkWriter);
    m_frameArrivedEventToken = RegisterFrameArrivedHandler(m_framePool, sinkWriterPtr, &m_frameHandler, &hr);

    // Start video capture
    hr = m_captureSession->StartCapture();
    if (FAILED(hr))
    {
        // If video capture fails, stop audio if it was started
        if (m_audioInputSource->IsRunning())
        {
            m_audioInputSource->Stop();
        }
        if (outHr) *outHr = hr;
        return false;
    }

    m_isActive = true;
    if (outHr) *outHr = S_OK;
    return true;
}

void WindowsGraphicsCaptureSession::Stop()
{
    if (!m_isActive)
    {
        return;
    }

    // Stop audio capture first
    if (m_audioInputSource)
    {
        m_audioInputSource->Stop();
    }
    
    // Remove the event registration
    if (m_framePool)
    {
        m_framePool->remove_FrameArrived(m_frameArrivedEventToken);
    }

    m_frameArrivedEventToken.value = 0;
    
    // Stop the frame handler and release our reference
    if (m_frameHandler)
    {
        m_frameHandler->Stop();
        m_frameHandler->Release();
        m_frameHandler = nullptr;
    }
    
    // Finalize MP4 file after both streams have stopped
    m_sinkWriter.Finalize();

    // Release capture session
    if (m_captureSession)
    {
        m_captureSession.reset();
    }

    // Release frame pool
    if (m_framePool)
    {
        m_framePool.reset();
    }

    m_isActive = false;
}

void WindowsGraphicsCaptureSession::ToggleAudioCapture(bool enabled)
{
    // Only toggle if audio capture is currently running
    if (m_audioInputSource && m_audioInputSource->IsRunning())
    {
        m_audioInputSource->SetEnabled(enabled);
    }
}
