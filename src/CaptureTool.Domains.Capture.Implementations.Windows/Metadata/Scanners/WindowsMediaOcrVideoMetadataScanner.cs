using CaptureTool.Domains.Capture.Interfaces;
using CaptureTool.Domains.Capture.Interfaces.Metadata;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using System.Runtime.InteropServices.WindowsRuntime;
using WinLanguage = Windows.Globalization.Language;

namespace CaptureTool.Domains.Capture.Implementations.Windows.Metadata.Scanners;

/// <summary>
/// Video metadata scanner that uses Windows OCR to extract text from video frames.
/// Uses native C++ texture conversion for better reliability and performance.
/// </summary>
public sealed class WindowsMediaOcrVideoMetadataScanner : IVideoMetadataScanner
{
    private readonly OcrEngine? _ocrEngine;
    private int _frameCounter = 0;
    private int _processedFrames = 0;
    private int _textDetectedFrames = 0;
    private Exception? _lastError;

    // Sample every 30th frame (at 30fps = 1 scan per second)
    private const int FRAME_SAMPLING_RATE = 30;

    public string ScannerId => "windows-ocr";
    public string Name => "Windows OCR Scanner";
    public MetadataScannerType ScannerType => MetadataScannerType.Video;

    public WindowsMediaOcrVideoMetadataScanner()
    {
        // Try to create OCR engine for user's default language
        _ocrEngine = OcrEngine.TryCreateFromUserProfileLanguages();
        
        if (_ocrEngine == null)
        {
            // Fallback to English if available
            var language = new WinLanguage("en-US");
            _ocrEngine = OcrEngine.TryCreateFromLanguage(language);
        }

        System.Diagnostics.Debug.WriteLine($"[OCR Scanner] Initialized. Engine available: {_ocrEngine != null}");
    }

    public async Task<MetadataEntry?> ScanFrameAsync(VideoFrameData frameData, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_ocrEngine == null)
                return null;

            // Increment frame counter
            _frameCounter++;

            // Only process every Nth frame for performance
            if (_frameCounter % FRAME_SAMPLING_RATE != 0)
                return null;

            // Skip if no texture
            if (frameData.pTexture == IntPtr.Zero)
                return null;

            // Skip very small frames (likely won't have readable text)
            if (frameData.Width < 100 || frameData.Height < 100)
                return null;

            _processedFrames++;

            // Convert D3D11 texture to SoftwareBitmap using native helper
            var softwareBitmap = ConvertTextureToSoftwareBitmap(frameData);
            if (softwareBitmap == null)
            {
                _lastError = new InvalidOperationException("Failed to convert texture to SoftwareBitmap");
                return null;
            }

            // Run OCR on the bitmap
            var ocrResult = await _ocrEngine.RecognizeAsync(softwareBitmap);
            
            // Dispose the bitmap
            softwareBitmap.Dispose();

            // If no text found, return null
            if (ocrResult == null || ocrResult.Lines.Count == 0)
                return null;

            // Extract all text
            var allText = string.Join(" ", ocrResult.Lines.Select(line => line.Text));
            
            // Only create metadata entry if we found meaningful text
            if (string.IsNullOrWhiteSpace(allText))
                return null;

            _textDetectedFrames++;

            System.Diagnostics.Debug.WriteLine($"[OCR Scanner] Text detected: {allText.Substring(0, Math.Min(50, allText.Length))}...");

            // Create metadata with detected text
            return new MetadataEntry(
                timestamp: frameData.Timestamp,
                scannerId: ScannerId,
                key: "ocr-text",
                value: allText.Trim(),
                additionalData: new Dictionary<string, object?>
                {
                    ["lineCount"] = ocrResult.Lines.Count,
                    ["wordCount"] = ocrResult.Lines.Sum(line => line.Words.Count),
                    ["textAngle"] = ocrResult.TextAngle,
                    ["frameWidth"] = frameData.Width,
                    ["frameHeight"] = frameData.Height,
                    ["processedFrames"] = _processedFrames,
                    ["textDetectedFrames"] = _textDetectedFrames
                }
            );
        }
        catch (Exception ex)
        {
            _lastError = ex;
            System.Diagnostics.Debug.WriteLine($"[OCR Scanner] Error in ScanFrameAsync: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[OCR Scanner] Stack trace: {ex.StackTrace}");
            return null;
        }
        finally
        {
            // Log stats periodically
            if (_processedFrames > 0 && _processedFrames % 10 == 0)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[OCR Scanner] Stats: Frames={_frameCounter}, Processed={_processedFrames}, Text Detected={_textDetectedFrames}");
            }
        }
    }

    private SoftwareBitmap? ConvertTextureToSoftwareBitmap(VideoFrameData frameData)
    {
        try
        {
            // Calculate buffer size
            uint bufferSize = frameData.Width * frameData.Height * 4;
            byte[] pixelBuffer = new byte[bufferSize];

            // Call native function to convert texture to pixel buffer
            bool success = CaptureInterop.ConvertTextureToPixelBuffer(
                frameData.pTexture,
                IntPtr.Zero,  // Device (null = auto-detect from texture)
                IntPtr.Zero,  // Context (null = auto-detect from device)
                pixelBuffer,
                bufferSize,
                out uint rowPitch);

            if (!success)
            {
                _lastError = new InvalidOperationException("Native texture conversion failed");
                return null;
            }

            // Create SoftwareBitmap from pixel buffer using CopyFromBuffer (avoids COM interop issues)
            var softwareBitmap = new SoftwareBitmap(
                BitmapPixelFormat.Bgra8,
                (int)frameData.Width,
                (int)frameData.Height,
                BitmapAlphaMode.Premultiplied);

            // Copy data using the safer CopyFromBuffer method
            softwareBitmap.CopyFromBuffer(pixelBuffer.AsBuffer());

            return softwareBitmap;
        }
        catch (Exception ex)
        {
            _lastError = ex;
            System.Diagnostics.Debug.WriteLine($"[OCR Scanner] Conversion error: {ex.Message}");
            return null;
        }
    }
}
