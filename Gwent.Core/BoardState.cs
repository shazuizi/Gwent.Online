using Gwent.Core;
using System.Collections.Generic;

namespace Gwent.Core
{
	public class BoardRow
	{
		public Row Row { get; set; }
		public List<Card> Player1Cards { get; set; } = new();
		public List<Card> Player2Cards { get; set; } = new();
	}

	public class BoardState
	{
		public List<BoardRow> Rows { get; set; } = new()
{
			new BoardRow { Row = Row.Melee },
			new BoardRow { Row = Row.Ranged },
			new BoardRow { Row = Row.Siege },
		};
	}
}
