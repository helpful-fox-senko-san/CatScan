using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Common.Math;

namespace CatScan.FFXIV;

public enum DynamicEventState : byte
{
    NotActive = 0,
    Registration = 1,
    Waiting = 2,
    BattleUnderway = 3
}

public enum DynamicEventRegistrationState : byte
{
    NotRegistered = 0,
    Registered = 1,
    Ready = 3,
    Deployed = 4
}

[StructLayout(LayoutKind.Explicit, Size = 0x1D80)]
public unsafe partial struct DynamicEventManager
{
    public const int TableSize = 16;
    [FieldOffset(0x8)] public fixed byte Events[0x1D0 * TableSize];
    [FieldOffset(0x1D7E)] public sbyte CurrentEventIdx; // -1 or index of registered/deployed DynamicEvent

    public unsafe DynamicEvent* GetEvent(int idx) =>
        (DynamicEvent*)((nint)Unsafe.AsPointer(ref this) + 0x08) + idx;

    private delegate DynamicEventManager* GetDynamicEventManagerDelegate();
    private static GetDynamicEventManagerDelegate? _getTableFn = null;
    private static int _getTableScanFails = 0;

    public static unsafe DynamicEventManager* GetDynamicEventManager()
    {
        if (_getTableFn == null)
        {
            // Give up its not working!
            if (_getTableScanFails > 100)
                return null;

            nint addr = 0;

            try
            {
                addr = DalamudService.SigScanner.ScanText("E8 ?? ?? ?? ?? 48 8B E8 48 85 C0 74 ?? 0F B7 56");
            }
            catch (Exception e)
            {
                DalamudService.Log.Error(e, "Failed to find DynamicEventManager");
            }

            if (addr == 0)
            {
                ++_getTableScanFails;
                return null;
            }

            _getTableFn = Marshal.GetDelegateForFunctionPointer<GetDynamicEventManagerDelegate>(addr);
        }

        return _getTableFn();
    }
}

[StructLayout(LayoutKind.Explicit, Size = 0x1D0)]
public unsafe partial struct DynamicEvent
{
    [FieldOffset(0x56)] public byte DynamicEventType; // from Excel field
    [FieldOffset(0x57)] public byte DynamicEventEnemyType; // from Excel field
    [FieldOffset(0x58)] public byte MaxCombatants; // from Excel field
    [FieldOffset(0x59)] public byte DurationMins; // from Excel field
    [FieldOffset(0x5A)] public byte LargeScaleBattleId; // from Excel field (CLL=1, Dalriada=5)
    [FieldOffset(0x5B)] public byte SingleBattleId; // from Excel field (references DynamicEventSingleBattle)
    [FieldOffset(0x60)] public int FinishTimeEpoch; // Updates at the start of each phase
    [FieldOffset(0x64)] public int SecondsRemaining; // Counts down only once the battle has actually begun, otherwise 0 -- doesn't count down for CLL unless inside
    [FieldOffset(0x68)] public short Duration; // Seems to always be 1200?
    [FieldOffset(0x74)] public ushort DynamicEventId; // Excel ID
    [FieldOffset(0x78)] public DynamicEventState State;
    [FieldOffset(0x79)] public DynamicEventRegistrationState RegistrationState;
    [FieldOffset(0x7A)] public byte NumCombatants;
    [FieldOffset(0x7B)] public byte Progress; // 0-100 -- doesn't update for CLL unless inside
    [FieldOffset(0x80)] public Utf8String Name;
    [FieldOffset(0xE8)] public Utf8String Description;
    [FieldOffset(0x150)] public uint MapIconId;
    [FieldOffset(0x170 + 0x1C)] public Vector3 Position;
}
