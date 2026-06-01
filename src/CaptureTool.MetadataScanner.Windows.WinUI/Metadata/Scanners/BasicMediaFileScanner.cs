using Windows.Storage;
using Windows.Storage.FileProperties;

namespace CaptureTool.MetadataScanner.Windows.WinUI.Metadata.Scanners;

public sealed class BasicMediaFileScanner : IMediaFileMetadataScanner
{
    public string ScannerId => "basic-media-file";
    public string Name => "Basic Media File Scanner";
    public MetadataScannerType ScannerType => MetadataScannerType.MediaFile;

    public async Task<IReadOnlyList<MetadataEntry>> ScanFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var fileInfo = new FileInfo(filePath);
        var storageFile = await StorageFile.GetFileFromPathAsync(filePath);
        VideoProperties videoProperties = await storageFile.Properties.GetVideoPropertiesAsync();
        MusicProperties musicProperties = await storageFile.Properties.GetMusicPropertiesAsync();

        long timestamp = DateTimeOffset.UtcNow.Ticks;
        var entries = new List<MetadataEntry>
        {
            new(
                timestamp,
                ScannerId,
                "file-info",
                fileInfo.Name,
                new Dictionary<string, object?>
                {
                    ["fullName"] = fileInfo.FullName,
                    ["length"] = fileInfo.Length,
                    ["extension"] = fileInfo.Extension,
                    ["createdUtc"] = fileInfo.CreationTimeUtc,
                    ["modifiedUtc"] = fileInfo.LastWriteTimeUtc
                })
        };

        if (videoProperties.Duration > TimeSpan.Zero || videoProperties.Width > 0 || videoProperties.Height > 0)
        {
            entries.Add(new MetadataEntry(
                timestamp,
                ScannerId,
                "video-properties",
                $"{videoProperties.Width}x{videoProperties.Height}",
                new Dictionary<string, object?>
                {
                    ["duration"] = videoProperties.Duration,
                    ["width"] = videoProperties.Width,
                    ["height"] = videoProperties.Height,
                    ["bitrate"] = videoProperties.Bitrate,
                    ["orientation"] = videoProperties.Orientation.ToString(),
                    ["title"] = videoProperties.Title,
                    ["subtitle"] = videoProperties.Subtitle,
                    ["year"] = videoProperties.Year
                }));
        }

        if (musicProperties.Duration > TimeSpan.Zero ||
            !string.IsNullOrWhiteSpace(musicProperties.Artist) ||
            !string.IsNullOrWhiteSpace(musicProperties.Title))
        {
            entries.Add(new MetadataEntry(
                timestamp,
                ScannerId,
                "audio-properties",
                musicProperties.Title,
                new Dictionary<string, object?>
                {
                    ["duration"] = musicProperties.Duration,
                    ["title"] = musicProperties.Title,
                    ["artist"] = musicProperties.Artist,
                    ["album"] = musicProperties.Album,
                    ["albumArtist"] = musicProperties.AlbumArtist,
                    ["bitrate"] = musicProperties.Bitrate,
                    ["trackNumber"] = musicProperties.TrackNumber,
                    ["year"] = musicProperties.Year
                }));
        }

        return entries;
    }
}
