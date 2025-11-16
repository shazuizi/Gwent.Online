namespace Gwent.Core
{
	/// <summary>
	/// Konfiguracja sesji gry – dane potrzebne obu stronom (host i guest).
	/// </summary>
	public class GameSessionConfiguration
	{
		public PlayerIdentity HostPlayer { get; set; } = new PlayerIdentity();
		public PlayerIdentity GuestPlayer { get; set; } = new PlayerIdentity();
		public int ServerPort { get; set; }
		public string ServerAddress { get; set; } = "127.0.0.1";
	}
}
