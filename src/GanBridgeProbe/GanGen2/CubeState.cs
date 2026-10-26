// Derived/ported from cubebridge main/cube_state.c (GPL-3.0), itself based on
// the standard csTimer CubieCube representation. See THIRD_PARTY_NOTICES.md.
namespace GanBridgeProbe.GanGen2;

internal sealed class CubeState
{
    private static readonly byte[][] BaseCa =
    {
        new byte[] { 3,0,1,2,4,5,6,7 },
        new byte[] { 20,1,2,8,15,5,6,19 },
        new byte[] { 9,21,2,3,16,12,6,7 },
        new byte[] { 0,1,2,3,5,6,7,4 },
        new byte[] { 0,10,22,3,4,17,13,7 },
        new byte[] { 0,1,11,23,4,5,18,14 }
    };
    private static readonly byte[][] BaseEa =
    {
        new byte[] { 6,0,2,4,8,10,12,14,16,18,20,22 },
        new byte[] { 16,2,4,6,22,10,12,14,8,18,20,0 },
        new byte[] { 0,19,4,6,8,17,12,14,3,11,20,22 },
        new byte[] { 0,2,4,6,10,12,14,8,16,18,20,22 },
        new byte[] { 0,2,20,6,8,10,18,14,16,4,12,22 },
        new byte[] { 0,2,4,23,8,10,12,21,16,18,7,15 }
    };
    private static readonly int[][] CFacelet =
    {
        new[] {8,9,20}, new[] {6,18,38}, new[] {0,36,47}, new[] {2,45,11},
        new[] {29,26,15}, new[] {27,44,24}, new[] {33,53,42}, new[] {35,17,51}
    };
    private static readonly int[][] EFacelet =
    {
        new[] {5,10}, new[] {7,19}, new[] {3,37}, new[] {1,46},
        new[] {32,16}, new[] {28,25}, new[] {30,43}, new[] {34,52},
        new[] {23,12}, new[] {21,41}, new[] {50,39}, new[] {48,14}
    };

    public byte[] Ca { get; } = new byte[8]; // orientation << 3 | permutation
    public byte[] Ea { get; } = new byte[12]; // permutation << 1 | orientation

    public CubeState() => Reset();

    public void Reset()
    {
        for (var i = 0; i < 8; i++) Ca[i] = (byte)i;
        for (var i = 0; i < 12; i++) Ea[i] = (byte)(i * 2);
    }

    public CubeState Clone()
    {
        var c = new CubeState(); Array.Copy(Ca, c.Ca, 8); Array.Copy(Ea, c.Ea, 12); return c;
    }

    private static CubeState Multiply(CubeState a, CubeState b)
    {
        var p = new CubeState();
        for (var cc = 0; cc < 8; cc++)
        {
            var bPiece = b.Ca[cc] & 7;
            var ori = ((a.Ca[bPiece] >> 3) + (b.Ca[cc] >> 3)) % 3;
            p.Ca[cc] = (byte)((a.Ca[bPiece] & 7) | (ori << 3));
        }
        for (var ed = 0; ed < 12; ed++)
        {
            var bPiece = b.Ea[ed] >> 1;
            p.Ea[ed] = (byte)(a.Ea[bPiece] ^ (b.Ea[ed] & 1));
        }
        return p;
    }

    private static CubeState MoveCube(int face, int turn)
    {
        var b = new CubeState(); Array.Copy(BaseCa[face], b.Ca, 8); Array.Copy(BaseEa[face], b.Ea, 12);
        if (turn == 0) return b;
        var twice = Multiply(b, b);
        if (turn == 1) return twice;
        return Multiply(twice, b);
    }

    public void ApplyMove(int face, int turn)
    {
        if (face is < 0 or > 5 || turn is < 0 or > 2) throw new ArgumentOutOfRangeException();
        var result = Multiply(MoveCube(face, turn), this);
        Array.Copy(result.Ca, Ca, 8); Array.Copy(result.Ea, Ea, 12);
    }

