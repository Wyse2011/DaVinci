using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using DaVinci.Diagnostics;
using DaVinci.Housing;
using DaVinci.Materials;
using DaVinci.Permissions;

namespace DaVinci.Windows;

/// <summary>Phase 0 probe window: selection, permissions, ownership, and the room list.</summary>
public sealed unsafe class MainWindow : Window
{
    private static readonly Vector4 Good = new(0.4f, 0.9f, 0.4f, 1f);
    private static readonly Vector4 Bad = new(0.95f, 0.45f, 0.4f, 1f);

    private readonly Plugin plugin;

    private nint lastSelected;
    private FurnitureEntry? selectedEntry;
    private List<ModelPartInfo> selectedParts = new();
    private List<FurnitureEntry>? furnitureList;

    private Vector3 testColor = new(0.2f, 0.6f, 1f);
    private string? lastStainResult;
    private bool devOverrideGate;
    private bool reportIncludeNames;

    public MainWindow(Plugin plugin) : base("DaVinci##main")
    {
        this.plugin = plugin;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(500, 380), MaximumSize = new Vector2(float.MaxValue) };
    }

    public override void Draw()
    {
        if (plugin.CurrentHouse is not { } house)
        {
            ImGui.TextWrapped("Not inside a house, apartment, or private chamber.");
            return;
        }

        ImGui.Text(house.Describe());
        ImGui.TextDisabled($"World {house.WorldId}   House id {house.Stem}   Saved customizations: {plugin.Store.Current?.Items.Count ?? 0}");

        UpdateSelection();

        using var tabs = ImRaii.TabBar("##tabs");
        if (!tabs.Success)
            return;

        using (var tab = ImRaii.TabItem("Selection"))
            if (tab.Success) DrawSelectionTab();
        using (var tab = ImRaii.TabItem("Permissions"))
            if (tab.Success) DrawPermissionsTab();
        using (var tab = ImRaii.TabItem("Ownership"))
            if (tab.Success) DrawOwnershipTab(house);
        using (var tab = ImRaii.TabItem("Room"))
            if (tab.Success) DrawRoomTab();
        using (var tab = ImRaii.TabItem("Report"))
            if (tab.Success) DrawReportTab(house);
    }

    // ---------- Selection ----------

    /// <summary>Re-inspect only when the selection changes; this is the only per-frame work.</summary>
    private void UpdateSelection()
    {
        var obj = HousingScanner.GetSelectedObject();
        if ((nint)obj == lastSelected)
            return;
        lastSelected = (nint)obj;
        Reinspect();
    }

    private void Reinspect()
    {
        var obj = HousingScanner.GetSelectedObject();
        if (obj != null && HousingScanner.TryDescribe(obj, out var entry))
        {
            selectedEntry = entry;
            selectedParts = MaterialInspector.Inspect(obj);
        }
        else
        {
            selectedEntry = null;
            selectedParts = new();
        }
    }

    private void DrawSelectionTab()
    {
        if (selectedEntry == null)
        {
            ImGui.TextWrapped("Enter housing edit mode and select or hover a furnishing.");
            return;
        }

        ImGui.Text($"Selected: {selectedEntry.Name}");
        ImGui.TextDisabled($"Key {selectedEntry.Key.Encode()}   Game dye {selectedEntry.Stain}");

        var perms = PermissionProbe.Capture();
        var canPaint = perms.ProposedCanPaint || devOverrideGate;
        using (ImRaii.Disabled(!canPaint))
        {
            if (ImGui.Button("Paint..."))
                plugin.PaintWindow.Open(selectedEntry, selectedParts);
        }
        ImGui.SameLine();
        if (ImGui.Button("Re-inspect"))
            Reinspect(); // materials can finish loading after the item appears

        DrawStainTest(canPaint);

        ImGui.Separator();
        foreach (var part in selectedParts)
        {
            var label = $"Part {part.PartIndex}: {System.IO.Path.GetFileName(MaterialInspector.StripPenumbraPrefix(part.ModelPath))}##part{part.PartIndex}";
            using var node = ImRaii.TreeNode(label);
            if (!node.Success)
                continue;

            ImGui.TextDisabled($"Depth {part.Depth}   Stain slot: {(part.HasStainBuffer ? "yes" : "no")}");
            ImGui.TextDisabled(MaterialInspector.StripPenumbraPrefix(part.ModelPath));
            foreach (var m in part.Materials)
            {
                ImGui.BulletText($"[{m.Slot}] {System.IO.Path.GetFileName(MaterialInspector.StripPenumbraPrefix(m.Path))}");
                using var indent = ImRaii.PushIndent();
                ImGui.TextDisabled($"Shader {m.Shader}   Textures {m.TextureCount}");
                ImGui.TextDisabled(m.HasColorTable
                    ? $"Color table {m.ColorTableWidth}x{m.ColorTableHeight}   Stain table: {(m.HasStainTable ? "yes" : "no")}"
                    : "No color table");
                ImGui.TextDisabled($"Handle 0x{m.HandleAddress:X}");
            }
        }

        if (selectedParts.Count == 0)
            ImGui.TextDisabled("No model parts found yet. Try Re-inspect.");
    }

    private void DrawStainTest(bool canPaint)
    {
        ImGui.Separator();
        ImGui.Text("Stain color test (visual only, this client only)");
        ImGui.ColorEdit3("Test color", ref testColor);

        using (ImRaii.Disabled(!canPaint))
        {
            if (ImGui.Button("Apply to selected"))
            {
                var obj = HousingScanner.GetSelectedObject();
                if (obj != null && selectedEntry != null)
                {
                    var (applied, skipped) = StainTester.Apply(obj, selectedEntry.Key, testColor);
                    lastStainResult = $"{selectedEntry.Name}: applied to {applied} part(s), {skipped} without a stain slot";
                    Plugin.Log.Information($"[StainTest] {lastStainResult}");
                }
            }
        }
        ImGui.SameLine();
        if (ImGui.Button("Restore selected"))
        {
            var obj = HousingScanner.GetSelectedObject();
            if (obj != null && selectedEntry != null)
                lastStainResult = $"Restored {StainTester.Restore(obj, selectedEntry.Key)} part(s)";
        }

        if (lastStainResult != null)
            ImGui.TextDisabled(lastStainResult);
    }

    // ---------- Permissions ----------

    private void DrawPermissionsTab()
    {
        var p = PermissionProbe.Capture();

        Flag("Game says you can decorate here (HasHousePermissions)", p.HasHousePermissions);
        Flag("Housing functions active (edit mode or furnishing menus)", p.UsingHousingFunctions);
        ImGui.Text($"Your relationship to this house: {p.Relationship}");
        Flag("Proposed gate: painting allowed", p.ProposedCanPaint);

        ImGui.Spacing();
        ImGui.Text("Houses the game says you own or share:");
        foreach (var o in p.OwnedHouses)
            ImGui.BulletText($"{o.Label}: {(o.House is { } h ? h.Describe() : "none")}");

        ImGui.Spacing();
        ImGui.Checkbox("Dev override: allow painting anyway (testing only, not saved)", ref devOverrideGate);
    }

    // ---------- Ownership ----------

    private void DrawOwnershipTab(HouseKey house)
    {
        Flag("Placard watcher hook active", plugin.Signboards.HookActive);
        ImGui.TextWrapped("The watcher records estate placard data whenever the game receives it. " +
                          "Test: note whether this fills in on entering, or only after viewing the placard outside.");

        var info = plugin.Signboards.Get(house);
        if (info == null)
        {
            ImGui.TextDisabled("No placard data seen for this estate yet.");
        }
        else
        {
            ImGui.Text($"Estate: {info.EstateName}");
            ImGui.Text($"Owner: {info.OwnerName}");
            ImGui.Text($"FC tag: {(info.FcTag.Length > 0 ? info.FcTag : "(none)")}");
            ImGui.Text($"Estate type {info.EstateType}, size {info.Size}");
            ImGui.TextDisabled($"Seen at {info.SeenAtUtc.ToLocalTime():T}   Greeting length {info.Greeting.Length}");
        }

        ImGui.Spacing();
        ImGui.TextDisabled($"Placards seen this session: {plugin.Signboards.Seen.Count}");
    }

    // ---------- Room ----------

    private void DrawRoomTab()
    {
        if (ImGui.Button(furnitureList == null ? "Scan room" : "Rescan room"))
            furnitureList = HousingScanner.ListFurniture();

        if (furnitureList == null)
            return;

        ImGui.SameLine();
        ImGui.Text($"{furnitureList.Count} furnishings, {furnitureList.Select(f => f.Name).Distinct().Count()} distinct");

        using var child = ImRaii.Child("##furniture", Vector2.Zero, true);
        if (!child.Success)
            return;

        foreach (var f in furnitureList)
        {
            var customized = plugin.Store.Get(f.Key) != null;
            ImGui.Text($"{(customized ? "*" : " ")} {f.Name}");
        }
    }

    // ---------- Report ----------

    private void DrawReportTab(HouseKey house)
    {
        ImGui.TextWrapped("Copies a text summary of everything above for the currently selected item. Names are redacted unless you tick the box.");
        ImGui.Checkbox("Include estate and owner names", ref reportIncludeNames);

        if (ImGui.Button("Copy report to clipboard"))
        {
            var report = ProbeReport.Build(
                PermissionProbe.Capture(force: true),
                plugin.Signboards.Get(house),
                plugin.Signboards.HookActive,
                selectedEntry,
                selectedParts,
                lastStainResult,
                reportIncludeNames);
            ImGui.SetClipboardText(report);
            Plugin.Log.Information(report);
        }
    }

    private static void Flag(string label, bool value)
    {
        ImGui.TextColored(value ? Good : Bad, value ? "YES" : "NO ");
        ImGui.SameLine();
        ImGui.Text(label);
    }
}
