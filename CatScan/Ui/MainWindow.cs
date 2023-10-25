using Dalamud.Interface.Windowing;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;

using TerritoryType = Lumina.Excel.GeneratedSheets.TerritoryType;

namespace CatScan.Ui;

public partial class MainWindow : Window, IDisposable
{
    // for debugging
    private GameScanner _gameScanner;
    private string _resourcePath = "";

    private static Vector4 RGB(float r, float g, float b)
    {
        return new Vector4(r / 255.0f, g / 255.0f, b / 255.0f, 255.0f);
    }

    private Dictionary<int, uint> _territoryToMapId = new();

    private bool _forceOpenConfig = false;

    private string? _cachedZoneName = null;
    private int _cachedZoneId = -1;

    public MainWindow(GameScanner gameScanner) : base("CatScan")
    {
        _gameScanner = gameScanner;
        this.SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(300, 300),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        var territoryData = DalamudService.DataManager.GetExcelSheet<TerritoryType>();

        if (territoryData != null)
        {
            foreach (var z in HuntData.Zones)
            {
                var row = territoryData.GetRow((uint)z.Key);

                if (row != null)
                    _territoryToMapId.Add(z.Key, row.Map.Row);
            }
        }

        _resourcePath = Path.Combine(DalamudService.PluginInterface.AssemblyLocation.Directory?.FullName!, "Resources");

        InitScanResults();
    }

    public void Dispose()
    {
    }

    private void DoMapLink(float mapX, float mapY)
    {
        if (_territoryToMapId.TryGetValue(HuntModel.Territory.ZoneId, out var mapId))
        {
            DalamudService.Framework.RunOnFrameworkThread(() => {
                var mapPayload = new Dalamud.Game.Text.SeStringHandling.Payloads.MapLinkPayload(
                    (uint)HuntModel.Territory.ZoneId, mapId, mapX, mapY
                );
                DalamudService.GameGui.OpenMapWithMapLink(mapPayload);
            });
        }
        else
        {
            DalamudService.Log.Error("Data missing to generate map link");
        }
    }

    private string? GetLuminaZoneName(int zoneId)
    {
        if (zoneId == _cachedZoneId)
            return _cachedZoneName;
        var territoryData = DalamudService.DataManager.GetExcelSheet<TerritoryType>();
        var territoryType = territoryData?.GetRow((uint)zoneId);
        _cachedZoneName = territoryType?.PlaceName?.Value?.Name?.ToString();
        return _cachedZoneName;
    }

    public void SwitchToConfigTab()
    {
        _forceOpenConfig = true;
    }

    public override void Draw()
    {
        string instanceText = "";
        if (HuntModel.Territory.Instance > 0)
            instanceText = " i" + HuntModel.Territory.Instance;
        // FIXME: There should not be a null dereference here...
        string zoneName = HuntModel.Territory.ZoneData.Name ?? string.Empty;
        if (zoneName.Length > 0 && zoneName.Substring(0, 1) == "#")
        {
            // May as well make the window useful while its visible in unknown zones
            string? luminaZoneName = GetLuminaZoneName(HuntModel.Territory.ZoneId);
            if (luminaZoneName != null)
                zoneName = luminaZoneName;
            else
                zoneName = $"Zone {zoneName}";
        }
        ImGuiHelpers.CenteredText($"{zoneName}{instanceText}");

        using var tabs = ImRaii.TabBar("MainWindowTabs");
        using (var tabItem = ImRaii.TabItem("Scan Results"))
        {
            if (tabItem.Success)
                DrawScanResults();
        }
        using (var tabItem = ImRaii.TabItem("Kill Count"))
        {
            if (tabItem.Success)
                DrawKillCounts();
        }
        using (var tabItem = _forceOpenConfig ? ImRaii.TabItem("Config", ref _forceOpenConfig, ImGuiTabItemFlags.SetSelected) : ImRaii.TabItem("Config"))
        {
            _forceOpenConfig = false;
            if (tabItem.Success)
                DrawConfig();
        }
#if DEBUG
        using (var tabItem = ImRaii.TabItem("Debug"))
        {
            if (tabItem.Success)
                DrawDebug();
        }
#endif
    }
}
