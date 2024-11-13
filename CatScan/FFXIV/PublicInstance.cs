using System.Runtime.InteropServices;

namespace CatScan.FFXIV;

// Temporary for Patch 7.1
// Substitute for FFXIVClientStructs.FFXIV.Client.Game.UI.PublicInstance

[StructLayout(LayoutKind.Explicit, Size = 0x9C)]
public unsafe struct PublicInstance {
    [FieldOffset(0x98)] public uint InstanceId;
}
