using System;
using System.Collections.Generic;
using System.Linq;

namespace Gwent.Core
{
	/// <summary>
	/// Silnik gry Gwint – pełna logika rund, kart i efektów.
	/// Nie wie nic o UI / sieci – operuje tylko na GameActionPayload i GameBoardState.
	/// </summary>
	public sealed class GameEngine
	{
		public GameBoardState BoardState { get; }

		private readonly GameSessionConfiguration sessionConfig;
		private readonly Random random = new();

		public GameEngine(GameSessionConfiguration sessionConfig)
		{
			this.sessionConfig = sessionConfig ?? throw new ArgumentNullException(nameof(sessionConfig));
			BoardState = new GameBoardState();

			// Inicjalizacja plansz graczy
			InitializePlayers();
			// Losujemy, kto zaczyna (uwzględniając Scoia'tael – wybór, ale uprośćmy: Scoia'tael startuje)
			InitializeStartingPlayer();

			RecalculateStrengths();
			AddLogEntry("Game started.");
		}

		/// <summary>
		/// Zwraca kopię stanu – do wysłania do klienta.
		/// </summary>
		public GameBoardState GetBoardStateSnapshot() => BoardState;

		/// <summary>
		/// Główna metoda – aplikuje akcję gracza do stanu gry.
		/// </summary>
		public void ApplyAction(GameActionPayload payload)
		{
			if (BoardState.IsGameFinished)
				return;

			if (!ValidateAction(payload, out var actingPlayer, out var opponent))
				return;

			switch (payload.ActionType)
			{
				case GameActionType.Mulligan:
					ApplyMulliganAction(actingPlayer, payload.CardInstanceId);
					break;

				case GameActionType.PlayCard:
					ApplyPlayCardAction(actingPlayer, opponent, payload);
					break;

				case GameActionType.PassTurn:
					ApplyPassTurnAction(actingPlayer, opponent);
					break;

				case GameActionType.UseLeaderAbility:
					ApplyLeaderAbilityAction(actingPlayer, payload.CardInstanceId);
					break;

				case GameActionType.Resign:
					ApplyResignAction(actingPlayer, opponent);
					break;
			}

			RecalculateStrengths();
		}

		#region Inicjalizacja

		private void InitializePlayers()
		{
			var hostBoard = BoardState.HostPlayerBoard;
			hostBoard.PlayerNickname = sessionConfig.HostPlayer.Nickname;
			hostBoard.Faction = sessionConfig.HostPlayer.Faction;
			hostBoard.Deck.AddRange(DeckFactory.CreateDeckInstances(hostBoard.Faction));
			Shuffle(hostBoard.Deck);
			DrawFromDeck(hostBoard, 10); // standardowo 10 kart na rękę

			var guestBoard = BoardState.GuestPlayerBoard;
			guestBoard.PlayerNickname = sessionConfig.GuestPlayer.Nickname;
			guestBoard.Faction = sessionConfig.GuestPlayer.Faction;
			guestBoard.Deck.AddRange(DeckFactory.CreateDeckInstances(guestBoard.Faction));
			Shuffle(guestBoard.Deck);
			DrawFromDeck(guestBoard, 10);
		}

		private void InitializeStartingPlayer()
		{
			var host = BoardState.HostPlayerBoard;
			var guest = BoardState.GuestPlayerBoard;

			// Scoia'tael mają bonus – mogą zdecydować, kto zaczyna.
			// Na razie: jeśli dokładnie jeden z graczy jest Scoia'tael, on zaczyna.
			bool hostScoia = host.Faction == FactionType.Scoiatael;
			bool guestScoia = guest.Faction == FactionType.Scoiatael;

			if (hostScoia && !guestScoia)
			{
				BoardState.ActivePlayerNickname = host.PlayerNickname;
				AddLogEntry("Scoia'tael bonus: host starts.");
			}
			else if (guestScoia && !hostScoia)
			{
				BoardState.ActivePlayerNickname = guest.PlayerNickname;
				AddLogEntry("Scoia'tael bonus: guest starts.");
			}
			else
			{
				// losowo
				var startHost = random.Next(2) == 0;
				BoardState.ActivePlayerNickname = startHost ? host.PlayerNickname : guest.PlayerNickname;
				AddLogEntry($"Random start player: {BoardState.ActivePlayerNickname}.");
			}
		}

