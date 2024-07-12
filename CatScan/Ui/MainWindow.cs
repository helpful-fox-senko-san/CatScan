using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
using System;
using System.IO;
using System.Numerics;

namespace CatScan.Ui;

public partial class MainWindow : Window, IDisposable
{
    public enum Tabs
    {
        ScanResults,
        TrainLog,
        Fates,
        KillCount
    }

    // for debugging
    private GameScanner _gameScanner;
    private string _resourcePath = "";

    private static Vector4 RGB(float r, float g, float b)
    {
        return new Vector4(r / 255.0f, g / 255.0f, b / 255.0f, 255.0f);
    }

    private Tabs? _forceOpenTab;
    private bool _showFieldOpsTabs = false;

    // Set to true when closed automatically
    // If set, then the window is re-opened automatically too
    public bool AutoClosed = false;

    public MainWindow(GameScanner gameScanner) : base("CatScan###CatScan")
    {
        _gameScanner = gameScanner;
        this.SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(300, 150),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        _resourcePath = Path.Combine(DalamudService.PluginInterface.AssemblyLocation.Directory?.FullName!, "Resources");

        InitScanResults();
        InitTrainLog();
        UpdateZoneName();

        TitleBarButtons.Add(new(){
            Icon = FontAwesomeIcon.Cog,
            IconOffset = new(2, 1),
            Click = (mb) => { if (mb == ImGuiMouseButton.Left) Plugin.OpenConfigUi(); }
        });
    }

    public void Dispose()
    {
    }

    public void OpenTab(Tabs? tab)
    {
        if (Collapsed.GetValueOrDefault(false))
            Collapsed = false;

        if (IsOpen)
            BringToFront();
        else
            IsOpen = true;

        _forceOpenTab = tab;
    }

    public void UpdateZoneName()
    {
        string instanceText = string.Empty;
        if (HuntModel.Territory.Instance > 0)
            instanceText = " i" + HuntModel.Territory.Instance;

        int zoneId = HuntModel.Territory.ZoneId;
        string zoneName = GameData.GetZoneData(zoneId).Name;

        this.WindowName = $"CatScan - {zoneName}{instanceText}###CatScan";
    }

    public void DrawMainWindow()
    {
        var doTab = (string name, Tabs tabId, Action drawfn) => {
            bool forceOpenFlag = (_forceOpenTab == tabId);

            using var tabItem = forceOpenFlag ? ImRaii.TabItem(name, ref forceOpenFlag, ImGuiTabItemFlags.SetSelected) : ImRaii.TabItem(name);

            if (tabItem.Success)
                drawfn();

            if (forceOpenFlag)
                _forceOpenTab = null;
        };

        if (_showFieldOpsTabs)
        {
            DrawEurekaTracker();
        }
        else
        {
            using var tabs = ImRaii.TabBar("MainWindowTabs");
            doTab("Scan Results##PrimaryTab", Tabs.ScanResults, DrawScanResults);
            doTab("Train Log", Tabs.TrainLog, DrawTrainLog);
            doTab("Fates", Tabs.Fates, DrawFates);
            if (KillCountAvailable())
                doTab("Kill Count", Tabs.KillCount, DrawKillCounts);
        }
    }

    public override void Draw()
    {
        try
        {
            // Enable field ops mode when enabled and in a Eureka zone
            bool fieldOpsEligible = Plugin.Configuration.SpecialFieldOps && HuntData.IsEurekaZone(HuntModel.Territory.ZoneId);

            if (fieldOpsEligible && !_showFieldOpsTabs)
            {
                _showFieldOpsTabs = true;
            }
            else if (!fieldOpsEligible && _showFieldOpsTabs)
            {
                _showFieldOpsTabs = false;
            }

            DrawMainWindow();
        }
        catch (Exception ex)
        {
            DalamudService.Log.Error(ex, "Draw");
        }
    }

    public override void OnOpen()
    {
        // Do not automatically re-open if the window was manually opened and closed
        AutoClosed = false;
    }
}
