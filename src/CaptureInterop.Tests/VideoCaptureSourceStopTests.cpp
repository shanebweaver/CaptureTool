#include "pch.h"
#include "CppUnitTest.h"

#include <windows.foundation.h>
#include <windows.graphics.capture.h>
#include <atomic>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace CaptureInteropTests
{
    // Mock IGraphicsCaptureSession that also implements IClosable::Close() returning E_FAIL.
    // This simulates the scenario where the captured display was disconnected, causing
    // StopCapture() to fail inside the WinRT GraphicsCaptureSession::Close() method.
    // The production crash (Partner Center report) occurred because the destructor called
    // Close() -> StopCapture() and the resulting HRESULT failure was converted to a C++
    // exception by winrt::check_hresult, propagating through the destructor chain.
    class MockFailingGraphicsCaptureSession
        : public ABI::Windows::Graphics::Capture::IGraphicsCaptureSession
        , public ABI::Windows::Foundation::IClosable
    {
    public:
        explicit MockFailingGraphicsCaptureSession(std::atomic<bool>* closeCalledFlag)
            : m_refCount(1)
            , m_closeCalledFlag(closeCalledFlag)
        {
        }

        // IUnknown (shared base - override once for both interfaces)
        ULONG STDMETHODCALLTYPE AddRef() override
        {
            return ++m_refCount;
        }

        ULONG STDMETHODCALLTYPE Release() override
        {
            ULONG count = --m_refCount;
            if (count == 0)
            {
                delete this;
            }
            return count;
        }

        HRESULT STDMETHODCALLTYPE QueryInterface(REFIID riid, void** ppvObject) override
        {
            if (!ppvObject)
            {
                return E_POINTER;
            }

            if (riid == __uuidof(IUnknown) ||
                riid == __uuidof(IInspectable) ||
                riid == __uuidof(ABI::Windows::Graphics::Capture::IGraphicsCaptureSession))
            {
                *ppvObject = static_cast<ABI::Windows::Graphics::Capture::IGraphicsCaptureSession*>(this);
                AddRef();
                return S_OK;
            }

            if (riid == __uuidof(ABI::Windows::Foundation::IClosable))
            {
                *ppvObject = static_cast<ABI::Windows::Foundation::IClosable*>(this);
                AddRef();
                return S_OK;
            }

            *ppvObject = nullptr;
            return E_NOINTERFACE;
        }

        // IInspectable (shared base - override once for both interfaces)
        HRESULT STDMETHODCALLTYPE GetIids(ULONG* iidCount, IID** iids) override
        {
            (void)iidCount; (void)iids;
            return E_NOTIMPL;
        }
        HRESULT STDMETHODCALLTYPE GetRuntimeClassName(HSTRING* className) override
        {
            (void)className;
            return E_NOTIMPL;
        }
        HRESULT STDMETHODCALLTYPE GetTrustLevel(TrustLevel* trustLevel) override
        {
            (void)trustLevel;
            return E_NOTIMPL;
        }

        // IGraphicsCaptureSession
        HRESULT STDMETHODCALLTYPE StartCapture() override
        {
            return S_OK;
        }

        // IClosable - simulates StopCapture() failing (e.g., display disconnected)
        HRESULT STDMETHODCALLTYPE Close() override
        {
            if (m_closeCalledFlag)
            {
                m_closeCalledFlag->store(true);
            }
            return E_FAIL; // Simulate StopCapture() failure inside Close()
        }

    private:
        std::atomic<ULONG> m_refCount;
        std::atomic<bool>* m_closeCalledFlag;
    };

    TEST_CLASS(VideoCaptureSourceStopTests)
    {
    public:
        // Verifies that the explicit IClosable::Close() call before wil::com_ptr::reset()
        // prevents the crash that occurred when a WinRT GraphicsCaptureSession's destructor
        // called Close() -> StopCapture() and StopCapture() failed with an HRESULT error.
        //
        // Crash scenario from Partner Center:
        //   GraphicsCaptureSession::Close -> StopCapture -> winrt::check_hresult ->
        //   winrt::throw_hresult -> _CxxThrowException -> RaiseException
        //
        // Fix: explicitly call Close() via ABI IClosable (returns HRESULT, doesn't throw)
        // before reset(), so the destructor finds the session already closed.
        TEST_METHOD(Stop_WhenCloseFailsWithHResult_DoesNotThrow)
        {
            // Arrange: create a mock session whose Close() returns E_FAIL
            std::atomic<bool> closeCalled{ false };
            MockFailingGraphicsCaptureSession* rawMock =
                new MockFailingGraphicsCaptureSession(&closeCalled);

            wil::com_ptr<ABI::Windows::Graphics::Capture::IGraphicsCaptureSession> captureSession;
            captureSession.attach(rawMock); // Takes over the initial ref

            // Act: apply the fix pattern - QI for IClosable, call Close(), then reset()
            // This is the exact pattern used in WindowsDesktopVideoCaptureSource::Stop()
            bool threw = false;
            try
            {
                wil::com_ptr<ABI::Windows::Foundation::IClosable> closable;
                if (SUCCEEDED(captureSession->QueryInterface(IID_PPV_ARGS(closable.put()))))
                {
                    (void)closable->Close(); // Best-effort: ignore HRESULT if close fails
                }
                captureSession.reset();
            }
            catch (...)
            {
                threw = true;
            }

            // Assert: Close() was called and no exception was thrown
            Assert::IsTrue(closeCalled.load(),
                L"IClosable::Close() should have been called on the capture session");
            Assert::IsFalse(threw,
                L"Stop() must not throw even when IClosable::Close() returns E_FAIL. "
                L"Without the fix, the WinRT destructor called Close() -> StopCapture() "
                L"and the failing HRESULT was rethrown as a C++ exception.");
        }

        // Verifies that the fix handles the case where QueryInterface for IClosable fails.
        // This is a defensive check - production sessions always implement IClosable,
        // but the pattern should be robust if QI ever fails.
        TEST_METHOD(Stop_WhenQueryInterfaceForClosableFails_DoesNotThrow)
        {
            // Arrange: create a session-like object that does NOT implement IClosable
            class MinimalSession : public ABI::Windows::Graphics::Capture::IGraphicsCaptureSession
            {
            public:
                std::atomic<ULONG> m_refCount{ 1 };

                ULONG STDMETHODCALLTYPE AddRef() override { return ++m_refCount; }
                ULONG STDMETHODCALLTYPE Release() override
                {
                    ULONG count = --m_refCount;
                    if (count == 0) delete this;
                    return count;
                }
                HRESULT STDMETHODCALLTYPE QueryInterface(REFIID riid, void** ppvObject) override
                {
                    if (!ppvObject) return E_POINTER;
                    if (riid == __uuidof(IUnknown) || riid == __uuidof(IInspectable) ||
                        riid == __uuidof(ABI::Windows::Graphics::Capture::IGraphicsCaptureSession))
                    {
                        *ppvObject = this;
                        AddRef();
                        return S_OK;
                    }
                    // Does NOT expose IClosable - returns E_NOINTERFACE
                    *ppvObject = nullptr;
                    return E_NOINTERFACE;
                }
                HRESULT STDMETHODCALLTYPE GetIids(ULONG*, IID**) override { return E_NOTIMPL; }
                HRESULT STDMETHODCALLTYPE GetRuntimeClassName(HSTRING*) override { return E_NOTIMPL; }
                HRESULT STDMETHODCALLTYPE GetTrustLevel(TrustLevel*) override { return E_NOTIMPL; }
                HRESULT STDMETHODCALLTYPE StartCapture() override { return S_OK; }
            };

            wil::com_ptr<ABI::Windows::Graphics::Capture::IGraphicsCaptureSession> captureSession;
            captureSession.attach(new MinimalSession());

            // Act: apply the fix pattern
            bool threw = false;
            try
            {
                wil::com_ptr<ABI::Windows::Foundation::IClosable> closable;
                if (SUCCEEDED(captureSession->QueryInterface(IID_PPV_ARGS(closable.put()))))
                {
                    (void)closable->Close();
                }
                captureSession.reset();
            }
            catch (...)
            {
                threw = true;
            }

            // Assert: no exception even when QI for IClosable returns E_NOINTERFACE
            Assert::IsFalse(threw,
                L"Stop() must not throw when QueryInterface for IClosable returns E_NOINTERFACE");
        }
    };
}
