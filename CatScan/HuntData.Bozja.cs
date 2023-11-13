using System.Collections.Generic;

namespace CatScan;

public enum BozjaEventType
{
    FATE,
    CE,
    Duel
}

public readonly struct BozjaCE
{
    public readonly int Zone;
    public readonly string CEName;
    public readonly string KCName;
    public readonly (BozjaEventType, string) Event1;
    public readonly (BozjaEventType, string)? Event2;
    public readonly (BozjaEventType, string)? Event3;
    public readonly (BozjaEventType, string)? Event4;

    public BozjaCE(int zone, string name, string kc, params (BozjaEventType, string)[] args)
    {
        Zone = zone;
        CEName = name;
        KCName = kc;
    }
}

public class BozjaZone
{
    public readonly BozjaCE[] CEs;

    public BozjaZone(params BozjaCE[] ceArray)
    {
        CEs = ceArray;
    }
}

public static partial class HuntData
{
    // -- Helper function for Bozja CE Data
    private static BozjaZone BozjaCEList(params BozjaCE[] ceArray)
    {
        return new BozjaZone(ceArray);
    }

    // This correlates CE/Fate names to their corresponding KC mobs
    // Also provides the structure needed to format a custom UI
    public readonly static Dictionary<int, BozjaZone> BozjaZones = new(){
        {920, BozjaCEList( // Bozjan Southern Front
            new BozjaCE(1, "The Shadow of Death's Hand", "4th Legion Roader",
                (BozjaEventType.CE, "The Shadow of Death's Hand")
            ),
            new BozjaCE(1, "The Baying of the Hound(s)", "4th Legion Death Claw",
                (BozjaEventType.CE, "The Baying of the Hound(s)")
            ),
            new BozjaCE(1, "Kill It with Fire", "4th Legion Nimrod",
                (BozjaEventType.FATE, "All Pets Are Off"),
                (BozjaEventType.CE, "Kill It with Fire")
            ),
            new BozjaCE(1, "Vigil for the Lost", "4th Legion Slasher",
                (BozjaEventType.FATE, "More Machine Now than Man"),
                (BozjaEventType.CE, "Vigil for the Lost"),
                (BozjaEventType.Duel, "Aces High")
            ),

            new BozjaCE(2, "The Final Furlong", "4th Legion Avenger",
                (BozjaEventType.FATE, "Unicorn Flakes"),
                (BozjaEventType.CE, "The Final Furlong")
            ),
            new BozjaCE(2, "Patriot Games", "4th Legion Gunship",
                (BozjaEventType.CE, "The Fires of War"),
                (BozjaEventType.CE, "Patriot Games")
            ),
            new BozjaCE(2, "The Hunt for Red Choctober", "4th Legion Vanguard",
                (BozjaEventType.FATE, "Red (Chocobo) Alert"),
                (BozjaEventType.CE, "The Hunt for Red Choctober"),
                (BozjaEventType.Duel, "Beast of Man")
            ),

            new BozjaCE(3, "Rise of the Robots", "4th Legion Hexadrone",
                (BozjaEventType.FATE, "I'm a Mechanical Man"),
                (BozjaEventType.CE, "Rise of the Robots")
            ),
            new BozjaCE(3, "Where Strode the Behemoth", "4th Legion Scorpion",
                (BozjaEventType.CE, "Trampled under Hoof"),
                (BozjaEventType.CE, "Where Strode the Behemoth")
            ),
            new BozjaCE(3, "Metal Fox Chaos", "4th Legion Nimrod",
                (BozjaEventType.FATE, "For Absent Friends"),
                (BozjaEventType.FATE, "Of Steel and Flame"),
                (BozjaEventType.CE, "Metal Fox Chaos"),
                (BozjaEventType.Duel, "And the Flames Went Higher")
            )
        )},

        {975, BozjaCEList( // Zadnor
            new BozjaCE(1, "With Diremite and Main", "4th Legion Nimrod",
                (BozjaEventType.CE, "With Diremite and Main")
            ),
            new BozjaCE(1, "From Beyond the Grave", "4th Legion Infantry",
                (BozjaEventType.CE, "From Beyond the Grave")
            ),
            new BozjaCE(1, "On Serpent's Wings", "4th Legion Gunship",
                (BozjaEventType.FATE, "An Immoral Dilemma"),
                (BozjaEventType.CE, "On Serpent's Wings")
            ),
            new BozjaCE(1, "Vigil for the Lost", "4th Legion Hexadrone",
                (BozjaEventType.FATE, "Another Pilot Episode"),
                (BozjaEventType.CE, "A Familiar Face"),
                (BozjaEventType.Duel, "The Broken Blade")
            ),

            new BozjaCE(2, "There Would Be Blood", "4th Legion Satellite",
                (BozjaEventType.CE, "There Would Be Blood")
            ),
            new BozjaCE(2, "Time to Burn", "4th Legion Colossus",
                (BozjaEventType.CE, "Time to Burn")
            ),
            new BozjaCE(2, "Here Comes The Cavalry", "4th Legion Armored Weapon",
                (BozjaEventType.FATE, "Tanking Up"),
                (BozjaEventType.CE, "Here Comes The Cavalry")
            ),
            new BozjaCE(2, "Never Cry Wolf", "4th Legion Death Machine",
                (BozjaEventType.FATE, "An End to Atrocities"),
                (BozjaEventType.CE, "Never Cry Wolf"),
                (BozjaEventType.Duel, "Head of the Snake")
            ),

            new BozjaCE(3, "Lean, Mean, Magitek Machines", "4th Legion Helldiver",
                (BozjaEventType.CE, "Lean, Mean, Magitek Machines")
            ),
            new BozjaCE(3, "Looks to Die For", "4th Legion Cavalry",
                (BozjaEventType.CE, "Looks to Die For")
            ),
            new BozjaCE(3, "Worn to a Shadow", "4th Legion Roader",
                (BozjaEventType.FATE, "The Beasts are Back"),
                (BozjaEventType.CE, "Worn to a Shadow")
            ),
            new BozjaCE(3, "Feeling the Burn", "4th Legion Rearguard",
                (BozjaEventType.FATE, "Hypertuned Havoc"),
                (BozjaEventType.FATE, "Attack of the Supersoldiers"),
                (BozjaEventType.CE, "Feeling the Burn"),
                (BozjaEventType.Duel, "Taking the Lyon's Share")
            )
        )}
    };

    public readonly static HashSet<string> LargeScaleBattles = new HashSet<string>{
        "The Battle of Castrum Lacus Litore",
        "The Dalriada"
    };
/*

            KC("4th Legion "),
            KC("4th Legion "),
            KC("4th Legion "),
            KC("4th Legion "),

            KC("4th Legion "),
            KC("4th Legion "),
            KC("4th Legion "),
            KC("4th Legion "),

            KC("4th Legion "),
            KC("4th Legion Cavalry"),
            KC("4th Legion Roader"),
            KC("4th Legion Rearguard")
*/
}
