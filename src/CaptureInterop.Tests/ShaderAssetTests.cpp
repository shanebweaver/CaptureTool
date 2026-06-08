#include "pch.h"
#include "CppUnitTest.h"

#include <filesystem>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace CaptureInteropTests
{
    TEST_CLASS(ShaderAssetTests)
    {
    public:
        TEST_METHOD(HdrToSdrToneMapperShader_IsBuilt)
        {
            const std::filesystem::path testSourcePath = __FILE__;
#if defined(_DEBUG)
            constexpr const wchar_t* ConfigurationName = L"Debug";
#else
            constexpr const wchar_t* ConfigurationName = L"Release";
#endif

#if defined(_M_ARM64)
            constexpr const wchar_t* PlatformName = L"ARM64";
#else
            constexpr const wchar_t* PlatformName = L"x64";
#endif

            const std::filesystem::path intermediatePixelShaderPath =
                testSourcePath.parent_path().parent_path() /
                L"CaptureInterop.Lib" /
                L"obj" /
                ConfigurationName /
                PlatformName /
                L"HdrToSdrToneMapper.cso";
            const std::filesystem::path intermediateVertexShaderPath =
                testSourcePath.parent_path().parent_path() /
                L"CaptureInterop.Lib" /
                L"obj" /
                ConfigurationName /
                PlatformName /
                L"HdrToSdrToneMapperVertex.cso";
            const std::filesystem::path packagedPixelShaderPath =
                testSourcePath.parent_path().parent_path().parent_path() /
                L"bin" /
                ConfigurationName /
                PlatformName /
                L"HdrToSdrToneMapper.cso";
            const std::filesystem::path packagedVertexShaderPath =
                testSourcePath.parent_path().parent_path().parent_path() /
                L"bin" /
                ConfigurationName /
                PlatformName /
                L"HdrToSdrToneMapperVertex.cso";

            Assert::IsTrue(
                std::filesystem::exists(intermediatePixelShaderPath),
                L"HDR-to-SDR tone mapper pixel shader bytecode should be produced by the native build.");
            Assert::IsTrue(
                std::filesystem::file_size(intermediatePixelShaderPath) > 0,
                L"HDR-to-SDR tone mapper pixel shader bytecode should not be empty.");
            Assert::IsTrue(
                std::filesystem::exists(intermediateVertexShaderPath),
                L"HDR-to-SDR tone mapper vertex shader bytecode should be produced by the native build.");
            Assert::IsTrue(
                std::filesystem::file_size(intermediateVertexShaderPath) > 0,
                L"HDR-to-SDR tone mapper vertex shader bytecode should not be empty.");
            Assert::IsTrue(
                std::filesystem::exists(packagedPixelShaderPath),
                L"HDR-to-SDR tone mapper pixel shader bytecode should be copied to the native output directory.");
            Assert::IsTrue(
                std::filesystem::file_size(packagedPixelShaderPath) > 0,
                L"Packaged HDR-to-SDR tone mapper pixel shader bytecode should not be empty.");
            Assert::IsTrue(
                std::filesystem::exists(packagedVertexShaderPath),
                L"HDR-to-SDR tone mapper vertex shader bytecode should be copied to the native output directory.");
            Assert::IsTrue(
                std::filesystem::file_size(packagedVertexShaderPath) > 0,
                L"Packaged HDR-to-SDR tone mapper vertex shader bytecode should not be empty.");
        }
    };
}
