using Dalamud.Interface.Windowing;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace CatScan.Ui;

public partial class MainWindow : Window, IDisposable
{
    private void DrawFateTable()
    {
        using var tabId = ImRaii.PushId("Fates");

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

        var fateList = new List<ActiveFate>(HuntModel.ActiveFates.Values);

        fateList.Sort((ActiveFate a, ActiveFate b) => {
            if (a.Running && !b.Running)
                return -1;
            else if (b.Running && !a.Running)
                return 1;
            else
                return (int)(a.EndTimeUtc - b.EndTimeUtc).TotalSeconds;
        });

        var playerPos = DalamudService.ClientState.LocalPlayer?.Position ?? Vector3.Zero;

        using var table = ImRaii.Table("FateTable", 4, ImGuiTableFlags.Reorderable | ImGuiTableFlags.Resizable | ImGuiTableFlags.Borders | ImGuiTableFlags.Sortable | ImGuiTableFlags.Hideable);
        ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch | ImGuiTableColumnFlags.NoHide);
        ImGui.TableSetupColumn("%", ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.DefaultHide);
        ImGui.TableSetupColumn("Dist.", ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.DefaultHide);
        ImGui.TableSetupColumn("Time", ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.DefaultSort);
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.TableHeader("Name");
        ImGui.TableNextColumn();
        ImGui.TableHeader("%");
        if (ImGui.IsItemHovered())
        {
            using var tt = ImRaii.Tooltip();
            ImGui.Text("Progress %%");
        }
        ImGui.TableNextColumn();
        ImGui.TableHeader("Dist.");
        ImGui.TableNextColumn();
        ImGui.TableHeader("Time");
        var calcDist2 = (ActiveFate f) => {
            var dx = System.Math.Abs(playerPos.X - f.RawX);
            var dz = System.Math.Abs(playerPos.Z - f.RawZ);
            return dx*dx + dz*dz;
        };
        var tableSortSpecs = ImGui.TableGetSortSpecs();
        if (tableSortSpecs.Specs.ColumnIndex == 0)
        {
            if (tableSortSpecs.Specs.SortDirection != ImGuiSortDirection.Descending)
                fateList.Sort((ActiveFate a, ActiveFate b) => a.Name.CompareTo(b.Name));
            else
                fateList.Sort((ActiveFate a, ActiveFate b) => b.Name.CompareTo(a.Name));
        }
        else if (tableSortSpecs.Specs.ColumnIndex == 1)
        {
            if (tableSortSpecs.Specs.SortDirection != ImGuiSortDirection.Descending)
                fateList.Sort((ActiveFate a, ActiveFate b) => a.ProgressPct.CompareTo(b.ProgressPct));
            else
                fateList.Sort((ActiveFate a, ActiveFate b) => b.ProgressPct.CompareTo(a.ProgressPct));

        }
        else if (tableSortSpecs.Specs.ColumnIndex == 2)
        {
            if (tableSortSpecs.Specs.SortDirection != ImGuiSortDirection.Descending)
                fateList.Sort((ActiveFate a, ActiveFate b) => calcDist2(a).CompareTo(calcDist2(b)));
            else
                fateList.Sort((ActiveFate a, ActiveFate b) => calcDist2(b).CompareTo(calcDist2(a)));

        }
        else if (tableSortSpecs.Specs.ColumnIndex == 3)
        {
            if (tableSortSpecs.Specs.SortDirection == ImGuiSortDirection.Descending)
                fateList.Reverse();
        }

