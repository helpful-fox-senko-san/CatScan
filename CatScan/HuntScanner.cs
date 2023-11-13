using Dalamud.Game.ClientState.Fates;
using System.Collections.Generic;

namespace CatScan;

// Consumes events from GameScanner and uses it to build up the state in HuntModel
public class HuntScanner
{
    private class KCEnemy
    {
        public string Name;
        public bool Missing = false;

        public KCEnemy(GameEnemy enemy)
        {
            Name = enemy.Name;
        }
    }

    private GameScanner _gameScanner;
    private Dictionary<uint, KCEnemy> _kcEnemies = new();
    private GameData.ZoneData _zoneData = new();

    // Event is appropriate to be consumed by notification generators
    public delegate void NewScanResultDelegate(ScanResult scanResult);
    public delegate void NewFateDelegate(ActiveFate fate);
    public delegate void ZoneChangeDelegate();

    public event NewScanResultDelegate? NewScanResult;
    public event NewFateDelegate? NewFate;
    public event ZoneChangeDelegate? ZoneChange;

    public HuntScanner(GameScanner gameScanner)
    {
        _gameScanner = gameScanner;

        _gameScanner.NewEnemy += OnNewEnemy;
        _gameScanner.LostEnemy += OnLostEnemy;
        _gameScanner.UpdatedEnemy += OnUpdatedEnemy;
        _gameScanner.NewOrUpdatedFate += OnFate;
        _gameScanner.LostFate += OnLostFate;
        _gameScanner.ZoneChange += OnZoneChange;
    }

    private static float ToMapOrd(float raw, float offset, float scale)
    {
        raw = raw * scale;
        return ((41.0f / scale) * ((raw + 1024.0f) / 2048.0f)) + 1.0f - offset;
    }

    // Clear kill count data after an S rank is killed
    // It could be cleared when it spawns instead, but you may want some time to see the final count
    private void KilledS(string englishName)
    {
        // Eureka/Bozja has multiple spawns associated with multiple kill count mobs
        // Only clear the singular associated KC

        if (HuntModel.Territory.ZoneData.Expansion == Expansion.Eureka)
        {
            if (!HuntData.EurekaZones.TryGetValue(HuntModel.Territory.ZoneId, out var eurekaZone))
                return;

            foreach (var nm in eurekaZone.NMs)
            {
                if (nm.NMName == englishName)
                {
                    foreach (var kcLogEntry in HuntModel.KillCountLog)
                    {
                        if (kcLogEntry.EnglishName == nm.KCName)
                        {
                            kcLogEntry.Killed = 0;
                            kcLogEntry.Missing = 0;
                        }
                    }
                    break;
                }
            }

            // TODO: Selectively clean-up _kcEnemies as well?
        }
        else if (HuntModel.Territory.ZoneData.Expansion == Expansion.Bozja)
        {
            // TODO: Implement Bozja logic
            return;
        }
        else
        {
            foreach (var kcLogEntry in HuntModel.KillCountLog)
            {
                kcLogEntry.Killed = 0;
                kcLogEntry.Missing = 0;
            }

            _kcEnemies.Clear();
        }
    }

    // Clear minion scan results after an SS is killed
    // (Removing one entry is enough, since only one is ever logged)
    private void KilledSS()
    {
        foreach (var result in HuntModel.ScanResults)
        {
            if (result.Value.Rank == Rank.Minion)
            {
                HuntModel.ScanResults.Remove(result.Key, out _);
                break;
            }
        }
    }

