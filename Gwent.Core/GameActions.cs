namespace Gwent.Core
{
	public enum GameActionType
	{
		PlayCard,
		PassTurn,
		Resign,
		Mulligan,
		UseLeaderAbility
	}

	public class GameActionPayload
	{
		public GameActionType ActionType { get; set; }

		public string ActingPlayerNickname { get; set; } = string.Empty;

		/// <summary>
		/// Id karty źródłowej (np. karta z ręki, którą zagrywamy / lider).
		/// </summary>
		public string? CardInstanceId { get; set; }

		/// <summary>
		/// Id karty celu (np. karta z cmentarza dla Medica, karta z rzędu dla Decoy/Mardroeme).
		/// </summary>
		public string? TargetInstanceId { get; set; }

		public CardRow? TargetRow { get; set; }
	}

	public class GameStateUpdatePayload
	{
		public GameBoardState BoardState { get; set; } = new GameBoardState();
	}
}
