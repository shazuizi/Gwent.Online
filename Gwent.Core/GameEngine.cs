using System;
using System.Collections.Generic;
using System.Linq;

namespace Gwent.Core
{
	public class GameEngine
	{
		private readonly Random random = new Random();

		private bool mainPhaseStarted;

		public GameBoardState BoardState { get; private set; } = new GameBoardState();

		public void InitializeNewGame(GameSessionConfiguration sessionConfiguration)
		{
			BoardState = new GameBoardState
			{
				HostPlayerBoard = new PlayerBoardState
				{
					PlayerNickname = sessionConfiguration.HostPlayer.Nickname,
					Faction = FactionType.NorthernRealms,
					LifeTokensRemaining = 2,
					RoundsWon = 0,
					HasPassedCurrentRound = false,
					MulligansRemaining = 2
				},
				GuestPlayerBoard = new PlayerBoardState
				{
					PlayerNickname = sessionConfiguration.GuestPlayer.Nickname,
					Faction = FactionType.Nilfgaard,
					LifeTokensRemaining = 2,
					RoundsWon = 0,
					HasPassedCurrentRound = false,
					MulligansRemaining = 2
				},
				CurrentRoundNumber = 1,
				IsGameFinished = false,
				WinnerNickname = null,
				WeatherCards = new List<GwentCard>()
			};

			BoardState.ActivePlayerNickname = DetermineStartingPlayerNickname();

			BoardState.HostPlayerBoard.Deck = CreateSimpleTestDeck(BoardState.HostPlayerBoard.Faction, BoardState.HostPlayerBoard.PlayerNickname);
			BoardState.GuestPlayerBoard.Deck = CreateSimpleTestDeck(BoardState.GuestPlayerBoard.Faction, BoardState.GuestPlayerBoard.PlayerNickname);

			BoardState.HostPlayerBoard.LeaderCard = CreateLeaderCard(BoardState.HostPlayerBoard.Faction, BoardState.HostPlayerBoard.PlayerNickname);
			BoardState.GuestPlayerBoard.LeaderCard = CreateLeaderCard(BoardState.GuestPlayerBoard.Faction, BoardState.GuestPlayerBoard.PlayerNickname);

			ShuffleDeck(BoardState.HostPlayerBoard.Deck);
			ShuffleDeck(BoardState.GuestPlayerBoard.Deck);

			DrawFromDeck(BoardState.HostPlayerBoard, 10);
			DrawFromDeck(BoardState.GuestPlayerBoard, 10);

			RecalculateStrengths();
		}

		public GameBoardState GetCurrentBoardStateSnapshot() => BoardState;

		public GameBoardState ApplyGameAction(GameActionPayload action)
		{
			if (BoardState.IsGameFinished)
				return BoardState;

			switch (action.ActionType)
			{
				case GameActionType.Mulligan:
					ApplyMulliganAction(action);
					break;

				case GameActionType.PlayCard:
					mainPhaseStarted = true;
					ApplyPlayCardAction(action);
					break;

				case GameActionType.PassTurn:
					mainPhaseStarted = true;
					ApplyPassTurnAction(action);
					break;

				case GameActionType.Resign:
					mainPhaseStarted = true;
					ApplyResignAction(action);
					break;

				case GameActionType.UseLeaderAbility:
					mainPhaseStarted = true;
					ApplyLeaderAbilityAction(action);
					break;
			}

			return BoardState;
		}

		#region Init / helpers

		private string DetermineStartingPlayerNickname()
		{
			var host = BoardState.HostPlayerBoard;
			var guest = BoardState.GuestPlayerBoard;

			if (host.Faction == FactionType.Scoiatael && guest.Faction != FactionType.Scoiatael)
				return host.PlayerNickname;

			if (guest.Faction == FactionType.Scoiatael && host.Faction != FactionType.Scoiatael)
				return guest.PlayerNickname;

			return host.PlayerNickname;
		}

