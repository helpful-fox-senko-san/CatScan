using System.Collections.Generic;

namespace CatScan;

public enum Expansion
{
    Unknown,
    ARR,
    HW,
    SB,
    ShB,
    EW,
    Eureka,
    Bozja
}

public enum Rank
{
    B,
    A,
    S,
    Minion,
    SS,
    FATE,
    KC
}

public readonly struct Mark
{
    public readonly Rank Rank;
    public readonly string Name;

    public Mark(Rank r, string n)
    {
        Rank = r;
        Name = n;
    }
}

public class Zone
{
    public readonly Expansion Expansion;
    public readonly string Name;
    public readonly int Instances;
    public readonly Mark[] Marks;

    public Zone(Expansion expansion, string name, int instances, params Mark[] marks)
    {
        Expansion = expansion;
        Name = name;
        Instances = instances;
        Marks = marks;
    }
}

public static partial class HuntData
{
    // -- Shorthand for creating Mark instances of a given rank
    private static Mark B(string name) { return new Mark(Rank.B, name); }
    private static Mark A(string name) { return new Mark(Rank.A, name); }
    private static Mark S(string name) { return new Mark(Rank.S, name); }
    private static Mark Minion(string name) { return new Mark(Rank.Minion, name); }
    private static Mark SS(string name) { return new Mark(Rank.SS, name); }
    private static Mark FATE(string name) { return new Mark(Rank.FATE, name); }
    private static Mark KC(string name) { return new Mark(Rank.KC, name); }

    // -- Shorthand for creating Zone instances for a given expansion
    private static Zone ARR_Zone(string name, int instances, params Mark[] marks)
    {
        return new Zone(Expansion.ARR, name, instances, marks);
    }

    private static Zone HW_Zone(string name, int instances, params Mark[] marks)
    {
        return new Zone(Expansion.HW, name, instances, marks);
    }

    private static Zone SB_Zone(string name, int instances, params Mark[] marks)
    {
        return new Zone(Expansion.SB, name, instances, marks);
    }

    // Add SS rank for all Shadowbringers zones
    private static Zone ShB_Zone(string name, int instances, params Mark[] marks)
    {
        var m2 = new Mark[marks.Length + 2];
        System.Array.Copy(marks, m2, marks.Length);
        m2[marks.Length] = SS("Forgiven Rebellion");
        m2[marks.Length + 1] = Minion("Forgiven Gossip");
        return new Zone(Expansion.ShB, name, instances, m2);
    }

    // Add SS rank for all Endwalker zones
    private static Zone EW_Zone(string name, int instances, params Mark[] marks)
    {
        var m2 = new Mark[marks.Length + 2];
        System.Array.Copy(marks, m2, marks.Length);
        m2[marks.Length] = SS("Ker");
        m2[marks.Length + 1] = Minion("Ker Shroud");
        return new Zone(Expansion.EW, name, instances, m2);
    }

    private static Zone Eureka_Zone(string name, params Mark[] marks)
    {
        return new Zone(Expansion.Eureka, name, 9, marks);
    }

    private static Zone Bozja_Zone(string name, params Mark[] marks)
    {
        return new Zone(Expansion.Bozja, name, 9, marks);
    }

    // -- Shorthand overloads for uninstanced maps
    private static Zone ARR_Zone(string name, params Mark[] marks) { return ARR_Zone(name, 1, marks); }
    private static Zone HW_Zone(string name, params Mark[] marks) { return HW_Zone(name, 1, marks); }
    private static Zone SB_Zone(string name, params Mark[] marks) { return SB_Zone(name, 1, marks); }
    private static Zone ShB_Zone(string name, params Mark[] marks) { return ShB_Zone(name, 1, marks); }
    private static Zone EW_Zone(string name, params Mark[] marks) { return EW_Zone(name, 1, marks); }

