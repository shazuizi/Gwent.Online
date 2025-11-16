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
		/// tasuje je i rozdaje początkowe karty.
		/// </summary>
		public void InitializeNewGame(GameSessionConfiguration sessionConfiguration)
		{
			BoardState = new GameBoardState
			{
				HostPlayerBoard = new PlayerBoardState
				{
					PlayerNickname = sessionConfiguration.HostPlayer.Nickname
				},
				GuestPlayerBoard = new PlayerBoardState
				{
					PlayerNickname = sessionConfiguration.GuestPlayer.Nickname
				},
				ActivePlayerNickname = sessionConfiguration.HostPlayer.Nickname,
				CurrentRoundNumber = 1,
				IsGameFinished = false
			};

			// Na razie generujemy proste talie testowe – później można wczytać z plików / deck editora.
			BoardState.HostPlayerBoard.Deck = CreateSimpleTestDeck(FactionType.NorthernRealms, BoardState.HostPlayerBoard.PlayerNickname);
			BoardState.GuestPlayerBoard.Deck = CreateSimpleTestDeck(FactionType.Nilfgaard, BoardState.GuestPlayerBoard.PlayerNickname);

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
			// na razie zwracamy referencję; jeśli chcesz pełne kopie – można dodać głębokie klonowanie
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

			if (gameActionPayload.ActionType == GameActionType.PlayCard)
			{
				ApplyPlayCardAction(gameActionPayload);
			}
			else if (gameActionPayload.ActionType == GameActionType.PassTurn)
			{
				ApplyPassTurnAction(gameActionPayload);
			}

			return BoardState;
		}

		/// <summary>
		/// Tworzy prostą przykładową talię testową (kilka kart na każdy rząd).
		/// </summary>
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

			// TODO: dodać karty pogody, Commander's Horn, Scorch itd.

			return deck;
		}

		/// <summary>
		/// Tasuje talię gracza.
		/// </summary>
		private void ShuffleDeck(List<GwentCard> deck)
		{
			for (int i = deck.Count - 1; i > 0; i--)
			{
				int swapIndex = random.Next(i + 1);
				(deck[i], deck[swapIndex]) = (deck[swapIndex], deck[i]);
			}
		}

		/// <summary>
		/// Dobiera określoną liczbę kart z talii do ręki (jeśli są dostępne).
		/// </summary>
		private void DrawStartingHand(PlayerBoardState playerBoardState, int cardsToDraw)
		{
			for (int i = 0; i < cardsToDraw && playerBoardState.Deck.Count > 0; i++)
			{
				var topCard = playerBoardState.Deck[0];
				playerBoardState.Deck.RemoveAt(0);
				playerBoardState.Hand.Add(topCard);
			}
		}

		/// <summary>
		/// Zwraca stan planszy gracza po jego nicku.
		/// </summary>
		private PlayerBoardState GetPlayerBoardByNickname(string playerNickname)
		{
			if (BoardState.HostPlayerBoard.PlayerNickname == playerNickname)
			{
				return BoardState.HostPlayerBoard;
			}

			return BoardState.GuestPlayerBoard;
		}

		/// <summary>
		/// Zwraca stan planszy przeciwnika dla podanego nicku.
		/// </summary>
		private PlayerBoardState GetOpponentBoardByNickname(string playerNickname)
		{
			if (BoardState.HostPlayerBoard.PlayerNickname == playerNickname)
			{
				return BoardState.GuestPlayerBoard;
			}

			return BoardState.HostPlayerBoard;
		}

		/// <summary>
		/// Stosuje akcję zagrania karty – przenosi kartę z ręki na odpowiedni rząd.
		/// </summary>
		private void ApplyPlayCardAction(GameActionPayload gameActionPayload)
		{
			var actingPlayerBoard = GetPlayerBoardByNickname(gameActionPayload.ActingPlayerNickname);

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
				default:
					actingPlayerBoard.MeleeRow.Add(cardToPlay);
					break;
			}

			// TODO: zastosować efekty kart (MoralBoost, Horn, Weather itd.).

			SwitchTurnToOpponent(actingPlayerBoard.PlayerNickname);
		}

		/// <summary>
		/// Stosuje akcję "pass" – gracz rezygnuje z dalszego zagrywania w tej rundzie.
		/// Gdy obaj zrobią pass, kończy rundę i przygotowuje następną lub kończy grę.
		/// </summary>
		private void ApplyPassTurnAction(GameActionPayload gameActionPayload)
		{
			var actingPlayerBoard = GetPlayerBoardByNickname(gameActionPayload.ActingPlayerNickname);
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

		/// <summary>
		/// Kończy aktualną rundę – przyznaje punkt zwycięzcy, czyści rzędy i przechodzi do kolejnej rundy
		/// lub kończy grę, jeśli ktoś wygrał wymaganą liczbę rund.
		/// </summary>
		private void EndRound()
		{
			int hostStrength = BoardState.HostPlayerBoard.GetTotalStrength();
			int guestStrength = BoardState.GuestPlayerBoard.GetTotalStrength();

			if (hostStrength > guestStrength)
			{
				BoardState.HostPlayerBoard.RoundsWon++;
			}
			else if (guestStrength > hostStrength)
			{
				BoardState.GuestPlayerBoard.RoundsWon++;
			}
			else
			{
				// Remis – brak punktu.
			}

			// Sprawdzenie zakończenia gry – klasycznie: 2 wygrane rundy.
			if (BoardState.HostPlayerBoard.RoundsWon >= 2 ||
				BoardState.GuestPlayerBoard.RoundsWon >= 2)
			{
				BoardState.IsGameFinished = true;
				BoardState.WinnerNickname = BoardState.HostPlayerBoard.RoundsWon > BoardState.GuestPlayerBoard.RoundsWon
					? BoardState.HostPlayerBoard.PlayerNickname
					: BoardState.GuestPlayerBoard.PlayerNickname;
				return;
			}

			// Czyścimy rzędy, flagi pass, zwiększamy numer rundy.
			ClearRows(BoardState.HostPlayerBoard);
			ClearRows(BoardState.GuestPlayerBoard);

			BoardState.HostPlayerBoard.HasPassedCurrentRound = false;
			BoardState.GuestPlayerBoard.HasPassedCurrentRound = false;

			BoardState.CurrentRoundNumber++;

			// W następnej rundzie zaczyna zwycięzca poprzedniej (tu: prosty wariant – host zawsze zaczyna).
			BoardState.ActivePlayerNickname = BoardState.HostPlayerBoard.PlayerNickname;
		}

		/// <summary>
		/// Czyści wszystkie rzędy jednostek danego gracza (karty idą na cmentarz).
		/// </summary>
		private void ClearRows(PlayerBoardState playerBoardState)
		{
			MoveRowToGraveyard(playerBoardState, playerBoardState.MeleeRow);
			MoveRowToGraveyard(playerBoardState, playerBoardState.RangedRow);
			MoveRowToGraveyard(playerBoardState, playerBoardState.SiegeRow);
		}

		/// <summary>
		/// Przenosi wszystkie karty z rzędu na cmentarz.
		/// </summary>
		private void MoveRowToGraveyard(PlayerBoardState playerBoardState, List<GwentCard> row)
		{
			foreach (var card in row)
			{
				playerBoardState.Graveyard.Add(card);
			}
			row.Clear();
		}

		/// <summary>
		/// Zmienia kolej na przeciwnika.
		/// </summary>
		private void SwitchTurnToOpponent(string currentPlayerNickname)
		{
			var opponentBoard = GetOpponentBoardByNickname(currentPlayerNickname);
			BoardState.ActivePlayerNickname = opponentBoard.PlayerNickname;
		}
	}
}
