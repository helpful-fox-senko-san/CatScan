using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Fates;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CatScan;

public class GameZoneInfo
{
    public int WorldId;
    public int ZoneId;
    public int Instance;

    // Just for simplicity, send the world name along as well
    public string WorldName = string.Empty;
}

public class GameEnemy
{
    public uint ObjectId;
    public string Name = string.Empty;
    // raw world coordinates
    public float X;
    public float Z;
    public float HpPct;

    // Set to true for untargetable enemies
    public bool Ignore = false;
    // This should be set to inform the GameScanner to actively poll information about this enemy
    public bool Interesting = false;
    // Set this instead to only poll for death
    public bool InterestingKC = false;

    // Milliseconds since the enemy was last seen in the object table
    // Since the table is scanned progressively, this will fluctuate between approximately 0-100ms
    public double OffscreenTimeMS = 0.0;
}

public class GameFate
{
    public uint FateId;
    public string Name = string.Empty;
    // raw world coordinates
    public float X;
    public float Z;
    public float ProgressPct;
    public FateState State;
    public System.DateTime? EndTimeUtc;

    // Milliseconds since the fate was last seen in the fate table
    public double OffscreenTimeMS = 0.0;
}

// Scans the game state for the current zone and visible enemies
public class GameScanner : IDisposable
{
    private readonly Dictionary<uint, GameEnemy> _enemyCache = new();
    private readonly Dictionary<uint, GameFate> _fateCache = new();
    private readonly HashSet<uint> _lostIds = new();

    // These events are picked up by HuntScanner and processed in to logical events of its own
    public delegate void NewEnemyDelegate(GameEnemy enemy);
    public delegate void LostEnemyDelegate(GameEnemy enemy);
    public delegate void UpdatedEnemyDelegate(GameEnemy enemy);
    public delegate void NewOrUpdatedFateDelegate(GameFate fate);
    public delegate void LostFateDelegate(GameFate fate);
    public delegate void ZoneChangeDelegate(GameZoneInfo zoneInfo);

    public event NewEnemyDelegate? NewEnemy;
    public event LostEnemyDelegate? LostEnemy;
    public event UpdatedEnemyDelegate? UpdatedEnemy;
    public event NewOrUpdatedFateDelegate? NewOrUpdatedFate;
    public event LostFateDelegate? LostFate;
    public event ZoneChangeDelegate? ZoneChange;

    private CancellationTokenSource _disposalCts = new();
    private ConcurrentQueue<Action> _emitQueue = new();

    private int _updateFailCount = 0;
    private bool _frameworkUpdateRegistered = false;

    private double _deltaMs = 0.0;
    private int _nextIdx = 0;
    private int _worldId = -1;
    private string _worldName = string.Empty;
    private int _zoneId = -1;
    private int _instance = -1;
    private bool _betweenAreas = false;
    private bool _territoryChanged = false;
    private bool _scanningEnabled = false;

    // Expose some state for debugging
    internal int EnemyCacheSize => _enemyCache.Count;
    internal int FateCacheSize => _fateCache.Count;
    internal int LostIdsSize => _lostIds.Count;
    internal bool BetweenAreas => _betweenAreas;
    internal bool TerritoryChanged => _territoryChanged;
    internal bool ScanningEnabled => _scanningEnabled;
    internal bool FrameworkUpdateRegistered => _frameworkUpdateRegistered;

    private bool _emitTaskActive = false;
    private TaskCompletionSource _emitTaskPokeSource = new();

    // Events are queued and emitted as a task to avoid blocking the game while running logic
    // Called from Framework thread, so uses a Task to emit events to avoid excessive work on the Framework thread
    private void FlushEmitQueue()
    {
        if (_emitQueue.Count == 0 || _emitTaskActive)
        {
            // Task is active, poke it to do work
            if (_emitQueue.Count > 0)
                _emitTaskPokeSource.TrySetResult();

            return;
        }

        _emitTaskActive = true;

        Task.Run(async () => {
            var isDisposed = () => _disposalCts.Token.IsCancellationRequested;

            while (!isDisposed())
            {
                while (!isDisposed() && _emitQueue.TryDequeue(out var action))
                    action();

                bool poked = false;

                // Try to keep the task alive for a while to avoid creating/destroying it repeatedly
                await Task.WhenAny(Task.Delay(1000, _disposalCts.Token), _emitTaskPokeSource.Task);

                // We were poked to process new events
                if (_emitTaskPokeSource.Task.IsCompleted)
                {
                    poked = true;
                    _emitTaskPokeSource = new();
                }

                // We weren't poked and the queue is empty, time to exit
                if (!poked && _emitQueue.Count == 0)
                    break;
            }

            _emitTaskActive = false;
        }, _disposalCts.Token);
    }