		private List<GwentCard> CreateSimpleTestDeck(FactionType faction, string ownerNickname)
		{
			var deck = new List<GwentCard>();

			for (int i = 0; i < 3; i++)
			{
				deck.Add(new GwentCard
				{
					TemplateId = $"melee_basic_{faction}_{i}",
					Name = $"{ownerNickname} Melee {i + 1}",
					Faction = faction,
					Category = CardCategory.Unit,
					BaseStrength = 4 + (i % 3),
					CurrentStrength = 4 + (i % 3),
					DefaultRow = CardRow.Melee
				});
			}

			for (int i = 0; i < 3; i++)
			{
				deck.Add(new GwentCard
				{
					TemplateId = $"tightbond_{faction}_{i}",
					Name = $"{ownerNickname} Bond {i + 1}",
					Faction = faction,
					Category = CardCategory.Unit,
					BaseStrength = 4,
					CurrentStrength = 4,
					DefaultRow = CardRow.Melee,
					GroupId = "bond_group_1",
					Abilities = { CardAbilityType.TightBond }
				});
			}

			deck.Add(new GwentCard
			{
				TemplateId = $"spy_{faction}",
				Name = $"{ownerNickname} Spy",
				Faction = faction,
				Category = CardCategory.Unit,
				BaseStrength = 4,
				CurrentStrength = 4,
				DefaultRow = CardRow.Ranged,
				Abilities = { CardAbilityType.Spy }
			});

			for (int i = 0; i < 3; i++)
			{
				deck.Add(new GwentCard
				{
					TemplateId = $"muster_{faction}_{i}",
					Name = $"{ownerNickname} Muster {i + 1}",
					Faction = faction,
					Category = CardCategory.Unit,
					BaseStrength = 2,
					CurrentStrength = 2,
					DefaultRow = CardRow.Melee,
					GroupId = "muster_group_1",
					Abilities = { CardAbilityType.Muster }
				});
			}

			deck.Add(new GwentCard
			{
				TemplateId = $"morale_{faction}",
				Name = $"{ownerNickname} Morale",
				Faction = faction,
				Category = CardCategory.Unit,
				BaseStrength = 3,
				CurrentStrength = 3,
				DefaultRow = CardRow.Melee,
				Abilities = { CardAbilityType.MoralBoost }
			});

			deck.Add(new GwentCard
			{
				TemplateId = $"horn_{faction}",
				Name = $"{ownerNickname} Horn",
				Faction = faction,
				Category = CardCategory.Special,
				BaseStrength = 0,
				CurrentStrength = 0,
				DefaultRow = CardRow.Melee,
				Abilities = { CardAbilityType.CommandersHorn }
			});

			deck.Add(new GwentCard
			{
				TemplateId = $"scorch_{faction}",
				Name = $"{ownerNickname} Scorch",
				Faction = faction,
				Category = CardCategory.Special,
				BaseStrength = 0,
				CurrentStrength = 0,
				DefaultRow = CardRow.WeatherGlobal,
				Abilities = { CardAbilityType.Scorch }
			});

			deck.Add(new GwentCard
			{
				TemplateId = $"frost_{faction}",
				Name = "Biting Frost",
				Faction = FactionType.Neutral,
				Category = CardCategory.Weather,
				BaseStrength = 0,
				CurrentStrength = 0,
				DefaultRow = CardRow.WeatherGlobal,
				Abilities = { CardAbilityType.WeatherBitingFrost }
			});

			deck.Add(new GwentCard
			{
				TemplateId = $"fog_{faction}",
				Name = "Impenetrable Fog",
				Faction = FactionType.Neutral,
				Category = CardCategory.Weather,
				BaseStrength = 0,
				CurrentStrength = 0,
				DefaultRow = CardRow.WeatherGlobal,
				Abilities = { CardAbilityType.WeatherImpenetrableFog }
			});

			deck.Add(new GwentCard
			{
				TemplateId = $"rain_{faction}",
				Name = "Torrential Rain",
				Faction = FactionType.Neutral,
				Category = CardCategory.Weather,
				BaseStrength = 0,
				CurrentStrength = 0,
				DefaultRow = CardRow.WeatherGlobal,
				Abilities = { CardAbilityType.WeatherTorrentialRain }
			});

			deck.Add(new GwentCard
			{
				TemplateId = $"clear_{faction}",
				Name = "Clear Weather",
				Faction = FactionType.Neutral,
				Category = CardCategory.Weather,
				BaseStrength = 0,
				CurrentStrength = 0,
				DefaultRow = CardRow.WeatherGlobal,
				Abilities = { CardAbilityType.ClearWeather }
			});

			deck.Add(new GwentCard
			{
				TemplateId = $"decoy_{faction}",
				Name = "Decoy",
				Faction = FactionType.Neutral,
				Category = CardCategory.Special,
				BaseStrength = 0,
				CurrentStrength = 0,
				DefaultRow = CardRow.Melee,
				Abilities = { CardAbilityType.Decoy }
			});

			deck.Add(new GwentCard
			{
				TemplateId = $"mardroeme_{faction}",
				Name = "Mardroeme",
				Faction = FactionType.Neutral,
				Category = CardCategory.Special,
				BaseStrength = 0,
				CurrentStrength = 0,
				DefaultRow = CardRow.Melee,
				Abilities = { CardAbilityType.Mardroeme }
			});

			return deck;
		}

