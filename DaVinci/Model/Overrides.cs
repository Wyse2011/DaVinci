using System.Collections.Generic;
using System.Numerics;

namespace DaVinci.Model;

/// <summary>
/// Color overrides for one section (one material on one model part).
/// Colors are packed 0xRRGGBB so a full house stays a few KB and is cheap to sync.
/// </summary>
public sealed class MaterialOverride
{
    public bool Enabled { get; set; } = true;
    public uint Diffuse { get; set; } = 0xFFFFFF;
    public uint Specular { get; set; } = 0x808080;
    public uint Emissive { get; set; } = 0x000000;

    public static Vector3 Unpack(uint rgb) =>
        new(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);

    public static uint Pack(Vector3 c) =>
        ((uint)(System.Math.Clamp(c.X, 0f, 1f) * 255f + 0.5f) << 16)
        | ((uint)(System.Math.Clamp(c.Y, 0f, 1f) * 255f + 0.5f) << 8)
        | (uint)(System.Math.Clamp(c.Z, 0f, 1f) * 255f + 0.5f);
}

/// <summary>All customized sections of one placed furnishing, keyed by section id ("p{part}m{slot}").</summary>
public sealed class ItemOverride
{
    public Dictionary<string, MaterialOverride> Sections { get; set; } = new();
}

/// <summary>Everything customized in one interior, keyed by FurnitureKey.Encode().</summary>
public sealed class HouseOverrides
{
    public int Version { get; set; } = 1;
    public Dictionary<string, ItemOverride> Items { get; set; } = new();
}
