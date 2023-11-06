using Dalamud.Interface.Windowing;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
using System;
using System.Numerics;

namespace CatScan.Ui;

public partial class MainWindow : Window, IDisposable
{
    private Vector4 _textColorKc = RGB(160, 192, 224);

    private void DrawKillCounts()
    {
        using var tabId = ImRaii.PushId("KillCounts");

        if (HuntModel.KillCountLog.Count == 0)
        {
            ImGui.Text("");
            using var pushColor = ImRaii.PushColor(ImGuiCol.Text, _textColorDead);
            ImGuiHelpers.CenteredText("No kill count in this zone.");
            return;
        }

        using var pushCellPadding = ImRaii.PushStyle(ImGuiStyleVar.CellPadding, new System.Numerics.Vector2(2.0f, 0.0f), HuntModel.KillCountLog.Count > 4);
        using var table = ImRaii.Table("KillCountsTable", 2);
        ImGui.TableSetupColumn("name", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("kills", ImGuiTableColumnFlags.WidthFixed);

        var eureka = (HuntModel.Territory.ZoneData.Expansion == Expansion.Eureka);
        var bozja = (HuntModel.Territory.ZoneId == 920);
        var zadnor = (HuntModel.Territory.ZoneId == 975);

        int rowNum = 1;

        foreach (var r in HuntModel.KillCountLog)
        {
            // XXX: Hide zero-kill KC mobs in eureka
            if (eureka && r.Killed == 0)
                continue;

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted($"{r.Name}");
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            using (ImRaii.PushColor(ImGuiCol.Text, _textColorKc))
            {
                if (Plugin.Configuration.ShowMissingKC)
                    ImGui.Text($" {r.Killed} ～ {r.Killed+r.Missing} ");
                else
                    ImGui.Text($" {r.Killed} ");
            }

            if ((bozja && (rowNum == 4 || rowNum == 7))
             || (zadnor && (rowNum == 4 || rowNum == 8)))
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.Separator();
                ImGui.TableNextColumn();
                ImGui.Separator();
            }

            ++rowNum;
        }

        table.Dispose();

        if (eureka && rowNum > 1)
        {
            ImGuiHelpers.CenterCursorForText("Clear Log");
            if (ImGui.Button("Clear Log"))
            {
                foreach (var r in HuntModel.KillCountLog)
                    r.Killed = 0;
            }
        }
    }
}
