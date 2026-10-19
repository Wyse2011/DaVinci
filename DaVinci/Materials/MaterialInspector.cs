using System;
using System.Collections.Generic;
using System.IO;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.FFXIV.Client.System.Resource.Handle;
using Lumina.Data.Files;

namespace DaVinci.Materials;

/// <summary>One material slot on one model part. Plain data only: safe to keep after the game frees the object.</summary>
public sealed record MaterialInfo(
    int Slot,
    string Path,
    string Shader,
    bool HasColorTable,
    int ColorTableWidth,
    int ColorTableHeight,
    bool HasStainTable,
    int TextureCount,
    nint HandleAddress);

/// <summary>One model inside a furnishing. Many furnishings are built from several parts.</summary>
public sealed record ModelPartInfo(
    int PartIndex,
    int Depth,
    string ModelPath,
    bool HasStainBuffer,
    nint BgObjectAddress,
    List<MaterialInfo> Materials)
{
    /// <summary>Stable id for a "section": part index plus material slot.</summary>
    public static string SectionId(int part, int slot) => $"p{part}m{slot}";
}

/// <summary>
/// Snapshots the models and materials of a furnishing. Run on selection change only.
/// </summary>
public static unsafe class MaterialInspector
{
    private static readonly Dictionary<string, int> MaterialCountCache = new();

    public static List<ModelPartInfo> Inspect(HousingObject* obj)
    {
        var parts = new List<ModelPartInfo>();
        FurnitureWalker.Walk(obj, (partIndex, depth, bg) =>
        {
            var part = InspectBgObject(bg, partIndex, depth);
            if (part != null)
                parts.Add(part);
        });
        return parts;
    }

    private static ModelPartInfo? InspectBgObject(BgObject* bg, int partIndex, int depth)
    {
        if (bg->ModelResourceHandle == null)
            return null;

        var mdl = bg->ModelResourceHandle;
        var modelPath = ((ResourceHandle*)mdl)->FileName.ToString();
        var materials = new List<MaterialInfo>();

        var count = GetMaterialCount(modelPath);
        if (mdl->MaterialResourceHandles != null)
        {
            for (var slot = 0; slot < count; slot++)
            {
                var mtrl = mdl->MaterialResourceHandles[slot];
                if (mtrl == null)
                    continue;

                materials.Add(new MaterialInfo(
                    slot,
                    ((ResourceHandle*)mtrl)->FileName.ToString(),
                    mtrl->ShpkName.ToString(),
                    mtrl->HasColorTable,
                    mtrl->ColorTableWidth,
                    mtrl->ColorTableHeight,
                    mtrl->HasStainTable,
                    mtrl->TextureCount,
                    (nint)mtrl));
            }
        }

        return new ModelPartInfo(partIndex, depth, modelPath, bg->StainBuffer != null, (nint)bg, materials);
    }

    /// <summary>
    /// The resource handle doesn't expose a material count, so read it from the .mdl header.
    /// Handles both vanilla game paths and Penumbra-redirected files on disk.
    /// </summary>
    private static int GetMaterialCount(string resourcePath)
    {
        if (MaterialCountCache.TryGetValue(resourcePath, out var cached))
            return cached;

        var path = StripPenumbraPrefix(resourcePath);
        var count = 0;
        try
        {
            var file = Path.IsPathRooted(path)
                ? Plugin.DataManager.GameData.GetFileFromDisk<MdlFile>(path)
                : Plugin.DataManager.GetFile<MdlFile>(path);
            count = file?.ModelHeader.MaterialCount ?? 0;
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, $"Could not read material count for {path}");
        }

        MaterialCountCache[resourcePath] = count;
        return count;
    }

    /// <summary>Penumbra prefixes redirected paths as "|collection|actual/path".</summary>
    public static string StripPenumbraPrefix(string path)
    {
        if (path.Length > 0 && path[0] == '|')
        {
            var end = path.IndexOf('|', 1);
            if (end > 0)
                return path[(end + 1)..];
        }
        return path;
    }
}
