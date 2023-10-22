using Dalamud.Interface.Windowing;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Internal; // for IDalamudTextureWrap
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;

using TerritoryType = Lumina.Excel.GeneratedSheets.TerritoryType;

namespace CatScan.Ui;

public class MainWindow : Window, IDisposable
{
    // for debugging
    private GameScanner _gameScanner;
    private string _resourcePath = "";

    private IDalamudTextureWrap? _iconB;
    private IDalamudTextureWrap? _iconA;
    private IDalamudTextureWrap? _iconS;
    private IDalamudTextureWrap? _iconF;

    private static Vector4 RGB(float r, float g, float b)
    {
        return new Vector4(r / 255.0f, g / 255.0f, b / 255.0f, 255.0f);
    }

    private Vector4 _textColorPulled = RGB(192, 32, 32);
    private Vector4 _textColorDead = RGB(160, 96, 96);
    private Vector4 _textColorGone = RGB(160, 160, 160);

    private Vector4 _textColorKc = RGB(160, 192, 224);

    private Dictionary<int, uint> _territoryToMapId = new();

    private bool _forceOpenConfig = false;

    public MainWindow(GameScanner gameScanner) : base("CatScan")
    {
        _gameScanner = gameScanner;
        this.SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(300, 300),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        var territoryData = DalamudService.DataManager.GetExcelSheet<TerritoryType>();

        if (territoryData != null)
        {
            foreach (var z in HuntData.Zones)
            {
                var row = territoryData.GetRow((uint)z.Key);

                if (row != null)
                    _territoryToMapId.Add(z.Key, row.Map.Row);
            }
        }

        _resourcePath = Path.Combine(DalamudService.PluginInterface.AssemblyLocation.Directory?.FullName!, "Resources");

        DalamudService.PluginInterface.UiBuilder.LoadImageAsync(Path.Combine(_resourcePath, "B.png")).ContinueWith(icon => {
            _iconB = icon.Result;
        });

        DalamudService.PluginInterface.UiBuilder.LoadImageAsync(Path.Combine(_resourcePath, "A.png")).ContinueWith(icon => {
            _iconA = icon.Result;
        });

        DalamudService.PluginInterface.UiBuilder.LoadImageAsync(Path.Combine(_resourcePath, "S.png")).ContinueWith(icon => {
            _iconS = icon.Result;
        });

        DalamudService.PluginInterface.UiBuilder.LoadImageAsync(Path.Combine(_resourcePath, "F.png")).ContinueWith(icon => {
            _iconF = icon.Result;
        });
    }

    public void Dispose()
    {
    }

    private void DrawRankIcon(Rank rank)
    {
        var icon = _iconS;

        switch (rank)
        {
            case Rank.B:
            case Rank.Minion:
                icon = _iconB;
                break;

            case Rank.A:
                icon = _iconA;
                break;

            case Rank.FATE:
                icon = _iconF;
                break;
        }

        if (icon != null)
            ImGui.Image(icon.ImGuiHandle, new(24, 24));
        else
            ImGui.Text("");
    }

