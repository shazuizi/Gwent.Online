using System;

namespace Gwent.Core
{
	/// <summary>
	/// Opis pojedynczej akcji wykonywanej przez gracza (przychodzi z klienta).
	/// </summary>
	public sealed class GameActionPayload
	{
		public GameActionType ActionType { get; set; }
		public string ActingPlayerNickname { get; set; } = string.Empty;

		/// <summary>
		/// Karta zagrana / użyta (np. jednostka, specjalna, lider).
		/// </summary>
		public Guid CardInstanceId { get; set; }

		/// <summary>
		/// Dodatkowy cel – np. karta z cmentarza (Medic) / karta na stole (Decoy / Mardroeme).
		/// </summary>
		public Guid? TargetInstanceId { get; set; }

		/// <summary>
		/// Docelowy rząd (dla jednostek, rogu, itp.).
		/// </summary>
		public CardRow? TargetRow { get; set; }
	}
}
