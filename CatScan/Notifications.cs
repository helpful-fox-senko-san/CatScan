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

    public void OnNewScanResult(ScanResult scanResult)
    {
        string? sfx = null;

        if (!Plugin.Configuration.SoundEnabled)
            return;

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

        if (sfx != null)
            PlaySfx(sfx);
    }
}
