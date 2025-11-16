namespace Gwent.Core
{
	/// <summary>
	/// Informacje identyfikujące gracza w rozgrywce.
	/// </summary>
	public class PlayerIdentity
	{
		public string Nickname { get; set; } = string.Empty;
		public string SelectedDeckId { get; set; } = string.Empty;
		public FactionType Faction { get; set; } = FactionType.NorthernRealms;
	}
}
