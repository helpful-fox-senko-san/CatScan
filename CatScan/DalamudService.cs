using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using System.Collections.Generic;

using TerritoryType = Lumina.Excel.GeneratedSheets.TerritoryType;

public class DalamudService
{
    [PluginService] public static DalamudPluginInterface PluginInterface { get; set; } = null!;
    [PluginService] public static IClientState ClientState { get; private set; } = null!;
    [PluginService] public static ICondition Condition { get; private set; } = null!;
    [PluginService] public static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] public static IDataManager DataManager { get; private set; } = null!;
    [PluginService] public static IFateTable FateTable { get; private set; } = null!;
    [PluginService] public static IFramework Framework { get; private set; } = null!;
    [PluginService] public static IGameGui GameGui { get; private set; } = null!;
    [PluginService] public static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] public static IPluginLog Log { get; private set; } = null!;

    public static void Initialize(DalamudPluginInterface pluginInterface)
    {
        pluginInterface.Create<DalamudService>();

        InitTerritoryData();
    }

    private static Dictionary<int, uint> _territoryToMapId = new();
    private static string? _cachedZoneName = null;
    private static int _cachedZoneId = -1;

    private static void InitTerritoryData()
    {
        var territoryData = DalamudService.DataManager.GetExcelSheet<TerritoryType>();

        if (territoryData != null)
        {
            foreach (var z in CatScan.HuntData.Zones)
            {
                var row = territoryData.GetRow((uint)z.Key);

                if (row != null)
                    _territoryToMapId.Add(z.Key, row.Map.Row);
            }
        }
    }

    public static void DoMapLink(float mapX, float mapY)
    {
        if (_territoryToMapId.TryGetValue(CatScan.HuntModel.Territory.ZoneId, out var mapId))
        {
            DalamudService.Framework.RunOnFrameworkThread(() => {
                var mapPayload = new Dalamud.Game.Text.SeStringHandling.Payloads.MapLinkPayload(
                    (uint)CatScan.HuntModel.Territory.ZoneId, mapId, mapX, mapY
                );
                DalamudService.GameGui.OpenMapWithMapLink(mapPayload);
            });
        }
        else
        {
            DalamudService.Log.Error("Data missing to generate map link");
        }
    }

    public static string? GetZoneName(int zoneId)
    {
        if (zoneId == _cachedZoneId)
            return _cachedZoneName;
        var territoryData = DalamudService.DataManager.GetExcelSheet<TerritoryType>();
        var territoryType = territoryData?.GetRow((uint)zoneId);
        _cachedZoneName = territoryType?.PlaceName?.Value?.Name?.ToString();
        return _cachedZoneName;
    }
}
