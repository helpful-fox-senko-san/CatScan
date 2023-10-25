using Dalamud.Interface.Windowing;
using ImGuiNET;
using System;

namespace CatScan.Ui;

public partial class MainWindow : Window, IDisposable
{
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
        ImGui.Text($"  - FateCache:{_gameScanner.FateCacheSize}");

        ImGui.Text("");
        ImGui.Text($"World {HuntModel.Territory.WorldId}, Zone {HuntModel.Territory.ZoneId}, Instance {HuntModel.Territory.Instance}");

        if (ImGui.Button("Copy Model"))
            ImGui.SetClipboardText(HuntModel.Serialize());
        ImGui.SameLine();
        if (ImGui.Button("Import from Clipboard"))
        {
            HuntModel.Deserialize(ImGui.GetClipboardText());
            // During Deserialization the Missing parameter is reset to true
            // Resetting the GameScanner allows it to re-detect actually in-range enemies
            _gameScanner.ClearCache();
        }
    }
}
