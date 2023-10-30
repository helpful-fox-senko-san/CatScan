// Utility functions that interact with the game
public class GameFunctions
{
    public static void DoMapLink(float mapX, float mapY)
    {
        var data = GameData.GetZoneData(CatScan.HuntModel.Territory.ZoneId);
        DalamudService.Framework.RunOnFrameworkThread(() => {
            var mapPayload = new Dalamud.Game.Text.SeStringHandling.Payloads.MapLinkPayload(
                (uint)CatScan.HuntModel.Territory.ZoneId, data.MapId, mapX, mapY
            );
            DalamudService.GameGui.OpenMapWithMapLink(mapPayload);
        });
    }
}
