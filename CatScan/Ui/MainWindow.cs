using Dalamud.Interface;
using Dalamud.Interface.Windowing;
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
        TrainLog,
        Fates,
        KillCount
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
        InitTrainLog();
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

    private string _cachedZoneName = string.Empty;
    private int _cachedZoneNameId = -1;

    public void DrawMainWindow()
    {
        string instanceText = "";
        if (HuntModel.Territory.Instance > 0)
            instanceText = " i" + HuntModel.Territory.Instance;

        int zoneId = HuntModel.Territory.ZoneId;
        string zoneName = (zoneId == _cachedZoneNameId) ? _cachedZoneName : string.Empty;

        if (zoneName.Length == 0)
        {
            zoneName = _cachedZoneName = GameData.GetZoneData(zoneId).Name;
            _cachedZoneNameId = zoneId;
        }

        // There is some excess space at the top of the window
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() - 2.0f);
        ImGuiHelpers.CenteredText($"{zoneName}{instanceText}");
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 2.0f);

        ImGui.SameLine();
        {
            using var pushFont = ImRaii.PushFont(UiBuilder.IconFont);
            ImGui.SetCursorPosX(ImGui.GetWindowWidth() - 30.0f);
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() - 4.0f);
            using var pushStyle = ImRaii.PushColor(ImGuiCol.Button, 0);
            if (ImGui.Button(Dalamud.Interface.FontAwesomeIcon.Cog.ToIconString()))
                Plugin.OpenConfigUi();
        }

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
        doTab("Train Log", Tabs.TrainLog, DrawTrainLog);
        doTab("Fates", Tabs.Fates, DrawFates);
        doTab("Kill Count", Tabs.KillCount, DrawKillCounts);
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
