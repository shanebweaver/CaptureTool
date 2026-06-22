#include "pch.h"
#include "WindowsWaveSinkWriter.h"
#include <algorithm>

namespace
{
    constexpr uint32_t RiffSizeOffset = 4;
    constexpr uint32_t PcmFormatChunkSize = 16;
}

WindowsWaveSinkWriter::~WindowsWaveSinkWriter()
{
    Finalize();
}

bool WindowsWaveSinkWriter::Initialize(const wchar_t* outputPath, WAVEFORMATEX* audioFormat, long* outHr)
{
    std::lock_guard<std::mutex> lock(m_mutex);

    if (outHr) *outHr = S_OK;
    if (!outputPath || !audioFormat || audioFormat->nBlockAlign == 0 || audioFormat->nSamplesPerSec == 0)
    {
        if (outHr) *outHr = E_INVALIDARG;
        return false;
    }

    FinalizeUnlocked();

    m_file = CreateFileW(
        outputPath,
        GENERIC_WRITE,
        0,
        nullptr,
        CREATE_ALWAYS,
        FILE_ATTRIBUTE_NORMAL,
        nullptr);

    if (m_file == INVALID_HANDLE_VALUE)
    {
        if (outHr) *outHr = HRESULT_FROM_WIN32(GetLastError());
        return false;
    }

    HRESULT hr = S_OK;
    uint32_t formatChunkSize = GetFormatChunkSize(audioFormat);
    m_dataBytesWritten = 0;
    m_dataSizeOffset = 12 + 8 + formatChunkSize + 4;
    m_riffSizeWithoutData = 4 + (8 + formatChunkSize) + 8;
    m_initialized = true;
    m_finalized = false;

    const char riff[] = { 'R', 'I', 'F', 'F' };
    const char wave[] = { 'W', 'A', 'V', 'E' };
    const char fmt[] = { 'f', 'm', 't', ' ' };
    const char data[] = { 'd', 'a', 't', 'a' };
    uint32_t placeholderSize = 0;

    if (!WriteBytes(riff, sizeof(riff), &hr) ||
        !WriteBytes(&placeholderSize, sizeof(placeholderSize), &hr) ||
        !WriteBytes(wave, sizeof(wave), &hr) ||
        !WriteBytes(fmt, sizeof(fmt), &hr) ||
        !WriteBytes(&formatChunkSize, sizeof(formatChunkSize), &hr) ||
        !WriteBytes(audioFormat, formatChunkSize, &hr) ||
        !WriteBytes(data, sizeof(data), &hr) ||
        !WriteBytes(&placeholderSize, sizeof(placeholderSize), &hr))
    {
        if (outHr) *outHr = hr;
        FinalizeUnlocked();
        return false;
    }

    LARGE_INTEGER position{};
    position.QuadPart = static_cast<LONGLONG>(m_dataSizeOffset) + sizeof(uint32_t);
    if (!SetFilePointerEx(m_file, position, nullptr, FILE_BEGIN))
    {
        if (outHr) *outHr = HRESULT_FROM_WIN32(GetLastError());
        FinalizeUnlocked();
        return false;
    }

    if (outHr) *outHr = S_OK;
    return true;
}

long WindowsWaveSinkWriter::WriteAudioSample(std::span<const uint8_t> data, int64_t)
{
    std::lock_guard<std::mutex> lock(m_mutex);

    if (!m_initialized ||
        m_finalized ||
        m_file == INVALID_HANDLE_VALUE ||
        data.empty() ||
        data.size() > UINT32_MAX)
    {
        return E_FAIL;
    }

    HRESULT hr = S_OK;
    if (!WriteBytes(data.data(), static_cast<uint32_t>(data.size()), &hr))
    {
        return hr;
    }

    m_dataBytesWritten += data.size();
    return S_OK;
}

void WindowsWaveSinkWriter::Finalize()
{
    std::lock_guard<std::mutex> lock(m_mutex);
    FinalizeUnlocked();
}

void WindowsWaveSinkWriter::FinalizeUnlocked()
{
    if (m_file == INVALID_HANDLE_VALUE)
    {
        m_initialized = false;
        m_finalized = true;
        return;
    }

    if (!m_finalized)
    {
        uint32_t clampedDataSize = static_cast<uint32_t>(std::min<uint64_t>(m_dataBytesWritten, UINT32_MAX));
        uint32_t riffSize = m_riffSizeWithoutData + clampedDataSize;

        HRESULT ignored = S_OK;
        PatchUInt32(RiffSizeOffset, riffSize, &ignored);
        PatchUInt32(m_dataSizeOffset, clampedDataSize, &ignored);
    }

    CloseHandle(m_file);
    m_file = INVALID_HANDLE_VALUE;
    m_initialized = false;
    m_finalized = true;
}

uint32_t WindowsWaveSinkWriter::GetFormatChunkSize(const WAVEFORMATEX* audioFormat)
{
    if (audioFormat->wFormatTag == WAVE_FORMAT_PCM)
    {
        return PcmFormatChunkSize;
    }

    return static_cast<uint32_t>(sizeof(WAVEFORMATEX) + audioFormat->cbSize);
}

bool WindowsWaveSinkWriter::WriteBytes(const void* data, uint32_t byteCount, HRESULT* outHr)
{
    DWORD bytesWritten = 0;
    if (!WriteFile(m_file, data, byteCount, &bytesWritten, nullptr) || bytesWritten != byteCount)
    {
        if (outHr) *outHr = HRESULT_FROM_WIN32(GetLastError());
        return false;
    }

    return true;
}

bool WindowsWaveSinkWriter::PatchUInt32(uint32_t offset, uint32_t value, HRESULT* outHr)
{
    LARGE_INTEGER position{};
    position.QuadPart = offset;
    if (!SetFilePointerEx(m_file, position, nullptr, FILE_BEGIN))
    {
        if (outHr) *outHr = HRESULT_FROM_WIN32(GetLastError());
        return false;
    }

    if (!WriteBytes(&value, sizeof(value), outHr))
    {
        return false;
    }

    LARGE_INTEGER endPosition{};
    endPosition.QuadPart = 0;
    return SetFilePointerEx(m_file, endPosition, nullptr, FILE_END) != FALSE;
}
