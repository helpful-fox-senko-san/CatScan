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

        using var table = ImRaii.Table("KillCountsTable", 2);
        ImGui.TableSetupColumn("name", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("kills", ImGuiTableColumnFlags.WidthFixed);

        var eureka = (HuntModel.Territory.ZoneData.Expansion == Expansion.Eureka);

        foreach (var r in HuntModel.KillCountLog)
        {
            // XXX: Hide zero-kill KC mobs in eureka
            if (eureka && r.Value.Killed == 0)
                continue;

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.Text($"{r.Key}");
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            using (ImRaii.PushColor(ImGuiCol.Text, _textColorKc))
            {
                if (Plugin.Configuration.ShowMissingKC)
                    ImGui.Text($" {r.Value.Killed} ～ {r.Value.Killed+r.Value.Missing} ");
                else
                    ImGui.Text($" {r.Value.Killed} ");
            }
        }
    }
}
