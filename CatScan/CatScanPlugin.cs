using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using CatScan.Ui;
using System;

namespace CatScan;

public sealed class CatScanPlugin : IDisposable
{
    private const string HuntCommandName = "/catscan";

    public static Configuration Configuration { get; private set; } = null!;
    public static WindowSystem WindowSystem = new("CatScan");

    public static ConfigWindow ConfigWindow { get; private set; } = null!;
    public static MainWindow MainWindow { get; private set; } = null!;

    private GameScanner _gameScanner { get; set; } = null!;
    public HuntScanner Scanner { get; set; } = null!;

    public CatScanPlugin()
    {
        Configuration = DalamudService.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        _gameScanner = new GameScanner();
        Scanner = new HuntScanner(_gameScanner);

        ConfigWindow = new ConfigWindow();
        MainWindow = new MainWindow();

        WindowSystem.AddWindow(CatScanPlugin.ConfigWindow);
        WindowSystem.AddWindow(CatScanPlugin.MainWindow);

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

        ConfigWindow.Dispose();
        MainWindow.Dispose();
    }

    private static void Draw()
    {
        CatScanPlugin.WindowSystem.Draw();
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
        if (ConfigWindow.IsOpen)
            ConfigWindow.BringToFront();
        else
            ConfigWindow.IsOpen = true;
    }

    private static void OnHuntCommand(string command, string args)
    {
        MainWindow.Toggle();
    }
}