		#endregion

		#region Walidacja / pomocnicze

		private bool ValidateAction(
			GameActionPayload payload,
			out PlayerBoardState actingPlayer,
			out PlayerBoardState opponent)
		{
			actingPlayer = GetPlayerBoard(payload.ActingPlayerNickname);
			opponent = GetOpponentBoard(payload.ActingPlayerNickname);

			if (actingPlayer == null || opponent == null)
				return false;

			// Tura – tylko aktywny gracz może grać (poza Resign)
			if (payload.ActionType != GameActionType.Resign &&
				payload.ActionType != GameActionType.Mulligan &&
				BoardState.ActivePlayerNickname != actingPlayer.PlayerNickname)
			{
				// ignorujemy nielegalną akcję
				return false;
			}

			// Po passie nie można grać (poza Resign – teoretycznie i tak koniec gry)
			if (payload.ActionType == GameActionType.PlayCard || payload.ActionType == GameActionType.PassTurn)
			{
				if (actingPlayer.HasPassedCurrentRound)
					return false;
			}

			return true;
		}

		private PlayerBoardState GetPlayerBoard(string nickname)
		{
			if (BoardState.HostPlayerBoard.PlayerNickname == nickname)
				return BoardState.HostPlayerBoard;
			if (BoardState.GuestPlayerBoard.PlayerNickname == nickname)
				return BoardState.GuestPlayerBoard;
			throw new InvalidOperationException($"Unknown player nickname: {nickname}");
		}

		private PlayerBoardState GetOpponentBoard(string nickname)
		{
			if (BoardState.HostPlayerBoard.PlayerNickname == nickname)
				return BoardState.GuestPlayerBoard;
			if (BoardState.GuestPlayerBoard.PlayerNickname == nickname)
				return BoardState.HostPlayerBoard;
			throw new InvalidOperationException($"Unknown player nickname: {nickname}");
		}

		private void Shuffle<T>(IList<T> list)
		{
			for (int i = list.Count - 1; i > 0; i--)
			{
				int j = random.Next(i + 1);
				(list[i], list[j]) = (list[j], list[i]);
			}
		}

		private void DrawFromDeck(PlayerBoardState player, int count)
		{
			for (int i = 0; i < count && player.Deck.Count > 0; i++)
			{
				var card = player.Deck[0];
				player.Deck.RemoveAt(0);
				player.Hand.Add(card);
			}
		}

		private void AddLogEntry(string message)
		{
			BoardState.GameLog ??= new List<string>();
			BoardState.GameLog.Add(message);
			if (BoardState.GameLog.Count > 200)
				BoardState.GameLog.RemoveAt(0);
		}

		#endregion

		#region Akcje

		/// <summary>
		/// Mulligan – w 1. rundzie każdy gracz ma np. 2 wymiany.
		/// </summary>
		private void ApplyMulliganAction(PlayerBoardState actingPlayer, Guid cardInstanceId)
		{
			if (BoardState.CurrentRoundNumber != 1 || actingPlayer.MulligansRemaining <= 0)
				return;

			var card = actingPlayer.Hand.FirstOrDefault(c => c.InstanceId == cardInstanceId);
			if (card == null)
				return;

			actingPlayer.Hand.Remove(card);
			actingPlayer.Deck.Add(card);
			Shuffle(actingPlayer.Deck);
			DrawFromDeck(actingPlayer, 1);

			actingPlayer.MulligansRemaining--;
			AddLogEntry($"{actingPlayer.PlayerNickname} mulliganed {card.Name}.");
		}