		private GwentCard CreateLeaderCard(FactionType faction, string ownerNickname)
		{
			var leader = new GwentCard
			{
				TemplateId = $"leader_{faction}",
				Name = $"{ownerNickname} Leader",
				Faction = faction,
				Category = CardCategory.Leader,
				BaseStrength = 0,
				CurrentStrength = 0,
				DefaultRow = CardRow.WeatherGlobal,
				IsHero = true,
				Abilities = new List<CardAbilityType>()
			};

			if (faction == FactionType.NorthernRealms)
			{
				// prosty przykład – NR leader dobiera 1 kartę
				leader.Abilities.Add(CardAbilityType.LeaderAbility_DrawExtraCard);
			}

			return leader;
		}

		private void ShuffleDeck(List<GwentCard> deck)
		{
			for (int i = deck.Count - 1; i > 0; i--)
			{
				int swapIndex = random.Next(i + 1);
				(deck[i], deck[swapIndex]) = (deck[swapIndex], deck[i]);
			}
		}

		private void DrawFromDeck(PlayerBoardState player, int count)
		{
			for (int i = 0; i < count && player.Deck.Count > 0; i++)
			{
				var top = player.Deck[0];
				player.Deck.RemoveAt(0);
				player.Hand.Add(top);
			}
		}

		private PlayerBoardState GetPlayerBoard(string nickname)
		{
			if (BoardState.HostPlayerBoard.PlayerNickname == nickname)
				return BoardState.HostPlayerBoard;
			return BoardState.GuestPlayerBoard;
		}

		private PlayerBoardState GetOpponentBoard(string nickname)
		{
			if (BoardState.HostPlayerBoard.PlayerNickname == nickname)
				return BoardState.GuestPlayerBoard;
			return BoardState.HostPlayerBoard;
		}

		#endregion

		#region Mulligan

		private void ApplyMulliganAction(GameActionPayload action)
		{
			if (BoardState.CurrentRoundNumber != 1 || mainPhaseStarted)
				return;

			var player = GetPlayerBoard(action.ActingPlayerNickname);
			if (player.MulligansRemaining <= 0 || action.CardInstanceId == null)
				return;

			var card = player.Hand.FirstOrDefault(c => c.InstanceId == action.CardInstanceId);
			if (card == null || player.Deck.Count == 0)
				return;

			player.Hand.Remove(card);

			int insertIndex = random.Next(player.Deck.Count + 1);
			player.Deck.Insert(insertIndex, card);

			DrawFromDeck(player, 1);

			player.MulligansRemaining--;

			RecalculateStrengths();
		}

		#endregion

		#region Play card + targeted abilities

