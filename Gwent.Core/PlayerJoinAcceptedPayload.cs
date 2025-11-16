namespace Gwent.Core
{
	/// <summary>
	/// Dane odsyłane przez serwer przy akceptacji dołączenia gracza.
	/// Zawiera pełną konfigurację sesji oraz przypisaną rolę (Host / Guest) dla tego klienta.
	/// </summary>
	public class PlayerJoinAcceptedPayload
	{
		public bool IsSessionFull { get; set; }

		/// <summary>
		/// Konfiguracja bieżącej sesji gry (obaj gracze, adres, port).
		/// </summary>
		public GameSessionConfiguration CurrentGameSessionConfiguration { get; set; } = new GameSessionConfiguration();

		/// <summary>
		/// Rola przypisana tej konkretnej instancji klienta (Host lub Guest).
		/// </summary>
		public GameRole AssignedRole { get; set; }
	}
}