		/// <summary>
		/// Główna akcja – zagranie karty (jednostki, specjalnej, pogody, itp.).
		/// </summary>
		private void ApplyPlayCardAction(
			PlayerBoardState actingPlayer,
			PlayerBoardState opponent,
			GameActionPayload payload)
		{
			// karta może być w ręce (normalnie) albo lider (LeaderAbility też idzie przez tę metodę, ale tam inny path)
			var card = actingPlayer.Hand.FirstOrDefault(c => c.InstanceId == payload.CardInstanceId);
			if (card == null)
			{
				// może to lider? – ten idziemy inną ścieżką
				return;
			}

			actingPlayer.Hand.Remove(card);

			// Specjalne karty pogody / ClearWeather / globalny Scorch:
			if (card.Category == CardCategory.Weather ||
				(card.Category == CardCategory.Special &&
				 (card.HasAbility(CardAbilityType.Scorch) ||
				  card.HasAbility(CardAbilityType.ClearWeather))))
			{
				ApplyGlobalSpecialCard(actingPlayer, opponent, card);
				SwitchTurnToOpponent(actingPlayer.PlayerNickname);
				return;
			}

			// Spy – kładziemy zawsze na rząd przeciwnika
			if (card.HasAbility(CardAbilityType.Spy))
			{
				var row = payload.TargetRow ?? card.DefaultRow;
				PlaceCardOnRow(opponent, card, row);
				ApplySpyBonusDraw(actingPlayer);
				AddLogEntry($"{actingPlayer.PlayerNickname} played Spy {card.Name} on opponent row {row}.");
				SwitchTurnToOpponent(actingPlayer.PlayerNickname);
				return;
			}

			// Medic – wymaga TargetInstanceId z cmentarza
			if (card.HasAbility(CardAbilityType.Medic))
			{
				var targetId = payload.TargetInstanceId;
				if (targetId == null)
				{
					// klient powinien zawsze podać cel
					actingPlayer.Graveyard.Add(card); // karta zmarnowana
					AddLogEntry($"{actingPlayer.PlayerNickname} played Medic {card.Name} without target (wasted).");
				}
				else
				{
					var revived = actingPlayer.Graveyard
						.FirstOrDefault(c => c.InstanceId == targetId.Value && c.Category == CardCategory.Unit && !c.IsHero);
					if (revived != null)
					{
						actingPlayer.Graveyard.Remove(revived);

						var row = payload.TargetRow ?? revived.DefaultRow;
						PlaceCardOnRow(actingPlayer, revived, row);
						AddLogEntry($"{actingPlayer.PlayerNickname} revived {revived.Name} with Medic {card.Name}.");
					}

					// Medic sam wchodzi na wybrany rząd
					var medicRow = payload.TargetRow ?? card.DefaultRow;
					PlaceCardOnRow(actingPlayer, card, medicRow);
				}

				SwitchTurnToOpponent(actingPlayer.PlayerNickname);
				return;
			}

			// Decoy/Mardroeme – target InstanceId z planszy
			if (card.HasAbility(CardAbilityType.Decoy))
			{
				ApplyDecoy(actingPlayer, payload, card);
				SwitchTurnToOpponent(actingPlayer.PlayerNickname);
				return;
			}

			if (card.HasAbility(CardAbilityType.Mardroeme))
			{
				ApplyMardroeme(actingPlayer, payload, card);
				SwitchTurnToOpponent(actingPlayer.PlayerNickname);
				return;
			}

			// Normalne jednostki / rogi / morale / TightBond / Muster / Agile
			var targetRow = payload.TargetRow ?? card.DefaultRow;
			PlaceCardOnRow(actingPlayer, card, targetRow);

			// Muster – dobijamy wszystkie kopie z decku/hand
			if (card.HasAbility(CardAbilityType.Muster) && !string.IsNullOrEmpty(card.MusterGroup))
			{
				ApplyMuster(actingPlayer, card.MusterGroup, targetRow);
			}

			// TightBond / MoralBoost / Horn / Agile itd. – ich efekty są uwzględniane w RecalculateStrengths()
			AddLogEntry($"{actingPlayer.PlayerNickname} played {card.Name} on row {targetRow}.");

			SwitchTurnToOpponent(actingPlayer.PlayerNickname);
		}

		/// <summary>
		/// Gracz pasuje – gdy obaj spasują, kończymy rundę.
		/// </summary>
		private void ApplyPassTurnAction(PlayerBoardState actingPlayer, PlayerBoardState opponent)
		{
			actingPlayer.HasPassedCurrentRound = true;
			AddLogEntry($"{actingPlayer.PlayerNickname} passed.");

			if (actingPlayer.HasPassedCurrentRound && opponent.HasPassedCurrentRound)
			{
				EndRound();
			}
			else
			{
				SwitchTurnToOpponent(actingPlayer.PlayerNickname);
			}
		}

