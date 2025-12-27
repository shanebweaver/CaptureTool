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

/// <summary>
/// Handles new capture frames and forwards them via callback.
/// Uses a background thread to avoid blocking the event callback thread.
/// 
/// Implements Rust Principles:
/// - Principle #5 (RAII Everything): Destructor calls Stop() to join background thread.
///   Frame textures use wil::com_ptr for automatic COM Release().
/// - Principle #6 (No Globals): Callback and clock reader passed via constructor.
/// - Principle #8 (Thread Safety by Design): Uses mutex and condition variable for thread-safe
///   queue access. Atomics for state flags (m_running, m_stopped, m_processingStarted).
/// 
/// COM pattern:
/// - Implements IUnknown with manual reference counting (required for COM event handlers)
/// - Reference count starts at 1 in constructor
/// - Uses InterlockedIncrement/Decrement for thread-safe ref counting
/// - Deletes self when ref count reaches 0
/// - NOTE: This is a necessary exception to Principle #5 due to COM requirements
/// 
/// Threading model:
/// - Invoke() called on Windows Graphics Capture event thread
/// - ProcessingThreadProc() runs on dedicated background thread
/// - Queue protected by mutex, signaled via condition variable
/// - Stop() is idempotent and thread-safe via atomic m_stopped flag
/// 
/// See docs/RUST_PRINCIPLES.md for more details.
/// </summary>
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

    // Get the count of dropped frames
    uint64_t GetDroppedFrameCount() const { return m_droppedFrameCount.load(); }

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
    std::atomic<uint64_t> m_droppedFrameCount{0};  // Count of frames dropped due to full queue
};

// Helper to register the frame-arrived event.
EventRegistrationToken RegisterFrameArrivedHandler(wil::com_ptr<IDirect3D11CaptureFramePool> framePool, VideoFrameReadyCallback callback, IMediaClockReader* clockReader, FrameArrivedHandler** outHandler = nullptr, HRESULT* outHr = nullptr);