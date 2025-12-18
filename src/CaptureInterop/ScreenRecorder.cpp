#include "pch.h"
#include "ScreenRecorder.h"
#include "MP4SinkWriter.h"
#include "FrameArrivedHandler.h"
#include "AudioCaptureHandler.h"
#include "ScreenCaptureSource.h"
#include "DesktopAudioSource.h"
#include "MicrophoneAudioSource.h"
#include "GraphicsCaptureHelpers.cpp"

using namespace GraphicsCaptureHelpers;

// Legacy path globals (for backward compatibility)
static wil::com_ptr<ABI::Windows::Graphics::Capture::IGraphicsCaptureSession> g_session;
static wil::com_ptr<ABI::Windows::Graphics::Capture::IDirect3D11CaptureFramePool> g_framePool;
static EventRegistrationToken g_frameArrivedEventToken;
static FrameArrivedHandler* g_frameHandler = nullptr;
static MP4SinkWriter g_sinkWriter;
static AudioCaptureHandler g_audioHandler;

// New source-based globals (Phase 2 implementation)
static ScreenCaptureSource* g_videoSource = nullptr;
static DesktopAudioSource* g_desktopAudioSource = nullptr;
static MicrophoneAudioSource* g_microphoneSource = nullptr;
static D3DDeviceAndContext g_d3dDevice;
static bool g_useSourceAbstraction = false;  // Flag to track which path is active

