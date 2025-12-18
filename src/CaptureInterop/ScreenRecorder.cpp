#include "pch.h"
#include "ScreenRecorder.h"
#include "MP4SinkWriter.h"
#include "FrameArrivedHandler.h"
#include "AudioCaptureHandler.h"
#include "ScreenCaptureSource.h"
#include "DesktopAudioSource.h"
#include "MicrophoneAudioSource.h"
#include "AudioRoutingConfig.h"
#include "AudioMixer.h"
#include "EncoderPipeline.h"
#include "GraphicsCaptureHelpers.cpp"
#include <thread>
#include <atomic>

using namespace GraphicsCaptureHelpers;

// Legacy path globals (for backward compatibility)
static wil::com_ptr<ABI::Windows::Graphics::Capture::IGraphicsCaptureSession> g_session;
static wil::com_ptr<ABI::Windows::Graphics::Capture::IDirect3D11CaptureFramePool> g_framePool;
static EventRegistrationToken g_frameArrivedEventToken;
static FrameArrivedHandler* g_frameHandler = nullptr;
static MP4SinkWriter g_sinkWriter;
static AudioCaptureHandler g_audioHandler;

// New source-based globals (Phase 2+3 implementation)
static ScreenCaptureSource* g_videoSource = nullptr;
static DesktopAudioSource* g_desktopAudioSource = nullptr;
static MicrophoneAudioSource* g_microphoneSource = nullptr;
static AudioMixer* g_audioMixer = nullptr;
static AudioRoutingConfig g_routingConfig;
static D3DDeviceAndContext g_d3dDevice;
static bool g_useSourceAbstraction = false;  // Flag to track which path is active
static uint64_t g_desktopAudioSourceId = 0;
static uint64_t g_microphoneSourceId = 0;

// Phase 4: Encoder pipeline globals
static EncoderPipeline* g_encoderPipeline = nullptr;
static bool g_useEncoderPipeline = false;  // Feature flag for Phase 4 encoder pipeline

// Phase 3: Audio mixing thread for periodic mixer polling
static std::thread g_mixerThread;
static std::atomic<bool> g_mixerThreadRunning = false;
static LONGLONG g_mixerTimestamp = 0;