		/// <summary>
		/// Dowódca używa zdolności.
		/// </summary>
		private void ApplyLeaderAbilityAction(PlayerBoardState actingPlayer, Guid leaderInstanceId)
		{
			if (actingPlayer.LeaderCard == null ||
				actingPlayer.LeaderAbilityUsed ||
				actingPlayer.LeaderCard.InstanceId != leaderInstanceId)
				return;

			// przykład: LeaderAbility_DrawExtraCard
			if (actingPlayer.LeaderCard.HasAbility(CardAbilityType.LeaderAbility_DrawExtraCard))
			{
				DrawFromDeck(actingPlayer, 1);
				AddLogEntry($"{actingPlayer.PlayerNickname} used leader ability (draw extra card).");
			}

			actingPlayer.LeaderAbilityUsed = true;
			SwitchTurnToOpponent(actingPlayer.PlayerNickname);
		}

		/// <summary>
		/// Poddanie gry – natychmiastowy koniec.
		/// </summary>
		private void ApplyResignAction(PlayerBoardState actingPlayer, PlayerBoardState opponent)
		{
			BoardState.IsGameFinished = true;
			BoardState.WinnerNickname = opponent.PlayerNickname;
			AddLogEntry($"{actingPlayer.PlayerNickname} surrendered. {opponent.PlayerNickname} wins the game.");
		}

		#endregion

		#region Efekty kart

		private void ApplyGlobalSpecialCard(PlayerBoardState actingPlayer, PlayerBoardState opponent, GwentCard card)
		{
			if (card.HasAbility(CardAbilityType.ClearWeather))
			{
				BoardState.WeatherCards.Clear();
				AddLogEntry($"{actingPlayer.PlayerNickname} played Clear Weather.");
				return;
			}

			if (card.HasAbility(CardAbilityType.Scorch))
			{
				ApplyGlobalScorch(actingPlayer, opponent);
				AddLogEntry($"{actingPlayer.PlayerNickname} played Scorch.");
				return;
			}

			// Biting Frost / Fog / Rain – pogoda
			BoardState.WeatherCards.Add(card);
			AddLogEntry($"{actingPlayer.PlayerNickname} played weather: {card.Name}.");
		}

		private void ApplyGlobalScorch(PlayerBoardState actingPlayer, PlayerBoardState opponent)
		{
			// Scorch: usuwa najsilniejsze jednostki (nie hero) z planszy obu graczy
			var allUnits = actingPlayer.EnumerateBoardCards()
				.Concat(opponent.EnumerateBoardCards())
				.Where(c => c.Category == CardCategory.Unit && !c.IsHero)
				.ToList();

			if (!allUnits.Any())
				return;

			int maxStrength = allUnits.Max(c => c.CurrentStrength);

			var toDestroy = allUnits.Where(c => c.CurrentStrength == maxStrength).ToList();

			RemoveCardsFromBoardToGraveyard(actingPlayer, toDestroy);
			RemoveCardsFromBoardToGraveyard(opponent, toDestroy);
		}

		private void ApplySpyBonusDraw(PlayerBoardState actingPlayer)
		{
			DrawFromDeck(actingPlayer, 2);
		}

		private void ApplyMuster(PlayerBoardState actingPlayer, string musterGroup, CardRow row)
		{
			// wszystkie karty z musterGroup z ręki i decku
			var fromHand = actingPlayer.Hand.Where(c => c.MusterGroup == musterGroup).ToList();
			var fromDeck = actingPlayer.Deck.Where(c => c.MusterGroup == musterGroup).ToList();

			foreach (var c in fromHand)
			{
				actingPlayer.Hand.Remove(c);
				PlaceCardOnRow(actingPlayer, c, row);
			}

			foreach (var c in fromDeck)
			{
				actingPlayer.Deck.Remove(c);
				PlaceCardOnRow(actingPlayer, c, row);
			}

			if (fromHand.Count + fromDeck.Count > 0)
			{
				AddLogEntry($"{actingPlayer.PlayerNickname}'s Muster pulled {fromHand.Count + fromDeck.Count} additional cards.");
			}
		}

