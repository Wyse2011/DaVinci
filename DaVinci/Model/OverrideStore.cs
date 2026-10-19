using System;
using System.IO;
using System.Text.Json;
using DaVinci.Housing;

namespace DaVinci.Model;

/// <summary>
/// Local persistence of overrides, one small JSON file per interior.
/// Later milestones layer sync on top of this; the store stays the single source of truth.
/// </summary>
public sealed class OverrideStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private readonly string directory;
    private HouseKey? currentKey;

    public HouseOverrides? Current { get; private set; }
    public bool IsDirty { get; private set; }

    public OverrideStore(string configDirectory)
    {
        directory = Path.Combine(configDirectory, "houses");
        Directory.CreateDirectory(directory);
    }

    private string PathFor(HouseKey key) => Path.Combine(directory, key.Stem + ".json");

    public void Load(HouseKey key)
    {
        currentKey = key;
        IsDirty = false;
        Current = new HouseOverrides();

        var path = PathFor(key);
        if (!File.Exists(path))
            return;

        try
        {
            Current = JsonSerializer.Deserialize<HouseOverrides>(File.ReadAllText(path), JsonOptions) ?? new HouseOverrides();
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, $"Failed to read overrides for {key}; starting empty.");
        }
    }

    public void Unload()
    {
        currentKey = null;
        Current = null;
        IsDirty = false;
    }

    public ItemOverride GetOrCreate(FurnitureKey key)
    {
        if (Current == null)
            throw new InvalidOperationException("No house loaded.");
        var id = key.Encode();
        if (!Current.Items.TryGetValue(id, out var item))
            Current.Items[id] = item = new ItemOverride();
        return item;
    }

    public ItemOverride? Get(FurnitureKey key) =>
        Current != null && Current.Items.TryGetValue(key.Encode(), out var item) ? item : null;

    public void Remove(FurnitureKey key)
    {
        if (Current?.Items.Remove(key.Encode()) == true)
            IsDirty = true;
    }

    public void MarkDirty() => IsDirty = true;

    public void SaveIfDirty()
    {
        if (!IsDirty || Current == null || currentKey is not { } key)
            return;

        // Drop empty entries so the file only ever holds real customizations.
        foreach (var (id, item) in Current.Items)
            if (item.Sections.Count == 0)
                Current.Items.Remove(id);

        try
        {
            var path = PathFor(key);
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(Current, JsonOptions));
            File.Move(tmp, path, overwrite: true);
            IsDirty = false;
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, $"Failed to save overrides for {key}.");
        }
    }
}
