using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CatScan;

// Cache of current zone's static information
public class HuntTerritory
{
	public Zone ZoneData => GetZoneData();
	public int WorldId { get; set; } = -1;
	public int ZoneId { get; set; } = -1;
	public int Instance { get; set; } = -1;

	public string WorldName { get; set; } = string.Empty;

	private int cachedZoneId = -1;
	private Zone cachedZoneData;

	private Zone GetZoneData()
	{
		if (cachedZoneId == ZoneId)
			return cachedZoneData;

		cachedZoneId = ZoneId;

		// Fill the current zone with dummy data if its not known
		if (!HuntData.Zones.TryGetValue(ZoneId, out cachedZoneData))
			cachedZoneData = new Zone(Expansion.Unknown, (ZoneId > 0) ? $"#{ZoneId}" : "-", 0);

		return cachedZoneData;
	}

	public bool IsValid()
	{
		return ZoneId >= 0;
	}
}

public class ScanResult
{
	public Rank Rank;
	public string Name;
	public float RawX;
	public float RawZ;
	public float MapX;
	public float MapY;
	public float HpPct;
	[JsonIgnore] public bool Missing;

	public bool Dead => (HpPct == 0.0);
	public bool Pulled => (HpPct < 100.0);

	public bool PossiblyDead => Dead || Missing;

	public System.DateTime lastSeenTimeUtc;
	public System.DateTime killTimeUtc;

	System.TimeSpan lastSeenAgo => Missing ? HuntModel.UtcNow - lastSeenTimeUtc : System.TimeSpan.Zero;
	System.TimeSpan killTimeAgo => Dead ? HuntModel.UtcNow - killTimeUtc : System.TimeSpan.Zero;

	[JsonConstructor]
	public ScanResult()
	{
		Name = "";
	}
}

public class KillCount
{
	public int Killed;
	public int Missing;
}

public readonly struct ZoneCacheKey
{
	public readonly int WorldId;
	public readonly int ZoneId;
	public readonly int Instance;

	public ZoneCacheKey(int worldId, int zoneId, int instance)
	{
		if (instance == 0)
			instance = 1;

		WorldId = worldId;
		ZoneId = zoneId;
		Instance = instance;
	}

	// Use string keys to support serialization
    public override string ToString()
    {
        return $"{WorldId}:{ZoneId}:{Instance}";
    }
}

public class ZoneCacheEntry
{
	public Dictionary<string, ScanResult> ScanResults = new();
	public Dictionary<string, KillCount> KillCountLog = new();
}

static class HuntModel
{
	public static System.DateTime UtcNow => System.DateTime.UtcNow;

	// Static information about the current zone
	public static HuntTerritory Territory = new();

	// A list of hunt marks that have been detected in the current zone
	// XXX: Since these are keyed by the monster's name, only one SS minion can be recorded at a time
	// XXX: Some epic fate bosses can get fucked up by duplicate entries too
	public static Dictionary<string, ScanResult> ScanResults => CurrentZoneCacheEntry.ScanResults;

	// A list of kc monsters in the current zone, and their kill counts
	public static Dictionary<string, KillCount> KillCountLog => CurrentZoneCacheEntry.KillCountLog;

	// --- Page data in and out for per-zone persistence

	private static Dictionary<string, ZoneCacheEntry> ZoneCache = new();

	private static ZoneCacheEntry CurrentZoneCacheEntry;

	static HuntModel()
	{
		// Point at some dummy data as a fail-safe
		CurrentZoneCacheEntry = new();
	}

	public static void SwitchZone(int worldId, int zoneId, int instance)
	{
		// Determine if there's any meaningful data stored for this zone, otherwise erase it from memory entirely when leaving the zone
		if (Territory.IsValid())
		{
			bool hasData = (ScanResults.Count > 0);

			if (!hasData)
			{
				foreach (var e in KillCountLog.Values)
				{
					if (e.Killed > 0 || e.Missing > 0)
					{
						hasData = true;
						break;
					}
				}
			}

			if (!hasData)
				ZoneCache.Remove(new ZoneCacheKey(Territory.WorldId, Territory.ZoneId, Territory.Instance).ToString());
		}

		Territory.WorldId = worldId;
		Territory.ZoneId = zoneId;
		Territory.Instance = instance;

		var zoneKey = new ZoneCacheKey(worldId, zoneId, instance).ToString();

		if (!ZoneCache.ContainsKey(zoneKey))
			ZoneCache.Add(zoneKey, new());

		CurrentZoneCacheEntry = ZoneCache[zoneKey];

		// Pre-fill the kill count log if appropriate
		if (CurrentZoneCacheEntry.KillCountLog.Count == 0)
		{
			foreach (var mark in HuntModel.Territory.ZoneData.Marks)
			{
				if (mark.Rank == Rank.KC)
					CurrentZoneCacheEntry.KillCountLog.Add(mark.Name, new());
			}
		}
	}

	// Get the hunt model data for a specific zone
	public static ZoneCacheEntry ForZone(int zoneId, int instance)
	{
		var zoneKey = new ZoneCacheKey(Territory.WorldId, zoneId, instance).ToString();

		if (!ZoneCache.ContainsKey(zoneKey))
			ZoneCache.Add(zoneKey, new());

		return ZoneCache[zoneKey];
	}

	public static string Serialize()
	{
		JsonSerializerOptions opt = new();
		opt.IncludeFields = true;
		opt.IgnoreReadOnlyProperties = true;
		opt.WriteIndented = true;
		return JsonSerializer.Serialize(ZoneCache, opt);
	}

	public static void Deserialize(string json)
	{
		JsonSerializerOptions opt = new();
		opt.IncludeFields = true;
		opt.IgnoreReadOnlyProperties = true;
		var data = JsonSerializer.Deserialize(json, typeof(Dictionary<string, ZoneCacheEntry>), opt) as Dictionary<string, ZoneCacheEntry>;

		ZoneCache.Clear();

		if (data != null)
		{
			foreach (var r in data)
				ZoneCache.Add(r.Key, r.Value);
		}

		// Update the current zone reference
		if (Territory.IsValid())
			SwitchZone(Territory.WorldId, Territory.ZoneId, Territory.Instance);
	}
}
