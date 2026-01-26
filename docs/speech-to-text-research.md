# Audio Sample Speech-to-Text Scanner - Technology Research

## Executive Summary

This document presents research findings and technology recommendations for implementing a speech-to-text metadata scanner for the CaptureTool application. The scanner will analyze audio samples from video captures and detect speech, outputting text that can be used for automatic caption generation.

## Current Architecture Context

CaptureTool is a Windows screen capture utility built with:
- **Platform**: Windows App SDK 1.8, WinUI 3
- **Language**: C# 14 / .NET 10
- **Architecture**: Clean architecture with domain-driven design

The application already has a metadata scanning framework in place:
- `IAudioMetadataScanner` interface for audio sample scanning
- `AudioSampleData` structure containing raw audio data (PCM format)
- `MetadataEntry` system for storing extracted metadata
- Real-time and batch scanning job support via `IRealTimeMetadataScanJob`

## Technology Options

### Option 1: Microsoft Azure Speech Service (RECOMMENDED)

**Description**: Cloud-based speech recognition service with comprehensive .NET SDK support.

**Pros**:
- ✅ **Excellent .NET Integration**: Official `Microsoft.CognitiveServices.Speech` NuGet package
- ✅ **Native Windows Support**: Built by Microsoft for seamless Windows integration
- ✅ **High Accuracy**: State-of-the-art speech recognition with continuous improvements
- ✅ **Real-Time Streaming**: Perfect fit for continuous audio capture scenarios
- ✅ **Multiple Languages**: Supports 100+ languages and dialects
- ✅ **Rich Features**: Speaker diarization, punctuation, custom vocabulary
- ✅ **Enterprise-Ready**: Strong security, compliance (GDPR, HIPAA), SLA guarantees
- ✅ **Flexible Deployment**: Cloud, hybrid, or on-premises containers available
- ✅ **Free Tier**: Generous free tier (5 audio hours/month) for development/testing

**Cons**:
- ❌ **Requires Internet**: Cloud-based (but on-premises option available)
- ❌ **Recurring Costs**: Pay-per-use after free tier ($1/hour for standard)
- ❌ **Privacy Considerations**: Audio data sent to cloud (mitigated with on-premises)

**Implementation Complexity**: ⭐⭐ Low (2/5)

**Sample Integration**:
```csharp
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;

public class AzureSpeechToTextScanner : IAudioMetadataScanner
{
    private readonly SpeechConfig _config;
    
    public string ScannerId => "azure-speech-to-text";
    public string Name => "Azure Speech Recognition";
    public MetadataScannerType ScannerType => MetadataScannerType.Audio;
    
    public async Task<MetadataEntry?> ScanSampleAsync(
        AudioSampleData sampleData, 
        CancellationToken cancellationToken)
    {
        // Convert AudioSampleData to audio stream
        var audioFormat = AudioStreamFormat.GetWaveFormatPCM(
            sampleData.SampleRate, 
            sampleData.BitsPerSample, 
            sampleData.Channels);
        
        using var pushStream = AudioInputStream.CreatePushStream(audioFormat);
        using var audioConfig = AudioConfig.FromStreamInput(pushStream);
        using var recognizer = new SpeechRecognizer(_config, audioConfig);
        
        // Push audio data
        byte[] audioBytes = new byte[sampleData.NumFrames * sampleData.Channels * 2];
        Marshal.Copy(sampleData.pData, audioBytes, 0, audioBytes.Length);
        pushStream.Write(audioBytes);
        
        // Recognize speech
        var result = await recognizer.RecognizeOnceAsync();
        
        if (result.Reason == ResultReason.RecognizedSpeech)
        {
            return new MetadataEntry(
                timestamp: sampleData.Timestamp,
                scannerId: ScannerId,
                key: "speech-text",
                value: result.Text,
                additionalData: new Dictionary<string, object?>
                {
                    ["confidence"] = result.Properties.GetProperty(
                        PropertyId.SpeechServiceResponse_JsonResult)
                });
        }
        
        return null;
    }
}
```

**Recommendation Level**: ⭐⭐⭐⭐⭐ Strongly Recommended

**Best For**: Production deployment, enterprise applications, applications requiring high accuracy and language support.

---

### Option 2: OpenAI Whisper via whisper.net (LOCAL INFERENCE)

**Description**: Local speech recognition using OpenAI's Whisper model through whisper.net (C++ bindings).

**Pros**:
- ✅ **Full Privacy**: All processing happens locally, no data leaves device
- ✅ **No Recurring Costs**: One-time implementation cost only
- ✅ **Offline Support**: Works without internet connection
- ✅ **Excellent Accuracy**: State-of-the-art open-source model
- ✅ **Cross-Platform**: Works on Windows, Linux, macOS
- ✅ **Multiple Languages**: Supports 99+ languages
- ✅ **Model Flexibility**: Choose from tiny, base, small, medium, large models
- ✅ **Active Community**: Well-maintained open-source project

