#include <functional>

// Forward declaration
class IMediaClockReader;

/// <summary>
/// Event arguments for video frame ready event.
/// Contains the video frame data and timing information.
/// </summary>
struct VideoFrameReadyEventArgs;

/// <summary>
/// Callback function type for video frame ready events.
/// </summary>
using VideoFrameReadyCallback = std::function<void(const VideoFrameReadyEventArgs&)>;

using namespace ABI::Windows::Foundation;
using namespace ABI::Windows::Graphics::Capture;

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
    explicit FrameArrivedHandler(VideoFrameReadyCallback callback, IMediaClockReader* clockReader) noexcept;
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
    VideoFrameReadyCallback m_callback;
    IMediaClockReader* m_clockReader;
    
    // Background processing
    std::queue<QueuedFrame> m_frameQueue;
    std::mutex m_queueMutex;
    std::condition_variable m_queueCV;
    std::thread m_processingThread;
    std::atomic<bool> m_running{true};
    std::atomic<bool> m_stopped{false};  // Guard for idempotent Stop()
    std::atomic<bool> m_processingStarted{false};  // Guard for StartProcessing()
};

// Helper to register the frame-arrived event.
EventRegistrationToken RegisterFrameArrivedHandler(wil::com_ptr<IDirect3D11CaptureFramePool> framePool, VideoFrameReadyCallback callback, IMediaClockReader* clockReader, FrameArrivedHandler** outHandler = nullptr, HRESULT* outHr = nullptr);