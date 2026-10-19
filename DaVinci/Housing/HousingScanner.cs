using System;
using System.Collections.Generic;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using SheetFurniture = Lumina.Excel.Sheets.HousingFurniture;

namespace DaVinci.Housing;

public sealed record FurnitureEntry(nint Address, FurnitureKey Key, string Name, byte Stain);

/// <summary>
/// Read-only access to the game's housing state. Nothing here writes game memory
/// or sends anything to the server.
/// </summary>
public static unsafe class HousingScanner
{
    private static readonly Dictionary<uint, string> NameCache = new();

    public static IndoorTerritory* Indoor
    {
        get
        {
            var hm = HousingManager.Instance();
            return hm == null ? null : hm->IndoorTerritory;
        }
    }

    public static HouseKey? GetCurrentHouse()
    {
        var indoor = Indoor;
        if (indoor == null)
            return null;

        var id = indoor->HouseId.Id;
        return HouseKey.IsValid(id) ? new HouseKey(id) : null;
    }

    /// <summary>The item selected in housing edit mode, falling back to the hovered one.</summary>
    public static HousingObject* GetSelectedObject()
    {
        var indoor = Indoor;
        if (indoor == null)
            return null;
        return indoor->TargetedHousingObject != null ? indoor->TargetedHousingObject : indoor->HoveredHousingObject;
    }

    public static bool TryDescribe(HousingObject* obj, out FurnitureEntry entry)
    {
        entry = null!;
        var indoor = Indoor;
        if (obj == null || indoor == null || obj->HousingObjectId.Type != HousingObjectType.Furniture)
            return false;

        var memory = indoor->FurnitureManager.FurnitureMemory;
        var index = obj->HousingFurnitureIndex;
        if (index < 0 || index >= memory.Length)
            return false;

        ref var f = ref memory[index];
        var key = FurnitureKey.From(f.Id, f.Position.X, f.Position.Y, f.Position.Z, f.Rotation);
        entry = new FurnitureEntry((nint)obj, key, ResolveName(f.Id), f.Stain);
        return true;
    }

    /// <summary>
    /// Enumerates every placed furnishing. Call on demand (refresh button, house load),
    /// never per frame.
    /// </summary>
    public static List<FurnitureEntry> ListFurniture()
    {
        var list = new List<FurnitureEntry>(512);
        var indoor = Indoor;
        if (indoor == null)
            return list;

        ref var array = ref indoor->FurnitureManager.ObjectManager.ObjectArray;
        var objects = array.Objects;
        var count = Math.Min((int)array.ObjectCount, objects.Length);

        for (var i = 0; i < count; i++)
        {
            var go = objects[i].Value;
            if (go == null || go->ObjectKind != ObjectKind.HousingEventObject)
                continue;
            if (TryDescribe((HousingObject*)go, out var entry))
                list.Add(entry);
        }
        return list;
    }

    private static string ResolveName(uint furnitureId)
    {
        if (NameCache.TryGetValue(furnitureId, out var cached))
            return cached;

        // ClientStructs documents indoor rows as (0x20000 | Id); try the 0x30000 range too
        // until this is confirmed in game. The resolved row is logged by the inspector.
        var sheet = Plugin.DataManager.GetExcelSheet<SheetFurniture>();
        var name = $"Unknown furnishing #{furnitureId}";
        foreach (var rowId in new[] { 0x20000u | furnitureId, 0x30000u | furnitureId, furnitureId })
        {
            if (sheet.GetRowOrDefault(rowId) is { } row && row.Item.ValueNullable is { } item)
            {
                var text = item.Name.ExtractText();
                if (!string.IsNullOrEmpty(text))
                {
                    name = text;
                    break;
                }
            }
        }

        NameCache[furnitureId] = name;
        return name;
    }
}