    public readonly static Dictionary<int, Zone> Zones = new Dictionary<int, Zone>{

    // -- La Noscea
        {134, ARR_Zone("Middle La Noscea",
            B("Skogs Fru"), A("Vogaal Ja"), S("Croque-mitaine")
        )},
        {135, ARR_Zone("Lower La Noscea",
            B("Barbastelle"), A("Unktehi"), S("Croakadile")
        )},
        {137, ARR_Zone("Eastern La Noscea",
            B("Bloody Mary"), A("Hellsclaw"), S("The Garlok")
        )},
        {138, ARR_Zone("Western La Noscea",
            B("Dark Helmet"), A("Nahn"), S("Bonnacon")
        )},
        {139, ARR_Zone("Upper La Noscea",
            B("Myradrosh"), A("Marberry"), S("Nandi")
        )},
        {180, ARR_Zone("Outer La Noscea",
            B("Vuokho"), A("Cornu"), S("Chernobog")
        )},

    // -- Thanalan
        {140, ARR_Zone("Western Thanalan",
            B("Sewer Syrup"), A("Alectryon"), S("Zona Seeker")
        )},
        {141, ARR_Zone("Central Thanalan",
            B("Ovjang"), A("Sabotender Bailarina"), S("Brontes")
        )},
        {145, ARR_Zone("Eastern Thanalan",
            B("Gatling"), A("Maahes"), S("Lampalagua")
        )},
        {146, ARR_Zone("Southern Thanalan",
            B("Albin the Ashen"), A("Zanig'oh"), S("Nunyunuwi")
        )},
        {147, ARR_Zone("Northern Thanalan",
            B("Flame Sergeant Dalvag"), A("Dalvag's Final Flame"), S("Minhocao"),
            KC("Earth Sprite")
        )},

    // -- Black Shroud
        {148, ARR_Zone("Central Shroud",
            B("White Joker"), A("Forneus"), S("Laideronnette"), FATE("Odin")
        )},
        {152, ARR_Zone("East Shroud",
            B("Stinging Sophie"), A("Melt"), S("Wulgaru"), FATE("Odin")
        )},
        {153, ARR_Zone("South Shroud",
            B("Monarch Ogrefly"), A("Ghede Ti Malice"), S("Mindflayer"), FATE("Odin")
        )},
        {154, ARR_Zone("North Shroud",
            B("Phecda"), A("Girtab"), S("Thousand-cast Theda"), FATE("Odin")
        )},

    // -- A Realm Reborn
        {155, ARR_Zone("Coerthas Central Highlands",
            B("Naul"), A("Marraco"), S("Safat"), FATE("Behemoth")
        )},
        {156, ARR_Zone("Mor Dhona",
            B("Leech King"), A("Kurrea"), S("Agrippa the Mighty")
        )},

    // -- Heavensward
        {397, HW_Zone("Coerthas Western Highlands",
            B("Alteci"), B("Kreutzet"), A("Mirka"), A("Lyuba"), S("Kaiser Behemoth")
        )},
        {398, HW_Zone("The Dravanian Forelands",
            B("Gnath Cometdrone"), B("Thextera"), A("Pylraster"), A("Lord of the Wyverns"), S("Senmurv"), FATE("Coeurlregina")
        )},
        {399, HW_Zone("The Dravanian Hinterlands",
            B("Pterygotus"), B("False Gigantopithecus"), A("Slipkinx Steeljoints"), A("Stolas"), S("The Pale Rider")
        )},
        {400, HW_Zone("The Churning Mists",
            B("Scitalis"), B("The Scarecrow"), A("Bune"), A("Agathos"), S("Gandarewa")
        )},
        {401, HW_Zone("The Sea of Clouds",
            B("Squonk"), B("Sanu Vali of Dancing Wings"), A("Enkelados"), A("Sisiutl"), S("Bird of Paradise")
        )},
        {402, HW_Zone("Azys Lla",
            B("Lycidas"), B("Omni"), A("Campacti"), A("Stench Blossom"), S("Leucrotta"), FATE("Noctilucale"),
            KC("Allagan Chimera"), KC("Lesser Hydra"), KC("Meracydian Vouivre")
        )},

    // -- Stormblood
        {612, SB_Zone("The Fringes",
            B("Shadow-dweller Yamini"), B("Ouzelum"), A("Orcus"), A("Erle"), S("Udumbara"),
            KC("Leshy"), KC("Diakka")
        )},
        {613, SB_Zone("The Ruby Sea",
            B("Guhuo Niao"), B("Gauki Strongblade"), A("Funa Yurei"), A("Oni Yumemi"), S("Okina"),
            KC("Yumemi"), KC("Naked Yumemi")
        )},
        {614, SB_Zone("Yanxia",
            B("Deidar"), B("Gyorai Quickstrike"), A("Gajasura"), A("Angada"), S("Gamma"), FATE("Tamamo Gozen")
        )},
        {620, SB_Zone("The Peaks",
            B("Gwas-y-neidr"), B("Buccaboo"), A("Vochstein"), A("Aqrabuamelu"), S("Bone Crawler")
        )},
        {621, SB_Zone("The Lochs",
            B("Kiwa"), B("Manes"), A("Mahisha"), A("Luminare"), S("Salt and Light"), FATE("Ixion")
        )},
        {622, SB_Zone("The Azim Steppe",
            B("Aswang"), B("Kurma"), A("Sum"), A("Girimekhala"), S("Orghana")
        )},

    // -- Shadowbringers
        {813, ShB_Zone("Lakeland",
            B("La Velue"), B("Itzpapalotl"), A("Nariphon"), A("Nuckelavee"), S("Tyger")
        )},
        {814, ShB_Zone("Kholusia",
            B("Coquecigrue"), B("Indomitable"), A("Huracan"), A("Li'l Murderer"), S("Forgiven Pedantry"), FATE("Formidable")
        )},
        {815, ShB_Zone("Amh Araeng",
            B("Worm of the Well"), B("Juggler Hecatomb"), A("Maliktender"), A("Sugaar"), S("Tarchia")
        )},
        {816, ShB_Zone("Il Mheg",
            B("Domovoi"), B("Vulpangue"), A("O Poorest Pauldia"), A("The Mudman"), S("Aglaope")
        )},
        {817, ShB_Zone("The Rak'tika Greatwood",
            B("Mindmaker"), B("Pachamama"), A("Grassman"), A("Supay"), S("Ixtab"),
            KC("Cracked Ronkan Doll"), KC("Cracked Ronkan Thorn"), KC("Cracked Ronkan Vessel")
        )},
        {818, ShB_Zone("The Tempest",
            B("Deacon"), B("Gilshs Aath Swiftclaw"), A("Baal"), A("Rusalka"), S("Gunitt"), FATE("Archaeotania")
        )},

    // -- Endwalker
        {956, EW_Zone("Labyrinthos",
            B("Ü-u-ü-u"), B("Green Archon"), A("Hulder"), A("Storsie"), S("Burfurlur the Canny")
        )},
        {957, EW_Zone("Thavnair",
            B("Iravati"), B("Vajrakumara"), A("Yilan"), A("Sugriva"), S("Sphatika"), FATE("Daivadipa"),
            KC("Vajralangula"), KC("Pisaca"), KC("Asvattha")
        )},
        {958, EW_Zone("Garlemald",
            B("Emperor's Rose"), B("Warmonger"), A("Minerva"), A("Aegeiros"), S("Armstrong")
        )},
        {959, EW_Zone("Mare Lamentorum",
            B("Genesis Rock"), B("Daphnia Magna"), A("Lunatender Queen"), A("Mousse Princess"), S("Ruminator"),
            KC("Thinker"), KC("Wanderer"), KC("Weeper")
        )},
        {960, EW_Zone("Ultima Thule",
            B("Oskh Rhei"), B("Level Cheater"), A("Fan Ail"), A("Arch-Eta"), S("Narrow-rift"), FATE("Chi")
        )},
        {961, EW_Zone("Elpis",
            B("Shockmaw"), B("Yumcax"), A("Petalodus"), A("Gurangatch"), S("Ophioneus")
        )},

    // -- Eureka
        {732, Eureka_Zone("Eureka Anemos",
            S("Sabotender Corrido"), S("The Lord of Anemos"), S("Teles"),
            S("The Emperor of Anemos"), S("Callisto"), S("Number"),
            S("Jahannam"), S("Amemet"), S("Caym"),
            S("Bombadeel"), S("Serket"), S("Judgmental Julika"),
            S("The White Rider"), S("Polyphemus"), S("Simurgh's Strider"),
            S("King Hazmat"), S("Fafnir"), S("Amarok"),
            S("Lamashtu"), S("Pazuzu"),

            KC("Flowering Sabotender"), KC("Sea Bishop"), KC("Anemos Harpeia"),
            KC("Darner"), KC("Val Bear"), KC("Pneumaflayer"),
            KC("Typhoon Sprite"), KC("Abraxas"), KC("Stalker Ziz"),
            KC("Traveling Gourmand"), KC("Khor Claw"), KC("Henbane"),
            KC("Duskfall Dullahan"), KC("Monoeye"), KC("Old World Zu"),
            KC("Anemos Anala"), KC("Fossil Dragon"), KC("Voidscale"),
            KC("Val Specter"), KC("Shadow Wraith")
        )},

        {763, Eureka_Zone("Eureka Pagos",
            S("The Snow Queen"), S("Taxim"), S("Ash Dragon"),
            S("Glavoid"), S("Anapos"), S("Hakutaku"),
            S("King Igloo"), S("Asag"), S("Surabhi"),
            S("King Arthro"), S("Mindertaur"), S("Eldertaur"), S("Holy Cow"),
            S("Hadhayosh"), S("Horus"), S("Arch Angra Mainyu"),
            S("Copycat Cassie"), S("Louhi"),

            KC("Yukinko"), KC("Demon of the Incunable"), KC("Blood Demon"),
            KC("Val Worm"), KC("Snowmelt Sprite"), KC("Blubber Eyes"),
            KC("Huwasi"), KC("Wandering Open"), KC("Pagos Billygoat"),
            KC("Val Snipper"), KC("Lab Minotaur"), KC("Elder Buffalo"),
            KC("Lesser Void Dragon"), KC("Void Vouivre"), KC("Gawper"),
            KC("Ameretat"), KC("Val Corpse")
        )},

        {795, Eureka_Zone("Eureka Pyros",
            S("Leucosia"), S("Flauros"), S("The Sophist"),
            S("Graffiacane"), S("Askalaphos"), S("Grand Duke Batym"),
            S("Aetolus"), S("Lesath"), S("Eldthurs"),
            S("Iris"), S("Lamebrix Strikebocks"), S("Dux"),
            S("The Weeping Willow"), S("Lumber Jack"), S("Glaukopis"), S("Ying-Yang"),
            S("Skoll"), S("Penthesilea"),

            KC("Pyros Bhoot"), KC("Thunderstorm Sprite"), KC("Pyros Apanda"),
            KC("Valking"), KC("Overdue Tome"), KC("Dark Troubadour"),
            KC("Islandhander"), KC("Bird Eater"), KC("Pyros Crab"),
            KC("Northern Swallow"), KC("Illuminati Escapee"), KC("Matanga Castaway"),
            KC("Pyros Treant"), KC("Val Skatene"), KC("Pyros Hecteyes"),
            KC("Pyros Shuck"), KC("Val Bloodglider")
        )},

        {827, Eureka_Zone("Eureka Hydatos",
            S("Khalamari"), S("Stegodon"), S("Molech"),
            S("Piasa"), S("Frostmane"), S("Daphne"),
            S("King Goldemar"), S("Leuke"), S("Barong"),
            S("Ceto"), S("Provenance Watcher"),
            S("Ovni"), S("Tristitia"),

            KC("Xzomit"), KC("Hydatos Primelephas"), KC("Val Nullchu"),
            KC("Vivid Gastornis"), KC("Northern Tiger"), KC("Dark Void Monk"),
            KC("Hydatos Wraith"), KC("Tigerhawk"), KC("Laboratory Lion"),
            KC("Hydatos Delphyne"), KC("Crystal Claw")
        )},

    // -- Bozja

        // XXX: Dividers between kc mobs are hard-coded in UI code

        {920, Bozja_Zone("Bozjan Southern Front",
            B("Ink Claw"),
            B("Tideborn Angel"),
            B("Fern Flower"),
            B("Viy"),
            B("Psoglav"),
            B("Smok"),
            B("Patty"),
            B("Clingy Clare"),
            B("Bird of Barathrum"),

            KC("4th Legion Roader"),
            KC("4th Legion Death Claw"),
            KC("4th Legion Nimrod"),
            KC("4th Legion Slasher"),

            KC("4th Legion Avenger"),
            KC("4th Legion Gunship"),
            KC("4th Legion Vanguard"),

            KC("4th Legion Hexadrone"),
            KC("4th Legion Armored Weapon"),
            KC("4th Legion Scorpion")
        )},

        {975, Bozja_Zone("Zadnor",
            B("Anancus"),
            B("Stratogryph"),
            B("Vinegaroon Executioner"),
            B("Glyptodon"),
            B("Molten Scorpion"),
            B("Vapula"),
            B("Aglaophotis"),
            B("Earth Eater"),
            B("Lord Ochu"),

            KC("4th Legion Nimrod"),
            KC("4th Legion Infantry"),
            KC("4th Legion Gunship"),
            KC("4th Legion Hexadrone"),

            KC("4th Legion Satellite"),
            KC("4th Legion Colossus"),
            KC("4th Legion Armored Weapon"),
            KC("4th Legion Death Machine"),

            KC("4th Legion Helldiver"),
            KC("4th Legion Cavalry"),
            KC("4th Legion Roader"),
            KC("4th Legion Rearguard")
        )}
    };