// Phase 3: Mixer thread function
static void MixerThreadProc()
{
    // Set thread priority
    SetThreadPriority(GetCurrentThread(), THREAD_PRIORITY_ABOVE_NORMAL);
    
    // Calculate frames per buffer (10ms worth at 48kHz)
    const UINT32 framesPerBuffer = 480;  // 48000 * 0.010 = 480 frames
    const UINT32 bufferSize = framesPerBuffer * 2 * 2;  // stereo * 16-bit (2 bytes per sample)
    std::vector<BYTE> mixBuffer(bufferSize);
    
    // Sleep duration for 10ms intervals
    const DWORD sleepMs = 10;
    
    while (g_mixerThreadRunning.load())
    {
        if (g_audioMixer)
        {
            if (g_useEncoderPipeline && g_encoderPipeline)
            {
                // Phase 4: Route through encoder pipeline
                if (g_routingConfig.IsMixedMode())
                {
                    // Mixed mode: Mix all sources and write to single track (track 0)
                    UINT32 framesMixed = g_audioMixer->MixAudio(mixBuffer.data(), framesPerBuffer, g_mixerTimestamp);
                    if (framesMixed > 0)
                    {
                        g_encoderPipeline->ProcessAudioSamples(mixBuffer.data(), framesMixed, g_mixerTimestamp, 0);
                    }
                }
                else
                {
                    // Separate track mode: Write each source to its assigned track (Phase 4.4)
                    std::vector<uint64_t> sourceIds = g_audioMixer->GetSourceIds();
                    
                    for (uint64_t sourceId : sourceIds)
                    {
                        // Get the track assignment for this source
                        int trackIndex = g_routingConfig.GetSourceTrack(sourceId);
                        if (trackIndex < 0 || trackIndex >= 6)
                        {
                            trackIndex = 0;  // Default to track 0 if invalid
                        }
                        
                        // Get audio data for this specific source
                        UINT32 framesRead = g_audioMixer->GetSourceAudio(sourceId, mixBuffer.data(), framesPerBuffer, g_mixerTimestamp);
                        
                        if (framesRead > 0)
                        {
                            // Write to the source's assigned track
                            g_encoderPipeline->ProcessAudioSamples(mixBuffer.data(), framesRead, g_mixerTimestamp, trackIndex);
                        }
                    }
                }
            }
            else
            {
                // Phase 3: Legacy MP4SinkWriter path
                UINT32 framesMixed = g_audioMixer->MixAudio(mixBuffer.data(), framesPerBuffer, g_mixerTimestamp);
                
                if (framesMixed > 0)
                {
                    if (g_routingConfig.IsMixedMode())
                    {
                        // Mixed mode: Write to single track
                        g_sinkWriter.WriteAudioSample(mixBuffer.data(), framesMixed, g_mixerTimestamp);
                    }
                    else
                    {
                        // Separate track mode: Write mixed audio to track 0 (Phase 3 limitation)
                        g_sinkWriter.WriteAudioSample(0, mixBuffer.data(), framesMixed, g_mixerTimestamp);
                    }
                }
            }
            
            // Advance timestamp (100ns units)
            // frames * 10,000,000 / sampleRate
            g_mixerTimestamp += (framesPerBuffer * 10000000LL) / 48000;
        }
        
        Sleep(sleepMs);
    }
}

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
        
        // Phase 4: Choose pipeline based on feature flag
        if (g_useEncoderPipeline)
        {
            // Initialize EncoderPipeline (Phase 4 path)
            g_encoderPipeline = new EncoderPipeline();
            
            EncoderPipelineConfig config = {};
            config.outputPath = outputPath;
            config.videoWidth = width;
            config.videoHeight = height;
            config.fps = 30;
            config.videoPreset = g_videoPreset;  // Use global configuration
            config.audioQuality = g_audioQuality;  // Use global configuration
            config.audioSampleRate = 48000;
            config.audioChannels = 2;
            
            if (FAILED(g_encoderPipeline->Initialize(g_d3dDevice.device.get(), config)))
            {
                delete g_encoderPipeline;
                g_encoderPipeline = nullptr;
                g_videoSource->Release();
                g_videoSource = nullptr;
                g_useSourceAbstraction = false;
                return false;
            }
            
            if (FAILED(g_encoderPipeline->Start()))
            {
                delete g_encoderPipeline;
                g_encoderPipeline = nullptr;
                g_videoSource->Release();
                g_videoSource = nullptr;
                g_useSourceAbstraction = false;
                return false;
            }
            
            // Set up video callback to route through encoder pipeline
            g_videoSource->SetFrameCallback([](ID3D11Texture2D* texture, LONGLONG timestamp) {
                if (g_encoderPipeline)
                {
                    g_encoderPipeline->ProcessVideoFrame(texture, timestamp);
                }
            });
        }
        else
        {
            // Initialize MP4 sink writer (Phase 3 legacy path)
            if (!g_sinkWriter.Initialize(outputPath, g_d3dDevice.device.get(), width, height, &hr))
            {
                g_videoSource->Release();
                g_videoSource = nullptr;
                g_useSourceAbstraction = false;
                return false;
            }
            
            // Set up video callback to route through MP4SinkWriter
            g_videoSource->SetFrameCallback([](ID3D11Texture2D* texture, LONGLONG timestamp) {
                g_sinkWriter.WriteFrame(texture, timestamp);
            });
        }
        
        // Phase 3: Initialize audio mixer if any audio sources are requested
        bool needsAudioMixer = captureDesktopAudio || captureMicrophone;
        
        if (needsAudioMixer)
        {
            // Create audio mixer with standard output format (48kHz, stereo, 16-bit)
            g_audioMixer = new AudioMixer();
            if (!g_audioMixer->Initialize(48000, 2, 16))
            {
                delete g_audioMixer;
                g_audioMixer = nullptr;
                needsAudioMixer = false;
            }
        }
        
        // Create desktop audio source if requested
        bool desktopAudioEnabled = false;
        if (captureDesktopAudio && g_audioMixer)
        {
            g_desktopAudioSource = new DesktopAudioSource();
            
            if (g_desktopAudioSource->Initialize())
            {
                // Register with audio mixer
                float volume = g_routingConfig.GetSourceVolume((uint64_t)g_desktopAudioSource);
                g_desktopAudioSourceId = g_audioMixer->RegisterSource(g_desktopAudioSource, volume);
                
                if (g_desktopAudioSourceId > 0)
                {
                    // Apply routing configuration
                    bool muted = g_routingConfig.IsSourceMuted(g_desktopAudioSourceId);
                    g_audioMixer->SetSourceMuted(g_desktopAudioSourceId, muted);
                    
                    if (g_desktopAudioSource->Start(&hr))
                    {
                        desktopAudioEnabled = true;
                    }
                }
            }
            
            if (!desktopAudioEnabled && g_desktopAudioSource)
            {
                if (g_desktopAudioSourceId > 0)
                {
                    g_audioMixer->UnregisterSource(g_desktopAudioSourceId);
                    g_desktopAudioSourceId = 0;
                }
                g_desktopAudioSource->Release();
                g_desktopAudioSource = nullptr;
            }
        }
        
        // Create microphone source if requested
        bool microphoneEnabled = false;
        if (captureMicrophone && g_audioMixer)
        {
            g_microphoneSource = new MicrophoneAudioSource();
            
            if (g_microphoneSource->Initialize())
            {
                // Register with audio mixer
                float volume = g_routingConfig.GetSourceVolume((uint64_t)g_microphoneSource);
                g_microphoneSourceId = g_audioMixer->RegisterSource(g_microphoneSource, volume);
                
                if (g_microphoneSourceId > 0)
                {
                    // Apply routing configuration
                    bool muted = g_routingConfig.IsSourceMuted(g_microphoneSourceId);
                    g_audioMixer->SetSourceMuted(g_microphoneSourceId, muted);
                    
                    if (g_microphoneSource->Start(&hr))
                    {
                        microphoneEnabled = true;
                    }
                }
            }
            
            if (!microphoneEnabled && g_microphoneSource)
            {
                if (g_microphoneSourceId > 0)
                {
                    g_audioMixer->UnregisterSource(g_microphoneSourceId);
                    g_microphoneSourceId = 0;
                }
                g_microphoneSource->Release();
                g_microphoneSource = nullptr;
            }
        }
        
        // Phase 3/4: Initialize audio tracks based on routing configuration
        if (g_audioMixer && (desktopAudioEnabled || microphoneEnabled))
        {
            WAVEFORMATEX* mixerFormat = const_cast<WAVEFORMATEX*>(g_audioMixer->GetOutputFormat());
            
            if (g_useEncoderPipeline)
            {
                // Phase 4: Initialize audio tracks in EncoderPipeline
                if (g_routingConfig.IsMixedMode())
                {
                    // Mixed mode: Single track with all sources mixed
                    g_encoderPipeline->InitializeAudioTrack(0, mixerFormat, L"Mixed Audio");
                }
                else
                {
                    // Separate track mode: Initialize tracks for each source
                    int trackIndex = 0;
                    if (desktopAudioEnabled)
                    {
                        int configuredTrack = g_routingConfig.GetSourceTrack(g_desktopAudioSourceId);
                        trackIndex = (configuredTrack >= 0) ? configuredTrack : trackIndex;
                        const wchar_t* trackName = g_routingConfig.GetTrackName(trackIndex);
                        g_encoderPipeline->InitializeAudioTrack(trackIndex, mixerFormat, 
                            trackName ? trackName : L"Desktop Audio");
                        trackIndex++;
                    }
                    
                    if (microphoneEnabled)
                    {
                        int configuredTrack = g_routingConfig.GetSourceTrack(g_microphoneSourceId);
                        trackIndex = (configuredTrack >= 0) ? configuredTrack : trackIndex;
                        const wchar_t* trackName = g_routingConfig.GetTrackName(trackIndex);
                        g_encoderPipeline->InitializeAudioTrack(trackIndex, mixerFormat, 
                            trackName ? trackName : L"Microphone");
                    }
                }
            }
            else
            {
                // Phase 3: Initialize audio tracks in MP4SinkWriter (legacy path)
                if (g_routingConfig.IsMixedMode())
                {
                    // Mixed mode: Single track with all sources mixed
                    if (g_sinkWriter.InitializeAudioStream(mixerFormat, &hr))
                    {
                        // Audio will be written via mixer callback
                    }
                }
                else
                {
                    // Separate track mode: Initialize tracks for each source
                    int trackIndex = 0;
                    if (desktopAudioEnabled)
                    {
                        int configuredTrack = g_routingConfig.GetSourceTrack(g_desktopAudioSourceId);
                        trackIndex = (configuredTrack >= 0) ? configuredTrack : trackIndex;
                        const wchar_t* trackName = g_routingConfig.GetTrackName(trackIndex);
                        g_sinkWriter.InitializeAudioTrack(trackIndex, mixerFormat, trackName ? trackName : L"Desktop Audio");
                        trackIndex++;
                    }
                    
                    if (microphoneEnabled)
                    {
                        int configuredTrack = g_routingConfig.GetSourceTrack(g_microphoneSourceId);
                        trackIndex = (configuredTrack >= 0) ? configuredTrack : trackIndex;
                        const wchar_t* trackName = g_routingConfig.GetTrackName(trackIndex);
                        g_sinkWriter.InitializeAudioTrack(trackIndex, mixerFormat, trackName ? trackName : L"Microphone");
                    }
                }
            }
        }
        
        // Start video capture
        if (!g_videoSource->Start())
        {
            // Cleanup on failure
            if (g_desktopAudioSource)
            {
                g_desktopAudioSource->Stop();
                if (g_desktopAudioSourceId > 0 && g_audioMixer)
                {
                    g_audioMixer->UnregisterSource(g_desktopAudioSourceId);
                    g_desktopAudioSourceId = 0;
                }
                g_desktopAudioSource->Release();
                g_desktopAudioSource = nullptr;
            }
            if (g_microphoneSource)
            {
                g_microphoneSource->Stop();
                if (g_microphoneSourceId > 0 && g_audioMixer)
                {
                    g_audioMixer->UnregisterSource(g_microphoneSourceId);
                    g_microphoneSourceId = 0;
                }
                g_microphoneSource->Release();
                g_microphoneSource = nullptr;
            }
            if (g_audioMixer)
            {
                delete g_audioMixer;
                g_audioMixer = nullptr;
            }
            g_videoSource->Release();
            g_videoSource = nullptr;
            g_useSourceAbstraction = false;
            return false;
        }
        
        // Phase 3: Start mixer thread if audio mixer is active
        if (g_audioMixer)
        {
            g_mixerTimestamp = 0;
            g_mixerThreadRunning.store(true);
            g_mixerThread = std::thread(MixerThreadProc);
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
            // New source-based path (Phase 3/4: with AudioMixer and optional EncoderPipeline)
            // Stop mixer thread first
            if (g_mixerThreadRunning.load())
            {
                g_mixerThreadRunning.store(false);
                if (g_mixerThread.joinable())
                {
                    g_mixerThread.join();
                }
            }
            
            // Unregister sources from mixer before stopping
            if (g_audioMixer)
            {
                if (g_microphoneSourceId > 0)
                {
                    g_audioMixer->UnregisterSource(g_microphoneSourceId);
                    g_microphoneSourceId = 0;
                }
                
                if (g_desktopAudioSourceId > 0)
                {
                    g_audioMixer->UnregisterSource(g_desktopAudioSourceId);
                    g_desktopAudioSourceId = 0;
                }
            }
            
            // Stop all audio sources
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
            
            // Delete audio mixer
            if (g_audioMixer)
            {
                delete g_audioMixer;
                g_audioMixer = nullptr;
            }
            
            // Stop video source
            if (g_videoSource)
            {
                g_videoSource->Stop();
                g_videoSource->Release();
                g_videoSource = nullptr;
            }
            
            // Phase 4: Stop and cleanup encoder pipeline or MP4SinkWriter
            if (g_useEncoderPipeline)
            {
                if (g_encoderPipeline)
                {
                    g_encoderPipeline->Stop();
                    delete g_encoderPipeline;
                    g_encoderPipeline = nullptr;
                }
            }
            else
            {
                // Phase 3: Finalize MP4 file (legacy path)
                g_sinkWriter.Finalize();
                // Reset sink writer to fresh state
                g_sinkWriter = MP4SinkWriter();
            }
            
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
            // New source-based path: toggle desktop audio through mixer
            if (g_audioMixer && g_desktopAudioSourceId > 0)
            {
                g_audioMixer->SetSourceMuted(g_desktopAudioSourceId, !enabled);
            }
            // Also toggle at source level for efficiency
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
    
    // Audio routing configuration exports
    static AudioRoutingConfig g_audioRoutingConfig;
    
    __declspec(dllexport) void SetAudioSourceTrack(uint64_t sourceHandle, int trackIndex)
    {
        g_audioRoutingConfig.SetSourceTrack(sourceHandle, trackIndex);
    }
    
    __declspec(dllexport) int GetAudioSourceTrack(uint64_t sourceHandle)
    {
        return g_audioRoutingConfig.GetSourceTrack(sourceHandle);
    }
    
    __declspec(dllexport) void SetAudioSourceVolume(uint64_t sourceHandle, float volume)
    {
        g_audioRoutingConfig.SetSourceVolume(sourceHandle, volume);
    }
    
    __declspec(dllexport) float GetAudioSourceVolume(uint64_t sourceHandle)
    {
        return g_audioRoutingConfig.GetSourceVolume(sourceHandle);
    }
    
    __declspec(dllexport) void SetAudioSourceMuted(uint64_t sourceHandle, bool muted)
    {
        g_audioRoutingConfig.SetSourceMuted(sourceHandle, muted);
    }
    
    __declspec(dllexport) bool GetAudioSourceMuted(uint64_t sourceHandle)
    {
        return g_audioRoutingConfig.IsSourceMuted(sourceHandle);
    }
    
    __declspec(dllexport) void SetAudioTrackName(int trackIndex, const wchar_t* name)
    {
        g_audioRoutingConfig.SetTrackName(trackIndex, name);
    }
    
    __declspec(dllexport) void SetAudioMixingMode(bool mixedMode)
    {
        g_audioRoutingConfig.SetMixedMode(mixedMode);
    }
    
    __declspec(dllexport) bool GetAudioMixingMode()
    {
        return g_audioRoutingConfig.IsMixedMode();
    }
    
    // Phase 4: Encoder pipeline configuration
    static EncoderPreset g_videoPreset = EncoderPreset::Balanced;
    static AudioQuality g_audioQuality = AudioQuality::High;
    
    __declspec(dllexport) void UseEncoderPipeline(bool enable)
    {
        g_useEncoderPipeline = enable;
    }
    
    __declspec(dllexport) bool IsEncoderPipelineEnabled()
    {
        return g_useEncoderPipeline;
    }
    
    __declspec(dllexport) void SetVideoEncoderPreset(int preset)
    {
        if (preset >= 0 && preset <= static_cast<int>(EncoderPreset::Lossless))
        {
            g_videoPreset = static_cast<EncoderPreset>(preset);
        }
    }
    
    __declspec(dllexport) int GetVideoEncoderPreset()
    {
        return static_cast<int>(g_videoPreset);
    }
    
    __declspec(dllexport) void SetAudioEncoderQuality(int quality)
    {
        if (quality >= 0 && quality <= static_cast<int>(AudioQuality::VeryHigh))
        {
            g_audioQuality = static_cast<AudioQuality>(quality);
        }
    }
    
    __declspec(dllexport) int GetAudioEncoderQuality()
    {
        return static_cast<int>(g_audioQuality);
    }
}