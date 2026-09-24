using System.Buffers.Binary;

namespace CaptureTool.Infrastructure.Analysis.Windows.Foundry;

internal static class PcmWave
{
    public static ReadOnlyMemory<byte> GetSamples(byte[] bytes)
    {
        ReadOnlySpan<byte> file = bytes;
        if (file.Length < 12 || !file[..4].SequenceEqual("RIFF"u8) || !file.Slice(8, 4).SequenceEqual("WAVE"u8))
            throw new InvalidDataException("Invalid normalized wave header.");
        bool pcm = false;
        for (int offset = 12; offset <= file.Length - 8;)
        {
            uint size = BinaryPrimitives.ReadUInt32LittleEndian(file.Slice(offset + 4, 4));
            if (size > file.Length - offset - 8) throw new InvalidDataException("Invalid wave chunk size.");
            int start = offset + 8;
            ReadOnlySpan<byte> chunk = file.Slice(start, (int)size);
            if (file.Slice(offset, 4).SequenceEqual("fmt "u8))
            {
                if (size < 16) throw new InvalidDataException("Missing PCM format.");
                ushort format = BinaryPrimitives.ReadUInt16LittleEndian(chunk);
                pcm = (format == 1 || format == 0xfffe && size >= 40 &&
                    new Guid(chunk.Slice(24, 16)) == new Guid("00000001-0000-0010-8000-00aa00389b71")) &&
                    BinaryPrimitives.ReadUInt16LittleEndian(chunk.Slice(2)) == 1 &&
                    BinaryPrimitives.ReadUInt32LittleEndian(chunk.Slice(4)) == 16000 &&
                    BinaryPrimitives.ReadUInt16LittleEndian(chunk.Slice(12)) == 2 &&
                    BinaryPrimitives.ReadUInt16LittleEndian(chunk.Slice(14)) == 16;
            }
            else if (file.Slice(offset, 4).SequenceEqual("data"u8))
            {
                if (!pcm || size % 2 != 0) throw new InvalidDataException("Expected mono 16 kHz PCM16 samples.");
                return bytes.AsMemory(start, (int)size);
            }
            offset = checked(start + (int)size + (int)(size % 2));
        }
        throw new InvalidDataException("No PCM samples.");
    }
}
