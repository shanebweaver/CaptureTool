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
static HANDLE g_initThread = nullptr;
static volatile bool g_initSuccess = false;

// Structure to pass initialization parameters to thread
struct InitParams
{
    HMONITOR hMonitor;
    std::wstring outputPath;
    bool enableAudio;
};

// Thread function for async initialization
static DWORD WINAPI InitializeRecordingThread(LPVOID param)
{
    std::unique_ptr<InitParams> params(static_cast<InitParams*>(param));
    
    HRESULT hr = S_OK;

// Thread function for async initialization
static DWORD WINAPI InitializeRecordingThread(LPVOID param)
{
    std::unique_ptr<InitParams> params(static_cast<InitParams*>(param));
    
    HRESULT hr = S_OK;

    wil::com_ptr<IGraphicsCaptureItemInterop> interop = GetGraphicsCaptureItemInterop(&hr);
    if (!interop)
    {
        g_initSuccess = false;
        return 1;
    }

    wil::com_ptr<IGraphicsCaptureItem> captureItem = GetGraphicsCaptureItemForMonitor(params->hMonitor, interop, &hr);
    if (!captureItem)
    {
        g_initSuccess = false;
        return 1;
    }

    D3DDeviceAndContext d3d = InitializeD3D(&hr);
    if (FAILED(hr))
    {
        g_initSuccess = false;
        return 1;
    }

    wil::com_ptr<ID3D11Device> device = d3d.device;
    wil::com_ptr<IDirect3DDevice> abiDevice = CreateDirect3DDevice(device, &hr);
    if (FAILED(hr))
    {
        g_initSuccess = false;
        return 1;
    }

    g_framePool = CreateCaptureFramePool(captureItem, abiDevice, &hr);
    if (FAILED(hr))
    {
        g_initSuccess = false;
        return 1;
    }

    g_session = CreateCaptureSession(g_framePool, captureItem, &hr);
    if (FAILED(hr))
    {
        g_initSuccess = false;
        return 1;
    }

    SizeInt32 size{};
    hr = captureItem->get_Size(&size);
    if (FAILED(hr))
    {
        g_initSuccess = false;
        return 1;
    }

    // Initialize audio capture if enabled
    WAVEFORMATEX defaultAudioFormat = {};
    WAVEFORMATEX* audioFormat = nullptr;
    if (params->enableAudio)
    {
        // Set up default audio format (16-bit PCM, 48kHz, stereo)
        // Audio capture will convert to this format
        defaultAudioFormat.wFormatTag = WAVE_FORMAT_PCM;
        defaultAudioFormat.nChannels = 2;
        defaultAudioFormat.nSamplesPerSec = 48000;
        defaultAudioFormat.wBitsPerSample = 16;
        defaultAudioFormat.nBlockAlign = defaultAudioFormat.nChannels * defaultAudioFormat.wBitsPerSample / 8;
        defaultAudioFormat.nAvgBytesPerSec = defaultAudioFormat.nSamplesPerSec * defaultAudioFormat.nBlockAlign;
        defaultAudioFormat.cbSize = 0;
        audioFormat = &defaultAudioFormat;
        
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
            params->enableAudio = false;
            audioFormat = nullptr;
        }
    }

    if (!g_sinkWriter.Initialize(params->outputPath.c_str(), device.get(), size.Width, size.Height, params->enableAudio, audioFormat, &hr))
    {
        if (g_audioCapture) g_audioCapture.reset();
        g_initSuccess = false;
        return 1;
    }
    
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
        g_initSuccess = false;
        return 1;
    }

    g_initSuccess = true;
    return 0;
}

// Exported API
extern "C"
{
    __declspec(dllexport) bool TryStartRecording(HMONITOR hMonitor, const wchar_t* outputPath, bool enableAudio)
    {
        // Clean up any previous init thread
        if (g_initThread)
        {
            CloseHandle(g_initThread);
            g_initThread = nullptr;
        }
        
        // Create parameters for init thread
        auto params = std::make_unique<InitParams>();
        params->hMonitor = hMonitor;
        params->outputPath = outputPath;
        params->enableAudio = enableAudio;
        
        // Launch initialization on background thread
        g_initSuccess = false;
        g_initThread = CreateThread(
            nullptr,
            0,
            InitializeRecordingThread,
            params.release(), // Thread takes ownership
            0,
            nullptr
        );
        
        if (!g_initThread)
        {
            return false;
        }
        
        // Return immediately - initialization happens asynchronously
        return true;
    }

    __declspec(dllexport) void TryPauseRecording()
    {
        // Not implemented yet, don't worry about it.
    }

    __declspec(dllexport) void TryStopRecording()
    {
        // Wait for init thread to complete if it's still running
        if (g_initThread)
        {
            WaitForSingleObject(g_initThread, 5000); // Wait up to 5 seconds
            CloseHandle(g_initThread);
            g_initThread = nullptr;
        }
        
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
    }
}