using NAudio.Wave;
using System.Collections.Generic;
using System.IO;

namespace CatScan;

public class Notifications
{
    private HuntScanner _huntScanner;
    private string _resourcePath = "";

    private class CachedSample
    {
        public byte[] Data;
        public WaveFormat Format;

        public CachedSample(byte[] data, WaveFormat format)
        {
            Data = data;
            Format = format;
        }

        public WaveStream MakeStream()
        {
            return new RawSourceWaveStream(Data, 0, Data.Length, Format);
        }
    };

    private Dictionary<string, CachedSample> _cachedWavFiles = new();

    // XXX: Low quality hack to try avoid pinging Coeurlregina during the Long Live the Coeurls fate
    private bool _coeurlHackFlag = false;

    private CachedSample PreloadSfx(string filename)
    {
        using var waveStream = new WaveFileReader(Path.Combine(_resourcePath, filename));
        using var ms = new MemoryStream();
        waveStream.CopyTo(ms);
        ms.Position = 0;
        var result = new CachedSample(ms.GetBuffer(), waveStream.WaveFormat);
        _cachedWavFiles.Add(filename, result);
        return result;
    }

    public Notifications(HuntScanner huntScanner)
    {
        _huntScanner = huntScanner;
        _huntScanner.NewScanResult += OnNewScanResult;
        _huntScanner.NewFate += OnNewFate;
        _huntScanner.ZoneChange += OnZoneChange;
        _resourcePath = Path.Combine(DalamudService.PluginInterface.AssemblyLocation.Directory?.FullName!, "Resources");

        PreloadSfx("ping1.wav");
        PreloadSfx("ping2.wav");
        PreloadSfx("ping3.wav");
    }

    public void PlaySfx(string filename)
    {
        if (!_cachedWavFiles.TryGetValue(filename, out var cachedSample))
            cachedSample = PreloadSfx(filename);

        if (cachedSample == null)
        {
            DalamudService.Log.Error($"Missing {filename}");
            return;
        }

        var waveStream = cachedSample.MakeStream();
        var waveChannel = new WaveChannel32(waveStream, Plugin.Configuration.SoundVolume, 1.0f){
            PadWithZeroes = false,
        };
        var waveOut = new WaveOutEvent();
        waveOut.Init(waveChannel);
        waveOut.Play();
        waveOut.PlaybackStopped += (object? sender, StoppedEventArgs args) => {
            waveChannel.Dispose();
            waveOut.Dispose();
        };
    }

    private void HandleAutoOpen(Rank rank, float mapX, float mapY)
    {
        if (Plugin.Configuration.AutoOpenEnabled)
        {
            if (Plugin.Configuration.AutoOpenS && rank == Rank.S)
                Plugin.MainWindow.OpenTab(Ui.MainWindow.Tabs.ScanResults);
            else if (Plugin.Configuration.AutoOpenFATE && rank == Rank.FATE)
                Plugin.MainWindow.OpenTab(Ui.MainWindow.Tabs.ScanResults);
        }

        if (Plugin.Configuration.AutoFlagEnabled)
        {
            if (Plugin.Configuration.AutoFlagS && rank == Rank.S)
                DalamudService.DoMapLink(mapX, mapY);
            else if (Plugin.Configuration.AutoFlagFATE && rank == Rank.FATE)
                DalamudService.DoMapLink(mapX, mapY);
        }
    }

    public void OnNewScanResult(ScanResult scanResult)
    {
        string? sfx = null;

        // These are handled by OnNewEpicFate instead
        if (scanResult.Name == "Tristitia")
            return;

        if (scanResult.Name == "Coeurlregina" && _coeurlHackFlag)
        {
            _coeurlHackFlag = false;
            return;
        }

        if (scanResult.Rank == Rank.FATE && Plugin.Configuration.SoundAlertFATE)
            sfx = "ping3.wav";
        else if (scanResult.Rank == Rank.SS && Plugin.Configuration.SoundAlertS)
            sfx = "ping3.wav";
        else if (scanResult.Rank == Rank.S && Plugin.Configuration.SoundAlertS)
            sfx = "ping3.wav";
        else if (scanResult.Rank == Rank.A && Plugin.Configuration.SoundAlertA)
            sfx = "ping2.wav";
        else if (scanResult.Rank == Rank.B && Plugin.Configuration.SoundAlertB)
            sfx = "ping1.wav";
        else if (scanResult.Rank == Rank.Minion && Plugin.Configuration.SoundAlertMinions)
            sfx = "ping1.wav";

        if (Plugin.Configuration.SoundEnabled && sfx != null)
            PlaySfx(sfx);

        HandleAutoOpen(scanResult.Rank, scanResult.MapX, scanResult.MapY);
    }

    public void OnNewFate(ActiveFate fate)
    {
        _coeurlHackFlag = false;

        // Notify for fates that don't spawn with the boss initially present
        if (fate.Name == "Long Live the Coeurl")
        {
            if (Plugin.Configuration.SoundEnabled && Plugin.Configuration.SoundAlertFATE)
                Plugin.Notifications.PlaySfx("ping3.wav");
            _coeurlHackFlag = true;
            HandleAutoOpen(Rank.FATE, fate.MapX, fate.MapY);
        }

        if (fate.Name == "The Baldesion Arsenal: Expedition Support")
        {
            if (Plugin.Configuration.SoundEnabled && Plugin.Configuration.SoundAlertS)
                Plugin.Notifications.PlaySfx("ping3.wav");
            HandleAutoOpen(Rank.S, fate.MapX, fate.MapY);
        }
    }

    public void OnZoneChange()
    {
        var isHuntZone = HuntData.Zones.ContainsKey(HuntModel.Territory.ZoneId);

        if (!isHuntZone && Plugin.MainWindow.IsOpen)
        {
            Plugin.MainWindow.IsOpen = false;
            Plugin.MainWindow.AutoClosed = true;
        }
        else if (isHuntZone && Plugin.MainWindow.AutoClosed)
        {
            Plugin.MainWindow.IsOpen = true;
            Plugin.MainWindow.AutoClosed = false;
        }
    }
}