// Exported API
extern "C"
{
    // New source-based recording with microphone support
    __declspec(dllexport) bool TryStartRecording(
        HMONITOR hMonitor,
        const wchar_t* outputPath,
        bool captureDesktopAudio,
        bool captureMicrophone)
    {
        HRESULT hr = S_OK;
        g_useSourceAbstraction = true;
        
        // Initialize D3D11 device (reused for video)
        g_d3dDevice = InitializeD3D(&hr);
        if (FAILED(hr))
        {
            g_useSourceAbstraction = false;
            return false;
        }
        
        // Create and initialize video source
        g_videoSource = new ScreenCaptureSource();
        g_videoSource->SetMonitor(hMonitor);
        g_videoSource->SetDevice(g_d3dDevice.device.get());
        
        if (!g_videoSource->Initialize())
        {
            g_videoSource->Release();
            g_videoSource = nullptr;
            g_useSourceAbstraction = false;
            return false;
        }
        
        // Get video resolution
        UINT32 width, height;
        g_videoSource->GetResolution(width, height);
        
        // Initialize MP4 sink writer
        if (!g_sinkWriter.Initialize(outputPath, g_d3dDevice.device.get(), width, height, &hr))
        {
            g_videoSource->Release();
            g_videoSource = nullptr;
            g_useSourceAbstraction = false;
            return false;
        }
        
        // Set up video callback
        g_videoSource->SetFrameCallback([](ID3D11Texture2D* texture, LONGLONG timestamp) {
            g_sinkWriter.WriteFrame(texture, timestamp);
        });
        
        // Create desktop audio source if requested
        bool desktopAudioEnabled = false;
        if (captureDesktopAudio)
        {
            g_desktopAudioSource = new DesktopAudioSource();
            
            if (g_desktopAudioSource->Initialize())
            {
                WAVEFORMATEX* audioFormat = g_desktopAudioSource->GetFormat();
                if (audioFormat && g_sinkWriter.InitializeAudioStream(audioFormat, &hr))
                {
                    g_desktopAudioSource->SetAudioCallback([](const BYTE* data, UINT32 frames, LONGLONG ts) {
                        g_sinkWriter.WriteAudioSample(data, frames, ts);
                    });
                    
                    if (g_desktopAudioSource->Start(&hr))
                    {
                        desktopAudioEnabled = true;
                    }
                }
            }
            
            if (!desktopAudioEnabled && g_desktopAudioSource)
            {
                g_desktopAudioSource->Release();
                g_desktopAudioSource = nullptr;
            }
        }
        
        // Create microphone source if requested
        bool microphoneEnabled = false;
        if (captureMicrophone)
        {
            g_microphoneSource = new MicrophoneAudioSource();
            
            if (g_microphoneSource->Initialize())
            {
                WAVEFORMATEX* micFormat = g_microphoneSource->GetFormat();
                
                // For Phase 2: microphone is captured but not yet mixed with desktop audio
                // Phase 3 will add mixing and multi-track support
                // For now, just capture to test the infrastructure (no write to avoid conflicts)
                if (micFormat)
                {
                    g_microphoneSource->SetAudioCallback([](const BYTE* data, UINT32 frames, LONGLONG ts) {
                        // TODO Phase 3: Route to mixer instead of direct write
                        // For now, just capture (no write to avoid conflicts with desktop audio)
                    });
                    
                    if (g_microphoneSource->Start(&hr))
                    {
                        microphoneEnabled = true;
                    }
                }
            }
            
            if (!microphoneEnabled && g_microphoneSource)
            {
                g_microphoneSource->Release();
                g_microphoneSource = nullptr;
            }
        }
        
        // Start video capture
        if (!g_videoSource->Start())
        {
            // Cleanup on failure
            if (g_desktopAudioSource)
            {
                g_desktopAudioSource->Stop();
                g_desktopAudioSource->Release();
                g_desktopAudioSource = nullptr;
            }
            if (g_microphoneSource)
            {
                g_microphoneSource->Stop();
                g_microphoneSource->Release();
                g_microphoneSource = nullptr;
            }
            g_videoSource->Release();
            g_videoSource = nullptr;
            g_useSourceAbstraction = false;
            return false;
        }
        
        return true;
    }
    
    // Legacy signature (for existing callers) - 3 parameter version
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
        if (g_useSourceAbstraction)
        {
            // New source-based path
            // Stop all audio sources first
            if (g_microphoneSource)
            {
                g_microphoneSource->Stop();
                g_microphoneSource->Release();
                g_microphoneSource = nullptr;
            }
            
            if (g_desktopAudioSource)
            {
                g_desktopAudioSource->Stop();
                g_desktopAudioSource->Release();
                g_desktopAudioSource = nullptr;
            }
            
            // Stop video source
            if (g_videoSource)
            {
                g_videoSource->Stop();
                g_videoSource->Release();
                g_videoSource = nullptr;
            }
            
            // Finalize MP4 file
            g_sinkWriter.Finalize();
            
            // Reset sink writer to fresh state
            g_sinkWriter = MP4SinkWriter();
            g_useSourceAbstraction = false;
        }
        else
        {
            // Legacy path (maintain backward compatibility)
            // Phase 4: Stop audio capture first
            g_audioHandler.Stop();
            
            // First remove the event registration (releases the event system's reference)
            if (g_framePool)
            {
                g_framePool->remove_FrameArrived(g_frameArrivedEventToken);
            }

            g_frameArrivedEventToken.value = 0;
            
            // Then stop the frame handler and release our reference
            // This ensures clean shutdown: event unregistered -> thread stopped -> resources released
            if (g_frameHandler)
            {
                g_frameHandler->Stop();
                g_frameHandler->Release();
                g_frameHandler = nullptr;
            }
            
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

            // Reset sink writer and audio handler to fresh state for next recording
            g_sinkWriter = MP4SinkWriter();
            g_audioHandler.~AudioCaptureHandler();
            new (&g_audioHandler) AudioCaptureHandler();
        }
    }

    __declspec(dllexport) void TryToggleAudioCapture(bool enabled)
    {
        if (g_useSourceAbstraction)
        {
            // New source-based path: toggle desktop audio only
            // Phase 3 will add per-source control in UI
            if (g_desktopAudioSource)
            {
                g_desktopAudioSource->SetEnabled(enabled);
            }
        }
        else
        {
            // Legacy path
            // Only toggle if audio capture is currently running
            // This prevents issues when audio was not started initially
            if (g_audioHandler.IsRunning())
            {
                g_audioHandler.SetEnabled(enabled);
            }
        }
    }
    
    // Audio device enumeration
    __declspec(dllexport) int EnumerateAudioCaptureDevices(AudioDeviceInfo** devices)
    {
        if (!devices)
        {
            return 0;
        }
        
        AudioDeviceEnumerator enumerator;
        std::vector<AudioDeviceInfo> deviceList;
        
        if (!enumerator.EnumerateCaptureDevices(deviceList))
        {
            *devices = nullptr;
            return 0;
        }
        
        if (deviceList.empty())
        {
            *devices = nullptr;
            return 0;
        }
        
        // Allocate array
        AudioDeviceInfo* devicesArray = new AudioDeviceInfo[deviceList.size()];
        for (size_t i = 0; i < deviceList.size(); i++)
        {
            devicesArray[i] = deviceList[i];
        }
        
        *devices = devicesArray;
        return static_cast<int>(deviceList.size());
    }
    
    __declspec(dllexport) int EnumerateAudioRenderDevices(AudioDeviceInfo** devices)
    {
        if (!devices)
        {
            return 0;
        }
        
        AudioDeviceEnumerator enumerator;
        std::vector<AudioDeviceInfo> deviceList;
        
        if (!enumerator.EnumerateRenderDevices(deviceList))
        {
            *devices = nullptr;
            return 0;
        }
        
        if (deviceList.empty())
        {
            *devices = nullptr;
            return 0;
        }
        
        // Allocate array
        AudioDeviceInfo* devicesArray = new AudioDeviceInfo[deviceList.size()];
        for (size_t i = 0; i < deviceList.size(); i++)
        {
            devicesArray[i] = deviceList[i];
        }
        
        *devices = devicesArray;
        return static_cast<int>(deviceList.size());
    }
    
    __declspec(dllexport) void FreeAudioDeviceInfo(AudioDeviceInfo* devices)
    {
        if (devices)
        {
            delete[] devices;
        }
    }
    
    // Source management exports
    __declspec(dllexport) SourceHandle RegisterVideoSource(void* sourcePtr)
    {
        if (!sourcePtr)
        {
            return INVALID_SOURCE_HANDLE;
        }
        
        IMediaSource* source = static_cast<IMediaSource*>(sourcePtr);
        return SourceManager::Instance().RegisterSource(source);
    }
    
    __declspec(dllexport) SourceHandle RegisterAudioSource(void* sourcePtr)
    {
        if (!sourcePtr)
        {
            return INVALID_SOURCE_HANDLE;
        }
        
        IMediaSource* source = static_cast<IMediaSource*>(sourcePtr);
        return SourceManager::Instance().RegisterSource(source);
    }
    
    __declspec(dllexport) void UnregisterSource(SourceHandle handle)
    {
        SourceManager::Instance().UnregisterSource(handle);
    }
    
    __declspec(dllexport) bool StartAllSources()
    {
        return SourceManager::Instance().StartAll();
    }
    
    __declspec(dllexport) void StopAllSources()
    {
        SourceManager::Instance().StopAll();
    }
    
    __declspec(dllexport) int GetSourceCount()
    {
        return static_cast<int>(SourceManager::Instance().GetSourceCount());
    }
}