using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace CatScan;

public class Notifications : IDisposable
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

    private CancellationTokenSource _disposalCts = new();
    private Channel<string> _playSfxChannel = Channel.CreateBounded<string>(2);
    private bool _sfxReady = false;

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

    private void OpenMapLink(float mapX, float mapY)
    {
        var mapRetryTimer = new PeriodicTimer(System.TimeSpan.FromMilliseconds(500));

        // Map linking can fail if we detect something immediately after a teleport
        // Retry for up to 5 seconds in the background
        Task.Run(async () => {
            for (int retries = 0; retries < 10; ++retries)
            {
                // Avoid spamming error text while we're teleporting
                if (!Plugin.BetweenAreas && await GameFunctions.OpenMapLink(mapX, mapY))
                    break;

                await mapRetryTimer.WaitForNextTickAsync();
            }
        });
    }

    private void StartSfxPlayerTask()
    {
        var sfxChannelReader = _playSfxChannel.Reader;

        Task.Run(async () => {
            while (await sfxChannelReader.WaitToReadAsync(_disposalCts.Token))
            {
                while (sfxChannelReader.TryRead(out var filename)
                 && !_disposalCts.Token.IsCancellationRequested)
                    ReallyPlaySfx(filename);
            }
        }, _disposalCts.Token);
    }

    public Notifications(HuntScanner huntScanner)
    {
        _huntScanner = huntScanner;
        _huntScanner.NewScanResult += OnNewScanResult;
        _huntScanner.NewFate += OnNewFate;
        _huntScanner.ZoneChange += OnZoneChange;
        _resourcePath = Path.Combine(DalamudService.PluginInterface.AssemblyLocation.Directory?.FullName!, "Resources");

        Task.Run(() => {
            PreloadSfx("ping1.wav");
            PreloadSfx("ping2.wav");
            PreloadSfx("ping3.wav");
            _sfxReady = true;
        });

        StartSfxPlayerTask();
    }

    public void Dispose()
    {
        _playSfxChannel.Writer.Complete();
        _disposalCts.Cancel();
    }

    private void ReallyPlaySfx(string filename)
    {
        if (!_sfxReady)
            return;

        if (!_cachedWavFiles.TryGetValue(filename, out var cachedSample))
            cachedSample = PreloadSfx(filename);

        if (cachedSample == null)
        {
            DalamudService.Log.Error($"Missing {filename}");
            return;
        }

        var waveStream = cachedSample.MakeStream();
        var waveChannel = new WaveChannel32(waveStream, Plugin.Configuration.SoundVolume, 0.0f){
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

    public void PlaySfx(string filename)
    {
        _playSfxChannel.Writer.TryWrite(filename);
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
                OpenMapLink(mapX, mapY);
            else if (Plugin.Configuration.AutoFlagA && rank == Rank.A)
                OpenMapLink(mapX, mapY);
            else if (Plugin.Configuration.AutoFlagB && rank == Rank.B)
                OpenMapLink(mapX, mapY);
            else if (Plugin.Configuration.AutoFlagFATE && rank == Rank.FATE)
                OpenMapLink(mapX, mapY);
        }
    }

    public void OnNewScanResult(ScanResult scanResult)
    {
        string? sfx = null;

        // Eureka NMs will be pinged based on their FATE instead, because they are not all infinite range
        if (HuntModel.Territory.ZoneData.Expansion == Expansion.Eureka)
            return;

        if (scanResult.EnglishName == "Coeurlregina" && _coeurlHackFlag)
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

    public void OnNewFate(ScannedFate fate)
    {
        _coeurlHackFlag = false;

        // Notify for fates that don't spawn with the boss initially present
        if (fate.EnglishName == "Long Live the Coeurl")
        {
            if (Plugin.Configuration.SoundEnabled && Plugin.Configuration.SoundAlertFATE)
                Plugin.Notifications.PlaySfx("ping3.wav");
            _coeurlHackFlag = true;
            HandleAutoOpen(Rank.FATE, fate.MapX, fate.MapY);
        }

        // Notify for NM fates in Eureka
        if (HuntData.EurekaZones.TryGetValue(HuntModel.Territory.ZoneId, out var eurekaZone))
        {
            foreach (var nm in eurekaZone.NMs)
            {
                if (fate.EnglishName == nm.FateName)
                {
                    if (Plugin.Configuration.SoundEnabled && Plugin.Configuration.SoundAlertS)
                        Plugin.Notifications.PlaySfx("ping3.wav");
                    HandleAutoOpen(Rank.S, fate.MapX, fate.MapY);
                    return;
                }
            }
        }

        // Alert for pre-CE fates in Bozja/Zadnor -- but not their Part 2 fates
        if (((HuntModel.Territory.ZoneId == 920 || HuntModel.Territory.ZoneId == 975) && fate.Epic)
         && fate.EnglishName != "Of Steel and Flame" && fate.EnglishName != "Attack of the Supersoldiers")
        {
            if (Plugin.Configuration.SoundEnabled && Plugin.Configuration.SoundAlertFATE)
                Plugin.Notifications.PlaySfx("ping3.wav");
        }
    }

    public void OnZoneChange()
    {
        var isHuntZone = HuntData.Zones.ContainsKey(HuntModel.Territory.ZoneId);

        if (Plugin.Configuration.AutoCloseEnabled)
        {
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

        if (isHuntZone)
            Plugin.MainWindow.SetTrainLogExpansion(HuntModel.Territory.ZoneData.Expansion);
    }
}
