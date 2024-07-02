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

[StructLayout(LayoutKind.Explicit, Size = 0x1B28)]
public unsafe partial struct DynamicEventManager
{
    public const int TableSize = 16;
    [FieldOffset(0x8)] public fixed byte Events[0x1B0 * TableSize];
    [FieldOffset(0x1B26)] public sbyte CurrentEventIdx; // -1 or index of registered/deployed DynamicEvent

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
                addr = DalamudService.SigScanner.ScanText("E8 ?? ?? ?? ?? 48 89 45 8F 4C 8B E8");
            }
            catch (Exception) { }

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

// XXX: Some of these field offsets may no longer be accurate as of 7.0
[StructLayout(LayoutKind.Explicit, Size = 0x1B8)]
public unsafe partial struct DynamicEvent
{
    [FieldOffset(0x40)] public uint QuestId; // from Excel field -- Quest required to engage with the event
    [FieldOffset(0x44)] public uint LogMessageId; // from Excel field -- Message ID displayed when event begins
    [FieldOffset(0x4E)] public byte DynamicEventType; // from Excel field
    [FieldOffset(0x4F)] public byte DynamicEventEnemyType; // from Excel field
    [FieldOffset(0x50)] public byte MaxCombatants; // from Excel field
    [FieldOffset(0x51)] public byte DurationMins; // from Excel field -- Probably duration in minutes, always 20
    [FieldOffset(0x52)] public byte LargeScaleBattleId; // from Excel field (CLL=1, Dalriada=5)
    [FieldOffset(0x53)] public byte SingleBattleId; // from Excel field (references DynamicEventSingleBattle)
    [FieldOffset(0x54)] public int FinishTimeEpoch; // Updates at the start of each phase
    [FieldOffset(0x58)] public int SecondsRemaining; // Counts down only once the battle has actually begun, otherwise 0 -- doesn't count down for CLL unless inside
    [FieldOffset(0x5C)] public short Duration; // Seems to always be 1200?
    [FieldOffset(0x60)] public ushort DynamicEventId; // Excel ID
    [FieldOffset(0x63)] public DynamicEventState State;
    [FieldOffset(0x64)] public DynamicEventRegistrationState RegistrationState;
    [FieldOffset(0x65)] public byte NumCombatants;
    [FieldOffset(0x66)] public byte Progress; // 0-100 -- doesn't update for CLL unless inside
    [FieldOffset(0x68)] public Utf8String Name;
    [FieldOffset(0xD0)] public Utf8String Description;
    [FieldOffset(0x138)] public uint MapIconId;
    [FieldOffset(0x170)] public Vector3 Position;
}
