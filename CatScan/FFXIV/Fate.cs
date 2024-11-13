using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Common.Math;

namespace CatScan.FFXIV;

// Temporary for Patch 7.1
// Substitute for FFXIVClientStructs.FFXIV.Client.Game.Fate.FateContext

[StructLayout(LayoutKind.Explicit, Size = 0x9C)]
public unsafe struct Fate {
	[FieldOffset(0x460)] public Vector3 Location;
}
