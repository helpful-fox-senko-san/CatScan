using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using CatScan.Ui;

namespace CatScan;

public sealed class Plugin : IDalamudPlugin
{
    private const string HuntCommandName = "/catscan";

    public static Configuration Configuration { get; private set; } = null!;
    public static WindowSystem WindowSystem = new("CatScan");

    public static MainWindow MainWindow { get; private set; } = null!;
    public static ConfigWindow ConfigWindow { get; private set; } = null!;

    private static GameScanner _gameScanner { get; set; } = null!;
    public static HuntScanner Scanner { get; set; } = null!;
    public static Notifications Notifications { get; set; } = null!;

    public static bool BetweenAreas => _gameScanner.BetweenAreas || _gameScanner.BetweenZones;
    public static bool Occupied => _gameScanner.Occupied;

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        DalamudService.Initialize(pluginInterface);
        GameData.Initialize();

        Configuration = DalamudService.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        _gameScanner = new GameScanner();
        Scanner = new HuntScanner(_gameScanner);
        Notifications = new Notifications(Scanner);

        MainWindow = new MainWindow(_gameScanner);
        ConfigWindow = new ConfigWindow(_gameScanner);
        WindowSystem.AddWindow(Plugin.MainWindow);
        WindowSystem.AddWindow(Plugin.ConfigWindow);

        DalamudService.CommandManager.AddHandler(HuntCommandName, new CommandInfo(OnHuntCommand)
        {
            HelpMessage = "Open hunt tracker UI"
        });

        DalamudService.PluginInterface.UiBuilder.Draw += Draw;
        DalamudService.PluginInterface.UiBuilder.OpenMainUi += OpenMainUi;
        DalamudService.PluginInterface.UiBuilder.OpenConfigUi += OpenConfigUi;

        string ver = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version!.ToString();

        if (DalamudService.PluginInterface.Reason == PluginLoadReason.Reload)
            MainWindow.IsOpen = true;

        Scanner.ZoneChange += () => MainWindow.UpdateZoneName();
    }

    public void Dispose()
    {
        _gameScanner.Dispose();
        Notifications.Dispose();

        DalamudService.PluginInterface.UiBuilder.Draw -= Draw;
        DalamudService.PluginInterface.UiBuilder.OpenMainUi -= OpenMainUi;
        DalamudService.PluginInterface.UiBuilder.OpenConfigUi -= OpenConfigUi;

        DalamudService.CommandManager.RemoveHandler(HuntCommandName);

        WindowSystem.RemoveAllWindows();

        MainWindow.Dispose();
        ConfigWindow.Dispose();
    }

    private static void Draw()
    {
        {
            using var modelLock = HuntModel.Lock(true);
            Plugin.WindowSystem.Draw();
        }
        _gameScanner.PulseEmitQueue();
    }

    public static void OpenMainUi()
    {
        MainWindow.OpenTab(null);
    }

    public static void OpenConfigUi()
    {
        ConfigWindow.OpenTab(ConfigWindow.Tabs.Config);
    }

    private static void OnHuntCommand(string command, string args)
    {
        MainWindow.Toggle();
    }
}
