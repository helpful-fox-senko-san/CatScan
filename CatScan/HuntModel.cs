using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;

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
    private Zone cachedZoneData = new Zone(Expansion.Unknown, string.Empty, 0);

    private Zone GetZoneData()
    {
        if (cachedZoneId == ZoneId)
            return cachedZoneData;

        cachedZoneId = ZoneId;

        // Fill the current zone with dummy data if its not known
        if (HuntData.Zones.TryGetValue(ZoneId, out var zoneData))
            cachedZoneData = zoneData;
        else
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
    public string EnglishName = string.Empty;
    public float RawX;
    public float RawZ;
    public float MapX;
    public float MapY;
    public float HpPct;
    [JsonIgnore] public bool Missing = true;

    // This lets us distinguish respawns
    public uint ObjectId;

    public bool MapWide => (Rank == Rank.S || Rank == Rank.FATE);
    public bool Dead => (HpPct == 0.0) || (MapWide && Missing);
    public bool Pulled => (HpPct < 100.0);

    public System.DateTime FirstSeenTimeUtc;
    public System.DateTime LastSeenTimeUtc;
    public System.DateTime KillTimeUtc;

    public System.TimeSpan FirstSeenAgo => HuntModel.UtcNow - FirstSeenTimeUtc;
    public System.TimeSpan LastSeenAgo => Missing ? HuntModel.UtcNow - LastSeenTimeUtc : System.TimeSpan.Zero;
    public System.TimeSpan KillTimeAgo => Dead ? HuntModel.UtcNow - KillTimeUtc : System.TimeSpan.Zero;
}

public class ScannedFate
{
    public bool Epic;
    public string Name = string.Empty;
    public string EnglishName = string.Empty;
    public float RawX;
    public float RawZ;
    public float MapX;
    public float MapY;
    public float ProgressPct;
    public bool Bonus;
    [JsonIgnore] public bool Missing = true;

    public bool IsCE = false;

    // Fates may be started only after talking to an NPC, or have a brief intro sequence that plays out
    public bool Running = false;

    public System.DateTime FirstSeenTimeUtc;
    public System.DateTime LastSeenTimeUtc;
    public System.DateTime EndTimeUtc;

    public System.TimeSpan FirstSeenAgo => HuntModel.UtcNow - FirstSeenTimeUtc;
    public System.TimeSpan TimeRemaining => Running ? (EndTimeUtc - HuntModel.UtcNow) : System.TimeSpan.Zero;
    public System.TimeSpan LastSeenAgo => Missing ? HuntModel.UtcNow - LastSeenTimeUtc : System.TimeSpan.Zero;
}

public class KillCount
{
    public string Name => GetLocalName();
    public string EnglishName = string.Empty;
    public int Killed;
    public int Missing;

    private string _cachedLocalName = string.Empty;

    private string GetLocalName()
    {
        if (_cachedLocalName.Length > 0)
            return _cachedLocalName;

        if (!GameData.NameDataReady)
            return EnglishName;

        _cachedLocalName = GameData.TranslateBNpcName(EnglishName);
        return _cachedLocalName;
    }
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
    public List<KillCount> KillCountLog = new();
}

public class HuntModelLock : IDisposable
{
    private Action? _action;

    public HuntModelLock(Action? action)
    {
        _action = action;
    }

    public void Dispose()
    {
        if (_action != null)
        {
            _action();
            _action = null;
        }
    }
}

public static class HuntModel
{
    public static System.DateTime UtcNow => System.DateTime.UtcNow;

    // Static information about the current zone
    public static HuntTerritory Territory = new();
    public static System.DateTime LastZoneChangeUtc = UtcNow;

    // --- Fields stored persistently in the zone cache

    // A list of hunt marks that have been detected in the current zone
    // XXX: Since these are keyed by the monster's name, only one SS minion can be recorded at a time
    public static Dictionary<string, ScanResult> ScanResults => CurrentZoneCacheEntry.ScanResults;

    // A list of kc monsters in the current zone, and their kill counts
    public static List<KillCount> KillCountLog => CurrentZoneCacheEntry.KillCountLog;

    // --- Fields NOT stored in the zone cache

    // A list of active and previously seen FATEs
    public static Dictionary<string, ScannedFate> Fates = new();

    public static IEnumerable<KeyValuePair<string, ScannedFate>> ActiveFates =>
        Fates.Where(f => !f.Value.Missing);

    public static int ActiveFateCount =>
        Fates.Count(f => !f.Value.Missing);

    public static IEnumerable<ScannedFate> ActiveFateValues =>
        Fates.Where(f => !f.Value.Missing).Select(f => f.Value);

    // Last failed FATE info for Southern Thanalan
    public static System.DateTime LastFailedFateUtc = System.DateTime.MinValue;
    public static string LastFailedFateName = string.Empty;

    // Last ended CE time for
    public static System.DateTime LastEndedCEUtc = System.DateTime.MinValue;

    // --- Page data in and out for per-zone persistence

    private static Dictionary<string, ZoneCacheEntry> ZoneCache = new();
    private static ZoneCacheEntry CurrentZoneCacheEntry;

    // --- Locking
    // SpinLock because encouraging the main game thread go to sleep seems like a bad idea.

    private static SpinLock _lock = new();

    public static HuntModelLock Lock(bool critical = false)
    {
        if (_lock.IsHeldByCurrentThread)
            return new HuntModelLock(null);

        bool lockTaken = false;

        if (!critical)
        {
            _lock.TryEnter(1, ref lockTaken);

            if (!lockTaken)
                System.Threading.Thread.Yield();
        }

        if (!lockTaken)
        {
            _lock.Enter(ref lockTaken);

            if (!lockTaken)
                throw new Exception("Failed to lock HuntModel");
        }

        return new HuntModelLock(() => {
            _lock.Exit(true);
        });
    }

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
                ZoneCache.Remove(new ZoneCacheKey(Territory.WorldId, Territory.ZoneId, Territory.Instance).ToString(), out _);
        }

        Territory.WorldId = worldId;
        Territory.ZoneId = zoneId;
        Territory.Instance = instance;

        var zoneKey = new ZoneCacheKey(worldId, zoneId, instance).ToString();

        if (!ZoneCache.ContainsKey(zoneKey))
            ZoneCache.TryAdd(zoneKey, new());

        CurrentZoneCacheEntry = ZoneCache[zoneKey];

        // Pre-fill the kill count log if appropriate
        if (CurrentZoneCacheEntry.KillCountLog.Count == 0)
        {
            foreach (var mark in HuntModel.Territory.ZoneData.Marks)
            {
                if (mark.Rank == Rank.KC)
                    CurrentZoneCacheEntry.KillCountLog.Add(new(){ EnglishName = mark.Name });
            }
        }
    }

    // Get the hunt model data for a specific zone
    public static ZoneCacheEntry ForZone(int zoneId, int instance)
    {
        var zoneKey = new ZoneCacheKey(Territory.WorldId, zoneId, instance).ToString();

        if (!ZoneCache.ContainsKey(zoneKey))
            ZoneCache.TryAdd(zoneKey, new());

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
                ZoneCache.TryAdd(r.Key, r.Value);
        }

        // After deserialization we need to update the current zone reference
        CurrentZoneCacheEntry = ForZone(Territory.ZoneId, Territory.Instance);
    }
}
