using CaptureTool.Domain.Capture.Abstractions;
using CaptureTool.Domain.Capture.Abstractions.Metadata;
using CaptureTool.Infrastructure.Abstractions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.Json;
using Windows.Graphics.Imaging;

namespace CaptureTool.Domain.Capture.Windows.Metadata.Scanners;

/// <summary>
/// Local ONNX object detection scanner for YOLO-style object detection models.
/// </summary>
public sealed class ObjectDetectionVideoMetadataScanner : IVideoMetadataScanner, ISoftwareBitmapVideoMetadataScanner, IDisposable
{
    private const int DefaultInputSize = 640;
    private const int FrameSamplingRate = 30;
    private const float ConfidenceThreshold = 0.35f;
    private const float NmsThreshold = 0.45f;
    private const int MaxDetections = 25;

    private readonly ILogService _logService;
    private readonly object _sessionLock = new();
    private readonly string _modelPath;
    private readonly string _labelsPath;
    private InferenceSession? _session;
    private string[]? _labels;
    private bool _modelUnavailableLogged;
    private int _frameCounter;

    public ObjectDetectionVideoMetadataScanner(ILogService logService)
    {
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        _modelPath = GetConfiguredPath(
            "CAPTURETOOL_OBJECT_DETECTION_MODEL",
            Path.Combine(AppContext.BaseDirectory, "MetadataModels", "object-detection.onnx"));
        _labelsPath = GetConfiguredPath(
            "CAPTURETOOL_OBJECT_DETECTION_LABELS",
            Path.Combine(AppContext.BaseDirectory, "MetadataModels", "object-detection-labels.txt"));
    }

    public string ScannerId => "onnx-object-detection";

    public string Name => "ONNX Object Detection Scanner";

    public MetadataScannerType ScannerType => MetadataScannerType.Video;

    public async Task<MetadataEntry?> ScanFrameAsync(VideoFrameData frameData, CancellationToken cancellationToken = default)
    {
        _frameCounter++;
        if (_frameCounter % FrameSamplingRate != 0)
        {
            return null;
        }

        if (frameData.pTexture == IntPtr.Zero || frameData.Width < 100 || frameData.Height < 100)
        {
            return null;
        }

        using SoftwareBitmap? softwareBitmap = ConvertTextureToSoftwareBitmap(frameData);
        if (softwareBitmap is null)
        {
            return null;
        }

        return await ScanBitmapAsync(softwareBitmap, frameData.Timestamp, cancellationToken);
    }

    public Task<MetadataEntry?> ScanBitmapAsync(
        SoftwareBitmap softwareBitmap,
        long timestamp,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(softwareBitmap);
        cancellationToken.ThrowIfCancellationRequested();

        InferenceSession? session = GetSession();
        if (session is null)
        {
            return Task.FromResult<MetadataEntry?>(null);
        }

        using SoftwareBitmap bitmapForDetection = softwareBitmap.BitmapPixelFormat == BitmapPixelFormat.Bgra8 &&
                                                  softwareBitmap.BitmapAlphaMode == BitmapAlphaMode.Premultiplied
            ? SoftwareBitmap.Copy(softwareBitmap)
            : SoftwareBitmap.Convert(softwareBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);

        string inputName = session.InputMetadata.Keys.First();
        ModelInputShape inputShape = GetInputShape(session);
        using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = session.Run(
        [
            NamedOnnxValue.CreateFromTensor(
                inputName,
                CreateInputTensor(bitmapForDetection, inputShape.Width, inputShape.Height))
        ]);

        Tensor<float> output = results.First().AsTensor<float>();
        List<ObjectDetection> detections = ApplyNonMaxSuppression(
            ParseDetections(output, inputShape.Width, inputShape.Height),
            NmsThreshold)
            .Take(MaxDetections)
            .ToList();

        if (detections.Count == 0)
        {
            return Task.FromResult<MetadataEntry?>(null);
        }

        string[] labels = [.. detections
            .Select(detection => detection.Label)
            .Distinct(StringComparer.OrdinalIgnoreCase)];

        var entry = new MetadataEntry(
            timestamp: timestamp,
            scannerId: ScannerId,
            key: "object-detections",
            value: string.Join(", ", labels),
            additionalData: new Dictionary<string, object?>
            {
                ["objectCount"] = detections.Count,
                ["labels"] = string.Join(",", labels),
                ["detections"] = JsonSerializer.Serialize(
                    detections.Select(detection => detection.ToDto()).ToList(),
                    MetadataJsonContext.Default.ListObjectDetectionMetadataDto),
                ["modelPath"] = _modelPath
            });

        return Task.FromResult<MetadataEntry?>(entry);
    }

