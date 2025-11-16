namespace Gwent.Core
{
	/// <summary>
	/// Dane wysyłane przez serwer przy starcie gry, zawierają konfigurację sesji.
	/// </summary>
	public class GameStartPayload
	{
		/// <summary>
		/// Pełna konfiguracja sesji gry (obie strony znają tę samą konfigurację).
		/// </summary>
		public GameSessionConfiguration GameSessionConfiguration { get; set; } = new GameSessionConfiguration();
	}
}
