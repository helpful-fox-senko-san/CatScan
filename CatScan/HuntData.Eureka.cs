using System.Collections.Generic;

namespace CatScan;

public readonly struct EurekaNM
{
    public readonly int Level;
    public readonly string NMName;
    public readonly string KCName;
    public readonly string FateName;

    public EurekaNM(int lvl, string nm, string kc, string fate)
    {
        Level = lvl;
        NMName = nm;
        KCName = kc;
        FateName = fate;
    }
}

public class EurekaZone
{
    public readonly EurekaNM[] NMs;

    public EurekaZone(params EurekaNM[] nmArray)
    {
        NMs = nmArray;
    }
}

public static partial class HuntData
{
    // -- Helper function for Eureka NM<->KC Data
    private static EurekaZone EurekaNMList(params EurekaNM[] nmArray)
    {
        return new EurekaZone(nmArray);
    }

    // This correlates Eureka NM data for the inegrated Eureka Tracker feature
    public readonly static Dictionary<int, EurekaZone> EurekaZones = new(){
        {732, EurekaNMList( // Anemos
            new EurekaNM(1, "Sabotender Corrido", "Flowering Sabotender", "Unsafety Dance"),
            new EurekaNM(2, "The Lord of Anemos", "Sea Bishop", "The Shadow over Anemos"),
            new EurekaNM(3, "Teles", "Anemos Harpeia", "Teles House"),
            new EurekaNM(4, "The Emperor of Anemos", "Darner", "The Swarm Never Sets"),
            new EurekaNM(5, "Callisto", "Val Bear", "One Missed Callisto"),
            new EurekaNM(6, "Number", "Pneumaflayer", "By Numbers"),
            new EurekaNM(7, "Jahannam", "Typhoon Sprite", "Disinherit the Wind"),
            new EurekaNM(8, "Amemet", "Abraxas", "Prove Your Amemettle"),
            new EurekaNM(9, "Caym", "Stalker Ziz", "Caym What May"),
            new EurekaNM(10, "Bombadeel", "Traveling Gourmand", "The Killing of a Sacred Bombardier"),
            new EurekaNM(11, "Serket", "Khor Claw", "Short Serket 2"),
            new EurekaNM(12, "Judgmental Julika", "Henbane", "Don't Judge Me, Morbol"),
            new EurekaNM(13, "The White Rider", "Duskfall Dullahan", "When You Ride Alone"),
            new EurekaNM(14, "Polyphemus", "Monoeye", "Sing, Muse"),
            new EurekaNM(15, "Simurgh's Strider", "Old World Zu", "Simurghasbord"),
            new EurekaNM(16, "King Hazmat", "Anemos Anala", "To the Mat"),
            new EurekaNM(17, "Fafnir", "Fossil Dragon", "Wine and Honey"),
            new EurekaNM(18, "Amarok", "Voidscale", "I Amarok"),
            new EurekaNM(19, "Lamashtu", "Val Specter", " Drama Lamashtu"), // This fate really does have a space at the start of the name
            new EurekaNM(20, "Pazuzu", "Shadow Wraith", "Wail in the Willows")
        )},

        {763, EurekaNMList( // Pagos
            new EurekaNM(20, "The Snow Queen", "Yukinko", "Eternity"),
            new EurekaNM(21, "Taxim", "Demon of the Incunable", "Cairn Blight 451"),
            new EurekaNM(22, "Ash Dragon", "Blood Demon", "Ash the Magic Dragon"),
            new EurekaNM(23, "Glavoid", "Val Worm", "Conqueror Worm"),
            new EurekaNM(24, "Anapos", "Snowmelt Sprite", "Melting Point"),
            new EurekaNM(25, "Hakutaku", "Blubber Eyes", "The Wobbler in Darkness"),
            new EurekaNM(26, "King Igloo", "Huwasi", "Does It Have to Be a Snowman"),
            new EurekaNM(27, "Asag", "Wandering Open", "Disorder in the Court"),
            new EurekaNM(28, "Surabhi", "Pagos Billygoat", "Cows for Concern"),
            new EurekaNM(29, "King Arthro", "Val Snipper", "Morte Arthro"),
            new EurekaNM(30, "Mindertaur", "Lab Minotaur", "Brothers"), // Second boss: Eldertaur
            new EurekaNM(31, "Holy Cow", "Elder Buffalo", "Apocalypse Cow"),
            new EurekaNM(32, "Hadhayosh", "Lesser Void Dragon", "Third Impact"),
            new EurekaNM(33, "Horus", "Void Vouivre", "Eye of Horus"),
            new EurekaNM(34, "Arch Angra Mainyu", "Gawper", "Eye Scream for Ice Cream"),
            new EurekaNM(35, "Copycat Cassie", "Ameretat", "Cassie and the Copycats"),
            new EurekaNM(35, "Louhi", "Val Corpse", "Louhi on Ice")
        )},

        {795, EurekaNMList( // Pyros
            new EurekaNM(35, "Leucosia", "Pyros Bhoot", "Medias Res"),
            new EurekaNM(36, "Flauros", "Thunderstorm Sprite", "High Voltage"),
            new EurekaNM(37, "The Sophist", "Pyros Apanda", "On the Nonexistent"),
            new EurekaNM(38, "Graffiacane", "Valking", "Creepy Doll"),
            new EurekaNM(39, "Askalaphos", "Overdue Tome", "Quiet, Please"),
            new EurekaNM(40, "Grand Duke Batym", "Dark Troubadour", "Up and Batym"),
            new EurekaNM(41, "Aetolus", "Islandhander", "Rondo Aetolus"),
            new EurekaNM(42, "Lesath", "Bird Eater", "Scorchpion King"),
            new EurekaNM(43, "Eldthurs", "Pyros Crab", "Burning Hunger"),
            new EurekaNM(44, "Iris", "Northern Swallow", "Dry Iris"),
            new EurekaNM(45, "Lamebrix Strikebocks", "Illuminati Escapee", "Thirty Whacks"),
            new EurekaNM(46, "Dux", "Matanga Castaway", "Put Up Your Dux"),
            new EurekaNM(47, "The Weeping Willow", "Pyros Treant", "You Do Know Jack"), // Phase 2: Lumber Jack
            new EurekaNM(48, "Glaukopis", "Val Skatene", "Mister Bright-eyes"),
            new EurekaNM(49, "Ying-Yang", "Pyros Hecteyes", "Haunter of the Dark"),
            new EurekaNM(50, "Skoll", "Pyros Shuck", "Heavens' Warg"),
            new EurekaNM(50, "Penthesilea", "Val Bloodglider", "Lost Epic")
        )},

        {827, EurekaNMList( // Hydatos
            new EurekaNM(50, "Khalamari", "Xzomit", "I Ink, Therefore I Am"),
            new EurekaNM(51, "Stegodon", "Hydatos Primelephas", "From Tusk till Dawn"),
            new EurekaNM(52, "Molech", "Val Nullchu", "Bullheaded Berserker"),
            new EurekaNM(53, "Piasa", "Vivid Gastornis", "Mad, Bad, and Fabulous to Know"),
            new EurekaNM(54, "Frostmane", "Northern Tiger", "Fearful Symmetry"),
            new EurekaNM(55, "Daphne", "Dark Void Monk", "Crawling Chaos"),
            new EurekaNM(56, "King Goldemar", "Hydatos Wraith", "Duty-free"),
            new EurekaNM(57, "Leuke", "Tigerhawk", "Leukewarm Reception"),
            new EurekaNM(58, "Barong", "Laboratory Lion", "Robber Barong"),
            new EurekaNM(59, "Ceto", "Hydatos Delphyne", "Stone-cold Killer"),
            new EurekaNM(60, "Provenance Watcher", "Crystal Claw", "Crystalline Provenance"),
            new EurekaNM(60, "Ovni", "", "I Don't Want to Believe"),
            new EurekaNM(60, "Tristitia", "", "The Baldesion Arsenal: Expedition Support")
        )}
    };

    public static bool IsEurekaZone(int zoneId)
    {
        return EurekaZones.ContainsKey(zoneId);
    }
}
