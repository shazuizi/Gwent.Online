namespace Gwent.Core
{
	/// <summary>
	/// Typ wiadomości przesyłanych pomiędzy klientem a serwerem.
	/// </summary>
	public enum NetworkMessageType
	{
		PlayerJoinRequest,
		PlayerJoinAccepted,
		PlayerJoinRejected,
		PlayerReady,
		BothPlayersReadyStartGame,
		GameFinished,
		Heartbeat,
		ErrorMessage
	}
}
