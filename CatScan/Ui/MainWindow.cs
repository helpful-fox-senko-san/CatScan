using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace CatScan.Ui;

public class MainWindow : Window, IDisposable
{
    public MainWindow() : base("CatScan",
        ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        this.SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(350, 350),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
    }

    public void Dispose()
    {
    }

    public override void Draw()
    {
        string instanceText = "";
        if (HuntModel.Territory.Instance != 0)
            instanceText = "i" + HuntModel.Territory.Instance;
        ImGui.Text($"Zone: {HuntModel.Territory.ZoneData.Name} {instanceText}");
        ImGui.Text($"World: {HuntModel.Territory.WorldName}");
        ImGui.Text("");
        ImGui.Text("Scan Results");
        ImGui.Text("---");
        foreach (var r in HuntModel.ScanResults.Values)
        {
            ImGui.Text($"[{r.Rank}] {r.Name} - HP:{r.HpPct:F1}%% - Pos:{r.MapX:F1},{r.MapY:F1}{(!r.InRange?" MISSING":"")}{(r.Dead?" DEAD":"")}");
        }
        ImGui.Text("");
        if (HuntModel.KillCountLog.Count > 0)
        {
            ImGui.Text("Kill Counts");
            ImGui.Text("---");
            foreach (var r in HuntModel.KillCountLog)
            {
                ImGui.Text($"{r.Key} - {r.Value.Killed} killed + {r.Value.Missing} missing");
            }
        }
    }
}
