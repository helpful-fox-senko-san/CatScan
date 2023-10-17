using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace CatScan.Ui;

public class ConfigWindow : Window, IDisposable
{
    public ConfigWindow() : base("CatScan Settings")
    {
        this.Size = new Vector2(300, 200);
        this.SizeCondition = ImGuiCond.Always;
    }

    public void Dispose() { }

    public override void Draw()
    {
    }
}
