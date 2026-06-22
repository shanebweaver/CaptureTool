#pragma once
#include <cstdint>
#include <string>

struct AudioRecordingConfig
{
    std::wstring outputPath;
    bool audioEnabled;
    std::wstring audioInputSourceId;
    uint32_t audioInputVolumePercentage;

    AudioRecordingConfig()
        : outputPath(L"")
        , audioEnabled(true)
        , audioInputSourceId(L"")
        , audioInputVolumePercentage(100)
    {
    }

    AudioRecordingConfig(
        std::wstring path,
        bool enabled = true,
        std::wstring sourceId = L"",
        uint32_t volumePercentage = 100)
        : outputPath(std::move(path))
        , audioEnabled(enabled)
        , audioInputSourceId(std::move(sourceId))
        , audioInputVolumePercentage(volumePercentage)
    {
    }

    bool IsValid() const
    {
        return !outputPath.empty() && audioInputVolumePercentage <= 100;
    }
};
