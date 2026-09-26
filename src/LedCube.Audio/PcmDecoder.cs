using LedCube.Core.Audio;

namespace LedCube.Audio;

internal static class PcmDecoder
{
    public static AudioBuffer Decode(byte[] buffer, int byteCount, int sampleRate, int channels,
        int bitsPerSample, bool floatingPoint)
    {
        var bytesPerSample = bitsPerSample / 8;
        var blockAlign = channels * bytesPerSample;
        if (channels <= 0 || bytesPerSample <= 0 || blockAlign <= 0)
            throw new ArgumentException("Invalid PCM format.");
        var frames = byteCount / blockAlign;
        var left = new float[frames];
        var right = new float[frames];
        for (var frame = 0; frame < frames; frame++)
        {
            var offset = frame * blockAlign;
            left[frame] = ReadSample(buffer, offset, bitsPerSample, floatingPoint);
            right[frame] = channels > 1
                ? ReadSample(buffer, offset + bytesPerSample, bitsPerSample, floatingPoint)
                : left[frame];
        }
        return new AudioBuffer(left, right, sampleRate) { SourceByteCount = byteCount };
    }

    private static float ReadSample(byte[] buffer, int offset, int bits, bool floatingPoint)
    {
        if (bits == 32 && floatingPoint) return BitConverter.ToSingle(buffer, offset);
        if (bits == 32) return BitConverter.ToInt32(buffer, offset) / 2147483648f;
        if (bits == 24)
        {
            var value = buffer[offset] | buffer[offset + 1] << 8 | buffer[offset + 2] << 16;
            if ((value & 0x800000) != 0) value |= unchecked((int)0xff000000);
            return value / 8388608f;
        }
        if (bits == 16) return BitConverter.ToInt16(buffer, offset) / 32768f;
        if (bits == 8) return (buffer[offset] - 128) / 128f;
        throw new NotSupportedException($"Unsupported PCM sample width: {bits} bits.");
    }
}