    public void Dispose()
    {
        lock (_sessionLock)
        {
            _session?.Dispose();
            _session = null;
        }
    }

    private InferenceSession? GetSession()
    {
        lock (_sessionLock)
        {
            if (_session is not null)
            {
                return _session;
            }

            if (!File.Exists(_modelPath))
            {
                if (!_modelUnavailableLogged)
                {
                    _logService.LogInformation(
                        $"Object detection model not found at '{_modelPath}'. Scanner will remain idle.");
                    _modelUnavailableLogged = true;
                }

                return null;
            }

            _labels = LoadLabels(_labelsPath);
            _session = new InferenceSession(_modelPath);
            _logService.LogInformation($"Loaded object detection model: {_modelPath}");

            return _session;
        }
    }

    private static string GetConfiguredPath(string environmentVariableName, string defaultPath)
    {
        string? configuredPath = Environment.GetEnvironmentVariable(environmentVariableName);
        return string.IsNullOrWhiteSpace(configuredPath) ? defaultPath : configuredPath;
    }

    private string[] LoadLabels(string labelsPath)
    {
        if (!File.Exists(labelsPath))
        {
            _logService.LogWarning(
                $"Object detection labels file not found at '{labelsPath}'. Class IDs will be used as labels.");
            return [];
        }

        return File.ReadAllLines(labelsPath)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .ToArray();
    }

    private static ModelInputShape GetInputShape(InferenceSession session)
    {
        NodeMetadata metadata = session.InputMetadata.Values.First();
        int[] dimensions = metadata.Dimensions;

        if (dimensions.Length == 4)
        {
            int height = dimensions[2] > 0 ? dimensions[2] : DefaultInputSize;
            int width = dimensions[3] > 0 ? dimensions[3] : DefaultInputSize;
            return new ModelInputShape(width, height);
        }

        return new ModelInputShape(DefaultInputSize, DefaultInputSize);
    }

    private static DenseTensor<float> CreateInputTensor(SoftwareBitmap bitmap, int inputWidth, int inputHeight)
    {
        int sourceWidth = bitmap.PixelWidth;
        int sourceHeight = bitmap.PixelHeight;
        byte[] pixels = new byte[sourceWidth * sourceHeight * 4];
        bitmap.CopyToBuffer(pixels.AsBuffer());

        var tensor = new DenseTensor<float>([1, 3, inputHeight, inputWidth]);
        for (int y = 0; y < inputHeight; y++)
        {
            int sourceY = y * sourceHeight / inputHeight;
            for (int x = 0; x < inputWidth; x++)
            {
                int sourceX = x * sourceWidth / inputWidth;
                int sourceIndex = ((sourceY * sourceWidth) + sourceX) * 4;

                tensor[0, 0, y, x] = pixels[sourceIndex + 2] / 255f;
                tensor[0, 1, y, x] = pixels[sourceIndex + 1] / 255f;
                tensor[0, 2, y, x] = pixels[sourceIndex] / 255f;
            }
        }

        return tensor;
    }

    private IEnumerable<ObjectDetection> ParseDetections(Tensor<float> output, int inputWidth, int inputHeight)
    {
        int[] dimensions = output.Dimensions.ToArray();
        if (dimensions.Length != 3)
        {
            _logService.LogWarning($"Unsupported object detection output shape: {string.Join("x", dimensions)}");
            yield break;
        }

        bool channelsFirst = dimensions[1] < dimensions[2];
        int channels = channelsFirst ? dimensions[1] : dimensions[2];
        int detectionCount = channelsFirst ? dimensions[2] : dimensions[1];
        bool hasObjectness = channels >= 85;
        int classStartIndex = hasObjectness ? 5 : 4;
        int classCount = channels - classStartIndex;

        if (classCount <= 0)
        {
            yield break;
        }

        float[] values = output.ToArray();
        for (int detectionIndex = 0; detectionIndex < detectionCount; detectionIndex++)
        {
            float objectness = hasObjectness
                ? ReadOutputValue(values, channelsFirst, channels, detectionCount, detectionIndex, 4)
                : 1f;

            int bestClassIndex = -1;
            float bestClassScore = 0;
            for (int classIndex = 0; classIndex < classCount; classIndex++)
            {
                float classScore = ReadOutputValue(
                    values,
                    channelsFirst,
                    channels,
                    detectionCount,
                    detectionIndex,
                    classStartIndex + classIndex);

                if (classScore > bestClassScore)
                {
                    bestClassScore = classScore;
                    bestClassIndex = classIndex;
                }
            }

            float confidence = objectness * bestClassScore;
            if (confidence < ConfidenceThreshold || bestClassIndex < 0)
            {
                continue;
            }

            float centerX = ReadOutputValue(values, channelsFirst, channels, detectionCount, detectionIndex, 0);
            float centerY = ReadOutputValue(values, channelsFirst, channels, detectionCount, detectionIndex, 1);
            float width = ReadOutputValue(values, channelsFirst, channels, detectionCount, detectionIndex, 2);
            float height = ReadOutputValue(values, channelsFirst, channels, detectionCount, detectionIndex, 3);
            bool normalized = centerX <= 1.5f && centerY <= 1.5f && width <= 1.5f && height <= 1.5f;

            if (!normalized)
            {
                centerX /= inputWidth;
                width /= inputWidth;
                centerY /= inputHeight;
                height /= inputHeight;
            }

            yield return new ObjectDetection(
                GetLabel(bestClassIndex),
                confidence,
                Clamp01(centerX - (width / 2)),
                Clamp01(centerY - (height / 2)),
                Clamp01(width),
                Clamp01(height));
        }
    }

