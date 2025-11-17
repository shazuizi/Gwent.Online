namespace Gwent.Core
{
	public class GameEngine
	{
		private readonly Random random = new Random();

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

			AddLogEntry("Game started.");

			RecalculateStrengths();
		}

		public GameBoardState GetCurrentBoardStateSnapshot() => BoardState;

		public GameBoardState ApplyGameAction(GameActionPayload action)
		{
			// Gra skończona – nic już nie rób
			if (BoardState.IsGameFinished)
			{
				return BoardState;
			}

			// Centralna walidacja – JEDNO miejsce z zasadami „czy akcja w ogóle jest dopuszczalna”
			if (!ValidateAction(action, out var actingPlayer, out var opponentPlayer))
			{
				AddLogEntry($"[Engine] Invalid action {action.ActionType} from {action.ActingPlayerNickname}.");
				return BoardState;
			}

			switch (action.ActionType)
			{
				case GameActionType.Mulligan:
					ApplyMulliganAction(action);
					break;

				case GameActionType.PlayCard:
					ApplyPlayCardAction(action);
					break;

				case GameActionType.PassTurn:
					ApplyPassTurnAction(action);
					break;

				case GameActionType.Resign:
					ApplyResignAction(action);
					break;

				case GameActionType.UseLeaderAbility:
					ApplyLeaderAbilityAction(action);
					break;
			}

			return BoardState;
		}

		/// <summary>
		/// Centralne miejsce walidacji akcji gry.
		/// Zasady:
		/// - akcje nie przechodzą, jeśli gra jest skończona,
		/// - prawie wszystkie akcje wymagają, żeby był Twój ruch,
		/// - po pass nie wolno grać ani znowu passować,
		/// - w 1. rundzie dopóki masz mulligany, wolno tylko Mulligan / Resign.
		/// </summary>
		private bool ValidateAction(
			GameActionPayload payload,
			out PlayerBoardState actingPlayer,
			out PlayerBoardState opponentPlayer)
		{
			actingPlayer = GetPlayerBoard(payload.ActingPlayerNickname);
			opponentPlayer = GetOpponentBoard(payload.ActingPlayerNickname);

			// Gracz musi istnieć
			if (actingPlayer == null || opponentPlayer == null)
			{
				return false;
			}

			// Gra skończona – nic nie wolno
			if (BoardState.IsGameFinished)
			{
				return false;
			}

			// Większość akcji wymaga, żeby to była tura danego gracza.
			// WYJĄTKI:
			// - Mulligan: może być poza turą (pre-game),
			// - Resign: możesz się poddać niezależnie od tury.
			if (payload.ActionType != GameActionType.Mulligan &&
				payload.ActionType != GameActionType.Resign &&
				BoardState.ActivePlayerNickname != actingPlayer.PlayerNickname)
			{
				return false;
			}

			// Po pass nie wolno już:
			// - grać kart
			// - passować drugi raz
			// - używać zdolności lidera
			if (actingPlayer.HasPassedCurrentRound &&
				(payload.ActionType == GameActionType.PlayCard ||
				 payload.ActionType == GameActionType.PassTurn ||
				 payload.ActionType == GameActionType.UseLeaderAbility))
			{
				return false;
			}

			// Wymuszony mulligan w 1. rundzie:
			// dopóki gracz ma MulligansRemaining > 0,
			// wolno mu tylko:
			// - Mulligan
			// - Resign
			if (BoardState.CurrentRoundNumber == 1 &&
				actingPlayer.MulligansRemaining > 0 &&
				payload.ActionType != GameActionType.Mulligan &&
				payload.ActionType != GameActionType.Resign)
			{
				return false;
			}

			return true;
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

		#endregion Init / helpers

		#region LogEntry

		private void AddLogEntry(string message)
		{
			if (BoardState.GameLog == null)
			{
				BoardState.GameLog = new List<string>();
			}

			BoardState.GameLog.Add(message);

			// prosta rotacja – max 200 wpisów
			if (BoardState.GameLog.Count > 200)
			{
				BoardState.GameLog.RemoveAt(0);
			}
		}

		#endregion LogEntry

		#region Mulligan

		private void ApplyMulliganAction(GameActionPayload action)
		{
			// Mulligan tylko w 1 rundzie.
			if (BoardState.CurrentRoundNumber != 1)
			{
				return;
			}

			var player = GetPlayerBoard(action.ActingPlayerNickname);
			if (player.MulligansRemaining <= 0 || action.CardInstanceId == null)
			{
				return;
			}

			var card = player.Hand.FirstOrDefault(c => c.InstanceId == action.CardInstanceId);
			if (card == null || player.Deck.Count == 0)
			{
				return;
			}

			// wrzucamy kartę z ręki losowo do talii
			player.Hand.Remove(card);
			int insertIndex = random.Next(player.Deck.Count + 1);
			player.Deck.Insert(insertIndex, card);

			// dobieramy nową
			DrawFromDeck(player, 1);

			player.MulligansRemaining--;

			RecalculateStrengths();
			AddLogEntry($"{player.PlayerNickname} mulliganed {card.Name}.");
		}

		#endregion Mulligan

		#region Play card + targeted abilities

		private void ApplyPlayCardAction(GameActionPayload action)
		{
			var actingPlayer = GetPlayerBoard(action.ActingPlayerNickname);
			var opponent = GetOpponentBoard(action.ActingPlayerNickname);

			if (BoardState.CurrentRoundNumber == 1 && actingPlayer.MulligansRemaining > 0)
			{
				return;
			}

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
			AddLogEntry($"{actingPlayer.PlayerNickname} played {card.Name}.");
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

		#endregion Play card + targeted abilities

		#region Pass / Resign / Leader

		private void ApplyPassTurnAction(GameActionPayload action)
		{
			var player = GetPlayerBoard(action.ActingPlayerNickname);

			if (BoardState.CurrentRoundNumber == 1 && player.MulligansRemaining > 0)
			{
				// nie możesz passować zanim nie wymienisz 2 kart
				return;
			}

			if (player.HasPassedCurrentRound)
				return;

			player.HasPassedCurrentRound = true;
			AddLogEntry($"{player.PlayerNickname} passed.");

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

			if (BoardState.CurrentRoundNumber == 1 && player.MulligansRemaining > 0)
			{
				// możesz wymusić też, że nie wolno się poddać w fazie mulliganu
				return;
			}

			var opponent = GetOpponentBoard(action.ActingPlayerNickname);

			BoardState.IsGameFinished = true;
			BoardState.WinnerNickname = opponent.PlayerNickname;
			AddLogEntry($"{player.PlayerNickname} surrendered. {opponent.PlayerNickname} wins the game.");
			player.LifeTokensRemaining = 0;
		}

		private void ApplyLeaderAbilityAction(GameActionPayload action)
		{
			var player = GetPlayerBoard(action.ActingPlayerNickname);

			if (BoardState.CurrentRoundNumber == 1 && player.MulligansRemaining > 0)
			{
				// najpierw wykorzystaj mulligany
				return;
			}

			if (player.LeaderCard == null || player.LeaderAbilityUsed)
				return;

			var leader = player.LeaderCard;

			if (leader.HasAbility(CardAbilityType.LeaderAbility_DrawExtraCard))
			{
				DrawFromDeck(player, 1);
			}

			player.LeaderAbilityUsed = true;
			RecalculateStrengths();
			AddLogEntry($"{player.PlayerNickname} used leader ability {leader.Name}.");
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
			bool isTrueDraw = false;

			// 1) Ustalamy zwycięzcę rundy na podstawie siły
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
				// 2) Remis siły – sprawdzamy bonus Nilfgaardu
				bool hostIsNg = host.Faction == FactionType.Nilfgaard;
				bool guestIsNg = guest.Faction == FactionType.Nilfgaard;

				if (hostIsNg && !guestIsNg)
				{
					// host = Nilfgaard → wygrywa remis
					roundWinner = host;
					roundLoser = guest;
				}
				else if (guestIsNg && !hostIsNg)
				{
					// guest = Nilfgaard → wygrywa remis
					roundWinner = guest;
					roundLoser = host;
				}
				else
				{
					// 3) Prawdziwy remis – żadna strona (albo obie) nie ma przewagi Nilfgaardu
					isTrueDraw = true;
				}
			}

			// 4) Obsługa prawdziwego remisu rundy (bez Nilfgaardu)
			if (isTrueDraw)
			{
				// Obie strony „przegrywają” po 1 życiu
				host.LifeTokensRemaining--;
				guest.LifeTokensRemaining--;

				host.RoundsWon++;
				guest.RoundsWon++;

				AddLogEntry($"Round {BoardState.CurrentRoundNumber} ended in a draw. " +
							$"Host: {hostStrength}, Guest: {guestStrength}. " +
							$"Lives after draw – Host: {host.LifeTokensRemaining}, Guest: {guest.LifeTokensRemaining}.");

				// 4a) Obaj mają 0 → remis całej gry
				if (host.LifeTokensRemaining <= 0 && guest.LifeTokensRemaining <= 0)
				{
					BoardState.IsGameFinished = true;
					BoardState.WinnerNickname = null;
					AddLogEntry("Game ended in a full draw (both players lost their last life).");
				}
				// 4b) Host padł, Guest żyje → Guest wygrywa
				else if (host.LifeTokensRemaining <= 0 && guest.LifeTokensRemaining > 0)
				{
					BoardState.IsGameFinished = true;
					BoardState.WinnerNickname = guest.PlayerNickname;
					AddLogEntry($"Game won by {guest.PlayerNickname} (host lost last life in draw round).");
				}
				// 4c) Guest padł, Host żyje → Host wygrywa
				else if (guest.LifeTokensRemaining <= 0 && host.LifeTokensRemaining > 0)
				{
					BoardState.IsGameFinished = true;
					BoardState.WinnerNickname = host.PlayerNickname;
					AddLogEntry($"Game won by {host.PlayerNickname} (guest lost last life in draw round).");
				}
			}
			// 5) Normalna sytuacja – ktoś wygrał rundę
			else if (roundWinner != null && roundLoser != null)
			{
				roundWinner.RoundsWon++;
				roundLoser.LifeTokensRemaining--;

				AddLogEntry($"Round {BoardState.CurrentRoundNumber} won by {roundWinner.PlayerNickname}. " +
							$"Host: {hostStrength}, Guest: {guestStrength}. " +
							$"Loser lives left: {roundLoser.LifeTokensRemaining}.");

				// Northern Realms bonus – po WYGRANEJ rundzie dobiera kartę
				if (roundWinner.Faction == FactionType.NorthernRealms)
				{
					DrawFromDeck(roundWinner, 1);
					AddLogEntry($"{roundWinner.PlayerNickname} draws a card due to Northern Realms bonus.");
				}

				// czy przegrany stracił ostatnie życie?
				if (roundLoser.LifeTokensRemaining <= 0)
				{
					BoardState.IsGameFinished = true;
					BoardState.WinnerNickname = roundWinner.PlayerNickname;
					AddLogEntry($"Game won by {roundWinner.PlayerNickname} (opponent lost last life).");
				}
			}

			// 6) Koniec rundy – czyścimy rzędy, pogodę, passy, zwiększamy nr rundy
			ClearRows(host);
			ClearRows(guest);

			host.HasPassedCurrentRound = false;
			guest.HasPassedCurrentRound = false;

			BoardState.WeatherCards.Clear();
			BoardState.CurrentRoundNumber++;

			// Nowa runda – jeśli gra jeszcze trwa, zaczyna host (prosto)
			if (!BoardState.IsGameFinished)
			{
				BoardState.ActivePlayerNickname = host.PlayerNickname;
			}

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

		#endregion Pass / Resign / Leader

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

		#endregion Scorch / Recalc
	}
}