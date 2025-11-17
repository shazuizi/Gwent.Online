namespace Gwent.Core
{
	/// <summary>
	/// Frakcje z Gwinta.
	/// </summary>
	public enum FactionType
	{
		Neutral,
		NorthernRealms,
		Nilfgaard,
		Scoiatael,
		Monsters
	}

	/// <summary>
	/// Rzędy na planszy (plus pomocniczy Global dla efektów globalnych).
	/// </summary>
	public enum CardRow
	{
		Melee,
		Ranged,
		Siege,
		Agile,          // karta zdolna iść na Melee/Ranged/Siege w zależności od definicji
		WeatherGlobal   // dla pogody / Scorch
	}

	/// <summary>
	/// Kategorie kart: zwykłe jednostki, herosi, pogoda, specjale, liderzy.
	/// </summary>
	public enum CardCategory
	{
		Unit,
		Hero,
		Weather,
		Special,
		Leader
	}

	/// <summary>
	/// Zdolności kart (jedna karta może mieć wiele).
	/// </summary>
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
		Hero, // specjalny „untouchable” – w W3 to bardziej cecha niż ability

		// Pogoda:
		WeatherBitingFrost,
		WeatherImpenetrableFog,
		WeatherTorrentialRain,
		ClearWeather,

		// Specjalne:
		Decoy,
		Mardroeme,

		// Zdolności dowódców (przykład):
		LeaderAbility_DrawExtraCard
	}

	/// <summary>
	/// Typ akcji wysyłanej z klienta do silnika.
	/// </summary>
	public enum GameActionType
	{
		PlayCard,
		Mulligan,
		PassTurn,
		UseLeaderAbility,
		Resign
	}
}
