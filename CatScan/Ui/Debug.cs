using Dalamud.Interface.Windowing;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
using System;
using CatScan.FFXIV;

namespace CatScan.Ui;

public partial class ConfigWindow : Window, IDisposable
{
    private bool _displayEnemyCache = false;
    private bool _displayFateCache = false;
    private bool _displayBNpcNameCache = false;
    private bool _displayFateNameCache = false;
    private bool _displayCENameCache = false;
    private bool _displayCETable = false;

    private void DrawDebug()
    {
        using var tabId = ImRaii.PushId("Debug");

        ImGui.Text($"GameScanner:");
        if (_gameScanner.BetweenZones)
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
        //ImGui.Text($"  - FrameworkUpdateRegistered: {_gameScanner.FrameworkUpdateRegistered}");
        if (!_gameScanner.ScanningEnabled || !_gameScanner.FrameworkUpdateRegistered)
        {
            if (ImGui.Button("Force Enable Scanner"))
                _gameScanner.EnableScanning();
        }

        ImGui.Separator();
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

            //doRow("ScanTicks", null, b.ScanTicks);
            // Just equal to ScanTicks currently
            //doRow("ScanFateTicks", null, b.ScanFateTicks);
            doRow("ObjectTableRows", null, b.ObjectTableRows);
            doRow("FateTableRows", null, b.FateTableRows);
            doRow("EmittedEvents", null, b.EmittedEvents);
            //doRow("GameStringReads", a.GameStringReads, null);
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
                if (ImGui.Selectable(entry.Value.Name.ToString()))
                    ImGui.SetClipboardText(entry.Value.Name.ToString());
                ImGui.TableNextColumn();
                if (ImGui.Selectable(entry.Value.EnglishName.ToString()))
                    ImGui.SetClipboardText(entry.Value.EnglishName.ToString());
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
            using var table = ImRaii.Table("EnemyCacheTable", 3);
            ImGui.TableSetupColumn("id", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("name", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("name2", ImGuiTableColumnFlags.WidthStretch);

            foreach (var entry in _gameScanner.FateCache)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(entry.Key.ToString());
                ImGui.TableNextColumn();
                if (ImGui.Selectable(entry.Value.Name.ToString()))
                    ImGui.SetClipboardText(entry.Value.Name.ToString());
                ImGui.TableNextColumn();
                if (ImGui.Selectable(entry.Value.EnglishName.ToString()))
                    ImGui.SetClipboardText(entry.Value.EnglishName.ToString());
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
                if (ImGui.Selectable(entry.Value.ToString()))
                    ImGui.SetClipboardText(entry.Value.ToString());
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
                if (ImGui.Selectable(entry.Value.ToString()))
                    ImGui.SetClipboardText(entry.Value.ToString());
            }
        }

        ImGui.Separator();

        if (ImGui.Checkbox($"Display CENameCache ({GameData.CENameCacheSize})", ref _displayCENameCache) || _displayCENameCache)
        {
            using var table = ImRaii.Table("CENameCacheTable", 2);
            ImGui.TableSetupColumn("id", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("name", ImGuiTableColumnFlags.WidthStretch);

            foreach (var entry in GameData.CENameCache)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(entry.Key.ToString());
                ImGui.TableNextColumn();
                if (ImGui.Selectable(entry.Value.ToString()))
                    ImGui.SetClipboardText(entry.Value.ToString());
            }
        }

        ImGui.Separator();

        if (ImGui.Checkbox($"Display Raw CE Table", ref _displayCETable) || _displayCETable)
        {
            unsafe
            {
                var cetable = DynamicEventManager.GetDynamicEventManager();

                if (cetable == null)
                {
                    ImGui.Text("DynamicEventTable is not available here.");
                    return;
                }

                using var table = ImRaii.Table("DynamicEventTable", 4);
                ImGui.TableSetupColumn("id", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn("name", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("status", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn("timer", ImGuiTableColumnFlags.WidthFixed);

                for (int eventIndex = 0; eventIndex < DynamicEventManager.TableSize; ++eventIndex)
                {
                    DynamicEvent* ce = cetable->GetEvent(eventIndex);
                    nint ceAddr = (nint)ce;
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted($"{ce->DynamicEventId}");

                    ImGui.TableNextColumn();
                    ImGui.Selectable(ce->Name.ToString());

                    ImGui.TableNextColumn();

                    string statusStr =
                        ce->State switch
                        {
                            DynamicEventState.NotActive => "Not Active",
                            DynamicEventState.Registration => "Register",
                            DynamicEventState.Waiting => "Waiting",
                            DynamicEventState.BattleUnderway => "Battle",
                            _ => "Unknown"
                        };

                    if (ce->State != DynamicEventState.NotActive)
                        statusStr += $" ({ce->NumCombatants} / {ce->MaxCombatants})";

                    ImGui.TextUnformatted(statusStr);

                    ImGui.TableNextColumn();

                    int now = (int)((DateTimeOffset)System.DateTime.UtcNow).ToUnixTimeSeconds();

                    int mins = (ce->FinishTimeEpoch - now) / 60;
                    int secs = (ce->FinishTimeEpoch - now) % 60;

                    string timeStr = "--:--";

                    if (ce->State != DynamicEventState.NotActive)
                        timeStr = $"{mins:00}:{secs:00}";

                    ImGui.TextUnformatted(timeStr);
                }
            }
        }
    }
}
