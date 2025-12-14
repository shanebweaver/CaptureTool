#include "pch.h"
#include "ScreenRecorder.h"
#include "MP4SinkWriter.h"
#include "FrameArrivedHandler.h"
#include "AudioCaptureManager.h"
#include "GraphicsCaptureHelpers.cpp"

using namespace GraphicsCaptureHelpers;

static wil::com_ptr<ABI::Windows::Graphics::Capture::IGraphicsCaptureSession> g_session;
static wil::com_ptr<ABI::Windows::Graphics::Capture::IDirect3D11CaptureFramePool> g_framePool;
static EventRegistrationToken g_frameArrivedEventToken;
static MP4SinkWriter g_sinkWriter;
static std::unique_ptr<AudioCaptureManager> g_audioCapture;

// Shared QPC timestamp used for synchronizing video frames with audio samples
extern LONGLONG g_recordingStartQPC = 0;

// Exported API
extern "C"
{
    __declspec(dllexport) bool TryStartRecording(HMONITOR hMonitor, const wchar_t* outputPath, bool enableAudio)
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

        // Initialize audio capture if enabled
        WAVEFORMATEX* audioFormat = nullptr;
        if (enableAudio)
        {
            g_audioCapture = std::make_unique<AudioCaptureManager>();
            
            // Set up callback for audio samples
            auto audioCallback = [](BYTE* data, UINT32 dataSize, LONGLONG timestamp) {
                if (data && dataSize > 0)
                {
                    g_sinkWriter.WriteAudioSample(data, dataSize, timestamp);
                }
            };

            hr = g_audioCapture->Initialize(audioCallback);
            if (FAILED(hr))
            {
                g_audioCapture.reset();
                enableAudio = false; // Continue without audio
            }
            else
            {
                audioFormat = g_audioCapture->GetAudioFormat();
            }
        }

        if (!g_sinkWriter.Initialize(outputPath, device.get(), size.Width, size.Height, enableAudio, audioFormat, &hr))
        {
            if (g_audioCapture) g_audioCapture.reset();
            return false;
        }
        
        // Record start time for synchronization
        LARGE_INTEGER qpc;
        QueryPerformanceCounter(&qpc);
        g_recordingStartQPC = qpc.QuadPart;
        
        g_frameArrivedEventToken = RegisterFrameArrivedHandler(g_framePool, &g_sinkWriter , &hr);

        // Start audio capture before video
        if (g_audioCapture)
        {
            hr = g_audioCapture->Start();
            if (FAILED(hr))
            {
                // Continue without audio if start fails
                g_audioCapture.reset();
            }
        }

        hr = g_session->StartCapture();
        if (FAILED(hr))
        {
            if (g_audioCapture) g_audioCapture.reset();
            return false;
        }

        return true;
    }

    __declspec(dllexport) void TryPauseRecording()
    {
        // Not implemented yet, don't worry about it.
    }

    __declspec(dllexport) void TryStopRecording()
    {
        // Stop audio capture first
        if (g_audioCapture)
        {
            g_audioCapture->Stop();
            g_audioCapture.reset();
        }

        if (g_framePool)
        {
            g_framePool->remove_FrameArrived(g_frameArrivedEventToken);
        }

        g_frameArrivedEventToken.value = 0;
        g_sinkWriter.Finalize();

        if (g_session)
        {
            g_session.reset();
        }

        if (g_framePool)
        {
            g_framePool.reset();
        }
        
        // Reset shared timestamp
        g_recordingStartQPC = 0;
    }
}