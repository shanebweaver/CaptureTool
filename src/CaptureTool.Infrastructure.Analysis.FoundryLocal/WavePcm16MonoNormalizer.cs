using System.Buffers.Binary;
using System.Text;

namespace CaptureTool.Infrastructure.Analysis.FoundryLocal;

internal static class WavePcm16MonoNormalizer
{
    public const int SampleRate = 16_000;
    public const short ChannelCount = 1;
    public const short BitsPerSample = 16;
    private const int MaximumInputBytes = 128 * 1024 * 1024;

    public static bool TryNormalize(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

        bool completed = false;
        try
        {
            if (!TryReadWave(sourcePath, out WaveSource? source) || source == null)
            {
                return false;
            }

            if (source.DataLength > MaximumInputBytes || source.FrameCount > int.MaxValue)
            {
                return false;
            }

            byte[] data = new byte[checked((int)source.DataLength)];
            using (var input = new FileStream(
                sourcePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81_920,
                FileOptions.SequentialScan))
            {
                input.Position = source.DataOffset;
                input.ReadExactly(data);
            }

            float[] mono = DecodeMono(data, source, cancellationToken);
            int outputFrameCount = checked((int)Math.Max(
                1,
                Math.Round(
                    mono.LongLength * (double)SampleRate / source.SampleRate,
                    MidpointRounding.AwayFromZero)));
            int outputDataLength = checked(outputFrameCount * sizeof(short));
            using var output = new FileStream(
                destinationPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81_920,
                FileOptions.SequentialScan);
            WriteHeader(output, outputDataLength);

            Span<byte> sampleBytes = stackalloc byte[sizeof(short)];
            double sourceFramesPerOutputFrame = source.SampleRate / (double)SampleRate;
            for (var outputIndex = 0; outputIndex < outputFrameCount; outputIndex++)
            {
                if ((outputIndex & 0x0fff) == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                double sourcePosition = Math.Min(
                    mono.Length - 1d,
                    outputIndex * sourceFramesPerOutputFrame);
                int leftIndex = (int)sourcePosition;
                int rightIndex = Math.Min(leftIndex + 1, mono.Length - 1);
                float fraction = (float)(sourcePosition - leftIndex);
                float sample = mono[leftIndex] + ((mono[rightIndex] - mono[leftIndex]) * fraction);
                short pcm = (short)Math.Clamp(
                    MathF.Round(sample * short.MaxValue),
                    short.MinValue,
                    short.MaxValue);
                BinaryPrimitives.WriteInt16LittleEndian(sampleBytes, pcm);
                output.Write(sampleBytes);
            }

            completed = true;
            return true;
        }
        catch (Exception exception) when (exception is
            EndOfStreamException or IOException or OverflowException or UnauthorizedAccessException)
        {
            return false;
        }
        finally
        {
            if (!completed)
            {
                TryDelete(destinationPath);
            }
        }
    }

    public static bool TryGetPcmDataRange(
        string path,
        out long dataOffset,
        out long dataLength)
    {
        dataOffset = 0;
        dataLength = 0;
        if (!TryReadWave(path, out WaveSource? source) ||
            source == null ||
            source.FormatTag != 1 ||
            source.ChannelCount != ChannelCount ||
            source.SampleRate != SampleRate ||
            source.BitsPerSample != BitsPerSample)
        {
            return false;
        }

        dataOffset = source.DataOffset;
        dataLength = source.DataLength;
        return true;
    }

    private static float[] DecodeMono(
        byte[] data,
        WaveSource source,
        CancellationToken cancellationToken)
    {
        var mono = new float[checked((int)source.FrameCount)];
        int bytesPerSample = source.BitsPerSample / 8;
        for (var frame = 0; frame < mono.Length; frame++)
        {
            if ((frame & 0x0fff) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            int frameOffset = checked(frame * source.BlockAlignment);
            double sum = 0;
            for (var channel = 0; channel < source.ChannelCount; channel++)
            {
                int offset = checked(frameOffset + (channel * bytesPerSample));
                sum += DecodeSample(data.AsSpan(offset, bytesPerSample), source);
            }

            mono[frame] = (float)Math.Clamp(sum / source.ChannelCount, -1, 1);
        }

        return mono;
    }

    private static double DecodeSample(ReadOnlySpan<byte> bytes, WaveSource source)
    {
        if (source.FormatTag == 3)
        {
            float value = BitConverter.Int32BitsToSingle(
                BinaryPrimitives.ReadInt32LittleEndian(bytes));
            return float.IsFinite(value) ? value : 0;
        }

        return source.BitsPerSample switch
        {
            8 => (bytes[0] - 128) / 128d,
            16 => BinaryPrimitives.ReadInt16LittleEndian(bytes) / 32768d,
            24 => ReadSigned24(bytes) / 8388608d,
            32 => BinaryPrimitives.ReadInt32LittleEndian(bytes) / 2147483648d,
            _ => throw new InvalidDataException("Unsupported PCM sample width."),
        };
    }

    private static int ReadSigned24(ReadOnlySpan<byte> bytes)
    {
        int value = bytes[0] | (bytes[1] << 8) | (bytes[2] << 16);
        return (value & 0x800000) == 0 ? value : value | unchecked((int)0xff000000);
    }

    private static bool TryReadWave(string path, out WaveSource? source)
    {
        source = null;
        try
        {
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4_096,
                FileOptions.SequentialScan);
            using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);
            if (stream.Length < 12 ||
                ReadFourCc(reader) != "RIFF" ||
                reader.ReadUInt32() > stream.Length - 8 ||
                ReadFourCc(reader) != "WAVE")
            {
                return false;
            }

            byte[]? format = null;
            long dataOffset = 0;
            long dataLength = 0;
            while (stream.Position <= stream.Length - 8)
            {
                string chunkId = ReadFourCc(reader);
                uint chunkLength = reader.ReadUInt32();
                long chunkOffset = stream.Position;
                long nextChunk = checked(chunkOffset + chunkLength + (chunkLength & 1));
                if (nextChunk > stream.Length)
                {
                    return false;
                }

                if (chunkId == "fmt " && format == null)
                {
                    format = reader.ReadBytes(checked((int)chunkLength));
                    if (format.Length != chunkLength)
                    {
                        return false;
                    }
                }
                else if (chunkId == "data" && dataLength == 0)
                {
                    dataOffset = chunkOffset;
                    dataLength = chunkLength;
                }

                stream.Position = nextChunk;
            }

            if (format == null || format.Length < 16 || dataLength <= 0)
            {
                return false;
            }

            ushort formatTag = BinaryPrimitives.ReadUInt16LittleEndian(format.AsSpan(0, 2));
            ushort channels = BinaryPrimitives.ReadUInt16LittleEndian(format.AsSpan(2, 2));
            uint sampleRate = BinaryPrimitives.ReadUInt32LittleEndian(format.AsSpan(4, 4));
            ushort blockAlignment = BinaryPrimitives.ReadUInt16LittleEndian(format.AsSpan(12, 2));
            ushort bitsPerSample = BinaryPrimitives.ReadUInt16LittleEndian(format.AsSpan(14, 2));
            if (formatTag == 0xfffe && format.Length >= 40)
            {
                formatTag = BinaryPrimitives.ReadUInt16LittleEndian(format.AsSpan(24, 2));
            }

            int expectedBlockAlignment = checked(channels * bitsPerSample / 8);
            bool supported =
                formatTag is 1 or 3 &&
                channels is >= 1 and <= 8 &&
                sampleRate is >= 1 and <= 384_000 &&
                (formatTag == 1 && bitsPerSample is 8 or 16 or 24 or 32 ||
                    formatTag == 3 && bitsPerSample == 32) &&
                blockAlignment == expectedBlockAlignment &&
                dataLength % blockAlignment == 0;
            if (!supported)
            {
                return false;
            }

            source = new WaveSource(
                formatTag,
                channels,
                checked((int)sampleRate),
                blockAlignment,
                bitsPerSample,
                dataOffset,
                dataLength,
                dataLength / blockAlignment);
            return true;
        }
        catch (Exception exception) when (exception is
            EndOfStreamException or IOException or OverflowException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void WriteHeader(Stream destination, int dataLength)
    {
        using var writer = new BinaryWriter(destination, Encoding.ASCII, leaveOpen: true);
        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(checked(36 + dataLength));
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));
        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write(ChannelCount);
        writer.Write(SampleRate);
        writer.Write(SampleRate * ChannelCount * BitsPerSample / 8);
        writer.Write((short)(ChannelCount * BitsPerSample / 8));
        writer.Write(BitsPerSample);
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(dataLength);
    }

    private static string ReadFourCc(BinaryReader reader)
    {
        byte[] value = reader.ReadBytes(4);
        if (value.Length != 4)
        {
            throw new EndOfStreamException();
        }

        return Encoding.ASCII.GetString(value);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // The destination is a uniquely named app-created working file.
        }
    }

    private sealed record WaveSource(
        ushort FormatTag,
        ushort ChannelCount,
        int SampleRate,
        ushort BlockAlignment,
        ushort BitsPerSample,
        long DataOffset,
        long DataLength,
        long FrameCount);
}