    // Apply dynamic data from a scanned GameEnemy on to an existing Scan Result
    private void UpdateScanResult(ScanResult scanResult, GameEnemy gameEnemy)
    {
        // name and rank are never updated
        scanResult.RawX = gameEnemy.X;
        scanResult.RawZ = gameEnemy.Z;
        scanResult.MapX = ToMapOrd(gameEnemy.X, _zoneData.MapOffsetX, _zoneData.MapScale);
        scanResult.MapY = ToMapOrd(gameEnemy.Z, _zoneData.MapOffsetY, _zoneData.MapScale);
        scanResult.Missing = false;
        scanResult.LastSeenTimeUtc = HuntModel.UtcNow;

        if (scanResult.HpPct != 0.0 && gameEnemy.HpPct == 0.0)
        {
            scanResult.KillTimeUtc = HuntModel.UtcNow;
            if (scanResult.Rank == Rank.S)
                KilledS(scanResult.EnglishName);
            if (scanResult.Rank == Rank.SS)
                KilledSS();
        }

        scanResult.HpPct = gameEnemy.HpPct;
    }

    // Apply dynamic data from a scanned GameFate on to an existing Fate
    private void UpdateActiveFate(ActiveFate activeFate, GameFate gameFate)
    {
        activeFate.RawX = gameFate.X;
        activeFate.RawZ = gameFate.Z;
        activeFate.MapX = ToMapOrd(gameFate.X, _zoneData.MapOffsetX, _zoneData.MapScale);
        activeFate.MapY = ToMapOrd(gameFate.Z, _zoneData.MapOffsetY, _zoneData.MapScale);
        activeFate.ProgressPct = gameFate.ProgressPct;

        if (gameFate.State != FateState.Preparation)
        {
            activeFate.EndTimeUtc = gameFate.EndTimeUtc ?? System.DateTime.MinValue;
            activeFate.Running = true;
        }
    }

    private void OnNewEnemy(GameEnemy enemy)
    {
        using var modelLock = HuntModel.Lock();

        foreach (var mark in HuntModel.Territory.ZoneData.Marks)
        {
            if (mark.Name == enemy.EnglishName)
            {
                // Don't actually log KC monsters as marks
                if (mark.Rank == Rank.KC)
                {
                    if (_kcEnemies.ContainsKey(enemy.ObjectId))
                    {
                        OnUpdatedEnemy(enemy);
                        break;
                    }

                    _kcEnemies.TryAdd(enemy.ObjectId, new KCEnemy(enemy));
                    // Tell GameScanner to only update us if the enemy is killed
                    enemy.InterestingKC = true;
                    break;
                }

                bool isNew = false;

                // There can be a new object ID picked up with the same name as an already logged mark
                // This is either a respawn, a bug, or in the case of SS minions: expected
                if (HuntModel.ScanResults.TryGetValue(enemy.EnglishName, out var scanResult))
                {
                    // XXX: An object ID of 0 comes from importing hunt train data
                    //      Avoid pinging when coming in to range of them
                    isNew = (scanResult.ObjectId != enemy.ObjectId) && (scanResult.ObjectId != 0);
                }
                else
                {
                    HuntModel.ScanResults.TryAdd(enemy.EnglishName, scanResult = new ScanResult(){
                        Rank = mark.Rank,
                        EnglishName = enemy.EnglishName,
                        Missing = false,
                        ObjectId = enemy.ObjectId,
                        FirstSeenTimeUtc = HuntModel.UtcNow
                    });
                    isNew = true;
                }

                UpdateScanResult(scanResult, enemy);
                // Tell GameScanner to continue to poll for information about this enemy
                enemy.Interesting = true;
                // Ping as a new scan result -- even if it was a respawn
                // Compare Object IDs to avoid re-pinging after re-entering a zone, though
                if (isNew)
                    NewScanResult?.Invoke(scanResult);

                break;
            }
        }
    }

    private void OnLostEnemy(GameEnemy enemy)
    {
        using var modelLock = HuntModel.Lock();

        // Its not possible to tell if a KC mob dies while out of range, so keep count of them
        if (_kcEnemies.TryGetValue(enemy.ObjectId, out var kcEnemy) && HuntModel.TryGetKillCount(enemy.Name, out var kcLogEntry))
        {
            if (!kcEnemy.Missing)
            {
                kcEnemy.Missing = true;
                ++kcLogEntry.Missing;
            }
        }

        if (HuntModel.ScanResults.TryGetValue(enemy.EnglishName, out var scanResult))
        {
            if (HuntModel.Territory.ZoneId != 827) // Hydatos
                scanResult.Missing = true;
        }
    }

