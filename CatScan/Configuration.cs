using Dalamud.Configuration;
using System;

namespace CatScan;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public bool SoundEnabled { get; set; } = true;
    public float SoundVolume { get; set; } = 0.6f;

    public bool SoundAlertFATE { get; set; } = true;
    public bool SoundAlertS { get; set; } = true;
    public bool SoundAlertA { get; set; } = true;
    public bool SoundAlertB { get; set; } = true;
    public bool SoundAlertMinions { get; set; } = true;

    public bool ShowMissingKC { get; set; } = false;

    public void Save()
    {
        DalamudService.PluginInterface.SavePluginConfig(this);
    }
}
