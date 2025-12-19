#include "pch.h"
#include "ScreenRecorderImpl.h"
#include "FrameArrivedHandler.h"
#include "AudioCaptureHandler.h"
#include "GraphicsCaptureHelpers.cpp"

using namespace GraphicsCaptureHelpers;

ScreenRecorderImpl::ScreenRecorderImpl()
    : m_frameHandler(nullptr)
    , m_audioHandler(std::make_unique<AudioCaptureHandler>())
{
    m_frameArrivedEventToken.value = 0;
}

ScreenRecorderImpl::~ScreenRecorderImpl()
{
    StopRecording();
}

bool ScreenRecorderImpl::StartRecording(HMONITOR hMonitor, const wchar_t* outputPath, bool captureAudio)
{
    HRESULT hr = S_OK;

    wil::com_ptr<IGraphicsCaptureItemInterop> interop = GetGraphicsCaptureItemInterop(&hr);
    if (!interop)
    {
        return false;
    }

    wil::com_ptr<IGraphicsCaptureItem> captureItem = GetGraphicsCaptureItemForMonitor(hMonitor, interop, &hr);
    if (!captureItem)
    {
        return false;
    }

    D3DDeviceAndContext d3d = InitializeD3D(&hr);
    if (FAILED(hr))
    {
        return false;
    }

    wil::com_ptr<ID3D11Device> device = d3d.device;
    wil::com_ptr<IDirect3DDevice> abiDevice = CreateDirect3DDevice(device, &hr);
    if (FAILED(hr))
    {
        return false;
    }

    m_framePool = CreateCaptureFramePool(captureItem, abiDevice, &hr);
    if (FAILED(hr))
    {
        return false;
    }

    m_session = CreateCaptureSession(m_framePool, captureItem, &hr);
    if (FAILED(hr))
    {
        return false;
    }

    SizeInt32 size{};
    hr = captureItem->get_Size(&size);
    if (FAILED(hr)) return false;
    
    // Initialize video sink writer
    if (!m_sinkWriter.Initialize(outputPath, device.get(), size.Width, size.Height, &hr))
    {
        return false;
    }
    
    // Initialize and start audio capture if requested
    bool audioEnabled = false;
    if (captureAudio)
    {
        // Initialize audio capture device (true = loopback mode for system audio)
        if (m_audioHandler->Initialize(true, &hr))
        {
            // Initialize audio stream on sink writer
            WAVEFORMATEX* audioFormat = m_audioHandler->GetFormat();
            if (audioFormat && m_sinkWriter.InitializeAudioStream(audioFormat, &hr))
            {
                // Set the sink writer on audio handler so it can write samples
                m_audioHandler->SetSinkWriter(&m_sinkWriter);
                
                // Start audio capture
                if (m_audioHandler->Start(&hr))
                {
                    audioEnabled = true;
                }
            }
        }
    }
    else
    {
        // For video-only recording, begin writing immediately
        // This ensures timeline is established before frames arrive
        if (!m_sinkWriter.BeginWritingForVideoOnly(&hr))
        {
            return false;
        }
    }
    
    wil::com_ptr<MP4SinkWriter> sinkWriterPtr(&m_sinkWriter);
    m_frameArrivedEventToken = RegisterFrameArrivedHandler(m_framePool, sinkWriterPtr, &m_frameHandler, &hr);

    hr = m_session->StartCapture();
    if (FAILED(hr))
    {
        // If video capture fails, stop audio if it was started
        if (audioEnabled)
        {
            m_audioHandler->Stop();
        }
        return false;
    }

    return true;
}

void ScreenRecorderImpl::PauseRecording()
{
    // Not implemented yet
}

void ScreenRecorderImpl::ResumeRecording()
{
    // Not implemented yet
}

void ScreenRecorderImpl::StopRecording()
{
    // Stop audio capture first
    if (m_audioHandler)
    {
        m_audioHandler->Stop();
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

    if (m_session)
    {
        m_session.reset();
    }

    if (m_framePool)
    {
        m_framePool.reset();
    }

    // Reset sink writer and audio handler to fresh state for next recording
    m_sinkWriter = MP4SinkWriter();
    m_audioHandler = std::make_unique<AudioCaptureHandler>();
}

void ScreenRecorderImpl::ToggleAudioCapture(bool enabled)
{
    // Only toggle if audio capture is currently running
    if (m_audioHandler && m_audioHandler->IsRunning())
    {
        m_audioHandler->SetEnabled(enabled);
    }
}
