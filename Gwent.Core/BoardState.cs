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

		/// <summary>
		/// Karta dowódcy gracza (poza talią).
		/// </summary>
		public GwentCard? LeaderCard { get; set; }

		/// <summary>
		/// Czy gracz zapasował w bieżącej rundzie.
		/// </summary>
		public bool HasPassedCurrentRound { get; set; }

		/// <summary>
		/// Liczba wygranych rund.
		/// </summary>
		public int RoundsWon { get; set; }

		/// <summary>
		/// Liczba pozostałych „żyć” / znaczników (standardowo 2).
		/// Tracisz 1 życie przy przegranej rundzie.
		/// </summary>
		public int LifeTokensRemaining { get; set; } = 2;

		/// <summary>
		/// Suma siły na wszystkich rzędach.
		/// </summary>
		public int GetTotalStrength()
		{
			return MeleeRow.Sum(c => c.CurrentStrength) +
				   RangedRow.Sum(c => c.CurrentStrength) +
				   SiegeRow.Sum(c => c.CurrentStrength);
		}

		/// <summary>
		/// Suma siły dla konkretnego rzędu.
		/// </summary>
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

	/// <summary>
	/// Pełny stan planszy dla obu graczy.
	/// </summary>
	public class GameBoardState
	{
		public PlayerBoardState HostPlayerBoard { get; set; } = new PlayerBoardState();
		public PlayerBoardState GuestPlayerBoard { get; set; } = new PlayerBoardState();

		/// <summary>
		/// Karty pogody aktywne na stole (globalne).
		/// </summary>
		public List<GwentCard> WeatherCards { get; set; } = new List<GwentCard>();

		/// <summary>
		/// Nick aktualnie wykonującego ruch gracza.
		/// </summary>
		public string ActivePlayerNickname { get; set; } = string.Empty;

		/// <summary>
		/// Numer bieżącej rundy (od 1 w górę).
		/// </summary>
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
