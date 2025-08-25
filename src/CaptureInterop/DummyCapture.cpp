#include "pch.h"
#include "DummyCapture.h"
#include "DummyCapture.g.cpp"

namespace winrt::CaptureInterop::implementation
{
    int32_t DummyCapture::AddNumbers(int32_t a, int32_t b)
    {
        return a + b;
    }
}
