using System.Buffers.Binary;
using System.Security.Cryptography;

namespace GanBridgeProbe.MoYu;

internal sealed class MoYuCrypto : IDisposable
{
    private static readonly byte[] RootKey =
    {
        0x15, 0x77, 0x3A, 0x5C, 0x67, 0x0E, 0x2D, 0x1F,
        0x17, 0x67, 0x2A, 0x13, 0x9B, 0x67, 0x52, 0x57
    };
    private static readonly byte[] RootIv =
    {
        0x11, 0x23, 0x26, 0x25, 0x86, 0x2A, 0x2C, 0x3B,
        0x55, 0x06, 0x7F, 0x31, 0x7E, 0x67, 0x21, 0x57
    };
    private readonly Aes _aes;
    private readonly byte[] _key;
    private readonly byte[] _iv;

    public MoYuCrypto(ulong bluetoothAddress)
    {
        var mac = new byte[6];
        for (var i = 0; i < 6; i++) mac[i] = (byte)((bluetoothAddress >> ((5 - i) * 8)) & 0xff);
        _key = (byte[])RootKey.Clone();
        _iv = (byte[])RootIv.Clone();
        for (var i = 0; i < 6; i++)
        {
            var salt = mac[5 - i];
            _key[i] = (byte)((_key[i] + salt) % 255);
            _iv[i] = (byte)((_iv[i] + salt) % 255);
        }
        _aes = Aes.Create();
        _aes.Mode = CipherMode.ECB;
        _aes.Padding = PaddingMode.None;
        _aes.KeySize = 128;
        _aes.BlockSize = 128;
        _aes.Key = _key;
        _aes.IV = _iv;
    }

    private void EncryptBlock(Span<byte> block)
    {
        using var enc = _aes.CreateEncryptor();
        enc.TransformFinalBlock(block.ToArray(), 0, 16).CopyTo(block);
    }

    private void DecryptBlock(Span<byte> block)
    {
        using var dec = _aes.CreateDecryptor();
        dec.TransformFinalBlock(block.ToArray(), 0, 16).CopyTo(block);
    }

    public byte[] Encode(ReadOnlySpan<byte> data)
    {
        var ret = data.ToArray();
        if (ret.Length < 16) throw new ArgumentException("MoYu packets must be at least 16 bytes.", nameof(data));
        for (var i = 0; i < 16; i++) ret[i] ^= _iv[i];
        var first = ret.AsSpan(0, 16).ToArray(); EncryptBlock(first); first.CopyTo(ret, 0);
        if (ret.Length > 16)
        {
            var offset = ret.Length - 16;
            for (var i = 0; i < 16; i++) ret[offset + i] ^= _iv[i];
            var last = ret.AsSpan(offset, 16).ToArray(); EncryptBlock(last); last.CopyTo(ret, offset);
        }
        return ret;
    }

    public byte[] Decode(ReadOnlySpan<byte> data)
    {
        var ret = data.ToArray();
        if (ret.Length < 16) return ret;
        if (ret.Length > 16)
        {
            var offset = ret.Length - 16;
            var last = ret.AsSpan(offset, 16).ToArray(); DecryptBlock(last);
            for (var i = 0; i < 16; i++) ret[offset + i] = (byte)(last[i] ^ _iv[i]);
        }
        var first = ret.AsSpan(0, 16).ToArray(); DecryptBlock(first);
        for (var i = 0; i < 16; i++) ret[i] = (byte)(first[i] ^ _iv[i]);
        return ret;
    }

    public void Dispose() => _aes.Dispose();
}

internal readonly record struct MoYuMove(byte Code, ushort TimeMs)
{
    public bool IsValid => Code <= 11;

    public byte ToGanCode() => Code switch
    {
        0 => 4, 1 => 5, 2 => 10, 3 => 11,
        4 => 0, 5 => 1, 6 => 6, 7 => 7,
        8 => 8, 9 => 9, 10 => 2, 11 => 3,
        _ => 12
    };

    public string Name => Code switch
    {
        0 => "F", 1 => "F'", 2 => "B", 3 => "B'", 4 => "U", 5 => "U'",
        6 => "D", 7 => "D'", 8 => "L", 9 => "L'", 10 => "R", 11 => "R'", _ => "?"
    };
}

internal static class MoYuPacket
{
    public static byte[] Command(byte opcode)
    {
        var packet = new byte[20]; packet[0] = opcode; return packet;
    }

    public static bool TryParseA3(byte[] p, out byte moveCounter)
    {
        moveCounter = 0;
        if (p.Length < 20 || p[0] != 0xA3) return false;
        moveCounter = p[19]; return true;
    }

    // Convert MoYu V10 AI packed FBUDLR (8 stickers/face) into standard 54-sticker URFDLB.
    public static bool TryParseA3Facelets(byte[] p, out byte moveCounter, out string facelets)
    {
        moveCounter = 0; facelets = string.Empty;
        if (p.Length < 20 || p[0] != 0xA3) return false;
        moveCounter = p[19];
        int[] sourceFaces = { 2, 5, 0, 3, 4, 1 }; // destination URFDLB -> source FBUDLR face index
        const string colors = "FBUDLR";
        var result = new char[54];
        var pos = 0;
        for (var dstFace = 0; dstFace < 6; dstFace++)
        {
            var srcFace = sourceFaces[dstFace];
            for (var sticker = 0; sticker < 8; sticker++)
            {
                var color = ReadBits(p, 8 + srcFace * 24 + sticker * 3, 3);
                if (color > 5) return false;
                result[pos++] = colors[color];
                if (sticker == 3) result[pos++] = colors[srcFace]; // center
            }
        }
        facelets = new string(result);
        return true;
    }

    public static bool TryParseA4(byte[] p, out byte battery)
    {
        battery = 0;
        if (p.Length < 2 || p[0] != 0xA4) return false;
        battery = p[1]; return true;
    }

    public static bool TryParseA1(byte[] p, out string model, out string version)
    {
        model = string.Empty; version = string.Empty;
        if (p.Length < 8 || p[0] != 0xA1) return false;
        var chars = p.Skip(1).Take(8).Where(x => x >= 32 && x <= 126).Select(x => (char)x).ToArray();
        model = new string(chars).TrimEnd('\0', ' ');
        if (p.Length >= 13) version = $"HW {p[9]}.{p[10]} / SW {p[11]}.{p[12]}";
        return true;
    }

    public static bool TryParseA5(byte[] p, out byte counter, out MoYuMove[] moves)
    {
        counter = 0; moves = Array.Empty<MoYuMove>();
        if (p.Length < 20 || p[0] != 0xA5) return false;
        counter = p[11];
        var list = new MoYuMove[5];
        for (var i = 0; i < 5; i++)
        {
            var time = BinaryPrimitives.ReadUInt16BigEndian(p.AsSpan(1 + i * 2, 2));
            var code = ReadBits(p, 96 + i * 5, 5);
            list[i] = new MoYuMove(code, time);
        }
        moves = list;
        return true;
    }

    private static byte ReadBits(byte[] data, int offset, int count)
    {
        var value = 0;
        for (var i = 0; i < count; i++)
        {
            var absolute = offset + i;
            value = (value << 1) | ((data[absolute >> 3] >> (7 - (absolute & 7))) & 1);
        }
        return (byte)value;
    }
}
