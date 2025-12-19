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
    
    // Initialize circular buffer (2 seconds of audio)
    size_t bufferSize = entry.format.nAvgBytesPerSec * 2;
    entry.audioBuffer.resize(bufferSize, 0);
    entry.writePos = 0;
    entry.readPos = 0;
    entry.availableBytes = 0;
    entry.lastTimestamp = 0;

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

    m_sources.push_back(std::move(entry));
    
    // Set up callback to receive audio data
    uint64_t sourceId = m_sources.back().sourceId;
    source->SetAudioCallback([this, sourceId](const BYTE* data, UINT32 numFrames, LONGLONG timestamp) {
        this->OnAudioCallback(sourceId, data, numFrames, timestamp);
    });
    
    return sourceId;
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

        // Convert and mix this source into the output
        ConvertAndMixSource(entry, outputBuffer, outputFrames, timestamp);
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
    // Read audio data from the source's circular buffer
    UINT32 framesRead = ReadFromBuffer(entry, m_tempBuffer.data(), mixFrames);
    if (framesRead == 0)
    {
        return 0;  // No data available
    }
    
    BYTE* sourceData = m_tempBuffer.data();
    UINT32 sourceFrames = framesRead;
    
    // Apply sample rate conversion if needed
    auto resamplerIt = m_resamplers.find(entry.sourceId);
    if (resamplerIt != m_resamplers.end())
    {
        IMFTransform* resampler = resamplerIt->second.get();
        
        // Create input sample
        wil::com_ptr<IMFSample> inputSample;
        wil::com_ptr<IMFMediaBuffer> inputBuffer;
        
        HRESULT hr = MFCreateMemoryBuffer(sourceFrames * entry.format.nBlockAlign, inputBuffer.put());
        if (FAILED(hr)) return 0;
        
        BYTE* bufferPtr = nullptr;
        hr = inputBuffer->Lock(&bufferPtr, nullptr, nullptr);
        if (FAILED(hr)) return 0;
        
        memcpy(bufferPtr, sourceData, sourceFrames * entry.format.nBlockAlign);
        inputBuffer->Unlock();
        inputBuffer->SetCurrentLength(sourceFrames * entry.format.nBlockAlign);
        
        hr = MFCreateSample(inputSample.put());
        if (FAILED(hr)) return 0;
        
        inputSample->AddBuffer(inputBuffer.get());
        
        // Process through resampler
        hr = resampler->ProcessInput(0, inputSample.get(), 0);
        if (FAILED(hr)) return 0;
        
        // Get output
        MFT_OUTPUT_DATA_BUFFER outputData = {};
        wil::com_ptr<IMFSample> outputSample;
        wil::com_ptr<IMFMediaBuffer> outputBuffer;
        
        hr = MFCreateSample(outputSample.put());
        if (FAILED(hr)) return 0;
        
        hr = MFCreateMemoryBuffer(mixFrames * m_outputFormat.nBlockAlign, outputBuffer.put());
        if (FAILED(hr)) return 0;
        
        outputSample->AddBuffer(outputBuffer.get());
        outputData.pSample = outputSample.get();
        
        DWORD status = 0;
        hr = resampler->ProcessOutput(0, 1, &outputData, &status);
        if (hr == MF_E_TRANSFORM_NEED_MORE_INPUT)
        {
            return 0;  // Need more input data
        }
        if (FAILED(hr)) return 0;
        
        // Extract resampled data
        wil::com_ptr<IMFMediaBuffer> outBuffer;
        hr = outputSample->ConvertToContiguousBuffer(outBuffer.put());
        if (FAILED(hr)) return 0;
        
        BYTE* outData = nullptr;
        DWORD outLength = 0;
        hr = outBuffer->Lock(&outData, nullptr, &outLength);
        if (FAILED(hr)) return 0;
        
        sourceFrames = outLength / m_outputFormat.nBlockAlign;
        memcpy(m_mixBuffer.data(), outData, outLength);
        outBuffer->Unlock();
        
        sourceData = m_mixBuffer.data();
    }
    
    // Apply volume to the source data
    ApplyVolume(sourceData, sourceFrames, entry.volume);
    
    // Mix into output buffer
    MixBuffers(mixBuffer, sourceData, sourceFrames);
    
    return sourceFrames;
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

    // Read audio data from circular buffer
    UINT32 framesRead = ReadFromBuffer(*entry, m_tempBuffer.data(), outputFrames);
    if (framesRead == 0)
    {
        // No data available, return silence
        ZeroMemory(outputBuffer, outputFrames * m_outputFormat.nBlockAlign);
        return 0;
    }
    
    BYTE* sourceData = m_tempBuffer.data();
    UINT32 sourceFrames = framesRead;
    
    // Apply sample rate conversion if needed
    auto resamplerIt = m_resamplers.find(sourceId);
    if (resamplerIt != m_resamplers.end())
    {
        IMFTransform* resampler = resamplerIt->second.get();
        
        // Create input sample
        wil::com_ptr<IMFSample> inputSample;
        wil::com_ptr<IMFMediaBuffer> inputBuffer;
        
        HRESULT hr = MFCreateMemoryBuffer(sourceFrames * entry->format.nBlockAlign, inputBuffer.put());
        if (SUCCEEDED(hr))
        {
            BYTE* bufferPtr = nullptr;
            hr = inputBuffer->Lock(&bufferPtr, nullptr, nullptr);
            if (SUCCEEDED(hr))
            {
                memcpy(bufferPtr, sourceData, sourceFrames * entry->format.nBlockAlign);
                inputBuffer->Unlock();
                inputBuffer->SetCurrentLength(sourceFrames * entry->format.nBlockAlign);
                
                hr = MFCreateSample(inputSample.put());
                if (SUCCEEDED(hr))
                {
                    inputSample->AddBuffer(inputBuffer.get());
                    hr = resampler->ProcessInput(0, inputSample.get(), 0);
                    
                    if (SUCCEEDED(hr))
                    {
                        // Get output
                        MFT_OUTPUT_DATA_BUFFER outputData = {};
                        wil::com_ptr<IMFSample> outputSample;
                        wil::com_ptr<IMFMediaBuffer> outputBuffer_;
                        
                        hr = MFCreateSample(outputSample.put());
                        if (SUCCEEDED(hr))
                        {
                            hr = MFCreateMemoryBuffer(outputFrames * m_outputFormat.nBlockAlign, outputBuffer_.put());
                            if (SUCCEEDED(hr))
                            {
                                outputSample->AddBuffer(outputBuffer_.get());
                                outputData.pSample = outputSample.get();
                                
                                DWORD status = 0;
                                hr = resampler->ProcessOutput(0, 1, &outputData, &status);
                                if (SUCCEEDED(hr))
                                {
                                    // Extract resampled data
                                    wil::com_ptr<IMFMediaBuffer> outBuffer;
                                    hr = outputSample->ConvertToContiguousBuffer(outBuffer.put());
                                    if (SUCCEEDED(hr))
                                    {
                                        BYTE* outData = nullptr;
                                        DWORD outLength = 0;
                                        hr = outBuffer->Lock(&outData, nullptr, &outLength);
                                        if (SUCCEEDED(hr))
                                        {
                                            sourceFrames = outLength / m_outputFormat.nBlockAlign;
                                            memcpy(outputBuffer, outData, outLength);
                                            outBuffer->Unlock();
                                            sourceData = outputBuffer;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }
    else
    {
        // No resampling needed, copy directly
        memcpy(outputBuffer, sourceData, sourceFrames * m_outputFormat.nBlockAlign);
    }
    
    // Apply volume
    ApplyVolume(outputBuffer, sourceFrames, entry->volume);
    
    return sourceFrames;
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

void AudioMixer::OnAudioCallback(uint64_t sourceId, const BYTE* data, UINT32 numFrames, LONGLONG timestamp)
{
    std::lock_guard<std::mutex> lock(m_mutex);
    
    AudioSourceEntry* entry = FindSource(sourceId);
    if (!entry)
    {
        return;  // Source not found
    }
    
    std::lock_guard<std::mutex> bufferLock(entry->bufferMutex);
    
    // Calculate bytes to write
    UINT32 bytesToWrite = numFrames * entry->format.nBlockAlign;
    
    // If buffer would overflow, drop oldest data
    if (entry->availableBytes + bytesToWrite > entry->audioBuffer.size())
    {
        // Advance read position to make room
        size_t bytesToDrop = (entry->availableBytes + bytesToWrite) - entry->audioBuffer.size();
        entry->readPos = (entry->readPos + bytesToDrop) % entry->audioBuffer.size();
        entry->availableBytes -= bytesToDrop;
    }
    
    // Write data to circular buffer
    for (UINT32 i = 0; i < bytesToWrite; i++)
    {
        entry->audioBuffer[entry->writePos] = data[i];
        entry->writePos = (entry->writePos + 1) % entry->audioBuffer.size();
    }
    
    entry->availableBytes += bytesToWrite;
    entry->lastTimestamp = timestamp;
}

UINT32 AudioMixer::ReadFromBuffer(AudioSourceEntry& entry, BYTE* buffer, UINT32 maxFrames)
{
    std::lock_guard<std::mutex> bufferLock(entry.bufferMutex);
    
    // Calculate bytes to read
    UINT32 maxBytes = maxFrames * entry.format.nBlockAlign;
    UINT32 bytesToRead = (maxBytes < entry.availableBytes) ? maxBytes : static_cast<UINT32>(entry.availableBytes);
    
    if (bytesToRead == 0)
    {
        return 0;  // No data available
    }
    
    // Read data from circular buffer
    for (UINT32 i = 0; i < bytesToRead; i++)
    {
        buffer[i] = entry.audioBuffer[entry.readPos];
        entry.readPos = (entry.readPos + 1) % entry.audioBuffer.size();
    }
    
    entry.availableBytes -= bytesToRead;
    
    // Return number of frames read
    return bytesToRead / entry.format.nBlockAlign;
}
