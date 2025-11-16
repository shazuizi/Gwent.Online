namespace Gwent.Core
{
	public enum FactionType
	{
		Neutral,
		NorthernRealms,
		Nilfgaard,
		Scoiatael,
		Monsters
	}

	public enum CardRow
	{
		Melee,
		Ranged,
		Siege,
		Agile,
		WeatherGlobal
	}

	public enum CardCategory
	{
		Unit,
		Hero,
		Weather,
		Special,
		Leader
	}

	public enum CardAbilityType
	{
		None,

		// jednostki:
		Spy,
		Medic,
		TightBond,
		Muster,
		Agile,
		MoralBoost,
		CommandersHorn,
		Scorch,
		Hero,

		// Pogoda:
		WeatherBitingFrost,
		WeatherImpenetrableFog,
		WeatherTorrentialRain,
		ClearWeather,

		// Specjalne:
		Decoy,
		Mardroeme,

		// Zdolności dowódców:
		LeaderAbility_DrawExtraCard
	}
}
