// Derived from cubebridge (GPL-3.0) CubieCube layout and MoYu/GAN public protocol notes.
namespace GanBridgeProbe;

internal static class BridgeSelfTests
{
    public static IReadOnlyList<string> Run()
    {
        var results = new List<string>();
        var solved = new GanGen2.CubeState();
        var solvedFacelet = solved.ToFacelet();
        if (solvedFacelet.Length != 54 || solvedFacelet != new string('U', 9) + new string('R', 9) + new string('F', 9) + new string('D', 9) + new string('L', 9) + new string('B', 9))
            throw new InvalidOperationException("CubeState solved serialization failed: " + solvedFacelet);
        if (!GanGen2.CubeState.TryFromFacelet(solvedFacelet, out var roundTrip) || !roundTrip.ToFacelet().Equals(solvedFacelet, StringComparison.Ordinal))
            throw new InvalidOperationException("CubeState solved facelet round trip failed.");
        results.Add("CubeState solved facelet round-trip OK");

        var reversible = new GanGen2.CubeState();
        reversible.ApplyMove(1, 0); // R
        reversible.ApplyMove(1, 2); // R'
        if (reversible.ToFacelet() != solvedFacelet) throw new InvalidOperationException("CubeState R/R' inverse test failed.");
        results.Add("CubeState R/R' inverse OK");

        var packet = new byte[20]; packet[0] = 0xA3; packet[19] = 37;
        int[] faceOrder = { 2, 5, 0, 3, 4, 1 };
        foreach (var f in faceOrder) for (var i = 0; i < 8; i++) WriteBits(packet, 8 + f * 24 + i * 3, 3, f);
        if (!MoYu.MoYuPacket.TryParseA3Facelets(packet, out var counter, out var converted) || counter != 37 || converted != solvedFacelet)
            throw new InvalidOperationException($"MoYu A3 solved-facelet conversion failed: counter={counter}; facelet={converted}");
        if (!GanGen2.CubeState.TryFromFacelet(converted, out _)) throw new InvalidOperationException("MoYu A3 output is not a valid cube state.");
        results.Add("MoYu A3 FBUDLR -> URFDLB conversion OK");

        using (var gan = new GanGen2.GanCrypto(new byte[] { 0x9C, 0xC7, 0xD3, 0xE6, 0x27, 0x6B }))
        {
            var plain = GanGen2.GanNotifications.HardwareInfo(1, 1, 1, 0, "GAN356iC", false);
            if (!gan.Decode(gan.Encode(plain)).SequenceEqual(plain)) throw new InvalidOperationException("GAN crypto round trip failed.");
        }
        using (var moyu = new MoYu.MoYuCrypto(0xCF301601ADA3))
        {
            var plain = MoYu.MoYuPacket.Command(0xA1);
            if (!moyu.Decode(moyu.Encode(plain)).SequenceEqual(plain)) throw new InvalidOperationException("MoYu crypto round trip failed.");
        }
        results.Add("GAN Gen2 / MoYu AES framing round-trip OK");

        var identity = GanGen2.GanNotifications.HardwareInfo(1, 1, 1, 0, "GAN356iC", false);
        if (identity[0] != 0x50 || identity[1] != 1 || identity[2] != 1 || identity[3] != 1 || identity[4] != 0 ||
            System.Text.Encoding.ASCII.GetString(identity, 5, 8) != "GAN356iC")
            throw new InvalidOperationException("Reference cubebridge GAN identity packet mismatch.");
        results.Add("cubebridge GAN356iC identity packet OK");

        var ganCode = new MoYu.MoYuMove(0, 123).ToGanCode(); // F -> GAN F
        if (ganCode != 4) throw new InvalidOperationException("MoYu F -> GAN F mapping failed.");
        results.Add("MoYu-to-GAN move mapping smoke test OK");

        // Round-trip our mode-4 / mode-2 writers through a port of Cube Station's
        // own GAN v2 parser (csTimer gancube.js parseV2Data). This validates the
        // bit offsets and the implicit 8th-corner / 12th-edge reconstruction.
        var scrambled = new GanGen2.CubeState();
        scrambled.ApplyMove(1, 0); // R
        scrambled.ApplyMove(4, 0); // L
        scrambled.ApplyMove(0, 2); // U'
        var expectedFacelet = scrambled.ToFacelet();
        var parsedState = ParseV2Mode4(GanGen2.GanNotifications.Facelets(200, scrambled), out var parsedCounter);
        if (parsedState is null) throw new InvalidOperationException("GAN v2 mode-4 packet rejected by parser.");
        if (parsedCounter != 200) throw new InvalidOperationException($"GAN v2 mode-4 counter mismatch: {parsedCounter}");
        if (parsedState.ToFacelet() != expectedFacelet)
            throw new InvalidOperationException($"GAN v2 mode-4 state mismatch: {parsedState.ToFacelet()} != {expectedFacelet}");
        results.Add("GAN v2 mode-4 face state round-trip OK");

        var movePacket = GanGen2.GanNotifications.Move(201, new byte[] { 4, 5, 0 }, new ushort[] { 100, 200, 300 });
        if (!ParseV2Mode2(movePacket, out var moveCounter, out var moves, out var times))
            throw new InvalidOperationException("GAN v2 mode-2 packet rejected by parser.");
        if (moveCounter != 201) throw new InvalidOperationException($"GAN v2 mode-2 counter mismatch: {moveCounter}");
        if (moves[0] != 4 || moves[1] != 5 || moves[2] != 0)
            throw new InvalidOperationException($"GAN v2 mode-2 move mismatch: {string.Join(",", moves)}");
        if (times[0] != 100 || times[1] != 200 || times[2] != 300)
            throw new InvalidOperationException($"GAN v2 mode-2 time mismatch: {string.Join(",", times)}");
        results.Add("GAN v2 mode-2 move packet round-trip OK");
        return results;
    }

