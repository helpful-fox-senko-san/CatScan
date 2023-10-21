using Dalamud.Game.ClientState.Conditions;
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
}

// Scans the game state for the current zone and visible enemies
public class GameScanner : IDisposable
{
    // Off-framework task for timing out enemies that are no longer visible
    private class OffscreenEnemyTask : IDisposable
    {
        // I wanted to just lock this dictionary as a whole but it didn't work right
        private readonly ConcurrentDictionary<uint, int> _offscreenList = new();
        private GameScanner _scanner;
        private CancellationTokenSource _disposalCts = new();
        private Action<uint> _action;
        private System.DateTime _lastTick = System.DateTime.UtcNow;

        public int OffscreenListSize => _offscreenList.Count;

        public OffscreenEnemyTask(GameScanner scanner, Action<uint> action)
        {
            _scanner = scanner;
            _action = action;

            Task.Run(async () => {
                while (!_disposalCts.IsCancellationRequested)
                {
                    try
                    {
                        Tick();
                    }
                    catch (Exception ex)
                    {
                        DalamudService.Log.Error(ex, "OffscreenEnemyTask");
                    }

                    await Task.Delay(100);
                }
            }, _disposalCts.Token);
        }

        private List<uint> _removeKeys = new();

        public void Tick()
        {
            var now = System.DateTime.UtcNow;
            var interval = now - _lastTick;
            var ms = (int)interval.TotalMilliseconds;
            _lastTick = now;

            foreach (var ot in _offscreenList)
            {
                // Consider an enemy gone once its been off-screen for about 500ms
                if (ot.Value >= 450)
                {
                    var id = ot.Key;
                    _removeKeys.Add(ot.Key);
                    _offscreenList[ot.Key] = int.MinValue;
                    _action(id);
                    continue;
                }

                if (ot.Value >= 0)
                    _offscreenList[ot.Key] = ot.Value + ms;
            }

            foreach (var k in _removeKeys)
                _offscreenList.Remove(k, out _);

            _removeKeys.Clear();
        }

        public void MarkSeen(uint id)
        {
            _offscreenList[id] = 0;
        }

        public void Clear()
        {
            _offscreenList.Clear();
        }

        public void Dispose()
        {
            _disposalCts.Cancel();
        }
    };

    private OffscreenEnemyTask _offscreenEnemyTask;
    private readonly Dictionary<uint, GameEnemy> _enemyCache = new();
    private readonly HashSet<uint> _lostIds = new();

    // These events are picked up by HuntScanner and processed in to logical events of its own
    public delegate void NewEnemyDelegate(GameEnemy enemy);
    public delegate void LostEnemyDelegate(GameEnemy enemy);
    public delegate void UpdatedEnemyDelegate(GameEnemy enemy);
    public delegate void ZoneChangeDelegate(GameZoneInfo zoneInfo);

    public event NewEnemyDelegate? NewEnemy;
    public event LostEnemyDelegate? LostEnemy;
    public event UpdatedEnemyDelegate? UpdatedEnemy;
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
    internal int LostIdsSize => _lostIds.Count;
    internal int OffscreenListSize => _offscreenEnemyTask.OffscreenListSize;
    internal bool BetweenAreas => _betweenAreas;
    internal bool TerritoryChanged => _territoryChanged;
    internal bool ScanningEnabled => _scanningEnabled;
    internal bool FrameworkUpdateRegistered => _frameworkUpdateRegistered;

