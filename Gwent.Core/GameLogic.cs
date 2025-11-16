namespace Gwent.Core
{
	public static class GameLogic
	{
		private static readonly Random Rng = new();

		public static void StartGame(GameState state)
		{
			state.Phase = GamePhase.Playing;
			state.RoundNumber = 1;

			InitDeck(state.Player1);
			InitDeck(state.Player2);

			DrawInitialHand(state.Player1, 10);
			DrawInitialHand(state.Player2, 10);

			state.CurrentPlayerId = Rng.Next(2) == 0
				? state.Player1.PlayerId
				: state.Player2.PlayerId;
		}

		private static void InitDeck(PlayerState player)
		{
			player.Deck = new List<Card>();

			for (int i = 0; i < 20; i++)
			{
				player.Deck.Add(new Card
				{
					Name = $"Jednostka {i + 1}",
					Type = CardType.Unit,
					Row = Row.Melee,
					BasePower = Rng.Next(1, 10),
					Ability = Ability.None
				});
			}

			player.Deck = player.Deck
				.OrderBy(_ => Rng.Next())
				.ToList();
		}

		private static void DrawInitialHand(PlayerState player, int count)
		{
			for (int i = 0; i < count && player.Deck.Any(); i++)
			{
				var top = player.Deck[0];
				player.Deck.RemoveAt(0);
				player.Hand.Add(top);
			}
		}

		public static bool PlayCard(GameState state, string playerId, string cardId, Row targetRow)
		{
			if (state.Phase != GamePhase.Playing) return false;
			if (state.CurrentPlayerId != playerId) return false;

			var player = state.GetPlayer(playerId);
			var card = player.Hand.FirstOrDefault(c => c.Id == cardId);
			if (card == null) return false;

			player.Hand.Remove(card);

			var row = state.Board.Rows.First(r => r.Row == targetRow);

			if (player == state.Player1)
				row.Player1Cards.Add(card);
			else
				row.Player2Cards.Add(card);

			// tu później dorzucimy obsługę Ability

			state.CurrentPlayerId = state.GetOpponent(playerId).PlayerId;
			CheckRoundEnd(state);
			return true;
		}

		public static void Pass(GameState state, string playerId)
		{
			var player = state.GetPlayer(playerId);
			player.HasPassed = true;
			state.CurrentPlayerId = state.GetOpponent(playerId).PlayerId;
			CheckRoundEnd(state);
		}

		public static void CheckRoundEnd(GameState state)
		{
			if (!state.Player1.HasPassed || !state.Player2.HasPassed)
				return;

			int p1 = CalculateTotalPowerFor(state, state.Player1);
			int p2 = CalculateTotalPowerFor(state, state.Player2);

			if (p1 > p2)
				state.Player2.Lives--;
			else if (p2 > p1)
				state.Player1.Lives--;
			else
			{
				state.Player1.Lives--;
				state.Player2.Lives--;
			}

			if (state.Player1.Lives <= 0 || state.Player2.Lives <= 0)
			{
				state.Phase = GamePhase.GameEnd;
				return;
			}

			state.RoundNumber++;
			state.Player1.HasPassed = false;
			state.Player2.HasPassed = false;
			state.Board = new BoardState();
			state.Phase = GamePhase.Playing;
		}

		public static int CalculateTotalPowerFor(GameState state, PlayerState player)
		{
			int sum = 0;
			foreach (var row in state.Board.Rows)
			{
				var list = player == state.Player1
					? row.Player1Cards
					: row.Player2Cards;

				sum += list.Sum(c => c.BasePower);
			}
			return sum;
		}
	}
}
