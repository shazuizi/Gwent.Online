namespace Gwent.Core
{
	public class NetMessage
	{
		public string Type { get; set; } = string.Empty; // "state", "playCard", "pass", "error"
		public string? PlayerId { get; set; }
		public string? CardId { get; set; }
		public Row TargetRow { get; set; }
		public GameState? GameState { get; set; }
		public string? Error { get; set; }
	}
}
