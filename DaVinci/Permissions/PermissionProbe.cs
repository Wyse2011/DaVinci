using System;
using System.Collections.Generic;
using System.Diagnostics;
using FFXIVClientStructs.FFXIV.Client.Game;
using DaVinci.Housing;

namespace DaVinci.Permissions;

public enum HouseRelationship
{
    NotInside,
    Unrelated,
    FreeCompanyEstate,
    FcPrivateChambers,
    InsideFcBuilding,
    PersonalEstate,
    SharedEstate,
    Apartment,
}

public sealed record OwnedHouse(string Label, EstateType Type, int Index, HouseKey? House);

public sealed record PermissionSnapshot(
    HouseKey? CurrentHouse,
    bool HasHousePermissions,
    bool UsingHousingFunctions,
    HouseRelationship Relationship,
    IReadOnlyList<OwnedHouse> OwnedHouses)
{
    /// <summary>
    /// Proposed client-side gate: the game says you can decorate here, or the house is one of yours.
    /// This is layer 1 of the permission design; the server and visitor checks come in Phase 3.
    /// </summary>
    public bool ProposedCanPaint =>
        CurrentHouse != null
        && (HasHousePermissions || Relationship is not (HouseRelationship.NotInside or HouseRelationship.Unrelated));
}

/// <summary>
/// Reads the game's own housing permission signals. Read-only; nothing here is sent anywhere.
/// Results are cached briefly so drawing the UI every frame stays cheap.
/// </summary>
public static unsafe class PermissionProbe
{
    private static readonly (string Label, EstateType Type, int Index)[] EstateQueries =
    {
        ("Free Company estate", EstateType.FreeCompanyEstate, -1),
        ("FC private chambers", EstateType.PersonalChambers, -1),
        ("Personal estate", EstateType.PersonalEstate, -1),
        ("Shared estate 1", EstateType.SharedEstate, 0),
        ("Shared estate 2", EstateType.SharedEstate, 1),
        ("Apartment", EstateType.ApartmentRoom, -1),
    };

    private static readonly Stopwatch CacheTimer = Stopwatch.StartNew();
    private static PermissionSnapshot? cached;
    private const long CacheMs = 250;

    public static PermissionSnapshot Capture(bool force = false)
    {
        if (!force && cached != null && CacheTimer.ElapsedMilliseconds < CacheMs)
            return cached;

        CacheTimer.Restart();
        cached = CaptureNow();
        return cached;
    }

    private static PermissionSnapshot CaptureNow()
    {
        var hm = HousingManager.Instance();
        var conditions = Conditions.Instance();
        var current = HousingScanner.GetCurrentHouse();

        var hasPerms = false;
        if (hm != null && current != null)
        {
            try { hasPerms = hm->HasHousePermissions(); }
            catch (Exception ex) { Plugin.Log.Warning(ex, "HasHousePermissions failed"); }
        }

        var owned = new List<OwnedHouse>();
        foreach (var (label, type, index) in EstateQueries)
        {
            HouseKey? house = null;
            try
            {
                var id = HousingManager.GetOwnedHouseId(type, index).Id;
                if (HouseKey.IsValid(id))
                    house = new HouseKey(id);
            }
            catch (Exception ex)
            {
                Plugin.Log.Warning(ex, $"GetOwnedHouseId({type}, {index}) failed");
            }
            owned.Add(new OwnedHouse(label, type, index, house));
        }

        return new PermissionSnapshot(
            current,
            hasPerms,
            conditions != null && conditions->UsingHousingFunctions,
            Classify(current, owned),
            owned);
    }

    private static HouseRelationship Classify(HouseKey? current, List<OwnedHouse> owned)
    {
        if (current is not { } here)
            return HouseRelationship.NotInside;

        // Exact matches first, then "same building" (e.g. standing in the FC house while owning chambers there).
        foreach (var o in owned)
        {
            if (o.House is not { } h || h.Id != here.Id)
                continue;
            return o.Type switch
            {
                EstateType.FreeCompanyEstate => HouseRelationship.FreeCompanyEstate,
                EstateType.PersonalChambers => HouseRelationship.FcPrivateChambers,
                EstateType.PersonalEstate => HouseRelationship.PersonalEstate,
                EstateType.SharedEstate => HouseRelationship.SharedEstate,
                EstateType.ApartmentRoom => HouseRelationship.Apartment,
                _ => HouseRelationship.Unrelated,
            };
        }

        foreach (var o in owned)
            if (o.House is { } h && h.SameBuilding(here) && o.Type is EstateType.FreeCompanyEstate or EstateType.PersonalChambers)
                return HouseRelationship.InsideFcBuilding;

        return HouseRelationship.Unrelated;
    }
}
