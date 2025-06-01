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
            new OccultCE("Noise Complaint", "Crescent Garula"), // Lv1
            new OccultCE("Company of Stone", "Crescent Marolith"), // Lv4
            new OccultCE("On the Hunt", "Crescent Fan"), // Lv5
            new OccultCE("Cursed Concern", "Crescent Karlabos"), // Lv6
            new OccultCE("Shark Attack", "Crescent Petalodite"), // Lv7
            new OccultCE("With Extreme Prejudice", "Crescent Uragnite"), // Lv8
            new OccultCE("Flame of Dusk", "???"), // Lv 11/12 ?
            new OccultCE("Trial by Claw", "???"), // Lv 12 ?
            new OccultCE("From Times Bygone", "Crescent Byblos"), // Lv13
            new OccultCE("The Black Regiment", "Crescent Panther"), // Lv14
            new OccultCE("Scourge of the Mind", "Crescent Monk"), // Lv15
            new OccultCE("Crawling Death", "Crescent Blackguard"), // Lv16
            new OccultCE("The Unbridled", "Crescent Demon Pawn"), // Lv17
            new OccultCE("Calamity Bound", "Crescent Inkstain"), // Lv20
            new OccultCE("Eternal Watch", "???") // Lv20/21 ?
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
