using System.Collections.Generic;

namespace Gwent.Core
{
	/// <summary>
	/// Tworzy talię startową dla danej frakcji.
	/// Tu możesz docelowo dorzucić pełne listy kart z W3.
	/// </summary>
	public static class DeckFactory
	{
		/// <summary>
		/// Zwraca definicje kart dla frakcji.
		/// </summary>
		public static List<CardDefinition> CreateStarterDeckDefinitions(FactionType faction)
		{
			var list = new List<CardDefinition>();

			switch (faction)
			{
				case FactionType.NorthernRealms:
					// TODO: pełna talia NR
					list.Add(new CardDefinition
					{
						Id = "nr_blue_stripes_commando",
						Name = "Blue Stripes Commando",
						Faction = FactionType.NorthernRealms,
						Category = CardCategory.Unit,
						DefaultRow = CardRow.Melee,
						BaseStrength = 4,
						Abilities = new[] { CardAbilityType.TightBond },
						TightBondGroup = "nr_blue_stripes"
					});
					// ... reszta
					break;

				case FactionType.Nilfgaard:
					// TODO: talia Nilfgaard
					break;

				case FactionType.Scoiatael:
					// TODO: Scoia'tael
					break;

				case FactionType.Monsters:
					// TODO: Monsters
					break;
			}

			return list;
		}

		/// <summary>
		/// Tworzy egzemplarze kart (GwentCard) z definicji.
		/// </summary>
		public static List<GwentCard> CreateDeckInstances(FactionType faction)
		{
			var defs = CreateStarterDeckDefinitions(faction);
			var result = new List<GwentCard>();

			foreach (var def in defs)
			{
				result.Add(new GwentCard(def));
			}

			return result;
		}
	}
}
