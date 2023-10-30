using Dalamud.Interface.Windowing;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Internal; // for IDalamudTextureWrap
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;

namespace CatScan.Ui;

public partial class MainWindow : Window, IDisposable
{
    private IDalamudTextureWrap? _iconB;
    private IDalamudTextureWrap? _iconA;
    private IDalamudTextureWrap? _iconS;
    private IDalamudTextureWrap? _iconF;
    private IDalamudTextureWrap? _iconStar;

    private Vector4 _textColorPulled = RGB(224, 96, 96);
    private Vector4 _textColorDead = RGB(160, 96, 96);
    private Vector4 _textColorGone = RGB(160, 160, 160);

    private System.DateTime _hideDeadCutoffEurekaUtc = System.DateTime.MinValue;

    private void InitScanResults()
    {
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

        DalamudService.PluginInterface.UiBuilder.LoadImageAsync(Path.Combine(_resourcePath, "Star.png")).ContinueWith(icon => {
            _iconStar = icon.Result;
        });
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

        // Display Eureka NMs / Bozja star mobs using a star icon
        if (HuntModel.Territory.ZoneData.Expansion == Expansion.Eureka
         || HuntModel.Territory.ZoneData.Expansion == Expansion.Bozja)
            icon = _iconStar;

        if (icon != null)
            ImGui.Image(icon.ImGuiHandle, new(24, 24));
        else
            ImGui.Text("");
    }

    private void DrawEpicFateBar()
    {
        var epicFateList = new List<ActiveFate>(HuntModel.ActiveFates.Values);
        epicFateList.RemoveAll((x) => !x.Epic);

        epicFateList.Sort((ActiveFate a, ActiveFate b) => {
            return (int)(a.EndTimeUtc - b.EndTimeUtc).TotalSeconds;
        });

        // We only expect one interesting fate to ever be active in a zone at the time
        if (epicFateList.Count > 0)
        {
            using var pushFateTableBorderColor1 = ImRaii.PushColor(ImGuiCol.TableBorderLight, RGB(96, 16, 96));
            using var pushFateTableBorderColor2 = ImRaii.PushColor(ImGuiCol.TableBorderStrong, RGB(96, 16, 96));
            using var pushFateTableBgColor = ImRaii.PushColor(ImGuiCol.TableRowBg, RGB(96, 16, 96));
            using var pushFateTableAltBgColor = ImRaii.PushColor(ImGuiCol.TableRowBgAlt, RGB(112, 20, 112));
            using var pushColorFate1 = ImRaii.PushColor(ImGuiCol.HeaderHovered, RGB(128, 24, 128));
            using var pushColorFate2 = ImRaii.PushColor(ImGuiCol.HeaderActive, RGB(128, 24, 128));
            using var fateTable = ImRaii.Table("FateTable", 1, ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders);
            ImGui.TableSetupColumn("fate", ImGuiTableColumnFlags.WidthStretch);
            foreach (var f in epicFateList)
            {
                var str = f.Name;
                var timeRemaining = f.TimeRemaining;
                if (f.Running)
                {
                    if (timeRemaining <= TimeSpan.Zero)
                        timeRemaining = TimeSpan.Zero;

                    var mins = System.Math.Floor(timeRemaining.TotalMinutes).ToString("0");
                    var secs = timeRemaining.Seconds.ToString("00");
                    str += $" ({mins}:{secs})";
                }
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                if (ImGui.Selectable("##clickableFate:" + f.Name, false, ImGuiSelectableFlags.AllowItemOverlap))
                    GameFunctions.DoMapLink(f.MapX, f.MapY);
                ImGui.SameLine();
                ImGuiHelpers.CenteredText(str);
            }
        }
    }

    private void DrawScanResults()
    {
        using var tabId = ImRaii.PushId("ScanResults");

        if (HuntModel.ScanResults.Count == 0 && HuntModel.ActiveFates.Count == 0 && !_gameScanner.ScanningEnabled)
        {
            {
                using var pushColor = ImRaii.PushColor(ImGuiCol.Text, _textColorDead);
                ImGui.Text("");
                ImGuiHelpers.CenteredText("Scanner disabled in this zone.");
                ImGui.Text("");
            }
            ImGuiHelpers.CenterCursorForText("Force Enable Scanner");
            if (ImGui.Button("Force Enable Scanner"))
                _gameScanner.EnableScanning();
            return;
        }

        DrawEpicFateBar();

        using var table = ImRaii.Table("ScanResultsTable", 2);
        ImGui.TableSetupColumn("icon", ImGuiTableColumnFlags.WidthFixed);
        ImGui.TableSetupColumn("name", ImGuiTableColumnFlags.WidthStretch);

        var eureka = (HuntModel.Territory.ZoneData.Expansion == Expansion.Eureka);
        var bozja = (HuntModel.Territory.ZoneData.Expansion == Expansion.Bozja);

        var resultList = new List<ScanResult>(HuntModel.ScanResults.Values);

        resultList.Sort((ScanResult a, ScanResult b) => {
            if (a.Rank == b.Rank)
            {
                if (a.Dead == b.Dead || !eureka)
                    return a.Name.CompareTo(b.Name);
                else
                    return a.Dead ? 1 : -1;
            }
            else
                return b.Rank - a.Rank;
        });

        int numClearable = 0;

        foreach (var r in resultList)
        {
            // Eureka NMs stack up fast and they're not too useful to list long term in their current state
            // Provide a feature to clear them -- note that Missing is effectively Dead for NMs
            if ((r.Dead || r.Missing) && eureka)
            {
                if (r.LastSeenTimeUtc < _hideDeadCutoffEurekaUtc)
                    continue;
                else
                    ++numClearable;
            }

            // Likewise star ranks aren't really worth tracking at all, especially because they never move
            if (r.Missing && bozja)
                continue;

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

            using var pushColor1 = ImRaii.PushColor(ImGuiCol.Text, color, color != Vector4.Zero);
            // The default hover colors are too intense
            using var pushColor2 = ImRaii.PushColor(ImGuiCol.HeaderHovered, RGB(48, 48, 48));
            using var pushColor3 = ImRaii.PushColor(ImGuiCol.HeaderActive, RGB(64, 64, 64));
            if (ImGui.Selectable($"{r.Name} ( {r.MapX:F1} , {r.MapY:F1} ) HP: {r.HpPct:F1}%"))
                GameFunctions.DoMapLink(r.MapX, r.MapY);
        }

        if (eureka && numClearable > 0)
        {
            ImGuiHelpers.CenterCursorForText("Clear Dead NMs");
            if (ImGui.Button("Clear Dead NMs"))
                _hideDeadCutoffEurekaUtc = HuntModel.UtcNow;
        }
    }
}
