#include "pch.h"
#include "VideoFrameProcessorFactory.h"
#include "HdrToSdrVideoFrameProcessor.h"
#include "PassthroughVideoFrameProcessor.h"

#include <filesystem>
#include <fstream>
#include <vector>

namespace
{
    std::unique_ptr<IVideoFrameProcessor> CreatePassthrough()
    {
        return std::make_unique<PassthroughVideoFrameProcessor>();
    }

    std::filesystem::path GetModuleDirectory()
    {
        HMODULE module = nullptr;
        BOOL moduleFound = GetModuleHandleExW(
            GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
            reinterpret_cast<LPCWSTR>(&GetModuleDirectory),
            &module);

        wchar_t modulePath[MAX_PATH]{};
        DWORD length = GetModuleFileNameW(moduleFound ? module : nullptr, modulePath, ARRAYSIZE(modulePath));
        if (length == 0)
        {
            return {};
        }

        return std::filesystem::path(modulePath).parent_path();
    }

    std::vector<std::filesystem::path> GetShaderDirectoryCandidates()
    {
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

        const auto moduleDirectory = GetModuleDirectory();
        std::vector<std::filesystem::path> candidates;
        candidates.push_back(moduleDirectory);
        if (!moduleDirectory.empty())
        {
            candidates.push_back(moduleDirectory.parent_path().parent_path() / L"bin" / ConfigurationName / PlatformName);
        }
        candidates.push_back(std::filesystem::current_path());
        candidates.push_back(std::filesystem::current_path() / L"bin" / ConfigurationName / PlatformName);
        return candidates;
    }

    std::vector<uint8_t> ReadAllBytes(const std::filesystem::path& path)
    {
        std::ifstream stream(path, std::ios::binary);
        if (!stream)
        {
            return {};
        }

        return std::vector<uint8_t>(
            std::istreambuf_iterator<char>(stream),
            std::istreambuf_iterator<char>());
    }
}

Result<std::unique_ptr<IVideoFrameProcessor>> VideoFrameProcessorFactory::CreateProcessor(const VideoFrameProcessorFactoryContext& context)
{
    if (!context.monitorHdrInfo.ShouldUseToneMapper() || !context.device || context.width == 0 || context.height == 0)
    {
        return Result<std::unique_ptr<IVideoFrameProcessor>>::Ok(CreatePassthrough());
    }

    wil::com_ptr<ID3D11DeviceContext> deviceContext;
    context.device->GetImmediateContext(deviceContext.put());
    if (!deviceContext)
    {
        return Result<std::unique_ptr<IVideoFrameProcessor>>::Ok(CreatePassthrough());
    }

    std::vector<uint8_t> vertexShader;
    std::vector<uint8_t> pixelShader;
    for (const auto& shaderDirectory : GetShaderDirectoryCandidates())
    {
        vertexShader = ReadAllBytes(shaderDirectory / L"HdrToSdrToneMapperVertex.cso");
        pixelShader = ReadAllBytes(shaderDirectory / L"HdrToSdrToneMapper.cso");
        if (!vertexShader.empty() && !pixelShader.empty())
        {
            break;
        }
    }

    if (vertexShader.empty() || pixelShader.empty())
    {
        return Result<std::unique_ptr<IVideoFrameProcessor>>::Ok(CreatePassthrough());
    }

    auto hdrProcessorResult = HdrToSdrVideoFrameProcessor::Create(
        context.device,
        deviceContext.get(),
        context.width,
        context.height,
        vertexShader,
        pixelShader);
    if (hdrProcessorResult.IsError())
    {
        return Result<std::unique_ptr<IVideoFrameProcessor>>::Ok(CreatePassthrough());
    }

    std::unique_ptr<IVideoFrameProcessor> processor = std::move(hdrProcessorResult.Value());
    return Result<std::unique_ptr<IVideoFrameProcessor>>::Ok(std::move(processor));
}
