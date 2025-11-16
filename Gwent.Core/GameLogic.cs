using System;
using System.Linq;

namespace Gwent.Core;

public static class GameLogic
{
	private static readonly Random Rng = new();

	public static void InitGame(GameState state)
	{
		state.Player1.Deck = GenerateDeck();
		state.Player2.Deck = GenerateDeck();

		Draw(state.Player1, 10);
		Draw(state.Player2, 10);

		state.CurrentPlayer = "P1";
		state.Phase = GamePhase.Playing;
		state.RoundNumber = 1;
	}

	private static List<Card> GenerateDeck()
	{
		return Enumerable.Range(1, 20)
			.Select(i => new Card
			{
				Name = $"Karta {i}",
				BasePower = Rng.Next(1, 10),
				Row = Row.Melee
			})
			.ToList();
	}

	private static void Draw(PlayerState p, int count)
	{
		for (int i = 0; i < count && p.Deck.Any(); i++)
		{
			p.Hand.Add(p.Deck[0]);
			p.Deck.RemoveAt(0);
		}
	}

	public static bool PlayCard(GameState s, PlayerState p, string cardId)
	{
		var card = p.Hand.FirstOrDefault(x => x.Id == cardId);
		if (card == null) return false;

		p.Hand.Remove(card);
		var row = s.Board.Rows.First(r => r.Row == card.Row);

		if (p.PlayerId == "P1") row.Player1Cards.Add(card);
		else row.Player2Cards.Add(card);

		s.CurrentPlayer = p.PlayerId == "P1" ? "P2" : "P1";
		return true;
	}
}
