using System.Diagnostics;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using DaVinci.Housing;
using DaVinci.Materials;
using DaVinci.Model;
using DaVinci.Ownership;
using DaVinci.Windows;

namespace DaVinci;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IGameInteropProvider GameInterop { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private const string Command = "/davinci";

    public OverrideStore Store { get; }
    public SignboardWatcher Signboards { get; }
    public HouseKey? CurrentHouse { get; private set; }

    public readonly WindowSystem WindowSystem = new("DaVinci");
    public MainWindow MainWindow { get; }
    public PaintWindow PaintWindow { get; }

    // House detection is polled, not run every frame.
    private readonly Stopwatch houseCheckTimer = Stopwatch.StartNew();
    private const long HouseCheckIntervalMs = 500;

    public Plugin()
    {
        Store = new OverrideStore(PluginInterface.GetPluginConfigDirectory());
        Signboards = new SignboardWatcher(GameInterop);

        MainWindow = new MainWindow(this);
        PaintWindow = new PaintWindow(this);
        WindowSystem.AddWindow(MainWindow);
        WindowSystem.AddWindow(PaintWindow);

        CommandManager.AddHandler(Command, new CommandInfo((_, _) => MainWindow.Toggle())
        {
            HelpMessage = "Open the DaVinci window.",
        });

        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi += MainWindow.Toggle;
        Framework.Update += OnFrameworkUpdate;
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (houseCheckTimer.ElapsedMilliseconds < HouseCheckIntervalMs)
            return;
        houseCheckTimer.Restart();

        var house = HousingScanner.GetCurrentHouse();
        if (house == CurrentHouse)
            return;

        // Left a house or moved to a different one: flush the old, load the new.
        Store.SaveIfDirty();
        StainTester.Clear(); // the game rebuilds furniture objects on zone change
        CurrentHouse = house;

        if (house is { } key)
        {
            Store.Load(key);
            Log.Debug($"Entered {key.Describe()} ({key.Stem}), {Store.Current?.Items.Count ?? 0} customized items.");
        }
        else
        {
            Store.Unload();
            PaintWindow.IsOpen = false;
        }
    }

    public void Dispose()
    {
        Framework.Update -= OnFrameworkUpdate;
        Store.SaveIfDirty();
        Signboards.Dispose();

        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi -= MainWindow.Toggle;
        WindowSystem.RemoveAllWindows();
        CommandManager.RemoveHandler(Command);
    }
}
