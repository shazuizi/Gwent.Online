namespace Gwent.Core
{
	/// <summary>
	/// Typ akcji wykonywanej przez gracza.
	/// </summary>
	public enum GameActionType
	{
		PlayCard,
		PassTurn,
		Resign
	}

	/// <summary>
	/// Dane pojedynczej akcji wysyłanej przez klienta na serwer.
	/// </summary>
	public class GameActionPayload
	{
		/// <summary>
		/// Typ akcji (zagranie karty, pass, poddanie).
		/// </summary>
		public GameActionType ActionType { get; set; }

		/// <summary>
		/// Nick gracza wykonującego akcję.
		/// </summary>
		public string ActingPlayerNickname { get; set; } = string.Empty;

		/// <summary>
		/// Id instancji karty (dla akcji PlayCard).
		/// </summary>
		public string? CardInstanceId { get; set; }

		/// <summary>
		/// Rząd docelowy – używane jeśli karta może być zagrana na różnych rzędach.
		/// </summary>
		public CardRow? TargetRow { get; set; }
	}

	/// <summary>
	/// Payload wysyłany przez serwer do klientów ze zaktualizowanym stanem gry.
	/// </summary>
	public class GameStateUpdatePayload
	{
		public GameBoardState BoardState { get; set; } = new GameBoardState();
	}
}
