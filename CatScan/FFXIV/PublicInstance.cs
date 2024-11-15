using System.Runtime.InteropServices;

namespace CatScan.FFXIV;

// Temporary for Patch 7.1
// Substitute for FFXIVClientStructs.FFXIV.Client.Game.UI.PublicInstance

[StructLayout(LayoutKind.Explicit, Size = 0x14)]
public unsafe struct PublicInstance {
    [FieldOffset(0x10)] public uint InstanceId;
}
