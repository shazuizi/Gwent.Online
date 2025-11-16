namespace Gwent.Core
{
	public class NetMessage
	{
		// "join", "state", "playCard", "pass", "error"
		public string Type { get; set; } = string.Empty;

		// używane głównie po stronie serwera (opcjonalne)
		public string? PlayerId { get; set; }

		// przy "join" – nick gracza
		public string? PlayerName { get; set; }

		// przy "playCard"
		public string? CardId { get; set; }
		public Row TargetRow { get; set; }

		// przy "state"
		public GameState? GameState { get; set; }

		// przy "error"
		public string? Error { get; set; }
	}
}
