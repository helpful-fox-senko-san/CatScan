using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Fates;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using CatScan.FFXIV;

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
    public uint NameId;
    // We use the English name to match against known hunt data
    // But present the client-localized name to the user
    public string Name => GetName();
    public string EnglishName => GetEnglishName();
    // raw world coordinates
    public float X;
    public float Z;
    public float HpPct;

    // This should be set to inform the GameScanner to actively poll information about this enemy
    public bool Interesting = false;
    // Set this instead to only poll for death
    public bool InterestingKC = false;

    // Milliseconds since the enemy was last seen in the object table
    // Since the table is scanned progressively, this will fluctuate between approximately 0-100ms
    public double OffscreenTimeMS = 0.0;

    // XXX: This is public -- non-English clients set this to the NPC's rendered name
    public string? _cachedName;
    private string? _cachedEnglishName;

    private string GetName()
    {
        if (_cachedName != null)
            return _cachedName;

        // Non-English clients should have set _cachedName already
        return GetEnglishName();
    }

    private string GetEnglishName()
    {
        if (_cachedEnglishName != null)
            return _cachedEnglishName;

        _cachedEnglishName = GameData.GetBNpcName(NameId) ?? $"#{NameId}";

        if (GameData.IsEnglish)
            _cachedName = _cachedEnglishName;

        return _cachedEnglishName;
    }
}

public class GameFate
{
    public virtual bool IsCE => false;
    public uint FateId;
    public string Name => GetName();
    public string EnglishName => GetEnglishName();
    // raw world coordinates
    public float X;
    public float Z;
    public float ProgressPct;
    public FateState State;
    public System.DateTime? EndTimeUtc;

    // Milliseconds since the fate was last seen in the fate table
    public double OffscreenTimeMS = 0.0;

    // XXX: This is public -- non-English clients set this to the FATE's rendered name
    public string? _cachedName;
    private string? _cachedEnglishName;

    protected virtual string GetName()
    {
        if (_cachedName != null)
            return _cachedName;

        // Non-English clients should have set _cachedName already
        return GetEnglishName();
    }

    protected virtual string GetEnglishName()
    {
        if (_cachedEnglishName != null)
            return _cachedEnglishName;

        _cachedEnglishName = GameData.GetFateName(FateId) ?? $"#{FateId}";

        if (GameData.IsEnglish)
            _cachedName = _cachedEnglishName;

        return _cachedEnglishName;
    }
}

// CEs operate pretty much the same way as fates, so they present using the same interface
public class GameCE : GameFate
{
    public override bool IsCE => true;
    public uint DynamicEventId;

    private string? _cachedEnglishName;

    protected override string GetEnglishName()
    {
        if (_cachedEnglishName != null)
            return _cachedEnglishName;

        _cachedEnglishName = GameData.GetCEName(DynamicEventId) ?? $"#{DynamicEventId}";

        if (GameData.IsEnglish)
            _cachedName = _cachedEnglishName;

        return _cachedEnglishName;
    }
}

// Scans the game state for the current zone and visible enemies
public class GameScanner : IDisposable
{
    private readonly Dictionary<uint, GameEnemy> _enemyCache = new();
    private readonly Dictionary<uint, GameFate> _fateCache = new();
    private readonly Dictionary<uint, GameCE> _ceCache = new();
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
    private Channel<Action> _emitChannel = Channel.CreateUnbounded<Action>();

    private int _updateFailCount = 0;
    private bool _frameworkUpdateRegistered = false;

    private double _deltaMs = 0.0;
    private int _nextIdx = 0;
    private int _worldId = -1;
    private string _worldName = string.Empty;
    private int _zoneId = -1;
    private int _instance = -1;
    private bool _betweenAreas = false;
    private bool _betweenZones = false;
    private bool _territoryChanged = false;
    private bool _scanningEnabled = false;

    public bool BetweenAreas => _betweenAreas;
    public bool BetweenZones => _betweenZones;

    // Expose some state for debugging
    internal int EnemyCacheSize => _enemyCache.Count;
    internal int FateCacheSize => _fateCache.Count;
    internal int LostIdsSize => _lostIds.Count;
    internal bool TerritoryChanged => _territoryChanged;
    internal bool ScanningEnabled => _scanningEnabled;
    internal bool FrameworkUpdateRegistered => _frameworkUpdateRegistered;
    internal Dictionary<uint, GameEnemy> EnemyCache => _enemyCache;
    internal Dictionary<uint, GameFate> FateCache => _fateCache;
    internal HashSet<uint> LostIds => _lostIds;

    internal record struct ScannerStats
    {
        internal int ScanTicks;
        internal int ScanFateTicks;
        internal int ObjectTableRows;
        internal int FateTableRows;
        internal int GameStringReads;
        internal int EmittedEvents;

