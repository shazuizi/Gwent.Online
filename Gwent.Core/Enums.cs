namespace Gwent.Core
{
	/// <summary>
	/// Frakcja talii / karty.
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
	/// Rząd, na którym karta normalnie się znajduje.
	/// </summary>
	public enum CardRow
	{
		Melee,
		Ranged,
		Siege,
		Agile,        // karta może być zagrana na kilku rzędach
		WeatherGlobal // efekt wpływa globalnie
	}

	/// <summary>
	/// Kategoria karty (rodzaj).
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
	/// Typ zdolności (efektu) karty.
	/// </summary>
	public enum CardAbilityType
	{
		None,

		// klasyczne efekty Gwinta:
		MoralBoost,
		CommandersHorn,
		TightBond,
		Medic,
		Spy,
		Muster,
		Scorch,

		// pogoda:
		Weather,
		ClearWeather
	}
}
