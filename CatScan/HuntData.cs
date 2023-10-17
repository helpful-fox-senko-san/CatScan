using System.Collections.Generic;

namespace CatScan;

public enum Expansion
{
	Unknown,
	ARR,
	HW,
	SB,
	ShB,
	EW
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

public readonly struct ZoneMapParams
{
	public readonly float OffsetX;
	public readonly float OffsetZ;
	public readonly float Scale;

	public ZoneMapParams(float x, float y, float scale)
	{
		OffsetX = x;
		OffsetZ = y;
		Scale = scale;
	}

	public static ZoneMapParams MakeDefault()
	{
		return new ZoneMapParams(0.0f, 0.0f, 1.0f);
	}

	public static ZoneMapParams MakeScaled(float scale)
	{
		return new ZoneMapParams(0.0f, 0.0f, scale);
	}
}

public readonly struct Zone
{
	public readonly Expansion Expansion;
	public readonly string Name;
	public readonly ZoneMapParams MapParams;
	public readonly int Instances;
	public readonly Mark[] Marks;

	public Zone(Expansion expansion, string name, int instances, params Mark[] marks)
	{
		Expansion = expansion;
		Name = name;
		MapParams = ZoneMapParams.MakeDefault();
		Instances = instances;
		Marks = marks;
	}

	public Zone(Expansion expansion, string name, ZoneMapParams mapParams, int instances, params Mark[] marks)
	{
		Expansion = expansion;
		Name = name;
		MapParams = mapParams;
		Instances = instances;
		Marks = marks;
	}
}

public static class HuntData
{
	private static int Uninstanced => 0;
	private static int Instanced(int n) => n;

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

	// All of the Heavensward field maps have a map scale of 0.95
	private static Zone HW_Zone(string name, int instances, params Mark[] marks)
	{
		return new Zone(Expansion.HW, name, ZoneMapParams.MakeScaled(0.95f), instances, marks);
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

	// -- Shorthand overloads for uninstanced maps
	private static Zone ARR_Zone(string name, params Mark[] marks) { return ARR_Zone(name, Uninstanced, marks); }
	private static Zone HW_Zone(string name, params Mark[] marks) { return HW_Zone(name, Uninstanced, marks); }
	private static Zone SB_Zone(string name, params Mark[] marks) { return SB_Zone(name, Uninstanced, marks); }
	private static Zone ShB_Zone(string name, params Mark[] marks) { return ShB_Zone(name, Uninstanced, marks); }
	private static Zone EW_Zone(string name, params Mark[] marks) { return EW_Zone(name, Uninstanced, marks); }

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
		// TODO: Figure out how to determine if an NPC is Odin
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
			B("Coquecigrue"), B("Indomitable"), A("Huracan"), A("Li'l Murderer"), S("Forgiven Pedantry"), FATE("Formidible")
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
		{956, EW_Zone("Labrynthos",
			B("Ü-u-ü-u"), B("Green Archon"), A("Hulder"), A("Storsie"), S("Burfurlur the Canny")
		)},
		{957, EW_Zone("Thavnair",
			B("Iravati"), B("Vajrakumara"), A("Yilan"), A("Sugriva"), S("Sphatika"), FATE("Daivadipa"),
			KC("Vajralangula"), KC("Pisaca"), KC("Asvattha")
		)},
		{958, EW_Zone("Garlemald",
			B("Emperor's Rose"), B("Warmonger"), A("Minerva"), A("Aegeiros"), S("Armstrong")
		)},
		{959, EW_Zone("Mare Lamentorium",
			B("Genesis Rock"), B("Daphnia Magna"), A("Lunatender Queen"), A("Mousse Princess"), S("Ruminator"),
			KC("Thinker"), KC("Wanderer"), KC("Weeper")
		)},
		{960, EW_Zone("Ultima Thule",
			B("Oskh Rhei"), B("Level Cheater"), A("Fan Ail"), A("Arch-Eta"), S("Narrow-rift"), FATE("Chi")
		)},
		{961, EW_Zone("Elpis",
			B("Shockmaw"), B("Yumcax"), A("Petalodus"), A("Gurangatch"), S("Ophioneus")
		)},
	};
}