    private void EmitNewEnemy(GameEnemy enemy)
    {
        _emitQueue.Enqueue(() => { NewEnemy?.Invoke(enemy); });
    }

    private void EmitLostEnemy(GameEnemy enemy)
    {
        _emitQueue.Enqueue(() => { LostEnemy?.Invoke(enemy); });
    }

    private void EmitUpdatedEnemy(GameEnemy enemy)
    {
        _emitQueue.Enqueue(() => { UpdatedEnemy?.Invoke(enemy); });
    }

    private void EmitFate(GameFate fate)
    {
        _emitQueue.Enqueue(() => { NewOrUpdatedFate?.Invoke(fate); });
    }

    private void EmitLostFate(GameFate fate)
    {
        _emitQueue.Enqueue(() => { LostFate?.Invoke(fate); });
    }

    private void EmitZoneChange(GameZoneInfo zoneInfo)
    {
        // Zone changes are critical and infrequent, just emit them directly
        _emitQueue.Clear();
        Task.Run(() => { ZoneChange?.Invoke(zoneInfo); });
    }

    public GameScanner()
    {
        RegisterFrameworkUpdate();
        DalamudService.ClientState.Login += OnLogin;
        DalamudService.ClientState.Logout += OnLogout;
        DalamudService.ClientState.TerritoryChanged += OnTerritoryChanged;
        DalamudService.Condition.ConditionChange += OnConditionChange;

        // Trigger an initial update based on the current territory
        _territoryChanged = true;
    }

    // This should be called in response to a ZoneChange event to enable object scanning
    public void EnableScanning()
    {
        _scanningEnabled = true;
        RegisterFrameworkUpdate();
    }

    public void Dispose()
    {
        UnregisterFrameworkUpdate();
        _disposalCts.Cancel();
        DalamudService.ClientState.Login -= OnLogin;
        DalamudService.ClientState.Logout -= OnLogout;
        DalamudService.ClientState.TerritoryChanged -= OnTerritoryChanged;
        DalamudService.Condition.ConditionChange -= OnConditionChange;
    }

    private unsafe void UpdateInstance()
    {
        var uistate = UIState.Instance();

        if (uistate == null)
        {
            _instance = -1;
            return;
        }

        _instance = uistate->AreaInstance.Instance;

        if (_instance < 0 || _instance > 9)
        {
            _instance = -1;
            return;
        }
    }

    private void UpdateWorldId()
    {
        var localPlayer = DalamudService.ClientState.LocalPlayer;

        if (localPlayer == null)
        {
            _worldId = -1;
            _worldName = string.Empty;
            return;
        }

        var gameData = localPlayer.CurrentWorld.GameData;

        if (gameData == null)
        {
            _worldId = -1;
            _worldName = string.Empty;
            return;
        }

        _worldId = (int)localPlayer.CurrentWorld.Id;
        _worldName = gameData.Name.ToString();
    }

    // Clear the current zone's information and cached enemy data
    // Can be called from Framework thread OR from UI
    public void ClearCache()
    {
        DalamudService.Framework.RunOnFrameworkThread(() => {
            _territoryChanged = true;
            _worldId = -1;
            _zoneId = -1;
            _instance = -1;
            _enemyCache.Clear();
            _fateCache.Clear();
            _lostIds.Clear();
            RegisterFrameworkUpdate();
        });
    }

    private void OnLogin()
    {
        _territoryChanged = true;
        RegisterFrameworkUpdate();
    }

    private void OnLogout()
    {
        if (_zoneId >= 0)
        {
            EmitZoneChange(new GameZoneInfo(){
                WorldId = _worldId,
                ZoneId = _zoneId,
                Instance = _instance,
                WorldName = _worldName
            });
        }
        UnregisterFrameworkUpdate();
    }

    private void OnTerritoryChanged(ushort id)
    {
        _territoryChanged = true;
        RegisterFrameworkUpdate();
    }

