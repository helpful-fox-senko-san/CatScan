using Dalamud.Game; // ISigScanner
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

public class DalamudService
{
    [PluginService] public static IDalamudPluginInterface PluginInterface { get; set; } = null!;
    [PluginService] public static IClientState ClientState { get; private set; } = null!;
    [PluginService] public static ICondition Condition { get; private set; } = null!;
    [PluginService] public static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] public static IDataManager DataManager { get; private set; } = null!;
    [PluginService] public static IFateTable FateTable { get; private set; } = null!;
    [PluginService] public static IFramework Framework { get; private set; } = null!;
    [PluginService] public static IGameGui GameGui { get; private set; } = null!;
    [PluginService] public static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] public static IPluginLog Log { get; private set; } = null!;
    [PluginService] public static ISigScanner SigScanner { get; private set; } = null!;
    [PluginService] public static ITextureProvider TextureProvider { get; private set; } = null!;

    public static uint ClientLanguage = 0;
    public static string GameVersion = string.Empty;

    public static void Initialize(IDalamudPluginInterface pluginInterface)
    {
        pluginInterface.Create<DalamudService>();
        unsafe
        {
            var framework = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework.Instance();
            ClientLanguage = framework->ClientLanguage;
            GameVersion = framework->GameVersionString;
        }
    }
}
