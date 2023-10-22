using Dalamud.Interface.Windowing;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
using System;
using System.Numerics;

namespace CatScan.Ui;

public class MainWindow : Window, IDisposable
{
    // for debugging
    private GameScanner _gameScanner;

    public MainWindow(GameScanner gameScanner) : base("CatScan",
        ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        _gameScanner = gameScanner;
        this.SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(300, 300),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
    }

    public void Dispose()
    {
    }

    private void DrawScanResults()
    {
        foreach (var r in HuntModel.ScanResults.Values)
        {
            var statusText = "";
            if (r.Dead)
                statusText = " DEAD";
            else if (r.Missing)
                statusText = " MISSING";
            ImGui.Text($"[{r.Rank}] {r.Name} - HP:{r.HpPct:F1}%% - Pos:{r.MapX:F1},{r.MapY:F1}{statusText}");
        }
    }

    private void DrawKillCounts()
    {
        foreach (var r in HuntModel.KillCountLog)
        {
            ImGui.Text($"{r.Key} - {r.Value.Killed} killed + {r.Value.Missing} missing");
        }
    }

    private void DrawDebug()
    {
        ImGui.Text($"GameScanner:");
        ImGui.Text($"  - BetweenAreas: {_gameScanner.BetweenAreas}");
        ImGui.Text($"  - TerritoryChanged: {_gameScanner.TerritoryChanged}");
        ImGui.Text($"  - ScanningEnabled: {_gameScanner.ScanningEnabled}");
        ImGui.Text($"  - FrameworkUpdateRegistered: {_gameScanner.FrameworkUpdateRegistered}");
        if (!_gameScanner.ScanningEnabled || !_gameScanner.FrameworkUpdateRegistered)
        {
            if (ImGui.Button("Force Enable Scanner"))
                _gameScanner.EnableScanning();
        }
        ImGui.Text($"  - EnemyCache:{_gameScanner.EnemyCacheSize}, Lost:{_gameScanner.LostIdsSize}");

        ImGui.Text("");
        ImGui.Text($"World {HuntModel.Territory.WorldId}, Zone {HuntModel.Territory.ZoneId}, Instance {HuntModel.Territory.Instance}");

        if (ImGui.Button("Copy Model"))
            ImGui.SetClipboardText(HuntModel.Serialize());
        ImGui.SameLine();
        if (ImGui.Button("Import from Clipboard"))
        {
            HuntModel.Deserialize(ImGui.GetClipboardText());
            // During Deserialization the InRange parameters are reset to false
            // Resetting the GameScanner allows it to re-detect actually in-range enemies
            _gameScanner.ClearCache();
        }
    }

    public override void Draw()
    {
        string instanceText = "";
        if (HuntModel.Territory.Instance > 0)
            instanceText = " i" + HuntModel.Territory.Instance;
        string zoneName = HuntModel.Territory.ZoneData.Name;
        if (zoneName.Substring(0, 1) == "#")
            zoneName = $"Zone {zoneName}";
        ImGuiHelpers.CenteredText($"{zoneName}{instanceText}");

        using var tabs = ImRaii.TabBar("MainWindowTabs");
        using (var tabItem = ImRaii.TabItem("Scan Results"))
        {
            if (tabItem.Success)
                DrawScanResults();
        }
        using (var tabItem = ImRaii.TabItem("Kill Count"))
        {
            if (tabItem.Success)
                DrawKillCounts();
        }
        using (var tabItem = ImRaii.TabItem("Debug"))
        {
            if (tabItem.Success)
                DrawDebug();
        }
    }
}
