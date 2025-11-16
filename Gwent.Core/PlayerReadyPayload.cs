namespace Gwent.Core
{
	/// <summary>
	/// Dane wysyłane przez klienta przy zgłoszeniu gotowości do rozpoczęcia gry.
	/// </summary>
	public class PlayerReadyPayload
	{
		/// <summary>
		/// Nick gracza, który zgłasza gotowość.
		/// </summary>
		public string Nickname { get; set; } = string.Empty;
	}
}