		private void ApplyPlayCardAction(GameActionPayload action)
		{
			var actingPlayer = GetPlayerBoard(action.ActingPlayerNickname);
			var opponent = GetOpponentBoard(action.ActingPlayerNickname);

			if (actingPlayer.HasPassedCurrentRound)
				return;

			if (action.CardInstanceId == null)
				return;

			var card = actingPlayer.Hand.FirstOrDefault(c => c.InstanceId == action.CardInstanceId);
			if (card == null)
				return;

			actingPlayer.Hand.Remove(card);

			if (card.HasAbility(CardAbilityType.Spy))
			{
				PlaceUnitOnRow(card, opponent, card.DefaultRow, action.TargetRow);
				DrawFromDeck(actingPlayer, 2);
			}
			else if (card.Category == CardCategory.Weather &&
					 (card.HasAbility(CardAbilityType.WeatherBitingFrost) ||
					  card.HasAbility(CardAbilityType.WeatherImpenetrableFog) ||
					  card.HasAbility(CardAbilityType.WeatherTorrentialRain)))
			{
				BoardState.WeatherCards.Add(card);
			}
			else if (card.HasAbility(CardAbilityType.ClearWeather))
			{
				BoardState.WeatherCards.Clear();
				actingPlayer.Graveyard.Add(card);
			}
			else if (card.HasAbility(CardAbilityType.Decoy))
			{
				ApplyDecoyEffect(actingPlayer, card, action.TargetInstanceId);
			}
			else if (card.HasAbility(CardAbilityType.Mardroeme))
			{
				ApplyMardroemeEffect(actingPlayer, card, action.TargetInstanceId);
			}
			else if (card.HasAbility(CardAbilityType.Scorch) && card.Category == CardCategory.Special)
			{
				ApplyScorchEffectGlobal();
				actingPlayer.Graveyard.Add(card);
			}
			else
			{
				PlaceUnitOnRow(card, actingPlayer, card.DefaultRow, action.TargetRow);

				if (card.HasAbility(CardAbilityType.Muster))
					ApplyMusterEffect(actingPlayer, card);

				if (card.HasAbility(CardAbilityType.Medic))
					ApplyMedicEffect(actingPlayer, card, action.TargetInstanceId);
			}

			RecalculateStrengths();
			SwitchTurnToOpponent(actingPlayer.PlayerNickname);
		}

		private void PlaceUnitOnRow(GwentCard card, PlayerBoardState owner, CardRow defaultRow, CardRow? chosenRow)
		{
			CardRow row = defaultRow;
			if (card.HasAbility(CardAbilityType.Agile) && chosenRow.HasValue)
				row = chosenRow.Value;

			var rowList = row switch
			{
				CardRow.Melee => owner.MeleeRow,
				CardRow.Ranged => owner.RangedRow,
				CardRow.Siege => owner.SiegeRow,
				_ => owner.MeleeRow
			};

			rowList.Add(card);
		}

		private void ApplyMusterEffect(PlayerBoardState player, GwentCard sourceCard)
		{
			if (string.IsNullOrEmpty(sourceCard.GroupId))
				return;

			var mustersFromHand = player.Hand
				.Where(c => c.GroupId == sourceCard.GroupId && c.TemplateId == sourceCard.TemplateId)
				.ToList();

			foreach (var card in mustersFromHand)
			{
				player.Hand.Remove(card);
				PlaceUnitOnRow(card, player, card.DefaultRow, null);
			}

			var mustersFromDeck = player.Deck
				.Where(c => c.GroupId == sourceCard.GroupId && c.TemplateId == sourceCard.TemplateId)
				.ToList();

			foreach (var card in mustersFromDeck)
			{
				player.Deck.Remove(card);
				PlaceUnitOnRow(card, player, card.DefaultRow, null);
			}
		}

		private void ApplyMedicEffect(PlayerBoardState player, GwentCard medicCard, string? targetInstanceId)
		{
			GwentCard? candidate = null;

			if (!string.IsNullOrEmpty(targetInstanceId))
			{
				candidate = player.Graveyard
					.FirstOrDefault(c => c.InstanceId == targetInstanceId &&
										 c.Category == CardCategory.Unit &&
										 !c.IsHero);
			}

			if (candidate == null)
			{
				candidate = player.Graveyard
					.Where(c => c.Category == CardCategory.Unit && !c.IsHero)
					.OrderByDescending(c => c.BaseStrength)
					.FirstOrDefault();
			}

			if (candidate == null)
				return;

			player.Graveyard.Remove(candidate);
			PlaceUnitOnRow(candidate, player, medicCard.DefaultRow, null);
		}

