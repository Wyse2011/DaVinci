using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using DaVinci.Housing;
using DaVinci.Materials;
using DaVinci.Model;

namespace DaVinci.Windows;

/// <summary>
/// The Paint popup: one set of color pickers per section (material on a model part).
/// Milestone 1 edits and saves overrides; rendering them in game comes in milestone 2.
/// </summary>
public sealed class PaintWindow : Window
{
    private readonly Plugin plugin;
    private FurnitureEntry? entry;
    private List<ModelPartInfo> parts = new();

    public PaintWindow(Plugin plugin) : base("Paint##paint", ImGuiWindowFlags.AlwaysAutoResize)
    {
        this.plugin = plugin;
    }

    public void Open(FurnitureEntry target, List<ModelPartInfo> targetParts)
    {
        entry = target;
        parts = targetParts;
        WindowName = $"Paint: {target.Name}##paint";
        IsOpen = true;
    }

    public override void OnClose() => plugin.Store.SaveIfDirty();

    public override void Draw()
    {
        if (entry == null || plugin.Store.Current == null)
        {
            ImGui.Text("Nothing selected.");
            return;
        }

        ImGui.TextDisabled("Preview isn't wired up yet; changes are saved for milestone 2.");

        var item = plugin.Store.Get(entry.Key);
        foreach (var part in parts)
        {
            foreach (var m in part.Materials)
            {
                var sectionId = ModelPartInfo.SectionId(part.PartIndex, m.Slot);
                DrawSection(sectionId, part, m, item);
            }
        }

        ImGui.Separator();
        if (ImGui.Button("Reset item"))
            plugin.Store.Remove(entry.Key);
        ImGui.SameLine();
        if (ImGui.Button("Save"))
            plugin.Store.SaveIfDirty();
    }

    private void DrawSection(string sectionId, ModelPartInfo part, MaterialInfo m, ItemOverride? item)
    {
        ImGui.PushID(sectionId);
        var name = System.IO.Path.GetFileNameWithoutExtension(MaterialInspector.StripPenumbraPrefix(m.Path));
        ImGui.Text($"Section {sectionId}  ({name})");
        if (!m.HasColorTable)
        {
            ImGui.SameLine();
            ImGui.TextDisabled("(no color table)");
        }

        MaterialOverride? ov = null;
        item?.Sections.TryGetValue(sectionId, out ov);

        if (ov == null)
        {
            if (ImGui.Button("Customize"))
            {
                plugin.Store.GetOrCreate(entry!.Key).Sections[sectionId] = new MaterialOverride();
                plugin.Store.MarkDirty();
            }
        }
        else
        {
            var enabled = ov.Enabled;
            if (ImGui.Checkbox("Enabled", ref enabled)) { ov.Enabled = enabled; plugin.Store.MarkDirty(); }

            EditColor("Diffuse", () => ov.Diffuse, v => ov.Diffuse = v);
            EditColor("Specular", () => ov.Specular, v => ov.Specular = v);
            EditColor("Emissive", () => ov.Emissive, v => ov.Emissive = v);

            if (ImGui.Button("Clear section"))
            {
                item!.Sections.Remove(sectionId);
                plugin.Store.MarkDirty();
            }
        }

        ImGui.Separator();
        ImGui.PopID();
    }

    private void EditColor(string label, System.Func<uint> get, System.Action<uint> set)
    {
        var color = MaterialOverride.Unpack(get());
        if (ImGui.ColorEdit3(label, ref color))
        {
            set(MaterialOverride.Pack(color));
            plugin.Store.MarkDirty();
        }
    }
}