        internal static ScannerStats Subtract(ScannerStats a, ScannerStats b)
        {
            return new(){
                ScanTicks = a.ScanTicks - b.ScanTicks,
                ScanFateTicks = a.ScanFateTicks - b.ScanFateTicks,
                ObjectTableRows = a.ObjectTableRows - b.ObjectTableRows,
                FateTableRows = a.FateTableRows - b.FateTableRows,
                GameStringReads = a.GameStringReads - b.GameStringReads,
                EmittedEvents = a.EmittedEvents - b.EmittedEvents,
            };
        }
    }

    private ScannerStats _stats;
    private ScannerStats _stats1sec;
    private ScannerStats _statsPrev;
    internal ScannerStats Stats => _stats;
    internal ScannerStats Stats1Sec => _stats1sec;

    private bool emitReleaseFlag = false;
    private object emitReleaseSignal = new();

    // Because Draw runs immediately after FrameworkUpdate, and event handlers
    // will contend with it for the HuntModel lock, PulseEmitQueue() will be called
    // after Draw() is complete to ensure the event handlers will run in the time
    // between frames, rather than potentially stalling the framework thread.
    public void PulseEmitQueue()
    {
        lock (emitReleaseSignal)
        {
            emitReleaseFlag = true;
            Monitor.Pulse(emitReleaseSignal);
        }
    }

    private void WaitEmitQueue()
    {
        lock (emitReleaseSignal)
        {
            while (!emitReleaseFlag)
                Monitor.Wait(emitReleaseSignal, 1);
            emitReleaseFlag = false;
        }
    }

    private void EmitNewEnemy(GameEnemy enemy)
    {
        ++_stats.EmittedEvents;
        _emitChannel.Writer.TryWrite(() => { NewEnemy?.Invoke(enemy); });
    }

    private void EmitLostEnemy(GameEnemy enemy)
    {
        ++_stats.EmittedEvents;
        _emitChannel.Writer.TryWrite(() => { LostEnemy?.Invoke(enemy); });
    }

    private void EmitUpdatedEnemy(GameEnemy enemy)
    {
        ++_stats.EmittedEvents;
        _emitChannel.Writer.TryWrite(() => { UpdatedEnemy?.Invoke(enemy); });
    }

    private void EmitFate(GameFate fate)
    {
        ++_stats.EmittedEvents;
        _emitChannel.Writer.TryWrite(() => { NewOrUpdatedFate?.Invoke(fate); });
    }

    private void EmitLostFate(GameFate fate)
    {
        ++_stats.EmittedEvents;
        _emitChannel.Writer.TryWrite(() => { LostFate?.Invoke(fate); });
    }

    private void EmitZoneChange(GameZoneInfo zoneInfo)
    {
        ++_stats.EmittedEvents;
        _emitChannel.Writer.TryWrite(() => { ZoneChange?.Invoke(zoneInfo); });
    }

    // Snapshot stat count changes every second
    private void StartStatsTask()
    {
        var statsTaskTimer = new PeriodicTimer(System.TimeSpan.FromSeconds(1));

        Task.Run(async () => {
            while (await statsTaskTimer.WaitForNextTickAsync(_disposalCts.Token))
            {
                _stats1sec = ScannerStats.Subtract(_stats, _statsPrev);
                _statsPrev = _stats;

            }
        }, _disposalCts.Token);
    }

    // Events are queued and emitted as a task to avoid blocking the game while running logic
    private void StartEmitTask()
    {
        var emitChannelReader = _emitChannel.Reader;

        Task.Run(async () => {
            var isDisposed = () => _disposalCts.Token.IsCancellationRequested;

            while (!isDisposed())
            {
                while (emitChannelReader.TryRead(out var action))
                {
                    try
                    {
                        action();
                    }
                    catch (Exception ex)
                    {
                        DalamudService.Log.Error(ex, "Event handler exception");
                    }
                }

                await emitChannelReader.WaitToReadAsync(_disposalCts.Token);

                WaitEmitQueue();
            }
        }, _disposalCts.Token);
    }

    // Called

    public GameScanner()
    {
        RegisterFrameworkUpdate();
        DalamudService.ClientState.Login += OnLogin;
        DalamudService.ClientState.Logout += OnLogout;
        DalamudService.ClientState.TerritoryChanged += OnTerritoryChanged;
        DalamudService.Condition.ConditionChange += OnConditionChange;

        // Trigger an initial update based on the current territory
        _territoryChanged = true;

        StartStatsTask();
        StartEmitTask();
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
        _emitChannel.Writer.Complete();
        _disposalCts.Cancel();
        PulseEmitQueue();
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

        _instance = (int)uistate->PublicInstance.InstanceId;

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
        ++_stats.GameStringReads;
    }