		private void ApplyDecoy(PlayerBoardState actingPlayer, GameActionPayload payload, GwentCard decoyCard)
		{
			if (payload.TargetInstanceId == null)
			{
				actingPlayer.Graveyard.Add(decoyCard);
				AddLogEntry($"{actingPlayer.PlayerNickname} played Decoy {decoyCard.Name} without target.");
				return;
			}

			var targetId = payload.TargetInstanceId.Value;

			var (rowList, targetCard) = FindBoardCardByInstanceId(actingPlayer, targetId);
			if (rowList == null || targetCard == null)
			{
				actingPlayer.Graveyard.Add(decoyCard);
				AddLogEntry($"{actingPlayer.PlayerNickname} played Decoy {decoyCard.Name} with invalid target.");
				return;
			}

			rowList.Remove(targetCard);
			actingPlayer.Hand.Add(targetCard);

			// Decoy idzie na miejsce jednostki
			rowList.Add(decoyCard);
			decoyCard.IsOnBoard = true;

			AddLogEntry($"{actingPlayer.PlayerNickname} used Decoy on {targetCard.Name}.");
		}

		private void ApplyMardroeme(PlayerBoardState actingPlayer, GameActionPayload payload, GwentCard mardroemeCard)
		{
			if (payload.TargetInstanceId == null)
			{
				actingPlayer.Graveyard.Add(mardroemeCard);
				AddLogEntry($"{actingPlayer.PlayerNickname} played Mardroeme {mardroemeCard.Name} without target.");
				return;
			}

			var targetId = payload.TargetInstanceId.Value;

			var (rowList, targetCard) = FindBoardCardByInstanceId(actingPlayer, targetId);
			if (rowList == null || targetCard == null)
			{
				actingPlayer.Graveyard.Add(mardroemeCard);
				AddLogEntry($"{actingPlayer.PlayerNickname} played Mardroeme {mardroemeCard.Name} with invalid target.");
				return;
			}

			// W W3 Mardroeme buffuje/bije, zależnie od karty – tu na razie proste +X
			targetCard.CurrentStrength += 2;
			actingPlayer.Graveyard.Add(mardroemeCard);

			AddLogEntry($"{actingPlayer.PlayerNickname} used Mardroeme on {targetCard.Name} (+2 strength).");
		}

		private (List<GwentCard>? rowList, GwentCard? card) FindBoardCardByInstanceId(PlayerBoardState player, Guid instanceId)
		{
			foreach (var row in new[]
					 {
						 player.MeleeRow,
						 player.RangedRow,
						 player.SiegeRow
					 })
			{
				var card = row.FirstOrDefault(c => c.InstanceId == instanceId);
				if (card != null)
					return (row, card);
			}

			return (null, null);
		}

		private void PlaceCardOnRow(PlayerBoardState player, GwentCard card, CardRow row)
		{
			List<GwentCard> targetRow = row switch
			{
				CardRow.Melee => player.MeleeRow,
				CardRow.Ranged => player.RangedRow,
				CardRow.Siege => player.SiegeRow,
				_ => player.MeleeRow
			};

			targetRow.Add(card);
			card.IsOnBoard = true;
		}

		private void RemoveCardsFromBoardToGraveyard(PlayerBoardState player, IEnumerable<GwentCard> cards)
		{
			var set = new HashSet<Guid>(cards.Select(c => c.InstanceId));

			player.MeleeRow.RemoveAll(c => set.Contains(c.InstanceId) && MoveToGrave(player, c));
			player.RangedRow.RemoveAll(c => set.Contains(c.InstanceId) && MoveToGrave(player, c));
			player.SiegeRow.RemoveAll(c => set.Contains(c.InstanceId) && MoveToGrave(player, c));
		}

		private bool MoveToGrave(PlayerBoardState player, GwentCard card)
		{
			card.IsOnBoard = false;
			player.Graveyard.Add(card);
			return true;
		}

		#endregion

		#region Runda / tura

