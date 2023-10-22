using Dalamud.Configuration;
using System;

namespace CatScan;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public void Save()
    {
        DalamudService.PluginInterface.SavePluginConfig(this);
    }
}
