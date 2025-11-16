namespace Gwent.Core
{
	/// <summary>
	/// Podstawowa informacja o talii – używana w menu i przy wyborze talii.
	/// </summary>
	public class DeckSummary
	{
		public string DeckId { get; set; } = string.Empty;
		public string DeckName { get; set; } = string.Empty;
		public int CardsCount { get; set; }
		public string FactionName { get; set; } = string.Empty;

		public override string ToString()
		{
			return $"{DeckName} ({FactionName}) - {CardsCount} cards";
		}
	}
}
