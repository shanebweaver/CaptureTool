using CaptureTool.Application.Abstractions.Analysis.Media;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Media.Editing;
using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;
using Windows.Storage;

namespace CaptureTool.Infrastructure.Analysis.Windows.Media;

public sealed class WindowsVideoAudioExtractionService : IVideoAudioExtractionService
{
    public async Task<VideoAudioExtractionResult> ExtractWaveAudioAsync(
        Stream video,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(video);
        if (!video.CanRead)
        {
            throw new ArgumentException("A video analysis source must be readable.", nameof(video));
        }

        string sourcePath = WindowsVideoAnalysisWorkingFiles.CreatePath(".mp4");
        string outputPath = WindowsVideoAnalysisWorkingFiles.CreatePath(".wav");
        bool outputTransferred = false;
        try
        {
            await WindowsVideoAnalysisWorkingFiles.CopyToNewFileAsync(
                video,
                sourcePath,
                cancellationToken).ConfigureAwait(false);

            StorageFile sourceFile = await StorageFile.GetFileFromPathAsync(sourcePath);
            MediaClip sourceClip = await MediaClip.CreateFromFileAsync(sourceFile);
            if (sourceClip.EmbeddedAudioTracks.Count == 0)
            {
                return VideoAudioExtractionResult.NoAudio;
            }

            await using (FileStream placeholder = new(
                outputPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 1,
                FileOptions.Asynchronous))
            {
                await placeholder.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            StorageFile outputFile = await StorageFile.GetFileFromPathAsync(outputPath);
            var transcoder = new MediaTranscoder
            {
                HardwareAccelerationEnabled = true,
            };
            MediaEncodingProfile profile = MediaEncodingProfile.CreateWav(
                AudioEncodingQuality.Medium);
            PrepareTranscodeResult prepared = await transcoder.PrepareFileTranscodeAsync(
                sourceFile,
                outputFile,
                profile);
            if (!prepared.CanTranscode)
            {
                return VideoAudioExtractionResult.Unsupported;
            }

            await prepared.TranscodeAsync().AsTask(cancellationToken);
            Stream audio = new FileStream(
                outputPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81_920,
                FileOptions.Asynchronous |
                    FileOptions.SequentialScan |
                    FileOptions.DeleteOnClose);
            outputTransferred = true;
            return VideoAudioExtractionResult.Succeeded(audio);
        }
        catch (OperationCanceledException)
        {
            return VideoAudioExtractionResult.Cancelled;
        }
        catch (PlatformNotSupportedException)
        {
            return VideoAudioExtractionResult.Unsupported;
        }
        catch
        {
            return VideoAudioExtractionResult.Failed;
        }
        finally
        {
            WindowsVideoAnalysisWorkingFiles.TryDelete(sourcePath);
            if (!outputTransferred)
            {
                WindowsVideoAnalysisWorkingFiles.TryDelete(outputPath);
            }
        }
    }
}
