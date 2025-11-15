using System.Collections.Generic;

namespace Gwent.Core
{
	public class PlayerState
	{
		public string PlayerId { get; set; } = string.Empty;
		public string Name { get; set; } = string.Empty;

		public List<Card> Deck { get; set; } = new();
		public List<Card> Hand { get; set; } = new();
		public List<Card> Graveyard { get; set; } = new();

		public bool HasPassed { get; set; } = false;
		public int Lives { get; set; } = 2;
	}
}
