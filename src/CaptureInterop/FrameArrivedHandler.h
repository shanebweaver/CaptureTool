#pragma once
#include <queue>
#include <mutex>
#include <condition_variable>
#include <thread>
#include <atomic>
#include <functional>
#include <d3d11.h>

// Forward declaration for MP4SinkWriter (for backward compatibility)
class MP4SinkWriter;

using namespace ABI::Windows::Foundation;
using namespace ABI::Windows::Graphics::Capture;

// Callback signature for frame delivery
using FrameCallback = std::function<void(ID3D11Texture2D* texture, LONGLONG timestamp)>;

// Structure to hold frame data for processing
struct QueuedFrame
{
    wil::com_ptr<ID3D11Texture2D> texture;
    LONGLONG relativeTimestamp;
};

// FrameArrivedHandler handles new capture frames and forwards them via callback.
// Uses a background thread to avoid blocking the event callback thread.
class FrameArrivedHandler final
    : public ITypedEventHandler<Direct3D11CaptureFramePool*, IInspectable*>
{
public:
    // New constructor: accepts callback for modern source-based architecture
    explicit FrameArrivedHandler(FrameCallback callback) noexcept;
    
    // Legacy constructor: accepts MP4SinkWriter for backward compatibility
    explicit FrameArrivedHandler(wil::com_ptr<MP4SinkWriter> sinkWriter) noexcept;
    
    ~FrameArrivedHandler();

    // IUnknown
    HRESULT STDMETHODCALLTYPE QueryInterface(REFIID riid, void** ppvObject) override;
    ULONG STDMETHODCALLTYPE AddRef() override;
    ULONG STDMETHODCALLTYPE Release() override;

    // ITypedEventHandler
    HRESULT STDMETHODCALLTYPE Invoke(IDirect3D11CaptureFramePool* sender, IInspectable* args) noexcept override;

    // Start the background processing thread (called after construction)
    void StartProcessing();

    // Stop the background processing thread
    void Stop();

private:
    void ProcessingThreadProc();

    volatile long m_ref;
    FrameCallback m_callback;
    wil::com_ptr<MP4SinkWriter> m_sinkWriter;  // For backward compatibility
    
    // Background processing
    std::queue<QueuedFrame> m_frameQueue;
    std::mutex m_queueMutex;
    std::condition_variable m_queueCV;
    std::thread m_processingThread;
    std::atomic<bool> m_running{true};
    std::atomic<bool> m_stopped{false};  // Guard for idempotent Stop()
    std::atomic<bool> m_processingStarted{false};  // Guard for StartProcessing()
    
    // First frame tracking for timestamp calculation
    std::atomic<LONGLONG> m_firstFrameSystemTime{0};
};

// Helper to register the frame-arrived event (legacy version for backward compatibility)
EventRegistrationToken RegisterFrameArrivedHandler(wil::com_ptr<IDirect3D11CaptureFramePool> framePool, wil::com_ptr<MP4SinkWriter> sinkWriter, FrameArrivedHandler** outHandler = nullptr, HRESULT* outHr = nullptr);

// Helper to register the frame-arrived event (new callback-based version)
EventRegistrationToken RegisterFrameArrivedHandlerWithCallback(wil::com_ptr<IDirect3D11CaptureFramePool> framePool, FrameCallback callback, FrameArrivedHandler** outHandler = nullptr, HRESULT* outHr = nullptr);