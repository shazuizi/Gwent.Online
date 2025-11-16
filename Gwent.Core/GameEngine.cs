using System;
using System.Collections.Generic;
using System.Linq;

namespace Gwent.Core
{
	/// <summary>
	/// Silnik gry działający po stronie serwera – zarządza stanem planszy,
	/// stosuje akcje graczy i wylicza zwycięzcę rund oraz całej gry.
	/// </summary>
	public class GameEngine
	{
		private readonly Random random = new Random();

		/// <summary>
		/// Aktualny stan planszy gry.
		/// </summary>
		public GameBoardState BoardState { get; private set; } = new GameBoardState();

		/// <summary>
		/// Inicjalizuje nową grę na podstawie konfiguracji sesji – tworzy talie,
		/// tasuje je i rozdaje początkowe karty, przydziela dowódców.
		/// </summary>
		public void InitializeNewGame(GameSessionConfiguration sessionConfiguration)
		{
			BoardState = new GameBoardState
			{
				HostPlayerBoard = new PlayerBoardState
				{
					PlayerNickname = sessionConfiguration.HostPlayer.Nickname,
					LifeTokensRemaining = 2,
					RoundsWon = 0,
					HasPassedCurrentRound = false
				},
				GuestPlayerBoard = new PlayerBoardState
				{
					PlayerNickname = sessionConfiguration.GuestPlayer.Nickname,
					LifeTokensRemaining = 2,
					RoundsWon = 0,
					HasPassedCurrentRound = false
				},
				ActivePlayerNickname = sessionConfiguration.HostPlayer.Nickname,
				CurrentRoundNumber = 1,
				IsGameFinished = false,
				WinnerNickname = null,
				WeatherCards = new List<GwentCard>()
			};

			BoardState.HostPlayerBoard.Deck = CreateSimpleTestDeck(FactionType.NorthernRealms, BoardState.HostPlayerBoard.PlayerNickname);
			BoardState.GuestPlayerBoard.Deck = CreateSimpleTestDeck(FactionType.Nilfgaard, BoardState.GuestPlayerBoard.PlayerNickname);

			// prości dowódcy – tylko do wyświetlenia w UI
			BoardState.HostPlayerBoard.LeaderCard = CreateLeaderCard(FactionType.NorthernRealms, BoardState.HostPlayerBoard.PlayerNickname);
			BoardState.GuestPlayerBoard.LeaderCard = CreateLeaderCard(FactionType.Nilfgaard, BoardState.GuestPlayerBoard.PlayerNickname);

			ShuffleDeck(BoardState.HostPlayerBoard.Deck);
			ShuffleDeck(BoardState.GuestPlayerBoard.Deck);

			DrawStartingHand(BoardState.HostPlayerBoard, 10);
			DrawStartingHand(BoardState.GuestPlayerBoard, 10);
		}

		/// <summary>
		/// Zwraca aktualny snapshot stanu planszy (do wysłania klientom).
		/// </summary>
		public GameBoardState GetCurrentBoardStateSnapshot()
		{
			return BoardState;
		}

		/// <summary>
		/// Stosuje akcję gracza do stanu gry i zwraca zaktualizowany snapshot.
		/// </summary>
		public GameBoardState ApplyGameAction(GameActionPayload gameActionPayload)
		{
			if (BoardState.IsGameFinished)
			{
				return BoardState;
			}

			switch (gameActionPayload.ActionType)
			{
				case GameActionType.PlayCard:
					ApplyPlayCardAction(gameActionPayload);
					break;

				case GameActionType.PassTurn:
					ApplyPassTurnAction(gameActionPayload);
					break;

				case GameActionType.Resign:
					ApplyResignAction(gameActionPayload);
					break;
			}

			return BoardState;
		}

