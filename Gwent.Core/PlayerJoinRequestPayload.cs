namespace Gwent.Core
{
	/// <summary>
	/// Dane wysyłane przez klienta przy próbie dołączenia do serwera.
	/// </summary>
	public class PlayerJoinRequestPayload
	{
		public PlayerIdentity JoiningPlayer { get; set; } = new PlayerIdentity();
	}
}