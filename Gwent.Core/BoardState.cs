using System.Collections.Generic;
using System.Linq;

namespace Gwent.Core
{
	/// <summary>
	/// Stan planszy po stronie pojedynczego gracza.
	/// </summary>
	public class PlayerBoardState
	{
		public string PlayerNickname { get; set; } = string.Empty;

		public List<GwentCard> Deck { get; set; } = new List<GwentCard>();
		public List<GwentCard> Hand { get; set; } = new List<GwentCard>();
		public List<GwentCard> Graveyard { get; set; } = new List<GwentCard>();

		public List<GwentCard> MeleeRow { get; set; } = new List<GwentCard>();
		public List<GwentCard> RangedRow { get; set; } = new List<GwentCard>();
		public List<GwentCard> SiegeRow { get; set; } = new List<GwentCard>();

		public bool HasPassedCurrentRound { get; set; }
		public int RoundsWon { get; set; }

		/// <summary>
		/// Zwraca sumę siły na wszystkich rzędach (po aktualnych buffach/debuffach).
		/// </summary>
		public int GetTotalStrength()
		{
			return MeleeRow.Sum(c => c.CurrentStrength) +
				   RangedRow.Sum(c => c.CurrentStrength) +
				   SiegeRow.Sum(c => c.CurrentStrength);
		}
	}

	/// <summary>
	/// Pełny stan planszy dla obu graczy.
	/// </summary>
	public class GameBoardState
	{
		public PlayerBoardState HostPlayerBoard { get; set; } = new PlayerBoardState();
		public PlayerBoardState GuestPlayerBoard { get; set; } = new PlayerBoardState();

		/// <summary>
		/// Nick aktualnie wykonującego ruch gracza.
		/// </summary>
		public string ActivePlayerNickname { get; set; } = string.Empty;

		public int CurrentRoundNumber { get; set; } = 1;

		/// <summary>
		/// Czy gra już się zakończyła.
		/// </summary>
		public bool IsGameFinished { get; set; }

		/// <summary>
		/// Nick zwycięzcy (jeśli gra zakończona).
		/// </summary>
		public string? WinnerNickname { get; set; }
	}
}
