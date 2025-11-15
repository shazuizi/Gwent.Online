namespace Gwent.Core
{
	public class GameState
	{
		public PlayerState Player1 { get; set; } = new();
		public PlayerState Player2 { get; set; } = new();
		public BoardState Board { get; set; } = new();

		public string CurrentPlayerId { get; set; } = string.Empty;
		public int RoundNumber { get; set; } = 1;
		public GamePhase Phase { get; set; } = GamePhase.WaitingForPlayers;

		public PlayerState GetPlayer(string id) =>
			Player1.PlayerId == id ? Player1 : Player2;

		public PlayerState GetOpponent(string id) =>
			Player1.PlayerId == id ? Player2 : Player1;
	}
}
