using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
            cachedZoneData = new Zone(Expansion.Unknown, string.Empty, 0);

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
    public string Name = string.Empty;
    public float RawX;
    public float RawZ;
    public float MapX;
    public float MapY;
    public float HpPct;
    [JsonIgnore] public bool Missing = true;

    public bool Dead => (HpPct == 0.0);
    public bool Pulled => (HpPct < 100.0);

    public bool PossiblyDead => Dead || Missing;

    public System.DateTime LastSeenTimeUtc;
    public System.DateTime KillTimeUtc;

    public System.TimeSpan LastSeenAgo => Missing ? HuntModel.UtcNow - LastSeenTimeUtc : System.TimeSpan.Zero;
    public System.TimeSpan KillTimeAgo => Dead ? HuntModel.UtcNow - KillTimeUtc : System.TimeSpan.Zero;
}

public class ActiveFate
{
    public bool Epic;
    public string Name = string.Empty;
    public float RawX;
    public float RawZ;
    public float MapX;
    public float MapY;
    public float ProgressPct;

    // Fates may be started only after talking to an NPC, or have a brief intro sequence that plays out
    public bool Running = false;

    public System.DateTime FirstSeenTimeUtc;
    public System.DateTime EndTimeUtc;

    public System.TimeSpan FirstSeenAgo => HuntModel.UtcNow - FirstSeenTimeUtc;
    public System.TimeSpan TimeRemaining => Running ? (EndTimeUtc - HuntModel.UtcNow) : System.TimeSpan.Zero;
}

public class KillCount
{
    public string Name = string.Empty;
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
    public Dictionary<uint, ScanResult> ScanResults = new();
    public List<KillCount> KillCountLog = new();
}

static class HuntModel
{
    public static System.DateTime UtcNow => System.DateTime.UtcNow;

    // Static information about the current zone
    public static HuntTerritory Territory = new();
    public static System.DateTime LastZoneChangeUtc = UtcNow;

    // --- Fields stored persistently in the zone cache

    // A list of hunt marks that have been detected in the current zone
    // XXX: Since these are keyed by the monster's name, only one SS minion can be recorded at a time
    public static Dictionary<uint, ScanResult> ScanResults => CurrentZoneCacheEntry.ScanResults;

    // A list of kc monsters in the current zone, and their kill counts
    public static List<KillCount> KillCountLog => CurrentZoneCacheEntry.KillCountLog;

    // --- Fields NOT stored in the zone cache

    // TODO: Maybe save this data when changing zones
    // In theory this information could be stored and then compared when re-entering
    // As long as you return in under 5 minutes, its very likely that no fates begun
    // and failed, at least in Southern Thanalan.

    // A list of active FATEs
    public static Dictionary<uint, ActiveFate> ActiveFates = new();

    public static System.DateTime LastFailedFateUtc = System.DateTime.MinValue;
    public static string LastFailedFateName = string.Empty;

    // --- Page data in and out for per-zone persistence

    private static Dictionary<string, ZoneCacheEntry> ZoneCache = new();

    private static ZoneCacheEntry CurrentZoneCacheEntry;

    static HuntModel()
    {
        // Point at some dummy data as a fail-safe
        CurrentZoneCacheEntry = new();
    }

    // Look up a KillCount log entry by NPC name
    public static bool TryGetKillCount(string name, [MaybeNullWhen(false)] out KillCount value)
    {
        foreach (var kc in KillCountLog)
        {
            // XXX: Almost certainly not capitalizing KC mob names correctly when pre-filling, so case-insensitive
            if (kc.Name.Equals(name, System.StringComparison.InvariantCultureIgnoreCase))
            {
                value = kc;
                return true;
            }
        }

        value = null;
        return false;
    }

    // Save the current zone data, and load data for the new zone
    public static void SwitchZone(int worldId, int zoneId, int instance)
    {
        // Determine if there's any meaningful data to save, otherwise erase it from memory entirely when leaving the zone
        // Ignore the case where we're switching to the zone we're already in
        if (Territory.IsValid() && (worldId != Territory.WorldId || zoneId != Territory.ZoneId || instance != Territory.Instance))
        {
            bool hasData = (ScanResults.Count > 0);

            if (!hasData)
            {
                foreach (var e in KillCountLog)
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
                {
                    // Have to go through a whole ton of work to try get a localized name...
                    var name = GameData.TranslateBNpcName(mark.Name);
                    CurrentZoneCacheEntry.KillCountLog.Add(new(){ Name = name });
                }
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

        // After deserialization we need to update the current zone reference
        CurrentZoneCacheEntry = ForZone(Territory.ZoneId, Territory.Instance);
    }
}
