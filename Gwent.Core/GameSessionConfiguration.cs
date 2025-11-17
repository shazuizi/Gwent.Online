namespace Gwent.Core
{
	public sealed class PlayerIdentity
	{
		public string Nickname { get; set; } = string.Empty;
		public bool IsHost { get; set; }
		public FactionType Faction { get; set; } = FactionType.NorthernRealms;
	}

	/// <summary>
	/// Konfiguracja sesji – kto jest hostem, kto guestem, jakimi frakcjami grają.
	/// </summary>
	public sealed class GameSessionConfiguration
	{
		public PlayerIdentity HostPlayer { get; set; } = new();
		public PlayerIdentity GuestPlayer { get; set; } = new();
	}
}
