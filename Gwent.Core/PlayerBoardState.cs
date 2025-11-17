using System.Collections.Generic;
using System.Linq;

namespace Gwent.Core
{
	/// <summary>
	/// Stan planszy konkretnego gracza.
	/// </summary>
	public sealed class PlayerBoardState
	{
		public string PlayerNickname { get; set; } = string.Empty;
		public FactionType Faction { get; set; } = FactionType.Neutral;

		public List<GwentCard> Deck { get; } = new();
		public List<GwentCard> Hand { get; } = new();
		public List<GwentCard> Graveyard { get; } = new();

		public List<GwentCard> MeleeRow { get; } = new();
		public List<GwentCard> RangedRow { get; } = new();
		public List<GwentCard> SiegeRow { get; } = new();

		public GwentCard? LeaderCard { get; set; }
		public bool LeaderAbilityUsed { get; set; }

		public int LifeTokensRemaining { get; set; } = 2; // W3: 2 życia
		public int RoundsWon { get; set; }

		public bool HasPassedCurrentRound { get; set; }

		public int MulligansRemaining { get; set; } = 2; // 2 wymiany w 1. rundzie

		/// <summary>
		/// Zwraca wszystkie karty na planszy gracza.
		/// </summary>
		public IEnumerable<GwentCard> EnumerateBoardCards()
		{
			foreach (var c in MeleeRow) yield return c;
			foreach (var c in RangedRow) yield return c;
			foreach (var c in SiegeRow) yield return c;
		}

		/// <summary>
		/// Zwraca siłę w danym rzędzie.
		/// </summary>
		public int GetRowStrength(CardRow row) =>
			row switch
			{
				CardRow.Melee => MeleeRow.Sum(c => c.CurrentStrength),
				CardRow.Ranged => RangedRow.Sum(c => c.CurrentStrength),
				CardRow.Siege => SiegeRow.Sum(c => c.CurrentStrength),
				_ => 0
			};

		/// <summary>
		/// Zwraca sumaryczną siłę we wszystkich rzędach.
		/// </summary>
		public int GetTotalStrength() =>
			GetRowStrength(CardRow.Melee) +
			GetRowStrength(CardRow.Ranged) +
			GetRowStrength(CardRow.Siege);
	}
}
