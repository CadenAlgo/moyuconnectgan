namespace GanBridgeProbe.GanGen2;

/// <summary>
/// Builds GAN Gen2 (mode 2/4/5/9) state-notification payloads.
/// Layout verified against csTimer gancube.js parseV2Data().
/// </summary>
internal static class GanNotifications
{
    public const int PacketLength = 20;

    public static byte[] HardwareInfo(byte hwMajor, byte hwMinor, byte swMajor, byte swMinor, string deviceName, bool gyroEnabled)
    {
        var w = new BitWriter(PacketLength);
        w.Write(5, 0, 4);                       // mode = hardware info
        w.Write(hwMajor, 8, 8);
        w.Write(hwMinor, 16, 8);
        w.Write(swMajor, 24, 8);
        w.Write(swMinor, 32, 8);
        var name = (deviceName ?? string.Empty).PadRight(8, '\0')[..8];
        for (var i = 0; i < 8; i++) w.Write(name[i], 40 + i * 8, 8);
        w.Write(gyroEnabled ? 1 : 0, 104, 1);
        return w.Buffer;
    }

    public static byte[] Battery(byte level)
    {
        var w = new BitWriter(PacketLength);
        w.Write(9, 0, 4);
        w.Write(level, 8, 8);
        return w.Buffer;
    }

    /// <summary>Solved facelet/cubie state. Corner/edge permutation+orientation, 7 corners + 11 edges.</summary>
    public static byte[] SolvedFacelets(byte moveCounter) => Facelets(moveCounter, new CubeState());

    public static byte[] Facelets(byte moveCounter, CubeState state)
    {
        var w = new BitWriter(PacketLength);
        w.Write(4, 0, 4);
        w.Write(moveCounter, 4, 8);
        for (var i = 0; i < 7; i++)
        {
            w.Write(state.Ca[i] & 7, 12 + i * 3, 3);
            w.Write(state.Ca[i] >> 3, 33 + i * 2, 2);
        }
        for (var i = 0; i < 11; i++)
        {
            w.Write(state.Ea[i] >> 1, 47 + i * 4, 4);
            w.Write(state.Ea[i] & 1, 91 + i, 1);
        }
        return w.Buffer;
    }
    /// <summary>A single move event carrying the last 7 moves and their time offsets.</summary>
    public static byte[] Move(byte moveCounter, IReadOnlyList<byte> moves, IReadOnlyList<ushort> offsets)
    {
        var w = new BitWriter(PacketLength);
        w.Write(2, 0, 4);                       // mode = cube move
        w.Write(moveCounter, 4, 8);
        for (var i = 0; i < 7; i++)
        {
            // Slot 0 is newest. GAN's parser rejects m>=12, so pad unused history
            // with the latest valid move; serial delta determines how many are consumed.
            var m = i < moves.Count ? moves[i] : (moves.Count > 0 ? moves[0] : (byte)0);
            w.Write(m, 12 + i * 5, 5);
        }
        for (var i = 0; i < 7; i++)
        {
            var t = i < offsets.Count ? offsets[i] : (offsets.Count > 0 ? offsets[0] : (ushort)0);
            w.Write(t, 47 + i * 16, 16);
        }
        return w.Buffer;
    }
}
