using System.Collections.Generic;
using System.Linq;

namespace Gwent.Core
{
	public class PlayerBoardState
	{
		public string PlayerNickname { get; set; } = string.Empty;
		public FactionType Faction { get; set; } = FactionType.Neutral;

		public List<GwentCard> Deck { get; set; } = new List<GwentCard>();
		public List<GwentCard> Hand { get; set; } = new List<GwentCard>();
		public List<GwentCard> Graveyard { get; set; } = new List<GwentCard>();

		public List<GwentCard> MeleeRow { get; set; } = new List<GwentCard>();
		public List<GwentCard> RangedRow { get; set; } = new List<GwentCard>();
		public List<GwentCard> SiegeRow { get; set; } = new List<GwentCard>();

		public GwentCard? LeaderCard { get; set; }

		public bool HasPassedCurrentRound { get; set; }
		public int RoundsWon { get; set; }
		public int LifeTokensRemaining { get; set; } = 2;

		public int MulligansRemaining { get; set; } = 2;

		/// <summary>
		/// Czy zdolność dowódcy została już użyta.
		/// </summary>
		public bool LeaderAbilityUsed { get; set; }

		public int GetTotalStrength()
		{
			return MeleeRow.Sum(c => c.CurrentStrength) +
				   RangedRow.Sum(c => c.CurrentStrength) +
				   SiegeRow.Sum(c => c.CurrentStrength);
		}

		public int GetRowStrength(CardRow row)
		{
			return row switch
			{
				CardRow.Melee => MeleeRow.Sum(c => c.CurrentStrength),
				CardRow.Ranged => RangedRow.Sum(c => c.CurrentStrength),
				CardRow.Siege => SiegeRow.Sum(c => c.CurrentStrength),
				_ => 0
			};
		}
	}

	public class GameBoardState
	{
		public PlayerBoardState HostPlayerBoard { get; set; } = new PlayerBoardState();
		public PlayerBoardState GuestPlayerBoard { get; set; } = new PlayerBoardState();

		public List<GwentCard> WeatherCards { get; set; } = new List<GwentCard>();

		public string ActivePlayerNickname { get; set; } = string.Empty;

		public int CurrentRoundNumber { get; set; } = 1;

		public bool IsGameFinished { get; set; }
		public string? WinnerNickname { get; set; }
	}
}
