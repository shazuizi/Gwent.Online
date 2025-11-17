using System.Collections.Generic;

namespace Gwent.Core
{
	/// <summary>
	/// Opis pojedynczej karty w kolekcji – bez konkretnego egzemplarza w grze.
	/// </summary>
	public sealed class CardDefinition
	{
		public string Id { get; init; } = string.Empty; // np. "nr_blue_stripes_commando"
		public string Name { get; init; } = string.Empty;
		public FactionType Faction { get; init; } = FactionType.Neutral;
		public CardCategory Category { get; init; } = CardCategory.Unit;
		public CardRow DefaultRow { get; init; } = CardRow.Melee;
		public int BaseStrength { get; init; } = 0;
		public IReadOnlyList<CardAbilityType> Abilities { get; init; } = new List<CardAbilityType>();
		public string MusterGroup { get; init; } = string.Empty;   // np. "nr_blue_stripes"
		public string TightBondGroup { get; init; } = string.Empty; // np. "nr_blue_stripes"
		public bool IsHero => Category == CardCategory.Hero || Abilities.Contains(CardAbilityType.Hero);

		public override string ToString() => Name;
	}
}