    // Special logic to deal with NMs not being infinite draw distance in Hydatos
    private void SynthesizeEurekaNMCommon(string englishName, System.Action<EurekaNM> action)
    {
        if (!HuntData.EurekaZones.TryGetValue(HuntModel.Territory.ZoneId, out var eurekaZone))
            return;

        foreach (var nm in eurekaZone.NMs)
        {
            if (nm.FateName == englishName)
                action(nm);
        }
    }

    private void SynthesizeNewEurekaNM(ActiveFate activeFate, GameFate gameFate)
    {
        SynthesizeEurekaNMCommon(activeFate.EnglishName, (EurekaNM nm) => {
            if (HuntModel.ScanResults.TryGetValue(nm.NMName, out var scanResult))
                return;

            HuntModel.ScanResults.TryAdd(nm.NMName, scanResult = new ScanResult(){
                Rank = Rank.S,
                EnglishName = nm.NMName,
                Missing = (activeFate.ProgressPct == 100.0f),
                ObjectId = 0,
                FirstSeenTimeUtc = HuntModel.UtcNow,
                RawX = activeFate.RawX,
                RawZ = activeFate.RawZ,
                MapX = activeFate.MapX,
                MapY = activeFate.MapY,
                HpPct = 100.0f - activeFate.ProgressPct
            });
        });
    }

    private void SynthesizeUpdatedEurekaNM(ActiveFate activeFate, GameFate gameFate)
    {
        SynthesizeEurekaNMCommon(activeFate.EnglishName, (EurekaNM nm) => {
            if (!HuntModel.ScanResults.TryGetValue(nm.NMName, out var scanResult))
                return;

            scanResult.Missing = (activeFate.ProgressPct == 100.0f);
            scanResult.HpPct = 100.0f - activeFate.ProgressPct;
        });
    }

    private void SynthesizeLostEurekaNM(ActiveFate activeFate, GameFate gameFate)
    {
        SynthesizeEurekaNMCommon(activeFate.EnglishName, (EurekaNM nm) => {
            if (!HuntModel.ScanResults.TryGetValue(nm.NMName, out var scanResult))
                return;

            scanResult.Missing = true;
            scanResult.HpPct = 0.0f;
            KilledS(nm.NMName);
        });
    }

    private void OnFate(GameFate fate)
    {
        using var modelLock = HuntModel.Lock();

        // Update to an already recorded FATE
        if (HuntModel.ActiveFates.TryGetValue(fate.EnglishName, out var activeFate))
        {
            UpdateActiveFate(activeFate, fate);

            if (HuntModel.Territory.ZoneId == 827) // Hydatos
                SynthesizeUpdatedEurekaNM(activeFate, fate);
        }
        else
        {
            // New fate -- only care if its a world boss fate
            activeFate = new ActiveFate(){
                Name = fate.Name,
                EnglishName = fate.EnglishName,
                Epic = HuntData.EpicFates.Contains(fate.EnglishName),
                FirstSeenTimeUtc = HuntModel.UtcNow,
                IsCE = fate.IsCE
            };
            HuntModel.ActiveFates.TryAdd(fate.EnglishName, activeFate);
            UpdateActiveFate(activeFate, fate);
            NewFate?.Invoke(activeFate);

            if (HuntModel.Territory.ZoneId == 827) // Hydatos
                SynthesizeNewEurekaNM(activeFate, fate);
        }
    }

