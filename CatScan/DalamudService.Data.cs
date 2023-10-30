using System.Collections.Generic;
using Map = Lumina.Excel.GeneratedSheets.Map;
using TerritoryType = Lumina.Excel.GeneratedSheets.TerritoryType;

public partial class DalamudService
{
    public struct ZoneData
    {
        public string Name = string.Empty;
        public uint MapId = uint.MaxValue;
        public float MapOffsetX = 0.0f;
        public float MapOffsetY = 0.0f;
        public float MapScale = 1.0f;

        public ZoneData() { }
    }

    private static Dictionary<int, ZoneData> _zoneData = new();
    private static string? _cachedZoneName = null;
    private static int _cachedZoneId = -1;

    private static Lumina.Excel.ExcelSheet<TerritoryType> _territoryExcel = null!;
    private static Lumina.Excel.ExcelSheet<Map> _mapExcel = null!;

    private static ZoneData CacheZoneData(int zoneId)
    {
        var zoneData = new ZoneData();
        var territoryRow = _territoryExcel.GetRow((uint)zoneId);

        if (territoryRow != null)
        {
            zoneData.MapId = territoryRow.Map.Row;
        	zoneData.Name = territoryRow?.PlaceName?.Value?.Name?.ToString() ?? "#" + zoneId;

            var mapRow = _mapExcel?.GetRow(zoneData.MapId);

            if (mapRow != null)
            {
                zoneData.MapOffsetX = (mapRow?.OffsetX / -50.0f) ?? 0.0f;
                zoneData.MapOffsetY = (mapRow?.OffsetY / -50.0f) ?? 0.0f;
                zoneData.MapScale = (mapRow?.SizeFactor / 100.0f) ?? 100.0f;
            }
        }

        _zoneData.Add(zoneId, zoneData);
        return zoneData;
    }

    public static ZoneData GetZoneData(int zoneId)
    {
        if (_zoneData.TryGetValue(zoneId, out var data))
        {
            return data;
        }
        else
        {
            return CacheZoneData(zoneId);
        }
    }

    private static void InitTerritoryData()
    {
        _territoryExcel = DalamudService.DataManager.GetExcelSheet<TerritoryType>()!;
        _mapExcel = DalamudService.DataManager.GetExcelSheet<Map>()!;

        if (_territoryExcel == null)
            throw new System.Exception("Territory data not available");

        if (_mapExcel == null)
            throw new System.Exception("Map data not available");

		// Pre-load data for known zones
        foreach (var territoryId in CatScan.HuntData.Zones.Keys)
            CacheZoneData(territoryId);
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