		private List<GwentCard> CreateSimpleTestDeck(FactionType faction, string ownerNickname)
		{
			var deck = new List<GwentCard>();

			for (int i = 0; i < 6; i++)
			{
				deck.Add(new GwentCard
				{
					TemplateId = $"melee_{faction}_{i}",
					Name = $"{ownerNickname} Melee {i + 1}",
					Faction = faction,
					Category = CardCategory.Unit,
					BaseStrength = 4 + (i % 3),
					CurrentStrength = 4 + (i % 3),
					DefaultRow = CardRow.Melee
				});
			}

			for (int i = 0; i < 6; i++)
			{
				deck.Add(new GwentCard
				{
					TemplateId = $"ranged_{faction}_{i}",
					Name = $"{ownerNickname} Ranged {i + 1}",
					Faction = faction,
					Category = CardCategory.Unit,
					BaseStrength = 4 + (i % 3),
					CurrentStrength = 4 + (i % 3),
					DefaultRow = CardRow.Ranged
				});
			}

			for (int i = 0; i < 4; i++)
			{
				deck.Add(new GwentCard
				{
					TemplateId = $"siege_{faction}_{i}",
					Name = $"{ownerNickname} Siege {i + 1}",
					Faction = faction,
					Category = CardCategory.Unit,
					BaseStrength = 5 + (i % 2),
					CurrentStrength = 5 + (i % 2),
					DefaultRow = CardRow.Siege
				});
			}

			// TODO: dodać realne karty pogody / specjalne.

			return deck;
		}

		private GwentCard CreateLeaderCard(FactionType faction, string ownerNickname)
		{
			return new GwentCard
			{
				TemplateId = $"leader_{faction}",
				Name = $"{ownerNickname} Leader",
				Faction = faction,
				Category = CardCategory.Leader,
				BaseStrength = 0,
				CurrentStrength = 0,
				DefaultRow = CardRow.WeatherGlobal,
				IsHero = true
			};
		}

		private void ShuffleDeck(List<GwentCard> deck)
		{
			for (int i = deck.Count - 1; i > 0; i--)
			{
				int swapIndex = random.Next(i + 1);
				(deck[i], deck[swapIndex]) = (deck[swapIndex], deck[i]);
			}
		}

		private void DrawStartingHand(PlayerBoardState playerBoardState, int cardsToDraw)
		{
			for (int i = 0; i < cardsToDraw && playerBoardState.Deck.Count > 0; i++)
			{
				var topCard = playerBoardState.Deck[0];
				playerBoardState.Deck.RemoveAt(0);
				playerBoardState.Hand.Add(topCard);
			}
		}

		private PlayerBoardState GetPlayerBoardByNickname(string playerNickname)
		{
			if (BoardState.HostPlayerBoard.PlayerNickname == playerNickname)
			{
				return BoardState.HostPlayerBoard;
			}

			return BoardState.GuestPlayerBoard;
		}

		private PlayerBoardState GetOpponentBoardByNickname(string playerNickname)
		{
			if (BoardState.HostPlayerBoard.PlayerNickname == playerNickname)
			{
				return BoardState.GuestPlayerBoard;
			}

			return BoardState.HostPlayerBoard;
		}

		private void ApplyPlayCardAction(GameActionPayload gameActionPayload)
		{
			var actingPlayerBoard = GetPlayerBoardByNickname(gameActionPayload.ActingPlayerNickname);

			if (actingPlayerBoard.HasPassedCurrentRound)
			{
				// Gracz, który już zapasował, nie może zagrywać kart.
				return;
			}

			if (gameActionPayload.CardInstanceId == null)
			{
				return;
			}

			GwentCard? cardToPlay = actingPlayerBoard.Hand.FirstOrDefault(c => c.InstanceId == gameActionPayload.CardInstanceId);
			if (cardToPlay == null)
			{
				return;
			}

			actingPlayerBoard.Hand.Remove(cardToPlay);

			CardRow targetRow = cardToPlay.DefaultRow;
			if (cardToPlay.DefaultRow == CardRow.Agile && gameActionPayload.TargetRow.HasValue)
			{
				targetRow = gameActionPayload.TargetRow.Value;
			}

			switch (targetRow)
			{
				case CardRow.Melee:
					actingPlayerBoard.MeleeRow.Add(cardToPlay);
					break;
				case CardRow.Ranged:
					actingPlayerBoard.RangedRow.Add(cardToPlay);
					break;
				case CardRow.Siege:
					actingPlayerBoard.SiegeRow.Add(cardToPlay);
					break;
				case CardRow.WeatherGlobal:
					BoardState.WeatherCards.Add(cardToPlay);
					break;
				default:
					actingPlayerBoard.MeleeRow.Add(cardToPlay);
					break;
			}

			// TODO: zastosować efekty kart (MoralBoost, Horn, Weather itd.).

			SwitchTurnToOpponent(actingPlayerBoard.PlayerNickname);
		}