		private void ApplyDecoyEffect(PlayerBoardState player, GwentCard decoy, string? targetInstanceId)
		{
			var allRows = new[]
			{
				(row: CardRow.Melee,  list: player.MeleeRow),
				(row: CardRow.Ranged, list: player.RangedRow),
				(row: CardRow.Siege,  list: player.SiegeRow)
			};

			(GwentCard card, CardRow row, List<GwentCard> list)? target = null;

			if (!string.IsNullOrEmpty(targetInstanceId))
			{
				foreach (var t in allRows)
				{
					var c = t.list.FirstOrDefault(x => x.InstanceId == targetInstanceId &&
													   x.Category == CardCategory.Unit &&
													   !x.IsHero);
					if (c != null)
					{
						target = (c, t.row, t.list);
						break;
					}
				}
			}

			if (target == null)
			{
				var unitsOnBoard = allRows
					.SelectMany(t => t.list.Select(c => (card: c, row: t.row, list: t.list)))
					.Where(x => x.card.Category == CardCategory.Unit && !x.card.IsHero)
					.ToList();

				if (!unitsOnBoard.Any())
				{
					player.Graveyard.Add(decoy);
					return;
				}

				target = unitsOnBoard.OrderByDescending(x => x.card.CurrentStrength).First();
			}

			var chosen = target.Value;

			chosen.list.Remove(chosen.card);
			player.Hand.Add(chosen.card);

			chosen.list.Add(decoy);
		}

		private void ApplyMardroemeEffect(PlayerBoardState player, GwentCard mardroemeCard, string? targetInstanceId)
		{
			GwentCard? target = null;

			var allUnits = player.MeleeRow
				.Concat(player.RangedRow)
				.Concat(player.SiegeRow)
				.Where(c => c.Category == CardCategory.Unit && !c.IsHero);

			if (!string.IsNullOrEmpty(targetInstanceId))
			{
				target = allUnits.FirstOrDefault(c => c.InstanceId == targetInstanceId);
			}

			if (target == null)
			{
				target = allUnits.OrderByDescending(c => c.CurrentStrength).FirstOrDefault();
			}

			if (target == null)
			{
				player.Graveyard.Add(mardroemeCard);
				return;
			}

			target.BaseStrength = 13;
			player.Graveyard.Add(mardroemeCard);
		}

		#endregion

		#region Pass / Resign / Leader

		private void ApplyPassTurnAction(GameActionPayload action)
		{
			var player = GetPlayerBoard(action.ActingPlayerNickname);
			if (player.HasPassedCurrentRound)
				return;

			player.HasPassedCurrentRound = true;

			var opponent = GetOpponentBoard(action.ActingPlayerNickname);

			if (opponent.HasPassedCurrentRound)
			{
				EndRound();
			}
			else
			{
				SwitchTurnToOpponent(player.PlayerNickname);
			}
		}

		private void ApplyResignAction(GameActionPayload action)
		{
			var player = GetPlayerBoard(action.ActingPlayerNickname);
			var opponent = GetOpponentBoard(action.ActingPlayerNickname);

			BoardState.IsGameFinished = true;
			BoardState.WinnerNickname = opponent.PlayerNickname;

			player.LifeTokensRemaining = 0;
		}

		private void ApplyLeaderAbilityAction(GameActionPayload action)
		{
			var player = GetPlayerBoard(action.ActingPlayerNickname);
			if (player.LeaderCard == null || player.LeaderAbilityUsed)
				return;

			var leader = player.LeaderCard;

			if (leader.HasAbility(CardAbilityType.LeaderAbility_DrawExtraCard))
			{
				DrawFromDeck(player, 1);
			}

			player.LeaderAbilityUsed = true;
			RecalculateStrengths();
			SwitchTurnToOpponent(player.PlayerNickname);
		}

		private void EndRound()
		{
			RecalculateStrengths();

			var host = BoardState.HostPlayerBoard;
			var guest = BoardState.GuestPlayerBoard;

			int hostStrength = host.GetTotalStrength();
			int guestStrength = guest.GetTotalStrength();

			PlayerBoardState? roundWinner = null;
			PlayerBoardState? roundLoser = null;

			if (hostStrength > guestStrength)
			{
				roundWinner = host;
				roundLoser = guest;
			}
			else if (guestStrength > hostStrength)
			{
				roundWinner = guest;
				roundLoser = host;
			}
			else
			{
				if (host.Faction == FactionType.Nilfgaard && guest.Faction != FactionType.Nilfgaard)
				{
					roundWinner = host;
					roundLoser = guest;
				}
				else if (guest.Faction == FactionType.Nilfgaard && host.Faction != FactionType.Nilfgaard)
				{
					roundWinner = guest;
					roundLoser = host;
				}
			}

			if (roundWinner != null && roundLoser != null)
			{
				roundWinner.RoundsWon++;
				roundLoser.LifeTokensRemaining--;

				if (roundWinner.Faction == FactionType.NorthernRealms)
					DrawFromDeck(roundWinner, 1);

				if (roundLoser.LifeTokensRemaining <= 0)
				{
					BoardState.IsGameFinished = true;
					BoardState.WinnerNickname = roundWinner.PlayerNickname;
				}
			}

			ClearRows(host);
			ClearRows(guest);

			host.HasPassedCurrentRound = false;
			guest.HasPassedCurrentRound = false;

			BoardState.WeatherCards.Clear();
			BoardState.CurrentRoundNumber++;
			mainPhaseStarted = false;

			if (!BoardState.IsGameFinished)
				BoardState.ActivePlayerNickname = host.PlayerNickname;

			RecalculateStrengths();
		}