    private void OnConditionChange(ConditionFlag flag, bool value)
    {
        // Regular BetweenAreas flag is set for teleports within the same zone
        // BetweenAreas51 is set when travelling between zones or instances, which is exactly what we want
        if (flag == ConditionFlag.BetweenAreas51)
        {
            // TerritoryChanged event triggered before BetweenAreas flag was read?
            // Ignore the condition to avoid shutting off the scanner again
            if (_territoryChanged && value)
            {
                DalamudService.Log.Warning("TerritoryChanged was triggered before BetweenAreas51");
                value = false;
            }

            _betweenAreas = value;

            if (!_betweenAreas)
                RegisterFrameworkUpdate();
        }
    }

    // Called by ScanTick in Framework thread
    // Creates a cache entry for a battle NPC, (optionally) emits a NewEnemy event, and returns the cache entry
    private GameEnemy DoNewEnemy(BattleNpc bnpc, uint id, bool wasLost = false)
    {
        // New enemy
        var pos = bnpc.Position;
        var hpPct = 100.0f;
        var maxHp = bnpc.MaxHp;

        if (maxHp > 0)
            hpPct = (float)bnpc.CurrentHp / (float)maxHp * 100.0f;

        string? customName = null;

        // HACK: Odin uses a player's name, but can be identified by its ID
        if (bnpc.DataId == 882)
            customName = "Odin";

        var newEnemy = new GameEnemy(){
            ObjectId = id,
            Name = customName ?? bnpc.Name.ToString(),
            X = pos.X,
            Z = pos.Z,
            HpPct = hpPct,
            Ignore = false // Obsolete: Was used to try flag FATE boss AoEs before
        };

        _enemyCache.Add(id, newEnemy);

        // Do not emit a new enemy event if this was an already known but lost enemy -- instead DoUpdateEnemy will emit an event
        if (!wasLost)
            EmitNewEnemy(newEnemy);

        return newEnemy;
    }

    // Called by ScanTick in Framework thread
    // Updates or re-creates a cache entry from a battle NPC, (optionally) emits an UpdatedEnemy event, and returns the cache entry
    // The enemy parameter may be null, in which case DoNewEnemy() is called to insert a new cache entry
    private GameEnemy DoUpdateEnemy(BattleNpc bnpc, uint id, GameEnemy? cachedEnemy)
    {
        var dirty = false;

        // If cachedEnemy is null, then it was lost and needs its cache entry to be re-created
        if (cachedEnemy == null)
        {
            cachedEnemy = DoNewEnemy(bnpc, id, true);
            // If the NPC was signalled as lost, re-signal it as updated even if nothing changes
            dirty = true;
        }

        // Check for death of both hunt marks and KC mobs
        if (cachedEnemy.HpPct != 0.0f && (cachedEnemy.Interesting || cachedEnemy.InterestingKC))
        {
            var hpPct = 100.0f;
            var maxHp = bnpc.MaxHp;

            if (maxHp > 0)
                hpPct = (float)bnpc.CurrentHp / (float)maxHp * 100.0f;

            bool isDead = false;

            if (hpPct == 0.0f || bnpc.IsDead)
            {
                isDead = true;
            }
            else if (hpPct < 1.0f && cachedEnemy.Name == "Archaeotania")
            {
                // HACK: Godzilla doesn't die but ends the fight at low HP
                isDead = true;
            }

            if (cachedEnemy.HpPct != 0.0f && isDead)
            {
                cachedEnemy.HpPct = hpPct = 0.0f;
                dirty = true;
            }

            if (double.Abs(hpPct - cachedEnemy.HpPct) >= 0.1)
            {
                cachedEnemy.HpPct = hpPct;
                dirty = dirty || cachedEnemy.Interesting;
            }
        }

        // Check the position of hunt marks (KC mobs don't need their position tracked)
        if (cachedEnemy.Interesting)
        {
            var pos = bnpc.Position;

            if (double.Abs(pos.X - cachedEnemy.X) >= 1.0)
            {
                cachedEnemy.X = pos.X;
                dirty = true;
            }

            if (double.Abs(pos.Z - cachedEnemy.Z) >= 1.0)
            {
                cachedEnemy.Z = pos.Z;
                dirty = true;
            }
        }

        if (dirty)
            EmitUpdatedEnemy(cachedEnemy);

        return cachedEnemy;
    }

