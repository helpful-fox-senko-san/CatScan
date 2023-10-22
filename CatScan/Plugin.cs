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

    private static GameScanner _gameScanner { get; set; } = null!;
    public static HuntScanner Scanner { get; set; } = null!;
    public static Notifications Notifications { get; set; } = null!;

    public Plugin(DalamudPluginInterface pluginInterface)
    {
        DalamudService.Initialize(pluginInterface);
        Configuration = DalamudService.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        _gameScanner = new GameScanner();
        Scanner = new HuntScanner(_gameScanner);
        Notifications = new Notifications(Scanner);

        MainWindow = new MainWindow(_gameScanner);
        WindowSystem.AddWindow(Plugin.MainWindow);

        DalamudService.CommandManager.AddHandler(HuntCommandName, new CommandInfo(OnHuntCommand)
        {
            HelpMessage = "Open hunt tracker UI"
        });

        DalamudService.PluginInterface.UiBuilder.Draw += Draw;
        DalamudService.PluginInterface.UiBuilder.OpenMainUi += OpenMainUi;
        DalamudService.PluginInterface.UiBuilder.OpenConfigUi += OpenConfigUi;

#if DEBUG
        MainWindow.IsOpen = true;
#endif
    }

    public void Dispose()
    {
        _gameScanner.Dispose();

        DalamudService.PluginInterface.UiBuilder.Draw -= Draw;
        DalamudService.PluginInterface.UiBuilder.OpenMainUi -= OpenMainUi;
        DalamudService.PluginInterface.UiBuilder.OpenConfigUi -= OpenConfigUi;

        DalamudService.CommandManager.RemoveHandler(HuntCommandName);

        WindowSystem.RemoveAllWindows();

        MainWindow.Dispose();
    }

    private static void Draw()
    {
        Plugin.WindowSystem.Draw();
    }

    public static void OpenMainUi()
    {
        if (MainWindow.IsOpen)
            MainWindow.BringToFront();
        else
            MainWindow.IsOpen = true;
    }

    public static void OpenConfigUi()
    {
        OpenMainUi();
        MainWindow.SwitchToConfigTab();
    }

    private static void OnHuntCommand(string command, string args)
    {
        MainWindow.Toggle();
    }
}