		private void ClearRows(PlayerBoardState player)
		{
			MoveRowToGraveyard(player, player.MeleeRow);
			MoveRowToGraveyard(player, player.RangedRow);
			MoveRowToGraveyard(player, player.SiegeRow);
		}

		private void MoveRowToGraveyard(PlayerBoardState player, List<GwentCard> row)
		{
			foreach (var card in row)
			{
				player.Graveyard.Add(card);
			}
			row.Clear();
		}

		private void SwitchTurnToOpponent(string currentPlayerNickname)
		{
			var currentPlayer = GetPlayerBoard(currentPlayerNickname);
			var opponentPlayer = GetOpponentBoard(currentPlayerNickname);

			// Jeśli przeciwnik już spasował, a bieżący gracz NIE spasował,
			// to tura musi zostać przy bieżącym graczu (on dogrywa sam do końca rundy).
			if (opponentPlayer.HasPassedCurrentRound && !currentPlayer.HasPassedCurrentRound)
			{
				BoardState.ActivePlayerNickname = currentPlayer.PlayerNickname;
			}
			else
			{
				// normalny przypadek – zmiana tury
				BoardState.ActivePlayerNickname = opponentPlayer.PlayerNickname;
			}
		}


		#endregion

		#region Scorch / Recalc

		private void ApplyScorchEffectGlobal()
		{
			var allUnits = new List<(PlayerBoardState player, List<GwentCard> list, GwentCard card)>();

			void Collect(PlayerBoardState p, List<GwentCard> row)
			{
				foreach (var c in row)
				{
					if (c.Category == CardCategory.Unit && !c.IsHero)
						allUnits.Add((p, row, c));
				}
			}

			Collect(BoardState.HostPlayerBoard, BoardState.HostPlayerBoard.MeleeRow);
			Collect(BoardState.HostPlayerBoard, BoardState.HostPlayerBoard.RangedRow);
			Collect(BoardState.HostPlayerBoard, BoardState.HostPlayerBoard.SiegeRow);

			Collect(BoardState.GuestPlayerBoard, BoardState.GuestPlayerBoard.MeleeRow);
			Collect(BoardState.GuestPlayerBoard, BoardState.GuestPlayerBoard.RangedRow);
			Collect(BoardState.GuestPlayerBoard, BoardState.GuestPlayerBoard.SiegeRow);

			if (!allUnits.Any())
				return;

			int maxStrength = allUnits.Max(x => x.card.CurrentStrength);
			var toRemove = allUnits.Where(x => x.card.CurrentStrength == maxStrength).ToList();

			foreach (var entry in toRemove)
			{
				entry.list.Remove(entry.card);
				entry.player.Graveyard.Add(entry.card);
			}
		}

