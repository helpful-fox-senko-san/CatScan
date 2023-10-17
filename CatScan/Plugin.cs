using Dalamud.Plugin;

namespace CatScan;

public sealed class Plugin : IDalamudPlugin
{
    private CatScanPlugin catScanPlugin;

    public Plugin(DalamudPluginInterface pluginInterface)
    {
        DalamudService.Initialize(pluginInterface);
        catScanPlugin = new CatScanPlugin();
    }

    public void Dispose()
    {
        catScanPlugin.Dispose();
    }
}
