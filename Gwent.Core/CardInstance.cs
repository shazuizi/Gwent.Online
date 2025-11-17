using System;
using System.Collections.Generic;

namespace Gwent.Core
{
	/// <summary>
	/// Konkretny egzemplarz karty w danej rozgrywce.
	/// </summary>
	public sealed class GwentCard
	{
		/// <summary>
		/// Unikalny ID egzemplarza w tej grze – używany przez klienta do targetowania.
		/// </summary>
		public Guid InstanceId { get; } = Guid.NewGuid();

		public CardDefinition Definition { get; }

		public string Name => Definition.Name;
		public FactionType Faction => Definition.Faction;
		public CardCategory Category => Definition.Category;
		public CardRow DefaultRow => Definition.DefaultRow;
		public IReadOnlyList<CardAbilityType> Abilities => Definition.Abilities;
		public string MusterGroup => Definition.MusterGroup;
		public string TightBondGroup => Definition.TightBondGroup;
		public bool IsHero => Definition.IsHero;

		/// <summary>
		/// Aktualna siła po wszystkich efektach.
		/// </summary>
		public int CurrentStrength { get; set; }

		/// <summary>
		/// Czy karta obecnie znajduje się w którymś rzędzie (a nie w ręce/decku/grave).
		/// </summary>
		public bool IsOnBoard { get; set; }

		public GwentCard(CardDefinition definition)
		{
			Definition = definition ?? throw new ArgumentNullException(nameof(definition));
			CurrentStrength = definition.BaseStrength;
		}

		/// <summary>
		/// Sprawdza, czy karta ma daną zdolność.
		/// </summary>
		public bool HasAbility(CardAbilityType ability) =>
			Definition.Abilities.Contains(ability);
	}
}
