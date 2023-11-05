using System.Threading.Tasks;

// Utility functions that interact with the game
public class GameFunctions
{
    public static Task<bool> OpenMapLink(int zoneId, float mapX, float mapY)
    {
        var data = GameData.GetZoneData(zoneId);
        return DalamudService.Framework.RunOnFrameworkThread<bool>(() => {
            var mapPayload = new Dalamud.Game.Text.SeStringHandling.Payloads.MapLinkPayload(
                (uint)CatScan.HuntModel.Territory.ZoneId, data.MapId, mapX, mapY
            );
            return DalamudService.GameGui.OpenMapWithMapLink(mapPayload);
        });
    }

    public static Task<bool> OpenMapLink(float mapX, float mapY)
    {
        return OpenMapLink(CatScan.HuntModel.Territory.ZoneId, mapX, mapY);
    }
}
