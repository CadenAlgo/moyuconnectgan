using System.Security.Cryptography;

namespace GanBridgeProbe.GanGen2;

/// <summary>
/// GAN Gen2 / Gen3 / Gen4 smart-cube framing: AES-128 with a key and IV derived
/// from a root key/IV salted by the cube BLE MAC address.
/// Reference: csTimer gancube.js and poliva/smartcube-web-bluetooth gan-cube-definitions.ts.
/// </summary>
internal sealed class GanCrypto : IDisposable
{
    // "Key used by GAN Gen2, Gen3 and Gen4 cubes" (public reverse-engineered value).
    private static readonly byte[] RootKey =
    {
        0x01, 0x02, 0x42, 0x28, 0x31, 0x91, 0x16, 0x07,
        0x20, 0x05, 0x18, 0x54, 0x42, 0x11, 0x12, 0x53
    };

    private static readonly byte[] RootIv =
    {
        0x11, 0x03, 0x32, 0x28, 0x21, 0x01, 0x76, 0x27,
        0x20, 0x95, 0x78, 0x14, 0x32, 0x12, 0x02, 0x43
    };

    private readonly Aes _aes;
    private readonly byte[] _key;
    private readonly byte[] _iv;

    public GanCrypto(byte[] macBytes)
    {
        if (macBytes is null || macBytes.Length != 6)
            throw new ArgumentException("MAC must be exactly 6 bytes.", nameof(macBytes));

        _key = (byte[])RootKey.Clone();
        _iv = (byte[])RootIv.Clone();
        for (var i = 0; i < 6; i++)
        {
            var salt = macBytes[5 - i];
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

    public byte[] Key => (byte[])_key.Clone();
    public byte[] Iv => (byte[])_iv.Clone();

    private void EncryptBlock(Span<byte> block)
    {
        using var enc = _aes.CreateEncryptor();
        var outp = enc.TransformFinalBlock(block.ToArray(), 0, 16);
        outp.CopyTo(block);
    }

    private void DecryptBlock(Span<byte> block)
    {
        using var dec = _aes.CreateDecryptor();
        var outp = dec.TransformFinalBlock(block.ToArray(), 0, 16);
        outp.CopyTo(block);
    }

    /// <summary>Decrypt data received from the GAN client (app -> cube direction).</summary>
    public byte[] Decode(ReadOnlySpan<byte> data)
    {
        var ret = data.ToArray();
        if (ret.Length > 16)
        {
            var offset = ret.Length - 16;
            var block = ret.AsSpan(offset, 16).ToArray();
            DecryptBlock(block);
            for (var i = 0; i < 16; i++) ret[offset + i] = (byte)(block[i] ^ _iv[i]);
        }
        if (ret.Length >= 16)
        {
            var first = ret.AsSpan(0, 16).ToArray();
            DecryptBlock(first);
            for (var i = 0; i < 16; i++) ret[i] = (byte)(first[i] ^ _iv[i]);
        }
        return ret;
    }

    /// <summary>Encrypt data to send to the GAN client (cube -> app direction).</summary>
    public byte[] Encode(ReadOnlySpan<byte> data)
    {
        var ret = data.ToArray();
        if (ret.Length >= 16)
        {
            for (var i = 0; i < 16; i++) ret[i] = (byte)(ret[i] ^ _iv[i]);
            var block = ret.AsSpan(0, 16).ToArray();
            EncryptBlock(block);
            for (var i = 0; i < 16; i++) ret[i] = block[i];
        }
        if (ret.Length > 16)
        {
            var offset = ret.Length - 16;
            for (var i = 0; i < 16; i++) ret[offset + i] = (byte)(ret[offset + i] ^ _iv[i]);
            var block = ret.AsSpan(offset, 16).ToArray();
            EncryptBlock(block);
            for (var i = 0; i < 16; i++) ret[offset + i] = block[i];
        }
        return ret;
    }

    public void Dispose() => _aes.Dispose();
}