    // Events are queued and emitted as a task to avoid blocking the game while running logic
    private void FlushEmitQueue()
    {
        if (_emitQueue.Count == 0)
            return;

        Task.Run(() => {
            Action? action;
            while (!_disposalCts.Token.IsCancellationRequested && _emitQueue.TryDequeue(out action))
                action();
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

    private void EmitZoneChange(GameZoneInfo zoneInfo)
    {
        // Zone changes are critical and infrequent, just emit them directly
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

        _offscreenEnemyTask = new(this, OnEnemyGone);
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
        _offscreenEnemyTask.Dispose();
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
            if (_enemyCache.Count > 0)
            {
                _enemyCache.Clear();
                _offscreenEnemyTask.Clear();
                lock (_lostIds)
                    _lostIds.Clear();
            }
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
        EmitZoneChange(new GameZoneInfo(){
            WorldId = _worldId,
            ZoneId = _zoneId,
            Instance = _instance,
            WorldName = _worldName
        });
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

    private void OnEnemyGone(uint id)
    {
        lock(_lostIds)
            _lostIds.Add(id);

        if (_enemyCache.ContainsKey(id) && !_enemyCache[id].Ignore)
            EmitLostEnemy(_enemyCache[id]);
    }

    // DoFrameworkUpdate - zone ID is complete and scanning is enabled
    private void ScanTick()
    {
        // Accumulate time if the update fails for a temporary reason
        _deltaMs += System.Math.Clamp(DalamudService.Framework.UpdateDelta.TotalMilliseconds, 1.0, 100.0);

        if (double.IsNaN(_deltaMs))
            _deltaMs = 100.0;

        // Process a portion of the object table each update, to reach a target of one full scan per 100ms
        const double targetMs = 100.0;
        int tableLen = DalamudService.ObjectTable.Length;

        // Number of table entries to process
        int n = (int)System.Math.Round((_deltaMs / targetMs) * tableLen);

        // Limit to one full table scan per update
        // Also ensure some reasonable minimum amount of progress is made per frame
        n = System.Math.Clamp(n, 10, tableLen);

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

            var npc = (obj as BattleNpc);

            if (npc == null || npc.BattleNpcKind != BattleNpcSubKind.Enemy)
                continue;

            var id = obj.ObjectId;
            _offscreenEnemyTask.MarkSeen(id);

            if (_enemyCache.ContainsKey(id))
            {
                // Update existing enemy
                var enemy = _enemyCache[id];
                var dirty = _lostIds.Contains(id);

                if (dirty)
                    _lostIds.Remove(id);

                if (enemy.Interesting || enemy.InterestingKC)
                {
                    var hpPct = 100.0f;
                    var maxHp = npc.MaxHp;

                    if (maxHp > 0)
                        hpPct = (float)npc.CurrentHp / (float)maxHp * 100.0f;

                    if (enemy.HpPct != 0.0f && obj.IsDead)
                    {
                        enemy.HpPct = hpPct = 0.0f;
                        dirty = true;
                    }

                    if (double.Abs(hpPct - enemy.HpPct) >= 0.1)
                    {
                        enemy.HpPct = hpPct;
                        dirty = dirty || enemy.Interesting;
                    }
                }

                if (enemy.Interesting)
                {
                    var pos = npc.Position;

                    if (double.Abs(pos.X - enemy.X) >= 1.0)
                    {
                        enemy.X = pos.X;
                        dirty = true;
                    }

                    if (double.Abs(pos.Z - enemy.Z) >= 1.0)
                    {
                        enemy.Z = pos.Z;
                        dirty = true;
                    }
                }

                if (dirty && !enemy.Ignore)
                    EmitUpdatedEnemy(enemy);
            }
            else
            {
                // New enemy
                var pos = npc.Position;
                var hpPct = 100.0f;
                var maxHp = npc.MaxHp;

                if (maxHp > 0)
                    hpPct = (float)npc.CurrentHp / (float)maxHp * 100.0f;

                string? customName = null;

                if (obj.DataId == 882)
                    customName = "Odin";

                var newEnemy = new GameEnemy(){
                    ObjectId = id,
                    Name = customName ?? npc.Name.ToString(),
                    X = pos.X,
                    Z = pos.Z,
                    HpPct = hpPct,
                    // TODO: How can I reliably detect boss fate aoe dummy objects?
                    Ignore = (npc.Level < 2 || npc.MaxHp < 2)
                };

                _enemyCache.Add(id, newEnemy);

                if (!newEnemy.Ignore)
                    EmitNewEnemy(newEnemy);
            }
        }

        _deltaMs = 0.0;
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

            _emitQueue.Clear();
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

        if (Monitor.TryEnter(_lostIds))
        {
            try
            {
                ScanTick();
            }
            finally
            {
                Monitor.Exit(_lostIds);
                FlushEmitQueue();
            }
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
            return;

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
