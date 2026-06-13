#include "pch.h"
#include "CppUnitTest.h"
#include "PassthroughVideoFrameProcessor.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace CaptureInteropTests
{
    TEST_CLASS(PassthroughVideoFrameProcessorTests)
    {
    public:
        TEST_METHOD(Process_ReturnsInputTexture)
        {
            PassthroughVideoFrameProcessor processor;
            auto* texture = reinterpret_cast<ID3D11Texture2D*>(0x1234);

            auto result = processor.Process(texture);

            Assert::IsTrue(result.IsOk());
            Assert::IsTrue(result.Value().texture == texture);
        }

        TEST_METHOD(Process_WithNullTexture_ReturnsError)
        {
            PassthroughVideoFrameProcessor processor;

            auto result = processor.Process(nullptr);

            Assert::IsTrue(result.IsError());
            Assert::AreEqual(static_cast<long>(E_INVALIDARG), static_cast<long>(result.Error().hr));
        }
    };
}
