#include "pch.h"
#include "ScreenRecorder.h"
#include "MP4SinkWriter.h"
#include "FrameArrivedHandler.h"
#include "AudioCaptureHandler.h"
#include "GraphicsCaptureHelpers.cpp"

using namespace GraphicsCaptureHelpers;

static wil::com_ptr<ABI::Windows::Graphics::Capture::IGraphicsCaptureSession> g_session;
static wil::com_ptr<ABI::Windows::Graphics::Capture::IDirect3D11CaptureFramePool> g_framePool;
static EventRegistrationToken g_frameArrivedEventToken;
static FrameArrivedHandler* g_frameHandler = nullptr;
static MP4SinkWriter g_sinkWriter;
static AudioCaptureHandler g_audioHandler;

// Exported API
extern "C"
{
    __declspec(dllexport) bool TryStartRecording(HMONITOR hMonitor, const wchar_t* outputPath, bool captureAudio)
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
        //wil::com_ptr<ID3D11DeviceContext> device = d3d.context;
        wil::com_ptr<IDirect3DDevice> abiDevice = CreateDirect3DDevice(device, &hr);
        if (FAILED(hr))
        {
            return false;
        }

        g_framePool = CreateCaptureFramePool(captureItem, abiDevice, &hr);
        if (FAILED(hr))
        {
            return false;
        }

        g_session = CreateCaptureSession(g_framePool, captureItem, &hr);
        if (FAILED(hr))
        {
            return false;
        }

        SizeInt32 size{};
        hr = captureItem->get_Size(&size);
        if (FAILED(hr)) return false;
        
        // Phase 4: Initialize video sink writer
        if (!g_sinkWriter.Initialize(outputPath, device.get(), size.Width, size.Height, &hr))
        {
            return false;
        }
        
        // Phase 4: Initialize and start audio capture if requested
        bool audioEnabled = false;  // Track actual audio capture state
        if (captureAudio)
        {
            // Initialize audio capture device (true = loopback mode for system audio)
            if (g_audioHandler.Initialize(true, &hr))
            {
                // Initialize audio stream on sink writer
                WAVEFORMATEX* audioFormat = g_audioHandler.GetFormat();
                if (audioFormat && g_sinkWriter.InitializeAudioStream(audioFormat, &hr))
                {
                    // Set the sink writer on audio handler so it can write samples
                    g_audioHandler.SetSinkWriter(&g_sinkWriter);
                    
                    // Start audio capture
                    if (g_audioHandler.Start(&hr))
                    {
                        audioEnabled = true;
                    }
                }
            }
        }
        
        g_frameArrivedEventToken = RegisterFrameArrivedHandler(g_framePool, &g_sinkWriter, &g_frameHandler, &hr);

        hr = g_session->StartCapture();
        if (FAILED(hr))
        {
            // If video capture fails, stop audio if it was started
            if (audioEnabled)
            {
                g_audioHandler.Stop();
            }
            return false;
        }

        return true;
    }

    __declspec(dllexport) void TryPauseRecording()
    {
        // Not implemented yet, don't worry about it.
    }

    __declspec(dllexport) void TryResumeRecording()
    {
        // Not implemented yet, don't worry about it.
    }

    __declspec(dllexport) void TryStopRecording()
    {
        // Phase 4: Stop audio capture first
        g_audioHandler.Stop();
        
        // Stop frame handler background thread before removing event
        if (g_frameHandler)
        {
            g_frameHandler->Stop();
            g_frameHandler->Release(); // Release our reference
            g_frameHandler = nullptr;
        }
        
        if (g_framePool)
        {
            g_framePool->remove_FrameArrived(g_frameArrivedEventToken);
        }

        g_frameArrivedEventToken.value = 0;
        
        // Finalize MP4 file after both streams have stopped
        g_sinkWriter.Finalize();

        if (g_session)
        {
            g_session.reset();
        }

        if (g_framePool)
        {
            g_framePool.reset();
        }
    }

    __declspec(dllexport) void TryToggleAudioCapture(bool enabled)
    {
        // Only toggle if audio capture is currently running
        // This prevents issues when audio was not started initially
        if (g_audioHandler.IsRunning())
        {
            g_audioHandler.SetEnabled(enabled);
        }
    }
}