		private void SwitchTurnToOpponent(string currentPlayerNickname)
		{
			var next = GetOpponentBoard(currentPlayerNickname);
			BoardState.ActivePlayerNickname = next.PlayerNickname;
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
				bool hostIsNg = host.Faction == FactionType.Nilfgaard;
				bool guestIsNg = guest.Faction == FactionType.Nilfgaard;

				if (hostIsNg && !guestIsNg)
				{
					roundWinner = host;
					roundLoser = guest;
				}
				else if (guestIsNg && !hostIsNg)
				{
					roundWinner = guest;
					roundLoser = host;
				}
				else
				{
					isTrueDraw = true;
				}
			}

			if (isTrueDraw)
			{
				host.LifeTokensRemaining--;
				guest.LifeTokensRemaining--;
				host.RoundsWon++;
				guest.RoundsWon++;

				AddLogEntry($"Round {BoardState.CurrentRoundNumber} ended in a draw. Host: {hostStrength}, Guest: {guestStrength}.");

				// pełny remis gry?
				if (host.LifeTokensRemaining <= 0 && guest.LifeTokensRemaining <= 0)
				{
					BoardState.IsGameFinished = true;
					BoardState.WinnerNickname = null;
					AddLogEntry("Game ended in a full draw (both players lost their last life).");
				}
				else if (host.LifeTokensRemaining <= 0 && guest.LifeTokensRemaining > 0)
				{
					BoardState.IsGameFinished = true;
					BoardState.WinnerNickname = guest.PlayerNickname;
					AddLogEntry($"Game won by {guest.PlayerNickname} (host lost last life in draw round).");
				}
				else if (guest.LifeTokensRemaining <= 0 && host.LifeTokensRemaining > 0)
				{
					BoardState.IsGameFinished = true;
					BoardState.WinnerNickname = host.PlayerNickname;
					AddLogEntry($"Game won by {host.PlayerNickname} (guest lost last life in draw round).");
				}
			}
			else if (roundWinner != null && roundLoser != null)
			{
				roundWinner.RoundsWon++;
				roundLoser.LifeTokensRemaining--;

				AddLogEntry($"Round {BoardState.CurrentRoundNumber} won by {roundWinner.PlayerNickname}. " +
							$"Host: {hostStrength}, Guest: {guestStrength}. " +
							$"Loser lives left: {roundLoser.LifeTokensRemaining}.");

				// Northern Realms – dobiera kartę po wygranej rundzie
				if (roundWinner.Faction == FactionType.NorthernRealms)
				{
					DrawFromDeck(roundWinner, 1);
					AddLogEntry($"{roundWinner.PlayerNickname} draws a card due to Northern Realms bonus.");
				}

				// Monsters – jedna losowa jednostka zostaje na planszy
				if (roundWinner.Faction == FactionType.Monsters)
				{
					KeepRandomMonsterUnitOnBoard(roundWinner);
				}

				if (roundLoser.LifeTokensRemaining <= 0)
				{
					BoardState.IsGameFinished = true;
					BoardState.WinnerNickname = roundWinner.PlayerNickname;
					AddLogEntry($"Game won by {roundWinner.PlayerNickname} (opponent lost last life).");
				}
			}

			// Czyścimy rzędy, pogodę, passy, zwiększamy nr rundy
			ClearRows(host);
			ClearRows(guest);
			host.HasPassedCurrentRound = false;
			guest.HasPassedCurrentRound = false;
			BoardState.WeatherCards.Clear();
			BoardState.CurrentRoundNumber++;

			if (!BoardState.IsGameFinished)
			{
				// prosto: zaczyna host – można tu dodać bardziej skomplikowane zasady
				BoardState.ActivePlayerNickname = host.PlayerNickname;
			}

			RecalculateStrengths();
		}

		private void KeepRandomMonsterUnitOnBoard(PlayerBoardState monstersPlayer)
		{
			var allUnits = monstersPlayer.EnumerateBoardCards()
				.Where(c => c.Category == CardCategory.Unit && !c.IsHero)
				.ToList();

			if (!allUnits.Any())
				return;

			var keep = allUnits[random.Next(allUnits.Count)];

			// wszystko inne w grave
			var others = allUnits.Where(c => c.InstanceId != keep.InstanceId).ToList();
			RemoveCardsFromBoardToGraveyard(monstersPlayer, others);

			AddLogEntry($"{monstersPlayer.PlayerNickname}'s Monsters bonus keeps {keep.Name} on board.");
		}

