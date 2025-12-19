#include "pch.h"
#include "AudioMixer.h"
#include <algorithm>
#include <cmath>

// Helper function to avoid Windows min/max macro conflicts
template<typename T>
inline T Clamp(T value, T minVal, T maxVal)
{
    if (value < minVal) return minVal;
    if (value > maxVal) return maxVal;
    return value;
}

AudioMixer::AudioMixer()
    : m_initialized(false)
    , m_nextSourceId(1)
{
    ZeroMemory(&m_outputFormat, sizeof(m_outputFormat));
}

AudioMixer::~AudioMixer()
{
    // Clear all resamplers
    m_resamplers.clear();
}

bool AudioMixer::Initialize(UINT32 sampleRate, UINT16 channels, UINT16 bitsPerSample)
{
    std::lock_guard<std::mutex> lock(m_mutex);

    if (m_initialized)
    {
        return false; // Already initialized
    }

    // Validate parameters
    if (sampleRate == 0 || (channels != 1 && channels != 2) || (bitsPerSample != 16 && bitsPerSample != 32))
    {
        return false;
    }

    // Initialize Media Foundation
    HRESULT hr = MFStartup(MF_VERSION);
    if (FAILED(hr))
    {
        return false;
    }

    // Set up output format
    m_outputFormat.wFormatTag = (bitsPerSample == 32) ? WAVE_FORMAT_IEEE_FLOAT : WAVE_FORMAT_PCM;
    m_outputFormat.nChannels = channels;
    m_outputFormat.nSamplesPerSec = sampleRate;
    m_outputFormat.wBitsPerSample = bitsPerSample;
    m_outputFormat.nBlockAlign = (channels * bitsPerSample) / 8;
    m_outputFormat.nAvgBytesPerSec = sampleRate * m_outputFormat.nBlockAlign;
    m_outputFormat.cbSize = 0;

    // Pre-allocate mixing buffers (10 seconds worth of audio)
    size_t bufferSize = m_outputFormat.nAvgBytesPerSec * 10;
    m_tempBuffer.resize(bufferSize);
    m_mixBuffer.resize(bufferSize);
    m_floatBuffer.resize(bufferSize / sizeof(float));

    m_initialized = true;
    return true;
}

uint64_t AudioMixer::RegisterSource(IAudioSource* source, float volume)
{
    if (!source)
    {
        return 0;
    }

    std::lock_guard<std::mutex> lock(m_mutex);

    if (!m_initialized)
    {
        return 0;
    }

    // Get the source format
    WAVEFORMATEX* sourceFormat = source->GetFormat();
    if (!sourceFormat)
    {
        return 0;
    }

    // Create entry
    AudioSourceEntry entry;
    entry.source = source;
    entry.volume = Clamp(volume, 0.0f, 2.0f);  // Clamp to 0.0-2.0
    entry.muted = false;
    entry.sourceId = m_nextSourceId++;
    CopyMemory(&entry.format, sourceFormat, sizeof(WAVEFORMATEX));

    // If source format doesn't match output format, create a resampler
    if (entry.format.nSamplesPerSec != m_outputFormat.nSamplesPerSec ||
        entry.format.nChannels != m_outputFormat.nChannels ||
        entry.format.wBitsPerSample != m_outputFormat.wBitsPerSample)
    {
        wil::com_ptr<IMFTransform> resampler;
        if (!CreateResampler(&entry.format, &m_outputFormat, resampler.put()))
        {
            return 0;
        }
        m_resamplers[entry.sourceId] = std::move(resampler);
    }

    m_sources.push_back(entry);
    return entry.sourceId;
}

void AudioMixer::UnregisterSource(uint64_t sourceId)
{
    std::lock_guard<std::mutex> lock(m_mutex);

    // Remove from sources
    m_sources.erase(
        std::remove_if(m_sources.begin(), m_sources.end(),
            [sourceId](const AudioSourceEntry& entry) { return entry.sourceId == sourceId; }),
        m_sources.end()
    );

    // Remove resampler if exists
    m_resamplers.erase(sourceId);
}

void AudioMixer::SetSourceVolume(uint64_t sourceId, float volume)
{
    std::lock_guard<std::mutex> lock(m_mutex);

    AudioSourceEntry* entry = FindSource(sourceId);
    if (entry)
    {
        entry->volume = Clamp(volume, 0.0f, 2.0f);  // Clamp to 0.0-2.0
    }
}