    // Called in Framework thread while zone ID is complete and scanning is enabled
    private void ScanTick()
    {
        int tableLen = DalamudService.ObjectTable.Length;
        int n = 20;

        // Only scan a portion of the object table each frame, to reduce the time spent per frame
        // This is based on the frame time to try guarantee at least 10 full table scans per second.
        {
            // Process a portion of the object table each update, to reach a target of one full scan per 100ms
            const double targetMs = 100.0;

            // Number of table entries to process
            n = (int)System.Math.Round((_deltaMs / targetMs) * tableLen);

            // Limit to one full table scan per update
            // Also ensure some reasonable minimum amount of progress is made per frame
            n = System.Math.Clamp(n, 20, tableLen);
        }

        int i = 0;

        // next() increments i, as well as nextIdx with wrap-around
        var next = () => {
            ++i;

            if (++_nextIdx > tableLen)
                _nextIdx = 0;
        };

        for (; i < n; next())
        {
            var idx = _nextIdx;
            var obj = DalamudService.ObjectTable[idx];

            if (obj == null)
                continue;

            var bnpc = (obj as BattleNpc);

            if (bnpc == null || bnpc.BattleNpcKind != BattleNpcSubKind.Enemy)
                continue;

            var id = obj.ObjectId;

            if (_enemyCache.TryGetValue(id, out var cachedEnemy))
            {
                // Mark existing enemy as being seen
                cachedEnemy.OffscreenTimeMS = 0.0f;
                DoUpdateEnemy(bnpc, id, cachedEnemy);
            }
            else
            {
                // This seems to be a temporary condition for legitimate monsters
                // They will appear as targetable NPCs in a different slot shortly after
                // This condition should also filter out FATE boss AoEs from ever being detected as real enemies
                if (!bnpc.IsTargetable)
                    continue;

                if (_lostIds.Contains(id))
                {
                    DoUpdateEnemy(bnpc, id, null);
                    // The enemy is no longer lost, so remove it from the list
                    _lostIds.Remove(id);
                }
                else
                {
                    DoNewEnemy(bnpc, id);
                }
            }
        }

        // Scan the enemy cache for off-screen enemies to mark as lost
        foreach (var entry in _enemyCache)
        {
            var cachedEnemy = entry.Value;

            cachedEnemy.OffscreenTimeMS += _deltaMs;

            // Consider an enemy as lost once it hasn't been seen for 500ms
            if (cachedEnemy.OffscreenTimeMS >= 500.0)
            {
                // We don't need to track IDs for non-hunt / non-kc NPCs
                if (cachedEnemy.Interesting || cachedEnemy.InterestingKC)
                    _lostIds.Add(entry.Key);
                _enemyCache.Remove(entry.Key);
                EmitLostEnemy(cachedEnemy);
                break;
            }
        }
    }

