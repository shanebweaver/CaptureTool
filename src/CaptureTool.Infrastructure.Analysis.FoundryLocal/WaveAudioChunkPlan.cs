using System.Buffers.Binary;
using System.Text;

namespace CaptureTool.Infrastructure.Analysis.FoundryLocal;

internal sealed class WaveAudioChunkPlan
{
    private const int CopyBufferSize = 81_920;
    private readonly string _sourcePath;
    private readonly byte[] _formatChunk;

    private WaveAudioChunkPlan(
        string sourcePath,
        byte[] formatChunk,
        IReadOnlyList<WaveAudioChunk> chunks)
    {
        _sourcePath = sourcePath;
        _formatChunk = formatChunk;
        Chunks = chunks;
    }

    public IReadOnlyList<WaveAudioChunk> Chunks { get; }

    public static bool TryCreate(
        string sourcePath,
        TimeSpan maximumChunkDuration,
        out WaveAudioChunkPlan? plan)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        if (maximumChunkDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumChunkDuration));
        }

        plan = null;
        try
        {
            using var source = new FileStream(
                sourcePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4_096,
                FileOptions.SequentialScan);
            using var reader = new BinaryReader(source, Encoding.ASCII, leaveOpen: true);
            if (source.Length < 12 ||
                ReadFourCc(reader) != "RIFF" ||
                reader.ReadUInt32() > source.Length - 8 ||
                ReadFourCc(reader) != "WAVE")
            {
                return false;
            }

            byte[]? formatChunk = null;
            long dataOffset = 0;
            long dataLength = 0;
            while (source.Position <= source.Length - 8)
            {
                string chunkId = ReadFourCc(reader);
                uint chunkLength = reader.ReadUInt32();
                long chunkOffset = source.Position;
                long nextChunk = chunkOffset + chunkLength + (chunkLength & 1);
                if (nextChunk > source.Length)
                {
                    return false;
                }

                if (chunkId == "fmt " && formatChunk == null)
                {
                    formatChunk = reader.ReadBytes(checked((int)chunkLength));
                    if (formatChunk.Length != chunkLength)
                    {
                        return false;
                    }
                }
                else if (chunkId == "data" && dataLength == 0)
                {
                    dataOffset = chunkOffset;
                    dataLength = chunkLength;
                }

                source.Position = nextChunk;
            }

            if (formatChunk == null || formatChunk.Length < 16 || dataLength <= 0)
            {
                return false;
            }

            ushort blockAlignment = BinaryPrimitives.ReadUInt16LittleEndian(formatChunk.AsSpan(12, 2));
            uint bytesPerSecond = BinaryPrimitives.ReadUInt32LittleEndian(formatChunk.AsSpan(8, 4));
            if (blockAlignment == 0 || bytesPerSecond == 0 || dataLength % blockAlignment != 0)
            {
                return false;
            }

            long maximumChunkBytes = checked((long)Math.Floor(
                bytesPerSecond * maximumChunkDuration.TotalSeconds));
            maximumChunkBytes -= maximumChunkBytes % blockAlignment;
            if (maximumChunkBytes <= 0)
            {
                return false;
            }

            var chunks = new List<WaveAudioChunk>();
            for (long relativeOffset = 0; relativeOffset < dataLength; relativeOffset += maximumChunkBytes)
            {
                long chunkLength = Math.Min(maximumChunkBytes, dataLength - relativeOffset);
                chunks.Add(new WaveAudioChunk(
                    dataOffset + relativeOffset,
                    chunkLength,
                    ToTimeSpan(relativeOffset, bytesPerSecond),
                    ToTimeSpan(relativeOffset + chunkLength, bytesPerSecond)));
            }

            plan = new WaveAudioChunkPlan(sourcePath, formatChunk, chunks.AsReadOnly());
            return true;
        }
        catch (Exception exception) when (exception is
            EndOfStreamException or IOException or OverflowException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public async Task WriteChunkAsync(
        WaveAudioChunk chunk,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

        await using var source = new FileStream(
            _sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            CopyBufferSize,
            FileOptions.Asynchronous | FileOptions.RandomAccess);
        await using var destination = new FileStream(
            destinationPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            CopyBufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        byte[] header = CreateHeader(_formatChunk, chunk.DataLength);
        await destination.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        source.Position = chunk.DataOffset;

        byte[] buffer = new byte[CopyBufferSize];
        long remaining = chunk.DataLength;
        while (remaining > 0)
        {
            int requested = (int)Math.Min(buffer.Length, remaining);
            int read = await source.ReadAsync(buffer.AsMemory(0, requested), cancellationToken)
                .ConfigureAwait(false);
            if (read == 0)
            {
                throw new EndOfStreamException("The wave data ended before the planned chunk boundary.");
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken)
                .ConfigureAwait(false);
            remaining -= read;
        }

        if ((chunk.DataLength & 1) != 0)
        {
            await destination.WriteAsync(new byte[1], cancellationToken).ConfigureAwait(false);
        }
    }

    private static byte[] CreateHeader(byte[] formatChunk, long dataLength)
    {
        int formatPadding = formatChunk.Length & 1;
        int dataPadding = (int)(dataLength & 1);
        long riffLength = 4L + 8 + formatChunk.Length + formatPadding + 8 + dataLength + dataPadding;
        if (dataLength > uint.MaxValue || riffLength > uint.MaxValue)
        {
            throw new InvalidOperationException("A wave analysis chunk cannot exceed the RIFF size limit.");
        }

        using var stream = new MemoryStream(checked(28 + formatChunk.Length + formatPadding));
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);
        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write((uint)riffLength);
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));
        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write((uint)formatChunk.Length);
        writer.Write(formatChunk);
        if (formatPadding != 0)
        {
            writer.Write((byte)0);
        }

        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write((uint)dataLength);
        return stream.ToArray();
    }

    private static string ReadFourCc(BinaryReader reader)
    {
        byte[] bytes = reader.ReadBytes(4);
        if (bytes.Length != 4)
        {
            throw new EndOfStreamException();
        }

        return Encoding.ASCII.GetString(bytes);
    }

    private static TimeSpan ToTimeSpan(long byteOffset, uint bytesPerSecond)
    {
        return TimeSpan.FromTicks(checked(byteOffset * TimeSpan.TicksPerSecond / bytesPerSecond));
    }
}

internal sealed record WaveAudioChunk(
    long DataOffset,
    long DataLength,
    TimeSpan StartTime,
    TimeSpan EndTime);
