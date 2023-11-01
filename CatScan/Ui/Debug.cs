using Dalamud.Interface.Windowing;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
using System;

namespace CatScan.Ui;

public partial class MainWindow : Window, IDisposable
{
    private bool _displayEnemyCache = false;
    private bool _displayFateCache = false;
    private bool _displayBNpcNameCache = false;
    private bool _displayFateNameCache = false;

    private void DrawDebug()
    {
        using var tabId = ImRaii.PushId("Debug");

        ImGui.Text($"GameScanner:");
        if (_gameScanner.BetweenAreas)
        {
            ImGui.SameLine();
            ImGui.Text("[BetweenAreas]");
        }
        if (_gameScanner.TerritoryChanged)
        {
            ImGui.SameLine();
            ImGui.Text("[TerritoryChanged]");
        }
        ImGui.Text($"  - ScanningEnabled: {_gameScanner.ScanningEnabled}");
        ImGui.Text($"  - FrameworkUpdateRegistered: {_gameScanner.FrameworkUpdateRegistered}");
        if (!_gameScanner.ScanningEnabled || !_gameScanner.FrameworkUpdateRegistered)
        {
            if (ImGui.Button("Force Enable Scanner"))
                _gameScanner.EnableScanning();
        }
        //ImGui.Text($"  - EnemyCache:{_gameScanner.EnemyCacheSize}, Lost:{_gameScanner.LostIdsSize}, FateCache:{_gameScanner.FateCacheSize}");
        //ImGui.Text($"  - BNpcNameCache:{GameData.BNpcNameCacheSize}, FateNameCache:{GameData.FateNameCacheSize}");

        ImGui.Text("");
        ImGui.Text($"World {HuntModel.Territory.WorldId}, Zone {HuntModel.Territory.ZoneId}, Instance {HuntModel.Territory.Instance}");

        if (HuntModel.Territory.ZoneId > 0)
        {
            var zoneData = GameData.GetZoneData(HuntModel.Territory.ZoneId);
            ImGui.Text($"Map OffsetX:{zoneData.MapOffsetX}, OffsetY:{zoneData.MapOffsetY}, Scale:{zoneData.MapScale}");
        }

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

        ImGui.Separator();

        {
            using var table = ImRaii.Table("ScannerStatsTable", 2);
            ImGui.TableSetupColumn("name", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("rate", ImGuiTableColumnFlags.WidthFixed);

            var doRow = (string name, int? total, int? sec) => {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(name);
                ImGui.TableNextColumn();
                if (total == null)
                    ImGui.TextUnformatted($"{sec}/sec");
                else
                    ImGui.TextUnformatted($"{total}");
            };

            var a = _gameScanner.Stats;
            var b = _gameScanner.Stats1Sec;

            doRow("ScanTicks", null, b.ScanTicks);
            // Just equal to ScanTicks currently
            //doRow("ScanFateTicks", null, b.ScanFateTicks);
            doRow("ObjectTableRows", null, b.ObjectTableRows);
            doRow("FateTableRows", null, b.FateTableRows);
            doRow("GameStringReads", a.GameStringReads, null);
            doRow("EmittedEvents", null, b.EmittedEvents);
        }

        ImGui.Separator();

        if (ImGui.Checkbox($"Display EnemyCache ({_gameScanner.EnemyCacheSize}, LostIds:{_gameScanner.LostIdsSize})", ref _displayEnemyCache) || _displayEnemyCache)
        {
            using var table = ImRaii.Table("EnemyCacheTable", 3);
            ImGui.TableSetupColumn("id", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("name", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("name2", ImGuiTableColumnFlags.WidthStretch);

            foreach (var entry in _gameScanner.EnemyCache)
            {
                var interesting = (entry.Value.Interesting || entry.Value.InterestingKC);
                using var pushColor = ImRaii.PushColor(ImGuiCol.Text, RGB(192.0f, 240.0f, 192.0f), interesting);
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(entry.Key.ToString("X"));
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(entry.Value.Name);
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(entry.Value.EnglishName);
            }

            foreach (var entry in _gameScanner.LostIds)
            {
                using var pushColor = ImRaii.PushColor(ImGuiCol.Text, RGB(255.0f, 224.0f, 224.0f));
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(entry.ToString("X"));
                ImGui.TableNextColumn();
                ImGui.TextUnformatted("(lost)");
            }
        }

        ImGui.Separator();

        if (ImGui.Checkbox($"Display FateCache ({_gameScanner.FateCacheSize})", ref _displayFateCache) || _displayFateCache)
        {
            using var table = ImRaii.Table("EnemyCacheTable",2);
            ImGui.TableSetupColumn("id", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("name", ImGuiTableColumnFlags.WidthStretch);

            foreach (var entry in _gameScanner.FateCache)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(entry.Key.ToString());
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(entry.Value.Name);
            }
        }

        ImGui.Separator();

        if (ImGui.Checkbox($"Display BNpcNameCache ({GameData.BNpcNameCacheSize})", ref _displayBNpcNameCache) || _displayBNpcNameCache)
        {
            using var table = ImRaii.Table("BNpcNameCacheTable", 2);
            ImGui.TableSetupColumn("id", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("name", ImGuiTableColumnFlags.WidthStretch);

            foreach (var entry in GameData.BNpcNameCache)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(entry.Key.ToString());
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(entry.Value.ToString());
            }
        }

        ImGui.Separator();

        if (ImGui.Checkbox($"Display FateNameCache ({GameData.FateNameCacheSize})", ref _displayFateNameCache) || _displayFateNameCache)
        {
            using var table = ImRaii.Table("FateNameCacheTable", 2);
            ImGui.TableSetupColumn("id", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("name", ImGuiTableColumnFlags.WidthStretch);

            foreach (var entry in GameData.FateNameCache)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(entry.Key.ToString());
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(entry.Value.ToString());
            }
        }
    }
}
