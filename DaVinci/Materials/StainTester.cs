using System.Collections.Generic;
using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Graphics;
using DaVinci.Housing;

namespace DaVinci.Materials;

/// <summary>
/// Phase 0 experiment: drive the game's own per-instance stain color slot with any RGB value.
/// Purely visual and client-side. Remembers original colors so they can be restored.
/// </summary>
public static unsafe class StainTester
{
    private static readonly Dictionary<(FurnitureKey Key, int Part), ByteColor> Originals = new();

    /// <summary>Returns how many parts accepted the color; parts without a stain slot are skipped.</summary>
    public static (int Applied, int Skipped) Apply(HousingObject* obj, FurnitureKey key, Vector3 rgb)
    {
        var applied = 0;
        var skipped = 0;
        var color = new ByteColor
        {
            R = ToByte(rgb.X),
            G = ToByte(rgb.Y),
            B = ToByte(rgb.Z),
        };

        FurnitureWalker.Walk(obj, (part, _, bg) =>
        {
            if (bg->StainBuffer == null)
            {
                skipped++;
                return;
            }

            var original = bg->StainBuffer->SrgbByteColor;
            Originals.TryAdd((key, part), original);

            var c = color;
            c.A = original.A; // keep whatever the game uses alpha for
            if (bg->TrySetStainColor(c))
                applied++;
            else
                skipped++;
        });

        return (applied, skipped);
    }

    public static int Restore(HousingObject* obj, FurnitureKey key)
    {
        var restored = 0;
        FurnitureWalker.Walk(obj, (part, _, bg) =>
        {
            if (bg->StainBuffer != null && Originals.Remove((key, part), out var original) && bg->TrySetStainColor(original))
                restored++;
        });
        return restored;
    }

    public static bool HasChanges(FurnitureKey key)
    {
        foreach (var k in Originals.Keys)
            if (k.Key == key)
                return true;
        return false;
    }

    /// <summary>Forget saved originals, e.g. after leaving the house (the game rebuilds objects anyway).</summary>
    public static void Clear() => Originals.Clear();

    private static byte ToByte(float v) => (byte)(System.Math.Clamp(v, 0f, 1f) * 255f + 0.5f);
}