    private static string ToBitString(byte[] packet)
    {
        var sb = new System.Text.StringBuilder(packet.Length * 8);
        foreach (var b in packet) sb.Append(Convert.ToString(b, 2).PadLeft(8, '0'));
        return sb.ToString();
    }

    private static int ReadBits(string bits, int offset, int count) => Convert.ToInt32(bits.Substring(offset, count), 2);

    /// <summary>Port of Cube Station / csTimer parseV2Data mode 4 (cube state).</summary>
    private static GanGen2.CubeState? ParseV2Mode4(byte[] packet, out int counter)
    {
        counter = 0;
        var bits = ToBitString(packet);
        if (ReadBits(bits, 0, 4) != 4) return null;
        counter = ReadBits(bits, 4, 8);
        var cc = new GanGen2.CubeState();
        var cchk = 0xf00;
        var echk = 0;
        for (var i = 0; i < 7; i++)
        {
            var perm = ReadBits(bits, 12 + i * 3, 3);
            var ori = ReadBits(bits, 33 + i * 2, 2);
            cchk -= ori << 3;
            cchk ^= perm;
            cc.Ca[i] = (byte)((ori << 3) | perm);
        }
        cc.Ca[7] = (byte)(((cchk & 0xff8) % 24) | (cchk & 0x7));
        for (var i = 0; i < 11; i++)
        {
            var perm = ReadBits(bits, 47 + i * 4, 4);
            var ori = ReadBits(bits, 91 + i, 1);
            echk ^= (perm << 1) | ori;
            cc.Ea[i] = (byte)((perm << 1) | ori);
        }
        cc.Ea[11] = (byte)echk;
        return cc;
    }

    /// <summary>Port of Cube Station / csTimer parseV2Data mode 2 (cube move).</summary>
    private static bool ParseV2Mode2(byte[] packet, out int counter, out int[] moves, out int[] times)
    {
        counter = 0;
        moves = new int[7];
        times = new int[7];
        var bits = ToBitString(packet);
        if (ReadBits(bits, 0, 4) != 2) return false;
        counter = ReadBits(bits, 4, 8);
        for (var i = 0; i < 7; i++)
        {
            moves[i] = ReadBits(bits, 12 + i * 5, 5);
            times[i] = ReadBits(bits, 47 + i * 16, 16);
        }
        return true;
    }

    private static void WriteBits(byte[] data, int offset, int count, int value)
    {
        for (var i = 0; i < count; i++)
        {
            var absolute = offset + i;
            var bit = (value >> (count - 1 - i)) & 1;
            if (bit != 0) data[absolute >> 3] |= (byte)(1 << (7 - (absolute & 7)));
        }
    }
}