float AudioMixer::GetSourceVolume(uint64_t sourceId) const
{
    std::lock_guard<std::mutex> lock(m_mutex);

    const AudioSourceEntry* entry = FindSource(sourceId);
    return entry ? entry->volume : 0.0f;
}

void AudioMixer::SetSourceMuted(uint64_t sourceId, bool muted)
{
    std::lock_guard<std::mutex> lock(m_mutex);

    AudioSourceEntry* entry = FindSource(sourceId);
    if (entry)
    {
        entry->muted = muted;
    }
}

bool AudioMixer::IsSourceMuted(uint64_t sourceId) const
{
    std::lock_guard<std::mutex> lock(m_mutex);

    const AudioSourceEntry* entry = FindSource(sourceId);
    return entry ? entry->muted : false;
}

size_t AudioMixer::GetSourceCount() const
{
    std::lock_guard<std::mutex> lock(m_mutex);
    return m_sources.size();
}

const WAVEFORMATEX* AudioMixer::GetOutputFormat() const
{
    return m_initialized ? &m_outputFormat : nullptr;
}

UINT32 AudioMixer::MixAudio(BYTE* outputBuffer, UINT32 outputFrames, LONGLONG timestamp)
{
    if (!m_initialized || !outputBuffer || outputFrames == 0)
    {
        return 0;
    }

    std::lock_guard<std::mutex> lock(m_mutex);

    // Clear output buffer
    size_t outputBytes = outputFrames * m_outputFormat.nBlockAlign;
    ZeroMemory(outputBuffer, outputBytes);

    if (m_sources.empty())
    {
        return outputFrames;  // Return silence
    }

    // Mix each source
    for (AudioSourceEntry& entry : m_sources)
    {
        if (entry.muted || entry.volume == 0.0f)
        {
            continue;  // Skip muted sources
        }

        // TODO: In Phase 3, we'll implement actual audio capture and mixing here
        // For now, this is a framework that will be filled in during implementation
        
        // ConvertAndMixSource(entry, outputBuffer, outputFrames, timestamp);
    }

    return outputFrames;
}

AudioSourceEntry* AudioMixer::FindSource(uint64_t sourceId)
{
    for (AudioSourceEntry& entry : m_sources)
    {
        if (entry.sourceId == sourceId)
        {
            return &entry;
        }
    }
    return nullptr;
}

const AudioSourceEntry* AudioMixer::FindSource(uint64_t sourceId) const
{
    for (const AudioSourceEntry& entry : m_sources)
    {
        if (entry.sourceId == sourceId)
        {
            return &entry;
        }
    }
    return nullptr;
}

bool AudioMixer::CreateResampler(const WAVEFORMATEX* inputFormat, const WAVEFORMATEX* outputFormat, IMFTransform** ppResampler)
{
    if (!inputFormat || !outputFormat || !ppResampler)
    {
        return false;
    }

    // Create Media Foundation resampler
    wil::com_ptr<IMFTransform> resampler;
    HRESULT hr = CoCreateInstance(CLSID_CResamplerMediaObject, nullptr, CLSCTX_INPROC_SERVER,
        IID_PPV_ARGS(resampler.put()));
    if (FAILED(hr))
    {
        return false;
    }

    // Create input media type
    wil::com_ptr<IMFMediaType> inputType;
    hr = MFCreateMediaType(inputType.put());
    if (FAILED(hr))
    {
        return false;
    }

    hr = MFInitMediaTypeFromWaveFormatEx(inputType.get(), inputFormat, sizeof(WAVEFORMATEX));
    if (FAILED(hr))
    {
        return false;
    }

    // Create output media type
    wil::com_ptr<IMFMediaType> outputType;
    hr = MFCreateMediaType(outputType.put());
    if (FAILED(hr))
    {
        return false;
    }

    hr = MFInitMediaTypeFromWaveFormatEx(outputType.get(), outputFormat, sizeof(WAVEFORMATEX));
    if (FAILED(hr))
    {
        return false;
    }

    // Set input type
    hr = resampler->SetInputType(0, inputType.get(), 0);
    if (FAILED(hr))
    {
        return false;
    }

    // Set output type
    hr = resampler->SetOutputType(0, outputType.get(), 0);
    if (FAILED(hr))
    {
        return false;
    }

    // Begin streaming
    hr = resampler->ProcessMessage(MFT_MESSAGE_NOTIFY_BEGIN_STREAMING, 0);
    if (FAILED(hr))
    {
        return false;
    }

    hr = resampler->ProcessMessage(MFT_MESSAGE_NOTIFY_START_OF_STREAM, 0);
    if (FAILED(hr))
    {
        return false;
    }

    *ppResampler = resampler.detach();
    return true;
}

