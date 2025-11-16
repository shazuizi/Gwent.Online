using Gwent.Core;

namespace Gwent.Client
{
	/// <summary>
	/// Konfiguracja klienta zapisywana w pliku (ostatnio użyte dane).
	/// </summary>
	public class ClientConfiguration
	{
		public string LastUsedNickname { get; set; } = string.Empty;
		public string LastUsedServerAddress { get; set; } = "127.0.0.1";
		public int LastUsedServerPort { get; set; } = 5000;
		public GameRole LastUsedRole { get; set; } = GameRole.Host;
		public string LastSelectedDeckId { get; set; } = "default-deck";
	}
}