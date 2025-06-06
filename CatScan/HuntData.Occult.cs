using System.Collections.Generic;

namespace CatScan;

public readonly struct OccultCE
{
    public readonly string CEName;
    public readonly string KCName;

    public OccultCE(string name, string kc)
    {
        CEName = name;
        KCName = kc;
    }
}

public class OccultZone
{
    public readonly OccultCE[] CEs;

    public OccultZone(params OccultCE[] ceArray)
    {
        CEs = ceArray;
    }
}

public static partial class HuntData
{
    // -- Helper function for Occult Crescent CE Data
    private static OccultZone OccultCEList(params OccultCE[] ceArray)
    {
        return new OccultZone(ceArray);
    }

    // This correlates CE/Fate names to their corresponding KC mobs
    // Also provides the structure needed to format a custom UI
    public readonly static Dictionary<int, OccultZone> OccultZones = new(){
        {1252, OccultCEList( // South Horn
            new OccultCE("Noise Complaint", "Crescent Garula"),
            new OccultCE("Company of Stone", ""),
            new OccultCE("On the Hunt", "Crescent Fan"),
            new OccultCE("Cursed Concern", ""),
            new OccultCE("Shark Attack", "Crescent Petalodite"),
            new OccultCE("With Extreme Prejudice", ""),
            new OccultCE("Trial by Claw", "Crescent Aetherscab"),
            new OccultCE("Flame of Dusk", ""),
            new OccultCE("From Times Bygone", "Crescent Byblos"),
            new OccultCE("The Black Regiment", "Crescent Panther"),
            new OccultCE("Scourge of the Mind", "Crescent Monk"),
            new OccultCE("Crawling Death", ""),
            new OccultCE("The Unbridled", ""),
            new OccultCE("Eternal Watch", ""),
            new OccultCE("Calamity Bound", "Crescent Inkstain")
        )},
    };

    public readonly static HashSet<string> OccultLargeScaleBattles = new HashSet<string>{
        "The Forked Tower: Blood"
    };

    public static bool IsOccultZone(int zoneId)
    {
        return OccultZones.ContainsKey(zoneId);
    }
}
