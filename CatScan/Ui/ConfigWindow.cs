using Dalamud.Interface.Windowing;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
using System;
using System.Numerics;

namespace CatScan.Ui;

public partial class ConfigWindow : Window, IDisposable
{
    public enum Tabs
    {
        Config,
        Debug
    }

    // for debugging
    private GameScanner _gameScanner;

    private static Vector4 RGB(float r, float g, float b)
    {
        return new Vector4(r / 255.0f, g / 255.0f, b / 255.0f, 255.0f);
    }

    private Vector4 _textColorGone = RGB(160, 160, 160);

    private Tabs? _forceOpenTab;

    // Set to true when closed automatically
    // If set, then the window is re-opened automatically too
    public bool AutoClosed = false;

    public ConfigWindow(GameScanner gameScanner) : base("CatScan Configuration##CatScanConfig")
    {
        _gameScanner = gameScanner;
        this.SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(300, 400),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
    }

    public void Dispose()
    {
    }

    public void OpenTab(Tabs? tab)
    {
        if (IsOpen)
            BringToFront();
        else
            IsOpen = true;

        _forceOpenTab = tab;
    }

    public void DrawConfigWindow()
    {
        var doTab = (string name, Tabs tabId, Action drawfn) => {
            bool forceOpenFlag = (_forceOpenTab == tabId);

            using var tabItem = forceOpenFlag ? ImRaii.TabItem(name, ref forceOpenFlag, ImGuiTabItemFlags.SetSelected) : ImRaii.TabItem(name);

            if (tabItem.Success)
                drawfn();

            if (forceOpenFlag)
                _forceOpenTab = null;
        };

        using var tabs = ImRaii.TabBar("ConfigWindowTabs");
        doTab("Config", Tabs.Config, DrawConfig);
        doTab("Debug", Tabs.Config, DrawDebug);
    }

    public override void Draw()
    {
        try
        {
            DrawConfigWindow();
        }
        catch (Exception ex)
        {
            DalamudService.Log.Error(ex, "Draw");
        }
    }
}