**Cons**:
- ❌ **CPU/GPU Intensive**: Requires significant computational resources
- ❌ **Slower Than Cloud**: Local inference is slower than cloud APIs
- ❌ **Memory Usage**: Larger models require significant RAM (1-10GB)
- ❌ **Initial Setup**: Model download and configuration required
- ❌ **Hardware Variability**: Performance varies greatly by user hardware

**Implementation Complexity**: ⭐⭐⭐ Medium (3/5)

**Sample Integration**:
```csharp
using Whisper.net;

public class WhisperLocalScanner : IAudioMetadataScanner
{
    private readonly WhisperFactory _factory;
    
    public string ScannerId => "whisper-local";
    public string Name => "Whisper Local Speech Recognition";
    public MetadataScannerType ScannerType => MetadataScannerType.Audio;
    
    public async Task<MetadataEntry?> ScanSampleAsync(
        AudioSampleData sampleData, 
        CancellationToken cancellationToken)
    {
        // Convert AudioSampleData to WAV format stream
        using var audioStream = ConvertToWavStream(sampleData);
        using var processor = _factory.CreateProcessor();
        
        // Process audio
        await foreach (var segment in processor.ProcessAsync(audioStream))
        {
            return new MetadataEntry(
                timestamp: sampleData.Timestamp,
                scannerId: ScannerId,
                key: "speech-text",
                value: segment.Text,
                additionalData: new Dictionary<string, object?>
                {
                    ["start"] = segment.Start,
                    ["end"] = segment.End,
                    ["probability"] = segment.Probability
                });
        }
        
        return null;
    }
}
```

**Recommendation Level**: ⭐⭐⭐⭐ Recommended (for privacy-focused scenarios)

**Best For**: Privacy-sensitive applications, offline scenarios, users with powerful hardware, avoiding recurring costs.

---

### Option 3: Windows.Media.SpeechRecognition (WinRT)

**Description**: Built-in Windows speech recognition API available since Windows 10.

**Pros**:
- ✅ **No Additional Dependencies**: Built into Windows 10/11
- ✅ **Free**: No recurring costs
- ✅ **Local Processing**: All processing on-device
- ✅ **Low Latency**: Fast recognition for command/control
- ✅ **WinUI Integration**: Native integration with Windows App SDK

**Cons**:
- ❌ **Lower Accuracy**: Not as accurate as Azure or Whisper for dictation
- ❌ **Limited Features**: Basic speech recognition only
- ❌ **Windows Only**: Not cross-platform
- ❌ **Command-Focused**: Better for commands than continuous dictation
- ❌ **Limited Language Support**: Fewer languages than cloud solutions

**Implementation Complexity**: ⭐⭐ Low (2/5)

**Sample Integration**:
```csharp
using Windows.Media.SpeechRecognition;

public class WinRTSpeechScanner : IAudioMetadataScanner
{
    private readonly SpeechRecognizer _recognizer;
    
    public string ScannerId => "winrt-speech";
    public string Name => "Windows Speech Recognition";
    public MetadataScannerType ScannerType => MetadataScannerType.Audio;
    
    public async Task<MetadataEntry?> ScanSampleAsync(
        AudioSampleData sampleData, 
        CancellationToken cancellationToken)
    {
        // Note: WinRT SpeechRecognizer typically works with microphone input
        // For audio streams, requires additional plumbing
        
        var result = await _recognizer.RecognizeAsync();
        
        if (result.Status == SpeechRecognitionResultStatus.Success)
        {
            return new MetadataEntry(
                timestamp: sampleData.Timestamp,
                scannerId: ScannerId,
                key: "speech-text",
                value: result.Text,
                additionalData: new Dictionary<string, object?>
                {
                    ["confidence"] = result.Confidence
                });
        }
        
        return null;
    }
}
```

**Recommendation Level**: ⭐⭐⭐ Consider (for simple scenarios)

**Best For**: Simple command recognition, prototypes, users wanting zero-cost solution with basic accuracy.

---

### Option 4: Google Cloud Speech-to-Text

**Description**: Google's cloud-based speech recognition service.

**Pros**:
- ✅ **High Accuracy**: Excellent recognition quality
- ✅ **Wide Language Support**: 125+ languages
- ✅ **Real-Time Streaming**: Low-latency streaming support
- ✅ **Rich Features**: Speaker diarization, profanity filtering
- ✅ **Free Tier**: 60 minutes/month free

**Cons**:
- ❌ **Less .NET Focus**: Not as well-integrated with .NET as Azure
- ❌ **Requires Internet**: Cloud-based only
- ❌ **Setup Complexity**: More complex authentication/setup
- ❌ **Recurring Costs**: Similar pricing to Azure

**Implementation Complexity**: ⭐⭐⭐ Medium (3/5)

**Recommendation Level**: ⭐⭐⭐ Consider (if already using Google Cloud)

---

### Option 5: Deepgram

**Description**: Specialized speech-to-text API with focus on real-time streaming.

