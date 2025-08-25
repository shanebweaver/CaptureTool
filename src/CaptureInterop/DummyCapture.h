#pragma once

#include "DummyCapture.g.h"

namespace winrt::CaptureInterop::implementation
{
    struct DummyCapture : DummyCaptureT<DummyCapture>
    {
        DummyCapture() = default;

        int32_t AddNumbers(int32_t a, int32_t b);
    };
}

namespace winrt::CaptureInterop::factory_implementation
{
    struct DummyCapture : DummyCaptureT<DummyCapture, implementation::DummyCapture>
    {
    };
}
