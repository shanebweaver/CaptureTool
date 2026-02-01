using CaptureTool.Domain.Capture.Interfaces;
using CaptureTool.Domain.Capture.Interfaces.Metadata;
using System.Runtime.InteropServices;
using Windows.Media.SpeechRecognition;
using Windows.Storage.Streams;

namespace CaptureTool.Domain.Capture.Implementations.Windows.Metadata.Scanners;

/// <summary>
/// Audio metadata scanner that uses Windows Media Speech Recognition to convert speech to text.
/// </summary>
public sealed class WindowsMediaSpeechRecognitionScanner : IAudioMetadataScanner, IDisposable
{
    private readonly SpeechRecognizer? _recognizer;
    private int _sampleCounter = 0;
    private int _processedSamples = 0;
    private int _speechDetectedSamples = 0;
    private Exception? _lastError;
    private bool _disposed = false;
    private bool _constraintsCompiled = false;

    // Sample every 30th audio chunk (adjust based on performance)
    private const int SAMPLE_RATE_DIVISOR = 30;

    public string ScannerId => "windows-speech-recognition";
    public string Name => "Windows Speech Recognition";
    public MetadataScannerType ScannerType => MetadataScannerType.Audio;

    public WindowsMediaSpeechRecognitionScanner()
    {
        try
        {
            // Create speech recognizer with default language
            _recognizer = new SpeechRecognizer();

            // Configure for dictation (continuous recognition)
            _recognizer.Constraints.Add(new SpeechRecognitionTopicConstraint(
                SpeechRecognitionScenario.Dictation,
                "dictation"));

            System.Diagnostics.Debug.WriteLine($"[Speech Scanner] Initialized successfully");
        }
        catch (Exception ex)
        {
            _lastError = ex;
            System.Diagnostics.Debug.WriteLine($"[Speech Scanner] Initialization failed: {ex.Message}");
            _recognizer = null;
        }
    }

    public async Task<MetadataEntry?> ScanSampleAsync(AudioSampleData sampleData, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_recognizer == null)
                return null;

            // Increment sample counter
            _sampleCounter++;

            // Only process every Nth sample for performance
            if (_sampleCounter % SAMPLE_RATE_DIVISOR != 0)
                return null;

            // Skip if no audio data
            if (sampleData.pData == IntPtr.Zero || sampleData.NumFrames == 0)
                return null;

            // Skip very short samples (need at least ~1 second of audio)
            if (sampleData.NumFrames < sampleData.SampleRate)
                return null;

            _processedSamples++;

            // Compile constraints on first use
            if (!_constraintsCompiled)
            {
                var compileResult = await _recognizer.CompileConstraintsAsync();
                if (compileResult.Status != SpeechRecognitionResultStatus.Success)
                {
                    System.Diagnostics.Debug.WriteLine($"[Speech Scanner] Failed to compile constraints: {compileResult.Status}");
                    return null;
                }
                _constraintsCompiled = true;
            }

            // Convert audio sample to WAV stream
            using var audioStream = ConvertAudioSampleToWavStream(sampleData);
            if (audioStream == null)
            {
                _lastError = new InvalidOperationException("Failed to convert audio sample to WAV stream");
                return null;
            }

