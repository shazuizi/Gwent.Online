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
		public GameActionType ActionType { get; set; }

		public string ActingPlayerNickname { get; set; } = string.Empty;

		public string? CardInstanceId { get; set; }

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