		private void RecalculateStrengths()
		{
			void Reset(PlayerBoardState p)
			{
				foreach (var c in p.MeleeRow.Concat(p.RangedRow).Concat(p.SiegeRow))
				{
					c.CurrentStrength = c.BaseStrength;
				}
			}

			Reset(BoardState.HostPlayerBoard);
			Reset(BoardState.GuestPlayerBoard);

			bool frost = BoardState.WeatherCards.Any(c => c.HasAbility(CardAbilityType.WeatherBitingFrost));
			bool fog = BoardState.WeatherCards.Any(c => c.HasAbility(CardAbilityType.WeatherImpenetrableFog));
			bool rain = BoardState.WeatherCards.Any(c => c.HasAbility(CardAbilityType.WeatherTorrentialRain));

			void ApplyWeatherToRow(List<GwentCard> row)
			{
				foreach (var c in row)
				{
					if (!c.IsHero && c.Category == CardCategory.Unit)
						c.CurrentStrength = 1;
				}
			}

			if (frost)
			{
				ApplyWeatherToRow(BoardState.HostPlayerBoard.MeleeRow);
				ApplyWeatherToRow(BoardState.GuestPlayerBoard.MeleeRow);
			}

			if (fog)
			{
				ApplyWeatherToRow(BoardState.HostPlayerBoard.RangedRow);
				ApplyWeatherToRow(BoardState.GuestPlayerBoard.RangedRow);
			}

			if (rain)
			{
				ApplyWeatherToRow(BoardState.HostPlayerBoard.SiegeRow);
				ApplyWeatherToRow(BoardState.GuestPlayerBoard.SiegeRow);
			}

			void ApplyHorn(PlayerBoardState p, List<GwentCard> row)
			{
				bool hornPresent = row.Any(c => c.HasAbility(CardAbilityType.CommandersHorn));
				if (!hornPresent) return;

				foreach (var c in row)
				{
					if (c.Category == CardCategory.Unit && !c.IsHero)
						c.CurrentStrength *= 2;
				}
			}

			ApplyHorn(BoardState.HostPlayerBoard, BoardState.HostPlayerBoard.MeleeRow);
			ApplyHorn(BoardState.HostPlayerBoard, BoardState.HostPlayerBoard.RangedRow);
			ApplyHorn(BoardState.HostPlayerBoard, BoardState.HostPlayerBoard.SiegeRow);

			ApplyHorn(BoardState.GuestPlayerBoard, BoardState.GuestPlayerBoard.MeleeRow);
			ApplyHorn(BoardState.GuestPlayerBoard, BoardState.GuestPlayerBoard.RangedRow);
			ApplyHorn(BoardState.GuestPlayerBoard, BoardState.GuestPlayerBoard.SiegeRow);

			void ApplyMorale(PlayerBoardState p, List<GwentCard> row)
			{
				int moraleCount = row.Count(c => c.HasAbility(CardAbilityType.MoralBoost));
				if (moraleCount == 0) return;

				foreach (var c in row)
				{
					if (!c.HasAbility(CardAbilityType.MoralBoost) &&
						c.Category == CardCategory.Unit && !c.IsHero)
					{
						c.CurrentStrength += moraleCount;
					}
				}
			}

			ApplyMorale(BoardState.HostPlayerBoard, BoardState.HostPlayerBoard.MeleeRow);
			ApplyMorale(BoardState.HostPlayerBoard, BoardState.HostPlayerBoard.RangedRow);
			ApplyMorale(BoardState.HostPlayerBoard, BoardState.HostPlayerBoard.SiegeRow);

			ApplyMorale(BoardState.GuestPlayerBoard, BoardState.GuestPlayerBoard.MeleeRow);
			ApplyMorale(BoardState.GuestPlayerBoard, BoardState.GuestPlayerBoard.RangedRow);
			ApplyMorale(BoardState.GuestPlayerBoard, BoardState.GuestPlayerBoard.SiegeRow);

			void ApplyTightBond(List<GwentCard> row)
			{
				var groups = row
					.Where(c => c.HasAbility(CardAbilityType.TightBond) && !string.IsNullOrEmpty(c.GroupId))
					.GroupBy(c => c.GroupId!);

				foreach (var g in groups)
				{
					int count = g.Count();
					if (count <= 1) continue;

					foreach (var c in g)
					{
						c.CurrentStrength *= count;
					}
				}
			}

			ApplyTightBond(BoardState.HostPlayerBoard.MeleeRow);
			ApplyTightBond(BoardState.HostPlayerBoard.RangedRow);
			ApplyTightBond(BoardState.HostPlayerBoard.SiegeRow);

			ApplyTightBond(BoardState.GuestPlayerBoard.MeleeRow);
			ApplyTightBond(BoardState.GuestPlayerBoard.RangedRow);
			ApplyTightBond(BoardState.GuestPlayerBoard.SiegeRow);
		}

		#endregion
	}
}