    public readonly static HashSet<string> EpicFates = new HashSet<string>{
        "Steel Reign",
        "Behold Now Behemoth", "He Taketh It with His Eyes",
        "Long Live the Coeurl", "Coeurls Chase Boys", "Coeurls Chase Boys Chase Coeurls",
        "Prey Online",
        "Foxy Lady",
        "A Horse Outside",
        "The Head, the Tail, the Whole Damned Thing",
        "A Finale Most Formidable",
        "Devout Pilgrims vs. Daivadipa",
        "Omicron Recall: Killing Order",

        // Should the Eureka NM fates all be added here too?
        // Support fate needs to be added at least, since it spawns with no boss
        "The Baldesion Arsenal: Expedition Support",

        // Bozja pre-CE fates
        "All Pets Are Off",
        "More Machine Now than Man",
        "Unicorn Flakes",
        "Red (Chocobo) Alert",
        "I'm a Mechanical Man",
        "For Absent Friends",
        "Of Steel and Flame",
        // and initial CEs
        "The Shadow of Death's Hand",
        "The Baying of the Hound(s)",
        "The Fires of War",
        "Trampled under Hoof",

        // Zadnor pre-CE fates
        "An Immoral Dilemma",
        "Another Pilot Episode",
        "Tanking Up",
        "An End to Atrocities",
        "The Beasts are Back",
        "Hypertuned Havoc",
        "Attack of the Supersoldiers",
        // and initial CEs
        "With Diremite and Main",
        "From Beyond the Grave",
        "There Would Be Blood",
        "Time to Burn",
        "Lean, Mean, Magitek Machines",
        "Looks to Die For"
    };

    // Zone orders for hunt trains, based on Teleport order

    public readonly static int[] ZoneOrderARR = new int[]{
        134, 135, 137, 138, 139, 180, // La Noscea
        148, 152, 153, 154, // Black Shroud
        140, 141, 145, 146, 147, // Thanalan
        155, // Coerthas
        156 // Mor Dhona
    };

    public readonly static int[] ZoneOrderHW = new int[]{
        397, 401, 402, 399, 398, 400
    };

    public readonly static int[] ZoneOrderSB = new int[]{
        612, 620, 621, 613, 614, 622
    };

    public readonly static int[] ZoneOrderShB = new int[]{
        813, 814, 815, 816, 817, 818
    };

    public readonly static int[] ZoneOrderEW = new int[]{
        957, 958, 956, 959, 961, 960
    };
}