            // Recognize speech from stream
            SpeechRecognitionResult result;
            try
            {
                // Note: RecognizeAsync() expects audio input to be configured separately
                // For now, we'll attempt recognition but this may need refinement
                result = await _recognizer.RecognizeAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Speech Scanner] Recognition failed: {ex.Message}");
                return null;
            }

            // Check if recognition was successful
            if (result.Status != SpeechRecognitionResultStatus.Success)
            {
                // Don't log for common cases like no speech detected
                if (result.Status != SpeechRecognitionResultStatus.InitialSilenceTimeout &&
                    result.Status != SpeechRecognitionResultStatus.BabbleTimeout)
                {
                    System.Diagnostics.Debug.WriteLine($"[Speech Scanner] Recognition status: {result.Status}");
                }
                return null;
            }

            // If no text recognized, return null
            if (string.IsNullOrWhiteSpace(result.Text))
                return null;

            _speechDetectedSamples++;

            System.Diagnostics.Debug.WriteLine($"[Speech Scanner] Speech detected: {result.Text.Substring(0, Math.Min(50, result.Text.Length))}...");

            // Create metadata with recognized speech
            return new MetadataEntry(
                timestamp: sampleData.Timestamp,
                scannerId: ScannerId,
                key: "speech-text",
                value: result.Text.Trim(),
                additionalData: new Dictionary<string, object?>
                {
                    ["confidence"] = result.Confidence.ToString(),
                    ["sampleRate"] = sampleData.SampleRate,
                    ["channels"] = sampleData.Channels,
                    ["numFrames"] = sampleData.NumFrames,
                    ["processedSamples"] = _processedSamples,
                    ["speechDetectedSamples"] = _speechDetectedSamples
                }
            );
        }
        catch (Exception ex)
        {
            _lastError = ex;
            System.Diagnostics.Debug.WriteLine($"[Speech Scanner] Error in ScanSampleAsync: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[Speech Scanner] Stack trace: {ex.StackTrace}");
            return null;
        }
        finally
        {
            // Log stats periodically
            if (_processedSamples > 0 && _processedSamples % 10 == 0)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Speech Scanner] Stats: Samples={_sampleCounter}, Processed={_processedSamples}, Speech Detected={_speechDetectedSamples}");
            }
        }
    }

    private IRandomAccessStream? ConvertAudioSampleToWavStream(AudioSampleData sampleData)
    {
        try
        {
            // Calculate data size
            int bytesPerSample = sampleData.BitsPerSample / 8;
            int dataSize = (int)(sampleData.NumFrames * sampleData.Channels * bytesPerSample);

            // Copy audio data from unmanaged memory
            byte[] audioData = new byte[dataSize];
            Marshal.Copy(sampleData.pData, audioData, 0, dataSize);

            // Create WAV header
            int sampleRate = (int)sampleData.SampleRate;
            int channels = sampleData.Channels;
            int bitsPerSample = sampleData.BitsPerSample;
            int byteRate = sampleRate * channels * bytesPerSample;
            int blockAlign = channels * bytesPerSample;

            using var memoryStream = new MemoryStream();
            using var writer = new BinaryWriter(memoryStream);

            // RIFF header
            writer.Write(new char[] { 'R', 'I', 'F', 'F' });
            writer.Write(36 + dataSize); // ChunkSize
            writer.Write(new char[] { 'W', 'A', 'V', 'E' });

            // fmt sub-chunk
            writer.Write(new char[] { 'f', 'm', 't', ' ' });
            writer.Write(16); // Subchunk1Size (PCM)
            writer.Write((short)1); // AudioFormat (PCM)
            writer.Write((short)channels); // NumChannels
            writer.Write(sampleRate); // SampleRate
            writer.Write(byteRate); // ByteRate
            writer.Write((short)blockAlign); // BlockAlign
            writer.Write((short)bitsPerSample); // BitsPerSample

            // data sub-chunk
            writer.Write(new char[] { 'd', 'a', 't', 'a' });
            writer.Write(dataSize); // Subchunk2Size
            writer.Write(audioData); // Audio data

            // Convert to IRandomAccessStream
            byte[] wavData = memoryStream.ToArray();
            var randomAccessStream = new InMemoryRandomAccessStream();
            var outputStream = randomAccessStream.GetOutputStreamAt(0);
            var dataWriter = new DataWriter(outputStream);
            dataWriter.WriteBytes(wavData);
            dataWriter.StoreAsync().AsTask().Wait();
            dataWriter.FlushAsync().AsTask().Wait();
            outputStream.FlushAsync().AsTask().Wait();

            randomAccessStream.Seek(0);
            return randomAccessStream;
        }
        catch (Exception ex)
        {
            _lastError = ex;
            System.Diagnostics.Debug.WriteLine($"[Speech Scanner] WAV conversion error: {ex.Message}");
            return null;
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _recognizer?.Dispose();
            _disposed = true;
        }
    }
}