    private void DrawScanResults()
    {
        using var table = ImRaii.Table("ScanResultsTable", 2);
        ImGui.TableSetupColumn("icon", ImGuiTableColumnFlags.WidthFixed);
        ImGui.TableSetupColumn("name", ImGuiTableColumnFlags.WidthStretch);

        foreach (var r in HuntModel.ScanResults.Values)
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            DrawRankIcon(r.Rank);

            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();

            Vector4 color = Vector4.Zero;
            if (r.Dead)
                color = _textColorDead;
            else if (r.Missing)
                color = _textColorGone;
            else if (r.HpPct < 100.0)
                color = _textColorPulled;

            using (ImRaii.PushColor(ImGuiCol.Text, color, color != Vector4.Zero))
            {
                if (ImGui.Selectable($"{r.Name} ( {r.MapX:F1} , {r.MapY:F1} ) HP: {r.HpPct:F1}%"))
                {
                    if (_territoryToMapId.TryGetValue(HuntModel.Territory.ZoneId, out var mapId))
                    {
                        var mapPayload = new Dalamud.Game.Text.SeStringHandling.Payloads.MapLinkPayload(
                            (uint)HuntModel.Territory.ZoneId, mapId, r.MapX, r.MapY
                        );
                        DalamudService.GameGui.OpenMapWithMapLink(mapPayload);
                    }
                    else
                    {
                        DalamudService.Log.Error("Data missing to generate map link");
                    }
                }
            }
        }
    }

    private void DrawKillCounts()
    {
        using var table = ImRaii.Table("KillCountsTable", 2);
        ImGui.TableSetupColumn("name", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("kills", ImGuiTableColumnFlags.WidthFixed);

        foreach (var r in HuntModel.KillCountLog)
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.Text($"{r.Key}");
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            using (ImRaii.PushColor(ImGuiCol.Text, _textColorKc))
            {
                ImGui.Text($"{r.Value.Killed} ～ {r.Value.Killed+r.Value.Missing}");
            }
        }
    }

    private void DrawConfig()
    {
        bool b;
        float f;

        b = Plugin.Configuration.SoundEnabled;
        if (ImGui.Checkbox("Enable Sound Alerts", ref b))
        {
            Plugin.Configuration.SoundEnabled = b;
            Plugin.Configuration.Save();
        }
        using (var soundIndent = ImRaii.PushIndent(24.0f))
        {
            f = Plugin.Configuration.SoundVolume * 100.0f;
            if (ImGui.SliderFloat("Volume", ref f, 0.0f, 100.0f, "%.0f%%"))
            {
                Plugin.Configuration.SoundVolume = f / 100.0f;
            }

            if (ImGui.IsItemDeactivatedAfterEdit())
            {
                Plugin.Notifications.PlaySfx("ping3.wav");
                Plugin.Configuration.Save();
            }

            b = Plugin.Configuration.SoundAlertFATE;
            if (ImGui.Checkbox("FATE Boss", ref b))
            {
                Plugin.Configuration.SoundAlertFATE = b;
                Plugin.Configuration.Save();
            }

            b = Plugin.Configuration.SoundAlertS;
            if (ImGui.Checkbox("S Rank", ref b))
            {
                Plugin.Configuration.SoundAlertS = b;
                Plugin.Configuration.Save();
            }

            ImGui.SameLine();
            b = Plugin.Configuration.SoundAlertA;
            if (ImGui.Checkbox("A Rank", ref b))
            {
                Plugin.Configuration.SoundAlertA = b;
                Plugin.Configuration.Save();
            }

            b = Plugin.Configuration.SoundAlertB;
            if (ImGui.Checkbox("B Rank", ref b))
            {
                Plugin.Configuration.SoundAlertB = b;
                Plugin.Configuration.Save();
            }

            ImGui.SameLine();
            b = Plugin.Configuration.SoundAlertMinions;
            if (ImGui.Checkbox("Minion", ref b))
            {
                Plugin.Configuration.SoundAlertMinions = b;
                Plugin.Configuration.Save();
            }
        }

        ImGui.Separator();

        b = Plugin.Configuration.ShowMissingKC;
        if (ImGui.Checkbox("Count missing KC mobs", ref b))
        {
            Plugin.Configuration.ShowMissingKC = b;
            Plugin.Configuration.Save();
        }

        ImGui.TextWrapped("Keeps track of mobs that are no longer visible to you. Can estimate a possible kill count range when you are not killing alone.");
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

    public void SwitchToConfigTab()
    {
        _forceOpenConfig = true;
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
        using (var tabItem = _forceOpenConfig ? ImRaii.TabItem("Config", ref _forceOpenConfig, ImGuiTabItemFlags.SetSelected) : ImRaii.TabItem("Config"))
        {
            _forceOpenConfig = false;
            if (tabItem.Success)
                DrawConfig();
        }
        using (var tabItem = ImRaii.TabItem("Debug"))
        {
            if (tabItem.Success)
                DrawDebug();
        }
    }
}
