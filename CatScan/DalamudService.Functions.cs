public partial class DalamudService
{
    public static void DoMapLink(float mapX, float mapY)
    {
        var data = GetZoneData(CatScan.HuntModel.Territory.ZoneId);
		DalamudService.Framework.RunOnFrameworkThread(() => {
			var mapPayload = new Dalamud.Game.Text.SeStringHandling.Payloads.MapLinkPayload(
				(uint)CatScan.HuntModel.Territory.ZoneId, data.MapId, mapX, mapY
			);
			DalamudService.GameGui.OpenMapWithMapLink(mapPayload);
		});
    }
}