    private static float ReadOutputValue(
        float[] values,
        bool channelsFirst,
        int channels,
        int detectionCount,
        int detectionIndex,
        int channelIndex)
    {
        return channelsFirst
            ? values[(channelIndex * detectionCount) + detectionIndex]
            : values[(detectionIndex * channels) + channelIndex];
    }

    private string GetLabel(int classIndex)
    {
        if (_labels is not null && classIndex >= 0 && classIndex < _labels.Length)
        {
            return _labels[classIndex];
        }

        return $"class-{classIndex}";
    }

    private static List<ObjectDetection> ApplyNonMaxSuppression(
        IEnumerable<ObjectDetection> detections,
        float threshold)
    {
        var selected = new List<ObjectDetection>();
        var candidates = detections.OrderByDescending(detection => detection.Confidence).ToList();

        while (candidates.Count > 0)
        {
            ObjectDetection best = candidates[0];
            selected.Add(best);
            candidates.RemoveAt(0);
            candidates.RemoveAll(candidate =>
                string.Equals(candidate.Label, best.Label, StringComparison.OrdinalIgnoreCase) &&
                CalculateIntersectionOverUnion(best, candidate) >= threshold);
        }

        return selected;
    }

    private static float CalculateIntersectionOverUnion(ObjectDetection left, ObjectDetection right)
    {
        float intersectionLeft = Math.Max(left.X, right.X);
        float intersectionTop = Math.Max(left.Y, right.Y);
        float intersectionRight = Math.Min(left.X + left.Width, right.X + right.Width);
        float intersectionBottom = Math.Min(left.Y + left.Height, right.Y + right.Height);
        float intersectionWidth = Math.Max(0, intersectionRight - intersectionLeft);
        float intersectionHeight = Math.Max(0, intersectionBottom - intersectionTop);
        float intersectionArea = intersectionWidth * intersectionHeight;
        float unionArea = (left.Width * left.Height) + (right.Width * right.Height) - intersectionArea;

        return unionArea <= 0 ? 0 : intersectionArea / unionArea;
    }

    private static float Clamp01(float value)
    {
        return Math.Clamp(value, 0f, 1f);
    }

    private SoftwareBitmap? ConvertTextureToSoftwareBitmap(VideoFrameData frameData)
    {
        try
        {
            uint bufferSize = frameData.Width * frameData.Height * 4;
            byte[] pixelBuffer = new byte[bufferSize];

            bool success = CaptureInterop.ConvertTextureToPixelBuffer(
                frameData.pTexture,
                IntPtr.Zero,
                IntPtr.Zero,
                pixelBuffer,
                bufferSize,
                out _);

            if (!success)
            {
                return null;
            }

            var softwareBitmap = new SoftwareBitmap(
                BitmapPixelFormat.Bgra8,
                (int)frameData.Width,
                (int)frameData.Height,
                BitmapAlphaMode.Premultiplied);

            softwareBitmap.CopyFromBuffer(pixelBuffer.AsBuffer());
            return softwareBitmap;
        }
        catch (Exception ex)
        {
            _logService.LogWarning($"Object detection texture conversion failed: {ex.Message}");
            return null;
        }
    }

    private readonly record struct ModelInputShape(int Width, int Height);

    private sealed record ObjectDetection(
        string Label,
        float Confidence,
        float X,
        float Y,
        float Width,
        float Height)
    {
        public ObjectDetectionMetadataDto ToDto()
        {
            return new ObjectDetectionMetadataDto
            {
                Label = Label,
                Confidence = Confidence,
                Box = new ObjectDetectionBoxMetadataDto
                {
                    X = X,
                    Y = Y,
                    Width = Width,
                    Height = Height
                }
            };
        }
    }
}
