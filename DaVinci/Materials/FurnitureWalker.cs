using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.FFXIV.Client.LayoutEngine;
using FFXIVClientStructs.FFXIV.Client.LayoutEngine.Group;
using FFXIVClientStructs.FFXIV.Client.LayoutEngine.Layer;

namespace DaVinci.Materials;

public unsafe delegate void BgObjectVisitor(int partIndex, int depth, BgObject* bg);

/// <summary>
/// Walks a furnishing's shared-group layout and visits each render object (BgObject).
/// Part indices are assigned in walk order, so they're stable for a given model.
/// </summary>
public static unsafe class FurnitureWalker
{
    private const int MaxDepth = 4; // the game nests shared groups at most a few levels deep

    public static void Walk(HousingObject* obj, BgObjectVisitor visitor)
    {
        if (obj == null)
            return;

        var sg = ((GameObject*)obj)->SharedGroupLayoutInstance;
        if (sg == null)
            return;

        var partIndex = 0;
        WalkGroup(sg, 0, visitor, ref partIndex);
    }

    private static void WalkGroup(SharedGroupLayoutInstance* sg, int depth, BgObjectVisitor visitor, ref int partIndex)
    {
        if (depth > MaxDepth)
            return;

        foreach (var child in sg->Instances.Instances.AsSpan())
        {
            var node = child.Value;
            if (node == null || node->Instance == null)
                continue;

            var instance = node->Instance;
            switch (instance->Id.Type)
            {
                case InstanceType.BgPart:
                    var bg = ((BgPartsLayoutInstance*)instance)->GraphicsObject;
                    if (bg != null)
                        visitor(partIndex++, depth, bg);
                    break;

                case InstanceType.SharedGroup:
                    WalkGroup((SharedGroupLayoutInstance*)instance, depth + 1, visitor, ref partIndex);
                    break;
            }
        }
    }
}
