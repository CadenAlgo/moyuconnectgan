namespace GanBridgeProbe.GanGen2;

/// <summary>MSB-first bit writer matching the GAN V2 notification packing.</summary>
internal sealed class BitWriter
{
    private readonly byte[] _buffer;

    public BitWriter(int byteLength) => _buffer = new byte[byteLength];

    public byte[] Buffer => _buffer;

    public void Write(int value, int bitOffset, int bitCount)
    {
        for (var i = 0; i < bitCount; i++)
        {
            var bit = (value >> (bitCount - 1 - i)) & 1;
            var absolute = bitOffset + i;
            var byteIndex = absolute >> 3;
            var bitIndex = 7 - (absolute & 7);
            if (byteIndex >= _buffer.Length) return;
            if (bit != 0) _buffer[byteIndex] |= (byte)(1 << bitIndex);
        }
    }

    public void WriteByteAligned(int value, int bitOffset) => Write(value, bitOffset, 8);
}
