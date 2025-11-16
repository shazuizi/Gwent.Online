namespace Gwent.Core
{
	/// <summary>
	/// Typ wiadomości przesyłanej pomiędzy klientem a serwerem.
	/// </summary>
	public enum NetworkMessageType
	{
		PlayerJoinRequest,
		PlayerJoinAccepted,
		PlayerReady,
		BothPlayersReadyStartGame,

		/// <summary>
		/// Żądanie wykonania akcji w grze (zagranie karty, pass).
		/// </summary>
		GameAction,

		/// <summary>
		/// Aktualny stan planszy (wysyłany do wszystkich klientów po każdej akcji).
		/// </summary>
		GameStateUpdate
	}
}