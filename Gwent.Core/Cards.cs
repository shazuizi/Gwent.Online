using System;

namespace Gwent.Core;

public class Card
{
	public string Id { get; set; } = Guid.NewGuid().ToString();
	public string Name { get; set; } = "";
	public CardType Type { get; set; }
	public Row Row { get; set; }
	public int BasePower { get; set; }
	public Ability Ability { get; set; }

	public override string ToString() => $"{Name} ({BasePower})";
}
