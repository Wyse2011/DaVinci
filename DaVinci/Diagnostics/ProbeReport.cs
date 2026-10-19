using System.Collections.Generic;
using System.Text;
using DaVinci.Housing;
using DaVinci.Materials;
using DaVinci.Ownership;
using DaVinci.Permissions;

namespace DaVinci.Diagnostics;

/// <summary>
/// Builds a plain-text Phase 0 report to paste back for review.
/// Names are redacted by default so the report is safe to share.
/// </summary>
public static class ProbeReport
{
    public static string Build(
        PermissionSnapshot perms,
        SignboardInfo? signboard,
        bool signboardHookActive,
        FurnitureEntry? selected,
        IReadOnlyList<ModelPartInfo> parts,
        string? stainResult,
        bool includeNames)
    {
        var sb = new StringBuilder();
        sb.AppendLine("== DaVinci Phase 0 report ==");

        sb.AppendLine($"House: {(perms.CurrentHouse is { } h ? $"{h.Describe()} (world {h.WorldId}, id {h.Stem})" : "not inside")}");
        sb.AppendLine($"HasHousePermissions: {perms.HasHousePermissions}");
        sb.AppendLine($"UsingHousingFunctions: {perms.UsingHousingFunctions}");
        sb.AppendLine($"Relationship: {perms.Relationship}");
        sb.AppendLine($"Proposed gate allows painting: {perms.ProposedCanPaint}");
        foreach (var o in perms.OwnedHouses)
            sb.AppendLine($"  Owned {o.Label}: {(o.House is { } oh ? $"{oh.Describe()} id {oh.Stem}" : "none")}");

        sb.AppendLine($"Signboard hook active: {signboardHookActive}");
        if (signboard != null)
        {
            sb.AppendLine($"Signboard for this estate: type {signboard.EstateType}, size {signboard.Size}, seen {signboard.SeenAtUtc:u}");
            sb.AppendLine($"  Estate name: {Redact(signboard.EstateName, includeNames)}");
            sb.AppendLine($"  Owner name:  {Redact(signboard.OwnerName, includeNames)}");
            sb.AppendLine($"  FC tag:      {Redact(signboard.FcTag, includeNames)}");
            sb.AppendLine($"  Greeting length: {signboard.Greeting.Length}");
        }
        else
        {
            sb.AppendLine("Signboard for this estate: not seen yet");
        }

        if (selected != null)
        {
            sb.AppendLine($"Selected: {selected.Name} key {selected.Key.Encode()} stain {selected.Stain}");
            foreach (var p in parts)
            {
                sb.AppendLine($"  Part {p.PartIndex} depth {p.Depth} stainSlot {(p.HasStainBuffer ? "yes" : "no")} mdl {MaterialInspector.StripPenumbraPrefix(p.ModelPath)}");
                foreach (var m in p.Materials)
                    sb.AppendLine($"    [{m.Slot}] {MaterialInspector.StripPenumbraPrefix(m.Path)} shpk {m.Shader} " +
                                  $"colorTable {(m.HasColorTable ? $"{m.ColorTableWidth}x{m.ColorTableHeight}" : "no")} " +
                                  $"stainTable {(m.HasStainTable ? "yes" : "no")} tex {m.TextureCount} handle 0x{m.HandleAddress:X}");
            }
        }
        else
        {
            sb.AppendLine("Selected: nothing");
        }

        if (stainResult != null)
            sb.AppendLine($"Last stain test: {stainResult}");

        return sb.ToString();
    }

    private static string Redact(string value, bool include)
    {
        if (include || value.Length == 0)
            return value.Length == 0 ? "(empty)" : value;
        return $"{value[0]}*** ({value.Length} chars)";
    }
}
