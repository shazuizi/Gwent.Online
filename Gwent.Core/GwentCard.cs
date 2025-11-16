using System;
using System.Collections.Generic;

namespace Gwent.Core
{
	/// <summary>
	/// Pojedyncza karta Gwinta – instancja w talii/grze (ma własne InstanceId).
	/// </summary>
	public class GwentCard
	{
		/// <summary>
		/// Identyfikator instancji karty, unikalny w ramach gry.
		/// </summary>
		public string InstanceId { get; set; } = Guid.NewGuid().ToString();

		/// <summary>
		/// Identyfikator szablonu karty (np. "nr_siegfried_01").
		/// </summary>
		public string TemplateId { get; set; } = string.Empty;

		public string Name { get; set; } = string.Empty;

		public FactionType Faction { get; set; } = FactionType.Neutral;

		public CardCategory Category { get; set; } = CardCategory.Unit;

		/// <summary>
		/// Bazowa siła karty.
		/// </summary>
		public int BaseStrength { get; set; }

		/// <summary>
		/// Aktualna siła karty na polu bitwy (po buffach/debuffach).
		/// </summary>
		public int CurrentStrength { get; set; }

		/// <summary>
		/// Domyślny rząd, na którym karta gra (lub Agile/Weather).
		/// </summary>
		public CardRow DefaultRow { get; set; } = CardRow.Melee;

		/// <summary>
		/// Czy karta jest bohaterem (nie wpływają na nią niektóre efekty).
		/// </summary>
		public bool IsHero { get; set; }

		/// <summary>
		/// Lista zdolności karty (MoralBoost, Spy, Weather itd.).
		/// </summary>
		public List<CardAbilityType> Abilities { get; set; } = new List<CardAbilityType>();

		/// <summary>
		/// Tworzy płytką kopię karty (używane np. przy generowaniu talii z szablonów).
		/// </summary>
		public GwentCard Clone()
		{
			return new GwentCard
			{
				InstanceId = Guid.NewGuid().ToString(),
				TemplateId = TemplateId,
				Name = Name,
				Faction = Faction,
				Category = Category,
				BaseStrength = BaseStrength,
				CurrentStrength = CurrentStrength,
				DefaultRow = DefaultRow,
				IsHero = IsHero,
				Abilities = new List<CardAbilityType>(Abilities)
			};
		}
	}
}