		private void ClearRows(PlayerBoardState player)
		{
			MoveRowToGrave(player, player.MeleeRow);
			MoveRowToGrave(player, player.RangedRow);
			MoveRowToGrave(player, player.SiegeRow);
		}

		private void MoveRowToGrave(PlayerBoardState player, List<GwentCard> row)
		{
			foreach (var c in row)
			{
				c.IsOnBoard = false;
				player.Graveyard.Add(c);
			}
			row.Clear();
		}

		#endregion

		#region Przeliczanie siły

		/// <summary>
		/// Przelicza CurrentStrength wszystkich kart wg efektów (pogoda, bond, morale, horn, agile, scorch jeszcze nie).
		/// </summary>
		private void RecalculateStrengths()
		{
			// Najpierw reset do bazowej siły
			foreach (var card in BoardState.HostPlayerBoard.EnumerateBoardCards()
						 .Concat(BoardState.GuestPlayerBoard.EnumerateBoardCards()))
			{
				card.CurrentStrength = card.Definition.BaseStrength;
			}

			ApplyWeatherModifiers(BoardState.HostPlayerBoard);
			ApplyWeatherModifiers(BoardState.GuestPlayerBoard);

			ApplyRowEffects(BoardState.HostPlayerBoard);
			ApplyRowEffects(BoardState.GuestPlayerBoard);
		}

		private void ApplyWeatherModifiers(PlayerBoardState player)
		{
			bool frost = BoardState.WeatherCards.Any(c => c.HasAbility(CardAbilityType.WeatherBitingFrost));
			bool fog = BoardState.WeatherCards.Any(c => c.HasAbility(CardAbilityType.WeatherImpenetrableFog));
			bool rain = BoardState.WeatherCards.Any(c => c.HasAbility(CardAbilityType.WeatherTorrentialRain));

			if (frost)
			{
				foreach (var c in player.MeleeRow.Where(c => !c.IsHero))
				{
					if (c.CurrentStrength > 1)
						c.CurrentStrength = 1;
				}
			}

			if (fog)
			{
				foreach (var c in player.RangedRow.Where(c => !c.IsHero))
				{
					if (c.CurrentStrength > 1)
						c.CurrentStrength = 1;
				}
			}

			if (rain)
			{
				foreach (var c in player.SiegeRow.Where(c => !c.IsHero))
				{
					if (c.CurrentStrength > 1)
						c.CurrentStrength = 1;
				}
			}
		}

		private void ApplyRowEffects(PlayerBoardState player)
		{
			ApplyRowEffectsForRow(player.MeleeRow);
			ApplyRowEffectsForRow(player.RangedRow);
			ApplyRowEffectsForRow(player.SiegeRow);
		}

		private void ApplyRowEffectsForRow(List<GwentCard> row)
		{
			if (!row.Any())
				return;

			bool hasHorn = row.Any(c => c.HasAbility(CardAbilityType.CommandersHorn));
			int moraleBoostCount = row.Count(c => c.HasAbility(CardAbilityType.MoralBoost));

			// TightBond: dla każdej grupy mnożymy siłę kart w tej grupie przez ilość
			var bondGroups = row
				.Where(c => !string.IsNullOrEmpty(c.TightBondGroup))
				.GroupBy(c => c.TightBondGroup);

			foreach (var group in bondGroups)
			{
				int count = group.Count();
				foreach (var card in group)
				{
					card.CurrentStrength *= count;
				}
			}

			// Morale Boost – +1 do wszystkich innych jednostek (nie hero) w rzędzie
			if (moraleBoostCount > 0)
			{
				foreach (var card in row.Where(c => !c.IsHero && !c.HasAbility(CardAbilityType.MoralBoost)))
				{
					card.CurrentStrength += moraleBoostCount;
				}
			}

			// Horn – mnożenie siły w rzędzie (zwykle x2)
			if (hasHorn)
			{
				foreach (var card in row.Where(c => !c.IsHero))
				{
					card.CurrentStrength *= 2;
				}
			}
		}

		#endregion
	}
}
