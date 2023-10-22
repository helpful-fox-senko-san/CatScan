using NAudio.Wave;
using System.Collections.Generic;
using System.IO;

namespace CatScan;

public class Notifications
{
	private HuntScanner _huntScanner;
	private string _resourcePath = "";
	private WaveOutEvent _waveOut = new();

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

	private void PlaySfx(string filename)
	{
		if (!_cachedWavFiles.TryGetValue(filename, out var cachedSample))
			cachedSample = PreloadSfx(filename);

		if (cachedSample == null)
		{
			DalamudService.Log.Error($"Missing {filename}");
			return;
		}

		var waveStream = cachedSample.MakeStream();
		var waveChannel = new WaveChannel32(waveStream, 0.6f, 0.0f);
		_waveOut.Init(waveChannel);
		_waveOut.Play();
		_waveOut.PlaybackStopped += (object? sender, StoppedEventArgs args) => {
			_waveOut.Stop();
			waveStream.Close();
			_waveOut.Dispose();
			waveStream.Dispose();
		};
	}

	private void NotifyS()
	{
		PlaySfx("ping3.wav");
	}

	private void NotifyA()
	{
		PlaySfx("ping2.wav");
	}

	private void NotifyB()
	{
		PlaySfx("ping1.wav");
	}

	public void OnNewScanResult(ScanResult scanResult)
	{
		switch (scanResult.Rank)
		{
			case Rank.FATE:
			case Rank.SS:
			case Rank.S:
				NotifyS();
				break;

			case Rank.A:
				NotifyA();
				break;

			case Rank.Minion:
			case Rank.B:
				NotifyB();
				break;
		}
	}
}
