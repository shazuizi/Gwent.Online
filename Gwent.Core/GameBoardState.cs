using System.Collections.Generic;

namespace Gwent.Core
{
	/// <summary>
	/// Pełny stan gry – gotowy do serializacji i wysyłania klientom.
	/// </summary>
	public sealed class GameBoardState
	{
		public PlayerBoardState HostPlayerBoard { get; set; } = new();
		public PlayerBoardState GuestPlayerBoard { get; set; } = new();

		/// <summary>
		/// Aktualne karty pogody na stole.
		/// </summary>
		public List<GwentCard> WeatherCards { get; set; } = new();

		public int CurrentRoundNumber { get; set; } = 1;

		public string ActivePlayerNickname { get; set; } = string.Empty;

		public bool IsGameFinished { get; set; }

		/// <summary>
		/// Nick zwycięzcy gry. null = remis.
		/// </summary>
		public string? WinnerNickname { get; set; }

		/// <summary>
		/// Historia tekstowa gry (do wyświetlenia w kliencie).
		/// </summary>
		public List<string> GameLog { get; set; } = new();
	}
}