    public string ToFacelet()
    {
        var perm = Enumerable.Range(0, 54).ToArray();
        for (var cc = 0; cc < 8; cc++)
        {
            var piece = Ca[cc] & 7; var ori = Ca[cc] >> 3;
            for (var n = 0; n < 3; n++) perm[CFacelet[cc][(n + ori) % 3]] = CFacelet[piece][n];
        }
        for (var ed = 0; ed < 12; ed++)
        {
            var piece = Ea[ed] >> 1; var ori = Ea[ed] & 1;
            for (var n = 0; n < 2; n++) perm[EFacelet[ed][(n + ori) % 2]] = EFacelet[piece][n];
        }
        const string faces = "URFDLB";
        return new string(perm.Select(x => faces[x / 9]).ToArray());
    }

    public static bool TryFromFacelet(string facelet, out CubeState state)
    {
        state = new CubeState();
        if (facelet is null || facelet.Length != 54) return false;
        var centers = new[] { facelet[4], facelet[13], facelet[22], facelet[31], facelet[40], facelet[49] };
        for (var i = 0; i < 6; i++) for (var j = 0; j < i; j++) if (centers[i] == centers[j]) return false;
        var f = new int[54]; var counts = new int[6];
        for (var i = 0; i < 54; i++)
        {
            var v = Array.IndexOf(centers, facelet[i]);
            if (v < 0) return false; f[i] = v; counts[v]++;
        }
        if (counts.Any(x => x != 9)) return false;
        var ca = new byte[8];
        for (var i = 0; i < 8; i++)
        {
            var ori = 0; while (ori < 3 && f[CFacelet[i][ori]] is not (0 or 3)) ori++;
            if (ori == 3) return false;
            var col1 = f[CFacelet[i][(ori + 1) % 3]]; var col2 = f[CFacelet[i][(ori + 2) % 3]];
            ca[i] = 0xff;
            for (var j = 0; j < 8; j++)
            {
                if (col1 == CFacelet[j][1] / 9 && col2 == CFacelet[j][2] / 9) { ca[i] = (byte)(j | ((ori % 3) << 3)); break; }
            }
            if (ca[i] == 0xff) return false;
        }
        var ea = new byte[12];
        for (var i = 0; i < 12; i++)
        {
            ea[i] = 0xff;
            for (var j = 0; j < 12; j++)
            {
                if (f[EFacelet[i][0]] == EFacelet[j][0] / 9 && f[EFacelet[i][1]] == EFacelet[j][1] / 9) { ea[i] = (byte)(j << 1); break; }
                if (f[EFacelet[i][0]] == EFacelet[j][1] / 9 && f[EFacelet[i][1]] == EFacelet[j][0] / 9) { ea[i] = (byte)((j << 1) | 1); break; }
            }
            if (ea[i] == 0xff) return false;
        }
        uint corners = 0, edges = 0, twists = 0, flips = 0; var cp = 0; var ep = 0;
        for (var i = 0; i < 8; i++)
        {
            var piece = ca[i] & 7; if ((corners & (1u << piece)) != 0) return false; corners |= 1u << piece; twists += (uint)(ca[i] >> 3);
            for (var j = 0; j < i; j++) if ((ca[j] & 7) > piece) cp ^= 1;
        }
        for (var i = 0; i < 12; i++)
        {
            var piece = ea[i] >> 1; if ((edges & (1u << piece)) != 0) return false; edges |= 1u << piece; flips ^= (uint)(ea[i] & 1);
            for (var j = 0; j < i; j++) if ((ea[j] >> 1) > piece) ep ^= 1;
        }
        if (twists % 3 != 0 || flips != 0 || cp != ep) return false;
        Array.Copy(ca, state.Ca, 8); Array.Copy(ea, state.Ea, 12); return true;
    }
}
