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

    private void KilledS()
    {
        // Eureka/Bozja has multiple "S" ranks and multiple kill counts
        // Skip clearing the kill count until there's an association between them individually
        if (HuntModel.Territory.ZoneData.Expansion == Expansion.Eureka
         || HuntModel.Territory.ZoneData.Expansion == Expansion.Bozja)
            return;

        // Clear kill count data after an S rank is killed
        // It could be cleared when it spawns instead, but you may want some time to see the final count
        foreach (var kcLogEntry in HuntModel.KillCountLog)
        {
            kcLogEntry.Killed = 0;
            kcLogEntry.Missing = 0;
        }

        _kcEnemies.Clear();
    }

    private void KilledSS()
    {
        // Clear minion scan results after an SS is killed
        // Removing one entry is enough, since only one is ever logged
        foreach (var result in HuntModel.ScanResults)
        {
            if (result.Value.Rank == Rank.Minion)
            {
                HuntModel.ScanResults.Remove(result.Key);
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

        scanResult.LastSeenTimeUtc = HuntModel.UtcNow;

        if (scanResult.HpPct != 0.0 && gameEnemy.HpPct == 0.0)
        {
            scanResult.KillTimeUtc = HuntModel.UtcNow;
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
        foreach (var mark in HuntModel.Territory.ZoneData.Marks)
        {
            if (mark.Name == enemy.EnglishName)
            {
                // Don't actually log KC monsters as marks
                if (mark.Rank == Rank.KC)
                {
                    if (_kcEnemies.ContainsKey(enemy.ObjectId))
                    {
                        DalamudService.Log.Warning($"Received NewEnemy event for the same object id twice.");
                        OnUpdatedEnemy(enemy);
                        break;
                    }

                    _kcEnemies.Add(enemy.ObjectId, new KCEnemy(enemy));
                    // Tell GameScanner to only update us if the enemy is killed
                    enemy.InterestingKC = true;
                    break;
                }

                // New object ID with the same name as an already logged mark
                // This is either a respawn, a bug, or in the case of SS minions: expected
                // ... oops, this can also happen when entering a zone with a saved scan list
                if (HuntModel.ScanResults.TryGetValue(enemy.NameId, out var scanResult))
                {
                    UpdateScanResult(scanResult, enemy);
                    NewScanResult?.Invoke(scanResult);
                    // This needs to be marked Interesting unconditionally
                    enemy.Interesting = true;
                }
                else
                {
                    HuntModel.ScanResults.Add(enemy.NameId, scanResult = new ScanResult(){
                        Rank = mark.Rank,
                        Name = enemy.Name,
                        Missing = false
                    });
                    UpdateScanResult(scanResult, enemy);
                    // Tell GameScanner to continue to poll for information about this enemy
                    enemy.Interesting = true;
                    NewScanResult?.Invoke(scanResult);
                }

                break;
            }
        }
    }

    private void OnLostEnemy(GameEnemy enemy)
    {
        // Its not possible to tell if a KC mob dies while out of range, so keep count of them
        if (_kcEnemies.TryGetValue(enemy.ObjectId, out var kcEnemy) && HuntModel.TryGetKillCount(enemy.Name, out var kcLogEntry))
        {
            if (!kcEnemy.Missing)
            {
                kcEnemy.Missing = true;
                ++kcLogEntry.Missing;
            }
        }

        if (HuntModel.ScanResults.TryGetValue(enemy.NameId, out var scanResult))
            scanResult.Missing = true;
    }

    private void OnFate(GameFate fate)
    {
        // Update to an already recorded FATE
        if (HuntModel.ActiveFates.TryGetValue(fate.FateId, out var activeFate))
        {
            UpdateActiveFate(activeFate, fate);
        }
        else
        {
            // New fate -- only care if its a world boss fate
            activeFate = new ActiveFate(){
                Name = fate.Name,
                Epic = HuntData.EpicFates.Contains(fate.EnglishName),
                FirstSeenTimeUtc = HuntModel.UtcNow
            };
            HuntModel.ActiveFates.Add(fate.FateId, activeFate);
            UpdateActiveFate(activeFate, fate);
            NewFate?.Invoke(activeFate);
        }
    }

    private void OnLostFate(GameFate fate)
    {
        if ((fate.State != FateState.Ended && fate.ProgressPct < 100.0f)
         || fate.State == FateState.Failed)
        {
            HuntModel.LastFailedFateUtc = HuntModel.UtcNow;
            HuntModel.LastFailedFateName = fate.Name;
        }

        // If Eureka support is added this would be a good place to clear the kill count for an NM
        HuntModel.ActiveFates.Remove(fate.FateId);
    }

    private void OnUpdatedEnemy(GameEnemy enemy)
    {
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
                ++kcLogEntry.Killed;
                _kcEnemies.Remove(enemy.ObjectId);
            }
        }

        if (HuntModel.ScanResults.TryGetValue(enemy.NameId, out var scanResult))
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

    // Apply dynamic data from a scanned GameEnemy on to an existing Scan Result
    private void UpdateFate(ScanResult scanResult, GameEnemy gameEnemy)
    {
        // name and rank are never updated
        scanResult.RawX = gameEnemy.X;
        scanResult.RawZ = gameEnemy.Z;
        scanResult.MapX = ToMapOrd(gameEnemy.X, _zoneData.MapOffsetX, _zoneData.MapScale);
        scanResult.MapY = ToMapOrd(gameEnemy.Z, _zoneData.MapOffsetY, _zoneData.MapScale);

        if (scanResult.HpPct != 0.0 && gameEnemy.HpPct == 0.0)
        {
            scanResult.KillTimeUtc = HuntModel.UtcNow;
            if (scanResult.Rank == Rank.S)
                KilledS();
            if (scanResult.Rank == Rank.SS)
                KilledSS();
        }

        scanResult.HpPct = gameEnemy.HpPct;
    }

    private void OnZoneChange(GameZoneInfo zoneInfo)
    {
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
