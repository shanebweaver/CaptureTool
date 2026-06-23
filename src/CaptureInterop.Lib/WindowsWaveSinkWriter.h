#pragma once
#include "IWavSinkWriter.h"
#include <Windows.h>
#include <cstdint>
#include <mutex>
#include <vector>

class WindowsWaveSinkWriter : public IWavSinkWriter
{
public:
    WindowsWaveSinkWriter() = default;
    ~WindowsWaveSinkWriter() override;

    WindowsWaveSinkWriter(const WindowsWaveSinkWriter&) = delete;
    WindowsWaveSinkWriter& operator=(const WindowsWaveSinkWriter&) = delete;

    bool Initialize(const wchar_t* outputPath, WAVEFORMATEX* audioFormat, long* outHr = nullptr) override;
    long WriteAudioSample(std::span<const uint8_t> data, int64_t timestamp) override;
    void Finalize() override;

private:
    static uint32_t GetFormatChunkSize(const WAVEFORMATEX* audioFormat);
    bool WriteBytes(const void* data, uint32_t byteCount, HRESULT* outHr = nullptr);
    bool PatchUInt32(uint32_t offset, uint32_t value, HRESULT* outHr = nullptr);
    void FinalizeUnlocked();

    std::mutex m_mutex;
    HANDLE m_file = INVALID_HANDLE_VALUE;
    uint64_t m_dataBytesWritten = 0;
    uint32_t m_dataSizeOffset = 0;
    uint32_t m_riffSizeWithoutData = 0;
    bool m_initialized = false;
    bool m_finalized = false;
};