UINT32 AudioMixer::ConvertAndMixSource(AudioSourceEntry& entry, BYTE* mixBuffer, UINT32 mixFrames, LONGLONG timestamp)
{
    // TODO: Implement in full Phase 3 implementation
    // This will:
    // 1. Get audio data from the source
    // 2. Apply sample rate conversion if needed (using resampler from m_resamplers)
    // 3. Apply volume
    // 4. Mix into output buffer
    return 0;
}

void AudioMixer::ApplyVolume(BYTE* buffer, UINT32 numFrames, float volume)
{
    if (volume == 1.0f)
    {
        return;  // No change needed
    }

    UINT32 numSamples = numFrames * m_outputFormat.nChannels;

    if (m_outputFormat.wBitsPerSample == 16)
    {
        // 16-bit PCM
        int16_t* samples = reinterpret_cast<int16_t*>(buffer);
        for (UINT32 i = 0; i < numSamples; i++)
        {
            float value = static_cast<float>(samples[i]) * volume;
            // Clamp to prevent overflow
            value = Clamp(value, -32768.0f, 32767.0f);
            samples[i] = static_cast<int16_t>(value);
        }
    }
    else if (m_outputFormat.wBitsPerSample == 32)
    {
        // 32-bit float
        float* samples = reinterpret_cast<float*>(buffer);
        for (UINT32 i = 0; i < numSamples; i++)
        {
            samples[i] *= volume;
            // Clamp to prevent clipping
            samples[i] = Clamp(samples[i], -1.0f, 1.0f);
        }
    }
}

void AudioMixer::MixBuffers(BYTE* dest, const BYTE* src, UINT32 numFrames)
{
    UINT32 numSamples = numFrames * m_outputFormat.nChannels;

    if (m_outputFormat.wBitsPerSample == 16)
    {
        // 16-bit PCM mixing
        int16_t* destSamples = reinterpret_cast<int16_t*>(dest);
        const int16_t* srcSamples = reinterpret_cast<const int16_t*>(src);
        
        for (UINT32 i = 0; i < numSamples; i++)
        {
            int32_t mixed = static_cast<int32_t>(destSamples[i]) + static_cast<int32_t>(srcSamples[i]);
            // Clamp to prevent overflow
            mixed = Clamp(mixed, -32768, 32767);
            destSamples[i] = static_cast<int16_t>(mixed);
        }
    }
    else if (m_outputFormat.wBitsPerSample == 32)
    {
        // 32-bit float mixing
        float* destSamples = reinterpret_cast<float*>(dest);
        const float* srcSamples = reinterpret_cast<const float*>(src);
        
        for (UINT32 i = 0; i < numSamples; i++)
        {
            destSamples[i] += srcSamples[i];
            // Clamp to prevent clipping
            destSamples[i] = Clamp(destSamples[i], -1.0f, 1.0f);
        }
    }
}

UINT32 AudioMixer::GetSourceAudio(uint64_t sourceId, BYTE* outputBuffer, UINT32 outputFrames, LONGLONG timestamp)
{
    if (!outputBuffer || outputFrames == 0)
    {
        return 0;
    }

    std::lock_guard<std::mutex> lock(m_mutex);

    if (!m_initialized)
    {
        return 0;
    }

    // Find the source
    AudioSourceEntry* entry = FindSource(sourceId);
    if (!entry || !entry->source)
    {
        // Source not found, return silence
        ZeroMemory(outputBuffer, outputFrames * m_outputFormat.nBlockAlign);
        return 0;
    }

    // If source is muted, return silence
    if (entry->muted)
    {
        ZeroMemory(outputBuffer, outputFrames * m_outputFormat.nBlockAlign);
        return outputFrames;
    }

    // TODO: Implement audio data retrieval in Phase 3
    // The IAudioSource uses a callback pattern rather than direct data access
    // For now, return silence as this is a placeholder implementation
    ZeroMemory(outputBuffer, outputFrames * m_outputFormat.nBlockAlign);
    return 0;
}

std::vector<uint64_t> AudioMixer::GetSourceIds() const
{
    std::lock_guard<std::mutex> lock(m_mutex);
    
    std::vector<uint64_t> sourceIds;
    sourceIds.reserve(m_sources.size());
    
    for (const auto& entry : m_sources)
    {
        sourceIds.push_back(entry.sourceId);
    }
    
    return sourceIds;
}