    // Clear the current zone's information and cached enemy data
    public void ClearCache(bool emitZoneChange = true)
    {
        DalamudService.Framework.RunOnFrameworkThread(() => {
            _enemyCache.Clear();
            _fateCache.Clear();
            _lostIds.Clear();
            RegisterFrameworkUpdate();

            // Emitting a zone change lets HuntScanner clear its own tracking data
            if (emitZoneChange)
            {
                EmitZoneChange(new GameZoneInfo(){
                    WorldId = _worldId,
                    ZoneId = _zoneId,
                    Instance = _instance,
                    WorldName = _worldName
                });
            }
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
                value = false;

            _betweenZones = value;

            if (!_betweenZones)
                RegisterFrameworkUpdate();
        }
        else if (flag == ConditionFlag.BetweenAreas)
        {
            // Can't open the map while between areas, so track this too
            _betweenAreas = value;
        }
    }

    // Called by ScanTick in Framework thread
    // Creates a cache entry for a battle NPC, (optionally) emits a NewEnemy event, and returns the cache entry
    private GameEnemy DoNewEnemy(IBattleNpc bnpc, uint id, bool wasLost = false)
    {
        // New enemy
        var pos = bnpc.Position;
        var hpPct = 100.0f;
        var maxHp = bnpc.MaxHp;

        if (maxHp > 0)
            hpPct = (float)bnpc.CurrentHp / (float)maxHp * 100.0f;

        var newEnemy = new GameEnemy(){
            ObjectId = id,
            NameId = bnpc.NameId,
            _cachedName = GameData.IsEnglish ? null : bnpc.Name.ToString(),
            X = pos.X,
            Z = pos.Z,
            HpPct = hpPct
        };
        ++_stats.GameStringReads;

        _enemyCache.Add(id, newEnemy);

        // Do not emit a new enemy event if this was an already known but lost enemy -- instead DoUpdateEnemy will emit an event
        if (!wasLost)
            EmitNewEnemy(newEnemy);

        return newEnemy;
    }

    // Called by ScanTick in Framework thread
    // Updates or re-creates a cache entry from a battle NPC, (optionally) emits an UpdatedEnemy event, and returns the cache entry
    // The enemy parameter may be null, in which case DoNewEnemy() is called to insert a new cache entry
    private GameEnemy DoUpdateEnemy(IBattleNpc bnpc, uint id, GameEnemy? cachedEnemy)
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

            if (cachedEnemy.HpPct != 0.0f && isDead)
            {
                cachedEnemy.HpPct = hpPct = 0.0f;
                dirty = true;
            }

            if (float.Abs(hpPct - cachedEnemy.HpPct) >= 0.1f)
            {
                cachedEnemy.HpPct = hpPct;
                dirty = dirty || cachedEnemy.Interesting;
            }
        }

        // Check the position of hunt marks (KC mobs don't need their position tracked)
        if (cachedEnemy.Interesting)
        {
            var pos = bnpc.Position;

            if (float.Abs(pos.X - cachedEnemy.X) >= 1.0f)
            {
                cachedEnemy.X = pos.X;
                dirty = true;
            }

            if (float.Abs(pos.Z - cachedEnemy.Z) >= 1.0f)
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
        ++_stats.ScanTicks;
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

        _stats.ObjectTableRows += n;
        for (; i < n; next())
        {
            var idx = _nextIdx;
            var obj = DalamudService.ObjectTable[idx];

            if (obj == null)
                continue;

            var bnpc = (obj as IBattleNpc);

            if (bnpc == null || bnpc.BattleNpcKind != BattleNpcSubKind.Enemy)
                continue;

            var id = obj.EntityId;

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
        ++_stats.ScanFateTicks;
        _stats.FateTableRows += DalamudService.FateTable.Length;
        foreach (var fate in DalamudService.FateTable)
        {
            var id = fate.FateId;
            var state = fate.State;

            if (_fateCache.TryGetValue(id, out var cachedFate))
            {
                var dirty = false;

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
                    dirty = true;
                }

                if (state != FateState.Preparation)
                {
                    var pct = (float)fate.Progress;

                    if (float.Abs(cachedFate.ProgressPct - pct) >= 0.1f)
                    {
                        cachedFate.ProgressPct = pct;
                        dirty = true;
                    }
                }

                if (dirty)
                    EmitFate(cachedFate);
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
                    _cachedName = GameData.IsEnglish ? null : fate.Name.ToString(),
                    X = pos.X,
                    Z = pos.Z,
                    EndTimeUtc = state == FateState.Preparation ? null : System.DateTimeOffset.FromUnixTimeSeconds(startTime + duration).UtcDateTime,
                    State = state
                };
                ++_stats.GameStringReads;

                _fateCache.Add(id, cachedFate);
                EmitFate(cachedFate);
            }
        }

