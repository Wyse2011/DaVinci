using System;

namespace DaVinci.Housing;

/// <summary>
/// Identifies one placed furnishing in a way every client agrees on.
/// Position and rotation come from the server, so quantizing them gives the
/// same key for everyone. Moving an item changes its key; this is intended,
/// since a moved item is effectively a new placement.
/// </summary>
public readonly record struct FurnitureKey(uint FurnitureId, int X, int Y, int Z, int Rot)
{
    private const float PosScale = 100f;   // 1 cm
    private const float RotScale = 1000f;  // ~0.06 degrees

    public static FurnitureKey From(uint furnitureId, float x, float y, float z, float rotation) =>
        new(furnitureId,
            (int)MathF.Round(x * PosScale),
            (int)MathF.Round(y * PosScale),
            (int)MathF.Round(z * PosScale),
            (int)MathF.Round(rotation * RotScale));

    public string Encode() => $"{FurnitureId}:{X}:{Y}:{Z}:{Rot}";

    public static bool TryParse(string s, out FurnitureKey key)
    {
        key = default;
        var p = s.Split(':');
        if (p.Length != 5) return false;
        if (!uint.TryParse(p[0], out var id) || !int.TryParse(p[1], out var x) || !int.TryParse(p[2], out var y)
            || !int.TryParse(p[3], out var z) || !int.TryParse(p[4], out var r))
            return false;
        key = new FurnitureKey(id, x, y, z, r);
        return true;
    }
}