**Pros**:
- ✅ **Excellent Real-Time Performance**: Very low latency
- ✅ **High Accuracy**: Competitive with major cloud providers
- ✅ **WebSocket Streaming**: Great for live audio
- ✅ **Competitive Pricing**: Often cheaper than Azure/Google

**Cons**:
- ❌ **Smaller Provider**: Less enterprise presence than Microsoft/Google
- ❌ **Less Mature .NET Support**: REST API integration required
- ❌ **Requires Internet**: Cloud-based only

**Implementation Complexity**: ⭐⭐⭐ Medium (3/5)

**Recommendation Level**: ⭐⭐⭐ Consider (for real-time streaming focus)

---

## Comparison Matrix

| Feature | Azure Speech | Whisper.net | WinRT | Google STT | Deepgram |
|---------|-------------|-------------|-------|------------|----------|
| **Accuracy** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐ |
| **.NET Integration** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐⭐ |
| **Real-Time Performance** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐⭐⭐ | ⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ |
| **Language Support** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐ |
| **Privacy (Local)** | ⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐ | ⭐ |
| **Cost (Free Tier)** | ⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐ | ⭐⭐⭐ |
| **Offline Support** | ⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐ | ⭐ |
| **Setup Simplicity** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐⭐ |

## Final Recommendations

### Primary Recommendation: Azure Speech Service

**Azure Speech Service is the best choice for CaptureTool** for the following reasons:

1. **Perfect Ecosystem Fit**: Built by Microsoft for .NET and Windows applications
2. **Production-Ready**: Enterprise-grade reliability, security, and support
3. **Excellent Developer Experience**: First-class .NET SDK with great documentation
4. **Generous Free Tier**: 5 hours/month free is sufficient for development and many users
5. **Feature-Rich**: Includes advanced features like speaker diarization and custom models
6. **Scalability**: Easily handles both individual users and enterprise deployments
7. **Real-Time Streaming**: Perfect for processing audio as it's captured

### Secondary Recommendation: Whisper.net (Local)

**For users who prioritize privacy or offline use**, Whisper.net is an excellent alternative:

1. **Complete Privacy**: All processing happens locally
2. **No Recurring Costs**: One-time implementation, no per-use fees
3. **Offline Support**: Works without internet connection
4. **Open Source**: Transparent, auditable, community-driven

### Implementation Strategy

I recommend a **hybrid approach**:

1. **Implement Azure Speech Service first** as the primary/default scanner
   - Provides best accuracy and user experience
   - Easy to implement and test
   - Great for most users

2. **Add Whisper.net as an optional scanner** for privacy-conscious users
   - Configurable via settings
   - Users can choose based on their privacy/cost preferences
   - Provides competitive advantage for privacy-focused market segment

3. **Feature Flag Support**: Use the existing `Feature_VideoCapture_MetadataCollection` flag
   - Allow users to enable/disable speech-to-text
   - Configure which scanner to use
   - A/B test different implementations

4. **Performance Optimization**:
   - Batch audio samples before sending to API (reduce API calls)
   - Implement caching for repeated phrases
   - Use async processing to avoid blocking UI
   - Provide quality settings (accuracy vs. speed trade-off)

## Technical Considerations

### Audio Format Requirements
- **Azure Speech**: Supports PCM, WAV, OGG, MP3, FLAC
- **Whisper**: Requires WAV format (easy to convert from PCM)
- Current `AudioSampleData` provides PCM format - perfect starting point

### Memory and Performance
- Audio buffering: Collect 1-2 seconds of audio before processing
- Async processing: Don't block video capture pipeline
- Error handling: Graceful degradation if service unavailable

### Privacy and Compliance
- Clear user notification when speech recognition is active
- Privacy policy updates for cloud processing
- Option to disable/enable feature
- Secure credential storage for API keys

### Cost Management
- Monitor API usage via Azure portal
- Implement usage quotas per user
- Warn users approaching limits
- Provide usage statistics in settings

## Next Steps

1. **Get approval** on technology choice (Azure Speech + optional Whisper.net)
2. **Proof of Concept**: Implement basic Azure Speech scanner
3. **Testing**: Verify accuracy with real captured audio
4. **UI/UX Design**: Design settings and caption display
5. **Full Implementation**: Complete scanner implementation
6. **Documentation**: Update user documentation
7. **Testing**: Comprehensive testing across scenarios
8. **Release**: Feature flag rollout

## References

- [Azure Speech Service Documentation](https://learn.microsoft.com/en-us/azure/ai-services/speech-service/)
- [Azure Speech SDK for .NET](https://www.nuget.org/packages/Microsoft.CognitiveServices.Speech)
- [whisper.net GitHub Repository](https://github.com/sandrohanea/whisper.net)
- [OpenAI Whisper Model](https://openai.com/research/whisper)
- [Windows.Media.SpeechRecognition Documentation](https://learn.microsoft.com/en-us/uwp/api/windows.media.speechrecognition)

---

**Document Version**: 1.0  
**Last Updated**: January 26, 2026  
**Author**: GitHub Copilot Agent  
**Status**: Awaiting Approval