        // Scan the fate cache for missing fates to consider finished
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

    // Called in Framework thread while zone ID is complete and scanning is enabled
    private unsafe void ScanCETick()
    {
        var dynamicEventManager = DynamicEventManager.GetDynamicEventManager();

        // Not in Bozja/Zadnor
        if (dynamicEventManager == null)
            return;

        _stats.FateTableRows += DynamicEventManager.TableSize;

        for (int i = 0; i < DynamicEventManager.TableSize; ++i)
        {
            var ce = dynamicEventManager->GetEvent(i);
            var id = ce->DynamicEventId;
            var ceState = ce->State;
            var state = ceState switch {
                DynamicEventState.NotActive => FateState.Ended,
                DynamicEventState.BattleUnderway => FateState.Running,
                _ => FateState.Preparation,
            };

            if (ce->Progress == 100)
                state = FateState.Ended;

            if (_fateCache.TryGetValue(id, out var cachedFate))
            {
                var dirty = false;

                if (state == FateState.Ended)
                {
                    cachedFate.State = FateState.Ended;
                    cachedFate.ProgressPct = 100.0f;
                    _fateCache.Remove(id);
                    EmitLostFate(cachedFate);
                    continue;
                }

                // Mark existing fate as being seen
                cachedFate.OffscreenTimeMS = 0.0f;

                var endTime = System.DateTimeOffset.FromUnixTimeSeconds(ce->FinishTimeEpoch).UtcDateTime;

                if (cachedFate.State != state)
                {
                    cachedFate.State = state;
                    dirty = true;
                }

                var pct = (float)ce->Progress;

                // Game inconsistently/accidentally sends information about progress
                if (ce->LargeScaleBattleId != 0 && dynamicEventManager->CurrentEventIdx != i && pct != 100.0f)
                    pct = 0.0f;

                if (float.Abs(cachedFate.ProgressPct - pct) >= 0.1f)
                {
                    cachedFate.ProgressPct = pct;
                    dirty = true;
                }

                if (endTime != cachedFate.EndTimeUtc)
                {
                    cachedFate.EndTimeUtc = endTime;
                    dirty = true;
                }

                if (dirty)
                    EmitFate(cachedFate);
            }
            else
            {
                if (state == FateState.WaitingForEnd || state == FateState.Ended)
                    continue;

                var endTime = ce->FinishTimeEpoch;
                var pos = ce->Position;

                cachedFate = new GameCE(){
                    FateId = 0,
                    DynamicEventId = id,
                    _cachedName = GameData.IsEnglish ? null : ce->Name.ToString(),
                    X = pos.X,
                    Z = pos.Z,
                    EndTimeUtc = state == FateState.Preparation ? null : System.DateTimeOffset.FromUnixTimeSeconds(endTime).UtcDateTime,
                    State = state
                };
                ++_stats.GameStringReads;

                _fateCache.Add(id, cachedFate);
                EmitFate(cachedFate);
            }
        }
    }

    // DoFrameworkUpdate
    private void Tick()
    {
        // No need to do anything while not logged in
        // If between areas, a territory change is probably about to happen -- don't scan to avoid mixing up zones
        if (!DalamudService.ClientState.IsLoggedIn || (_betweenZones && !_territoryChanged))
        {
            _territoryChanged = true;
            _worldId = -1;
            _zoneId = -1;
            _instance = -1;
            ClearCache(false);
            UnregisterFrameworkUpdate();
            return;
        }

        // Clear the enemy list and emit a zone change event
        if (_territoryChanged || _worldId < 0 || _zoneId < 0 || _instance < 0)
        {
            // Avoid flickering back and forth between states
            if (_betweenZones)
                return;

            ClearCache(false);

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

        // Don't scan anything until we've loaded bnpc and fate name data
        if (!GameData.NameDataReady)
            return;

        // Accumulate time if the update fails for a temporary reason
        _deltaMs += System.Math.Clamp(DalamudService.Framework.UpdateDelta.TotalMilliseconds, 1.0, 100.0);

        if (double.IsNaN(_deltaMs))
            _deltaMs = 100.0;

        ScanTick();
        ScanCETick();
        ScanFateTick();

        _deltaMs = 0.0;
    }

    private void UnregisterFrameworkUpdate()
    {
        // Disabled due to rare bug
        /*
        if (!_frameworkUpdateRegistered)
            return;
        DalamudService.Framework.Update -= OnFrameworkUpdate;
        _frameworkUpdateRegistered = false;
        */
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
            DalamudService.Framework.Update -= OnFrameworkUpdate;
            _frameworkUpdateRegistered = false;
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