    private void OnLostFate(GameFate fate)
    {
        using var modelLock = HuntModel.Lock();

        if ((fate.State != FateState.Ended && fate.ProgressPct < 100.0f)
         || fate.State == FateState.Failed)
        {
            HuntModel.LastFailedFateUtc = HuntModel.UtcNow;
            HuntModel.LastFailedFateName = fate.Name;
        }

        if (HuntModel.Territory.ZoneId == 827) // Hydatos
        {
            if (HuntModel.ActiveFates.TryGetValue(fate.EnglishName, out var activeFate))
                SynthesizeLostEurekaNM(activeFate, fate);
        }

        HuntModel.ActiveFates.Remove(fate.EnglishName, out _);
    }

    private void OnUpdatedEnemy(GameEnemy enemy)
    {
        using var modelLock = HuntModel.Lock();

        // This is a KC mob dying or coming back in range
        if (_kcEnemies.TryGetValue(enemy.ObjectId, out var kcEnemy) && HuntModel.TryGetKillCount(enemy.Name, out var kcLogEntry))
        {
            if (kcEnemy.Missing)
            {
                // If the enemy was missing, it needs to be be re-marked as Interesting to the GameScanner
                enemy.InterestingKC = true;
                kcEnemy.Missing = false;
                --kcLogEntry.Missing;
            }

            if (enemy.HpPct == 0.0)
            {
                // TODO: Don't count KC while in a eureka zone if:
                // - NM is already spawned
                // - NM is dead and has been seen in the last 2 hours
                ++kcLogEntry.Killed;
                _kcEnemies.Remove(enemy.ObjectId);
            }
        }

        if (HuntModel.ScanResults.TryGetValue(enemy.EnglishName, out var scanResult))
        {
            // If the enemy was missing, it needs to be be re-marked as Interesting to the GameScanner
            if (scanResult.Missing)
            {
                enemy.Interesting = true;
                scanResult.Missing = false;
            }

            UpdateScanResult(scanResult, enemy);
        }
    }

    private void OnZoneChange(GameZoneInfo zoneInfo)
    {
        using var modelLock = HuntModel.Lock();

        // Roll back the Missing count for the currently tracked lost enemies
        foreach (var kcEnemy in _kcEnemies.Values)
        {
            if (kcEnemy.Missing)
            {
                if (HuntModel.TryGetKillCount(kcEnemy.Name, out var kcLogEntry))
                    --kcLogEntry.Missing;
            }
        }

        _kcEnemies.Clear();

        // Clear the active fate list
        HuntModel.ActiveFates.Clear();
        HuntModel.LastFailedFateUtc = HuntModel.UtcNow;
        HuntModel.LastFailedFateName = string.Empty;

        // Clear non-A rank monsters
        //todo...

        // If its Eureka/Bozja, clear everything, because we can't track instances
        if (HuntModel.Territory.ZoneData.Expansion == Expansion.Eureka
         || HuntModel.Territory.ZoneData.Expansion == Expansion.Bozja)
        {
            // An unneccessary call happens after debug deserialization
            // Avoid wiping out data by checking for this case
            if (zoneInfo.ZoneId != HuntModel.Territory.ZoneId)
            {
                HuntModel.KillCountLog.Clear();
                HuntModel.ScanResults.Clear();
            }
        }

        // ---

        // Load new zone's game data (map offsets)
        _zoneData = GameData.GetZoneData(zoneInfo.ZoneId);

        // Tell GameScanner to scan for enemies if we're in a known hunt zone
        if (HuntData.Zones.ContainsKey(zoneInfo.ZoneId))
            _gameScanner.EnableScanning();

        // HuntModel handles the saving/loading of zone-cached data itself
        HuntModel.SwitchZone(zoneInfo.WorldId, zoneInfo.ZoneId, zoneInfo.Instance);
        HuntModel.Territory.WorldName = zoneInfo.WorldName;
        HuntModel.LastZoneChangeUtc = HuntModel.UtcNow;

        ZoneChange?.Invoke();
    }
}
