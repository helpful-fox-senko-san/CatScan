﻿using Dalamud.Interface.Windowing;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
using System;
using System.IO;
using System.Numerics;

namespace CatScan.Ui;

public partial class MainWindow : Window, IDisposable
{
    public enum Tabs
    {
        ScanResults,
        Fates,
        KillCount,
        Config,
        Debug
    }

    // for debugging
    private GameScanner _gameScanner;
    private string _resourcePath = "";

    private static Vector4 RGB(float r, float g, float b)
    {
        return new Vector4(r / 255.0f, g / 255.0f, b / 255.0f, 255.0f);
    }

    private Tabs? _forceOpenTab;

    // Set to true when closed automatically
    // If set, then the window is re-opened automatically too
    public bool AutoClosed = false;

    public MainWindow(GameScanner gameScanner) : base("CatScan")
    {
        _gameScanner = gameScanner;
        this.SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(300, 200),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        _resourcePath = Path.Combine(DalamudService.PluginInterface.AssemblyLocation.Directory?.FullName!, "Resources");

        InitScanResults();
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

    public void DrawMainWindow()
    {
        string instanceText = "";
        if (HuntModel.Territory.Instance > 0)
            instanceText = " i" + HuntModel.Territory.Instance;
        // FIXME: There should not be a null dereference here...
        string zoneName = HuntModel.Territory.ZoneData.Name ?? string.Empty;
        if (zoneName.Length == 0 || (zoneName.Length > 0 && zoneName.Substring(0, 1) == "#"))
            zoneName = GameData.GetZoneData(HuntModel.Territory.ZoneId).Name;

        // There is some excess space at the top of the window
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() - 2.0f);
        // I think the blurry scaled text is charming but idk
        //ImGui.SetWindowFontScale(1.2f);
        ImGuiHelpers.CenteredText($"{zoneName}{instanceText}");
        //ImGui.SetWindowFontScale(1.0f);
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 2.0f);

        var doTab = (string name, Tabs tabId, Action drawfn) => {
            bool forceOpenFlag = (_forceOpenTab == tabId);

            using var tabItem = forceOpenFlag ? ImRaii.TabItem(name, ref forceOpenFlag, ImGuiTabItemFlags.SetSelected) : ImRaii.TabItem(name);

            if (tabItem.Success)
                drawfn();

            if (forceOpenFlag)
                _forceOpenTab = null;
        };

        using var tabs = ImRaii.TabBar("MainWindowTabs");
        doTab("Scan Results", Tabs.ScanResults, DrawScanResults);
        doTab("Fates", Tabs.Fates, DrawFates);
        doTab("Kill Count", Tabs.KillCount, DrawKillCounts);
        doTab("Config", Tabs.Config, DrawConfig);
        if (Plugin.Configuration.DebugEnabled)
            doTab("Debug", Tabs.Config, DrawDebug);
    }

    public override void Draw()
    {
        try
        {
            DrawMainWindow();
        }
        catch (Exception ex)
        {
            DalamudService.Log.Error(ex, "Draw");
        }
    }
}
