#include "MP4SinkWriter.h"

using namespace ABI::Windows::Foundation;
using namespace ABI::Windows::Graphics::Capture;

// Structure to hold frame data for processing
struct QueuedFrame
{
    wil::com_ptr<ID3D11Texture2D> texture;
    LONGLONG relativeTimestamp;
    bool isDuplicateFrame = false;  // True if this is a timer-generated duplicate frame
};

// FrameArrivedHandler handles new capture frames and forwards them to the MP4SinkWriter.
// Uses a background thread to avoid blocking the event callback thread.
// Also generates duplicate frames at regular intervals for smooth video when screen is static.
class FrameArrivedHandler final
    : public ITypedEventHandler<Direct3D11CaptureFramePool*, IInspectable*>
{
public:
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
    void TimerThreadProc();  // Timer thread to generate duplicate frames

    volatile long m_ref;
    wil::com_ptr<MP4SinkWriter> m_sinkWriter;
    
    // Background processing
    std::queue<QueuedFrame> m_frameQueue;
    std::mutex m_queueMutex;
    std::condition_variable m_queueCV;
    std::thread m_processingThread;
    std::thread m_timerThread;  // Timer thread for duplicate frames
    std::atomic<bool> m_running{true};
    std::atomic<bool> m_stopped{false};  // Guard for idempotent Stop()
    std::atomic<bool> m_processingStarted{false};  // Guard for StartProcessing()
    
    // First frame tracking for timestamp calculation
    std::atomic<LONGLONG> m_firstFrameSystemTime{0};
    
    // Last frame tracking for duplicate frame generation
    wil::com_ptr<ID3D11Texture2D> m_lastTexture;
    std::mutex m_lastTextureMutex;
    std::atomic<LONGLONG> m_lastFrameTimestamp{0};
    std::atomic<LONGLONG> m_nextExpectedTimestamp{0};
};

// Helper to register the frame-arrived event.
EventRegistrationToken RegisterFrameArrivedHandler(wil::com_ptr<IDirect3D11CaptureFramePool> framePool, wil::com_ptr<MP4SinkWriter> sinkWriter, FrameArrivedHandler** outHandler = nullptr, HRESULT* outHr = nullptr);