		private void ApplyPassTurnAction(GameActionPayload gameActionPayload)
		{
			var actingPlayerBoard = GetPlayerBoardByNickname(gameActionPayload.ActingPlayerNickname);

			if (actingPlayerBoard.HasPassedCurrentRound)
			{
				return;
			}

			actingPlayerBoard.HasPassedCurrentRound = true;

			var opponentBoard = GetOpponentBoardByNickname(gameActionPayload.ActingPlayerNickname);

			if (opponentBoard.HasPassedCurrentRound)
			{
				EndRound();
			}
			else
			{
				SwitchTurnToOpponent(actingPlayerBoard.PlayerNickname);
			}
		}

		private void ApplyResignAction(GameActionPayload gameActionPayload)
		{
			var actingPlayerBoard = GetPlayerBoardByNickname(gameActionPayload.ActingPlayerNickname);
			var opponentBoard = GetOpponentBoardByNickname(gameActionPayload.ActingPlayerNickname);

			BoardState.IsGameFinished = true;
			BoardState.WinnerNickname = opponentBoard.PlayerNickname;

			actingPlayerBoard.LifeTokensRemaining = 0;
		}

		/// <summary>
		/// Kończy rundę:
		/// - przegrany traci 1 życie,
		/// - jeśli ma 0 żyć → drugi gracz wygrywa grę.
		/// </summary>
		private void EndRound()
		{
			int hostStrength = BoardState.HostPlayerBoard.GetTotalStrength();
			int guestStrength = BoardState.GuestPlayerBoard.GetTotalStrength();

			PlayerBoardState? roundWinner = null;
			PlayerBoardState? roundLoser = null;

			if (hostStrength > guestStrength)
			{
				roundWinner = BoardState.HostPlayerBoard;
				roundLoser = BoardState.GuestPlayerBoard;
			}
			else if (guestStrength > hostStrength)
			{
				roundWinner = BoardState.GuestPlayerBoard;
				roundLoser = BoardState.HostPlayerBoard;
			}

			if (roundWinner != null && roundLoser != null)
			{
				roundWinner.RoundsWon++;
				roundLoser.LifeTokensRemaining--;

				// 👉 tu jest dokładnie to o co prosiłeś:
				// jeśli przegrany ma 0 żyć, gra się kończy, drugi wygrywa.
				if (roundLoser.LifeTokensRemaining <= 0)
				{
					BoardState.IsGameFinished = true;
					BoardState.WinnerNickname = roundWinner.PlayerNickname;
					return;
				}
			}

			ClearRows(BoardState.HostPlayerBoard);
			ClearRows(BoardState.GuestPlayerBoard);

			BoardState.HostPlayerBoard.HasPassedCurrentRound = false;
			BoardState.GuestPlayerBoard.HasPassedCurrentRound = false;

			BoardState.CurrentRoundNumber++;

			// nową rundę zaczyna host (na razie prosto)
			BoardState.ActivePlayerNickname = BoardState.HostPlayerBoard.PlayerNickname;
		}

		private void ClearRows(PlayerBoardState playerBoardState)
		{
			MoveRowToGraveyard(playerBoardState, playerBoardState.MeleeRow);
			MoveRowToGraveyard(playerBoardState, playerBoardState.RangedRow);
			MoveRowToGraveyard(playerBoardState, playerBoardState.SiegeRow);
		}

		private void MoveRowToGraveyard(PlayerBoardState playerBoardState, List<GwentCard> row)
		{
			foreach (var card in row)
			{
				playerBoardState.Graveyard.Add(card);
			}
			row.Clear();
		}

		private void SwitchTurnToOpponent(string currentPlayerNickname)
		{
			var opponentBoard = GetOpponentBoardByNickname(currentPlayerNickname);
			BoardState.ActivePlayerNickname = opponentBoard.PlayerNickname;
		}
	}
}
