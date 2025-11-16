using System.Collections.Generic;

namespace Gwent.Core;

public class PlayerState
{
	public string PlayerId { get; set; } = "";
	public string Name { get; set; } = "";

	public List<Card> Deck { get; set; } = new();
	public List<Card> Hand { get; set; } = new();
	public List<Card> Graveyard { get; set; } = new();

	public int Lives { get; set; } = 2;
	public bool Passed { get; set; } = false;
}