    // Called in Framework thread while zone ID is complete and scanning is enabled
    private void ScanFateTick()
    {
        foreach (var fate in DalamudService.FateTable)
        {
            var id = fate.FateId;
            var state = fate.State;

            if (_fateCache.TryGetValue(id, out var cachedFate))
            {
                if (state == FateState.WaitingForEnd || state == FateState.Ended)
                {
                    cachedFate.State = FateState.Ended;
                    cachedFate.ProgressPct = (float)fate.Progress;
                    _fateCache.Remove(id);
                    EmitLostFate(cachedFate);
                    continue;
                }

                // Mark existing fate as being seen
                cachedFate.OffscreenTimeMS = 0.0f;

                if (cachedFate.State != state)
                {
                    var startTime = fate.StartTimeEpoch;
                    var duration = fate.Duration;

                    // Fates may temporarily appear in the Running state but without the timer set up
                    if (startTime == 0 || duration == 0)
                        continue;

                    cachedFate.State = state;
                    cachedFate.EndTimeUtc = System.DateTimeOffset.FromUnixTimeSeconds(startTime + duration).UtcDateTime;
                }

                if (state != FateState.Preparation)
                {
                    // TODO: dont update unless something changes
                    cachedFate.ProgressPct = (float)fate.Progress;
                    EmitFate(cachedFate);
                }
            }
            else
            {
                if (state == FateState.WaitingForEnd || state == FateState.Ended)
                    continue;

                var startTime = fate.StartTimeEpoch;
                var duration = fate.Duration;

                // Fates may temporarily appear in the Running state but without the timer set up
                // Forcing the state to Preparation ensures that it will continue to be polled until the timer is set
                if (state != FateState.Preparation && (startTime == 0 || duration == 0))
                    state = FateState.Preparation;

                var pos = fate.Position;

                // Fates report an inital position of 0,0 after spawning
                // Skip the fate until it reports real data
                if (pos.X == 0.0 && pos.Y == 0.0)
                    continue;

                cachedFate = new GameFate(){
                    FateId = id,
                    Name = fate.Name.ToString(),
                    X = pos.X,
                    Z = pos.Z,
                    EndTimeUtc = state == FateState.Preparation ? null : System.DateTimeOffset.FromUnixTimeSeconds(startTime + duration).UtcDateTime,
                    State = state
                };

                _fateCache.Add(id, cachedFate);
                EmitFate(cachedFate);
                bool valid = fate.IsValid();
            }
        }

        // Scan the enemy cache for off-screen enemies to mark as lost
        foreach (var entry in _fateCache)
        {
            var cachedFate = entry.Value;

            cachedFate.OffscreenTimeMS += _deltaMs;

            // Consider a fate as lost once it hasn't been seen for 500ms
            if (cachedFate.OffscreenTimeMS >= 500.0)
            {
                _fateCache.Remove(entry.Key);
                EmitLostFate(cachedFate);
                break;
            }
        }
    }

    // DoFrameworkUpdate
    private void Tick()
    {
        // No need to do anything while not logged in
        // If between areas, a territory change is probably about to happen -- don't scan to avoid mixing up zones
        if (!DalamudService.ClientState.IsLoggedIn || _betweenAreas)
        {
            ClearCache();
            UnregisterFrameworkUpdate();
            return;
        }

        // Clear the enemy list and emit a zone change event
        if (_territoryChanged || _worldId < 0 || _zoneId < 0 || _instance < 0)
        {
            ClearCache();

            if (_territoryChanged || _zoneId < 0)
                _zoneId = DalamudService.ClientState.TerritoryType;

            if (_worldId < 0) UpdateWorldId();
            if (_worldId < 0) return;

            if (_instance < 0) UpdateInstance();
            if (_instance < 0) return;

            EmitZoneChange(new GameZoneInfo(){
                WorldId = _worldId,
                ZoneId = _zoneId,
                Instance = _instance,
                WorldName = _worldName
            });

            _territoryChanged = false;
            _scanningEnabled = false;
            UnregisterFrameworkUpdate();
            return;
        }

        // Don't scan until we handle the zone change event and confirm this is a zone we care about
        if (!_scanningEnabled)
        {
            UnregisterFrameworkUpdate();
            return;
        }

        try
        {
            // Accumulate time if the update fails for a temporary reason
            _deltaMs += System.Math.Clamp(DalamudService.Framework.UpdateDelta.TotalMilliseconds, 1.0, 100.0);

            if (double.IsNaN(_deltaMs))
                _deltaMs = 100.0;

            var x = _nextIdx;
            ScanTick();

            // Scan fate table only once for each full object table sweep
            //if (x <= _nextIdx)
                ScanFateTick();

            _deltaMs = 0.0;
        }
        finally
        {
            FlushEmitQueue();
        }
    }

    private void UnregisterFrameworkUpdate()
    {
        if (!_frameworkUpdateRegistered)
            return;
        DalamudService.Framework.Update -= OnFrameworkUpdate;
        _frameworkUpdateRegistered = false;
    }

    private void RegisterFrameworkUpdate()
    {
        if (_frameworkUpdateRegistered)
            return;
        DalamudService.Framework.Update += OnFrameworkUpdate;
        _frameworkUpdateRegistered = true;
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        // Stop running after 20 consecutive exceptions
        if (_updateFailCount > 20)
        {
            DalamudService.Log.Error("Stopping scanner due to too many errors");
            UnregisterFrameworkUpdate();
            return;
        }

        try
        {
            Tick();
            _updateFailCount = 0;
        }
        catch (Exception ex)
        {
            DalamudService.Log.Error(ex, "OnFrameworkUpdate");
            ++_updateFailCount;
        }
    }
}
