namespace Gwent.Core
{
	public class NetMessage
	{
		// "join", "joined", "state", "playCard", "pass", "error"
		public string Type { get; set; } = string.Empty;

		public string? PlayerId { get; set; }

		public string? Nickname { get; set; }
		public string? CardId { get; set; }
		public Row TargetRow { get; set; }

		// Stan gry zwracany przez serwer
		public GameState? GameState { get; set; }

		// Komunikaty błędów
		public string? Error { get; set; }

		// Nick gracza przy logowaniu/joinie
		public string? Nick { get; set; }
	}
}

