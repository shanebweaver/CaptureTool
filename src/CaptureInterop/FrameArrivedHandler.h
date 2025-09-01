#include "MP4SinkWriter.h"

using namespace ABI::Windows::Foundation;
using namespace ABI::Windows::Graphics::Capture;

// FrameArrivedHandler handles new capture frames and forwards them to the MP4SinkWriter.
class FrameArrivedHandler final
    : public ITypedEventHandler<Direct3D11CaptureFramePool*, IInspectable*>
{
public:
    explicit FrameArrivedHandler(wil::com_ptr<MP4SinkWriter> sinkWriter) noexcept;

    // IUnknown
    HRESULT STDMETHODCALLTYPE QueryInterface(REFIID riid, void** ppvObject) override;
    ULONG STDMETHODCALLTYPE AddRef() override;
    ULONG STDMETHODCALLTYPE Release() override;

    // ITypedEventHandler
    HRESULT STDMETHODCALLTYPE Invoke(IDirect3D11CaptureFramePool* sender, IInspectable* args) noexcept override;

private:
    volatile long m_ref;
    wil::com_ptr<MP4SinkWriter> m_sinkWriter;
};

// Helper to register the frame-arrived event.
EventRegistrationToken RegisterFrameArrivedHandler(wil::com_ptr<IDirect3D11CaptureFramePool> framePool, wil::com_ptr<MP4SinkWriter> sinkWriter, HRESULT* outHr = nullptr);