        bool pctVisible = ImGui.TableGetColumnFlags(1).HasFlag(ImGuiTableColumnFlags.IsVisible);
        foreach (var f in fateList)
        {
            var str = f.Name;
            var timeRemaining = f.TimeRemaining;
            ImGui.TableNextRow();
            ImGui.TableNextColumn();

            Vector4 color = Vector4.Zero;

            if (f.Running && timeRemaining <= TimeSpan.Zero)
                color = _textColorDead;
            else if (f.ProgressPct > 0.0)
                color = _textColorPulled;

            using var pushColor1 = ImRaii.PushColor(ImGuiCol.Text, color, color != Vector4.Zero);
            // The default hover colors are too intense
            using var pushColor2 = ImRaii.PushColor(ImGuiCol.HeaderHovered, RGB(48, 48, 48));
            using var pushColor3 = ImRaii.PushColor(ImGuiCol.HeaderActive, RGB(64, 64, 64));

            if (ImGui.Selectable("##clickableFate:" + f.Name, false, ImGuiSelectableFlags.AllowItemOverlap))
                GameFunctions.OpenMapLink(f.MapX, f.MapY);
            if (f.IsCE)
            {
                ImGui.SameLine();
                if (_iconCE != null)
                    ImGui.Image(_iconCE.ImGuiHandle, new Vector2(16.0f, 16.0f));
                else
                    ImGui.Text("[CE]");
            }
            ImGui.SameLine();
            ImGui.Text(f.Name);

            if (pctVisible)
                ImGui.TableNextColumn();

            if (f.ProgressPct > 0.0f)
            {
                if (pctVisible)
                {
                    ImGui.Text(f.ProgressPct.ToString("0.\\%\\%"));
                }
                else
                {
                    ImGui.SameLine();
                    ImGui.Text(f.ProgressPct.ToString("\\(0.\\%\\%\\)"));
                }
            }
            else
            {
                if (pctVisible)
                    ImGui.Text("0%%");
            }

            if (!pctVisible)
                ImGui.TableNextColumn();

            // XXX: This isn't exactly correct and doesn't factor in height but noone will notice right
            var dist = System.Math.Sqrt(calcDist2(f));
            ImGui.TableNextColumn();
            {
                var cappedDist = (float)System.Math.Clamp((dist - 32.0) / 2.0, 32.0, 224.0);
                using var pushDistColor = ImRaii.PushColor(ImGuiCol.Text, RGB(31.0f + cappedDist, 255.0f, 31.0f + cappedDist));
                ImGui.Text(dist.ToString("0.y"));
            }

            ImGui.TableNextColumn();
            if (f.Running)
            {
                // The fate has ended so it should not be visible anymore
                if (timeRemaining <= TimeSpan.Zero)
                    timeRemaining = TimeSpan.Zero;

                {
                    var cappedTime = (float)System.Math.Clamp((900.0 - timeRemaining.TotalSeconds) / 4.0, 32.0, 224.0);
                    using var pushTimeColor = ImRaii.PushColor(ImGuiCol.Text, RGB(cappedTime, 255.0f - cappedTime, 32.0f));

                    var mins = System.Math.Floor(timeRemaining.TotalMinutes).ToString("0");
                    var secs = timeRemaining.Seconds.ToString("00");
                    ImGui.Text($"{mins}:{secs}");
                }
            }
            else
            {
                // XXX: This timer will not be accurate when first entering a zone
                //      Need a way to track which fates were initially active
                var span = f.FirstSeenAgo;
                var mins = System.Math.Floor(span.TotalMinutes).ToString("0");
                var secs = span.Seconds.ToString("00");
                using var pushTimeColor = ImRaii.PushColor(ImGuiCol.Text, _textColorGone);
                ImGui.Text($"({mins}:{secs})");
            }
        }
    }

    private void DrawFates()
    {
        DrawFateTable();

        // Fate fail timer for Southern Thanalan
        if (HuntModel.Territory.ZoneId == 146)
        {
            var span = HuntModel.UtcNow - HuntModel.LastFailedFateUtc;
            span = TimeSpan.FromSeconds(System.Math.Floor(span.TotalSeconds));
            ImGuiHelpers.CenteredText(span.ToString());

            if (HuntModel.LastFailedFateName.Length > 0)
            {
                var tw1 = (int)ImGui.CalcTextSize("Failed: " + HuntModel.LastFailedFateName).X;
                var tw2 = (int)ImGui.CalcTextSize("Dismiss").X;
                var ww = (int)ImGui.GetWindowWidth();

                {
                    using var pushColor = ImRaii.PushColor(ImGuiCol.Text, _textColorDead);
                    ImGuiHelpers.CenterCursorFor(tw1);
                    ImGui.Text("Failed: " + HuntModel.LastFailedFateName);
                }

                if ((ww - tw1) / 2 < tw2 + 24)
                    ImGuiHelpers.CenterCursorFor(tw2);
                else
                    ImGui.SameLine();

                if (ImGui.SmallButton("Dismiss##DismissFateFailed"))
                    HuntModel.LastFailedFateName = string.Empty;
            }
        }
    }
}
