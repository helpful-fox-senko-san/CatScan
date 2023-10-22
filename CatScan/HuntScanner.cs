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

    // Event is appropriate to be consumed by notification generators
    public delegate void NewScanResultDelegate(ScanResult scanResult);

    public event NewScanResultDelegate? NewScanResult;

    public HuntScanner(GameScanner gameScanner)
    {
        _gameScanner = gameScanner;

        _gameScanner.NewEnemy += OnNewEnemy;
        _gameScanner.LostEnemy += OnLostEnemy;
        _gameScanner.UpdatedEnemy += OnUpdatedEnemy;
        _gameScanner.ZoneChange += OnZoneChange;
    }

    private static float ToMapOrd(float raw, float offset, float scale)
    {
        raw = raw * scale;
        return ((41.0f / scale) * ((raw + 1024.0f) / 2048.0f)) + 1.0f - offset;
    }

    private void KilledSS()
    {
        // Clear out both minion scan results and kill count data after an SS is killed
        foreach (var mark in HuntModel.Territory.ZoneData.Marks)
        {
            if (mark.Rank == Rank.KC)
            {
                if (HuntModel.KillCountLog.TryGetValue(mark.Name, out var kcLogEntry))
                {
                    kcLogEntry.Killed = 0;
                    kcLogEntry.Missing = 0;
                }
            }
            else if (mark.Rank == Rank.Minion)
            {
                HuntModel.ScanResults.Remove(mark.Name);
            }
        }

        _kcEnemies.Clear();
    }

    // Apply dynamic data from a scanned GameEnemy on to an existing Scan Result
    private void UpdateScanResult(ScanResult scanResult, GameEnemy gameEnemy)
    {
        // name and rank are never updated
        scanResult.RawX = gameEnemy.X;
        scanResult.RawZ = gameEnemy.Z;
        scanResult.MapX = ToMapOrd(gameEnemy.X, HuntModel.Territory.ZoneData.MapParams.OffsetX, HuntModel.Territory.ZoneData.MapParams.Scale);
        scanResult.MapY = ToMapOrd(gameEnemy.Z, HuntModel.Territory.ZoneData.MapParams.OffsetZ, HuntModel.Territory.ZoneData.MapParams.Scale);

        scanResult.lastSeenTimeUtc = HuntModel.UtcNow;

        if (scanResult.HpPct != 0.0 && gameEnemy.HpPct == 0.0)
        {
            scanResult.killTimeUtc = HuntModel.UtcNow;
            if (scanResult.Rank == Rank.SS)
                KilledSS();
        }

        scanResult.HpPct = gameEnemy.HpPct;
    }

    private void OnNewEnemy(GameEnemy enemy)
    {
        foreach (var mark in HuntModel.Territory.ZoneData.Marks)
        {
            if (mark.Name == enemy.Name)
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
                if (HuntModel.ScanResults.TryGetValue(enemy.Name, out var scanResult))
                {
                    UpdateScanResult(scanResult, enemy);
                    NewScanResult?.Invoke(scanResult);
                    // This needs to be marked Interesting unconditionally
                    enemy.Interesting = true;
                }
                else
                {
                    HuntModel.ScanResults.Add(mark.Name, scanResult = new ScanResult(){
                        Rank = mark.Rank,
                        Name = enemy.Name
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
        if (_kcEnemies.TryGetValue(enemy.ObjectId, out var kcEnemy) && HuntModel.KillCountLog.TryGetValue(enemy.Name, out var kcLogEntry))
        {
            if (!kcEnemy.Missing)
            {
                kcEnemy.Missing = true;
                ++kcLogEntry.Missing;
            }
        }

        if (HuntModel.ScanResults.TryGetValue(enemy.Name, out var scanResult))
            scanResult.Missing = true;
    }

    private void OnUpdatedEnemy(GameEnemy enemy)
    {
        // This is a KC mob dying or coming back in range
        if (_kcEnemies.TryGetValue(enemy.ObjectId, out var kcEnemy) && HuntModel.KillCountLog.TryGetValue(enemy.Name, out var kcLogEntry))
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

        if (HuntModel.ScanResults.TryGetValue(enemy.Name, out var scanResult))
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
        // Roll back the Missing count for the currently tracked lost enemies before clearing the list
        foreach (var kcEnemy in _kcEnemies.Values)
        {
            if (kcEnemy.Missing)
            {
                if (HuntModel.KillCountLog.TryGetValue(kcEnemy.Name, out var kcLogEntry))
                    --kcLogEntry.Missing;
            }
        }

        _kcEnemies.Clear();

        // Tell GameScanner to scan for enemies if we're in a known hunt zone
        if (HuntData.Zones.ContainsKey(zoneInfo.ZoneId))
            _gameScanner.EnableScanning();

        HuntModel.SwitchZone(zoneInfo.WorldId, zoneInfo.ZoneId, zoneInfo.Instance);
        HuntModel.Territory.WorldName = zoneInfo.WorldName;
    }
}
