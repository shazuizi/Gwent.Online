using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Gwent.Core;

namespace Gwent.Client
{
	public partial class GamePage : Page
	{
		private readonly MainWindow mainWindow;
		private readonly GameClientController gameClientController;

		private bool gameResultAlreadyShown;

		private enum PendingTargetType
		{
			None,
			MedicFromGraveyard,
			DecoyFromBoard,
			MardroemeFromBoard
		}

		private PendingTargetType pendingTargetType = PendingTargetType.None;
		private GwentCard? pendingSourceCard;
		private CardRow? pendingMedicRow;

		/// <summary>
		/// Aktualnie wybrana karta w ręce (kliknięta w HandListBox).
		/// </summary>
		private GwentCard? selectedHandCard;

		public GamePage(MainWindow mainWindow, GameClientController gameClientController)
		{
			InitializeComponent();
			this.mainWindow = mainWindow;
			this.gameClientController = gameClientController;

			this.gameClientController.GameSessionUpdated += OnGameSessionUpdated;
			this.gameClientController.GameStateUpdated += OnGameStateUpdated;
			this.gameClientController.ServerDisconnected += OnServerDisconnected;

			UpdatePlayerNicknamesFromSessionConfiguration();
			UpdateBoardUi();
			UpdateSelectedCardPanel();
		}

		#region Eventy z kontrolera klienta

		private void OnGameSessionUpdated(object? sender, EventArgs e)
		{
			Dispatcher.Invoke(UpdatePlayerNicknamesFromSessionConfiguration);
		}

		private void OnGameStateUpdated(object? sender, EventArgs e)
		{
			Dispatcher.Invoke(UpdateBoardUi);
		}

		private void OnServerDisconnected(object? sender, EventArgs e)
		{
			Dispatcher.Invoke(() =>
			{
				MessageBox.Show("Connection to server was lost.", "Disconnected",
					MessageBoxButton.OK, MessageBoxImage.Warning);

				mainWindow.CurrentGameClientController?.TryStopServerProcess();
				mainWindow.CurrentGameClientController = null;
				mainWindow.NavigateToMainMenuPage();
			});
		}

		#endregion

		#region UI – nicki, plansza, itp.

		private void UpdatePlayerNicknamesFromSessionConfiguration()
		{
			var sessionConfiguration = gameClientController.CurrentGameSessionConfiguration;
			if (sessionConfiguration == null)
			{
				LocalPlayerNicknameTextBlock.Text = "Unknown";
				OpponentNicknameTextBlock.Text = "Unknown";
				LocalPlayerRoleTextBlock.Text = string.Empty;
				OpponentRoleTextBlock.Text = string.Empty;
				return;
			}

			string localNickname;
			string opponentNickname;
			string localRoleText;
			string opponentRoleText;

			if (gameClientController.LocalPlayerRole == GameRole.Host)
			{
				localNickname = sessionConfiguration.HostPlayer.Nickname;
				opponentNickname = sessionConfiguration.GuestPlayer.Nickname;
				localRoleText = "Role: Host";
				opponentRoleText = "Role: Guest";
			}
			else
			{
				localNickname = sessionConfiguration.GuestPlayer.Nickname;
				opponentNickname = sessionConfiguration.HostPlayer.Nickname;
				localRoleText = "Role: Guest";
				opponentRoleText = "Role: Host";
			}

			if (string.IsNullOrWhiteSpace(localNickname))
				localNickname = "Unknown";

			if (string.IsNullOrWhiteSpace(opponentNickname))
				opponentNickname = "Waiting...";

			LocalPlayerNicknameTextBlock.Text = localNickname;
			OpponentNicknameTextBlock.Text = opponentNickname;
			LocalPlayerRoleTextBlock.Text = localRoleText;
			OpponentRoleTextBlock.Text = opponentRoleText;
		}

		private void UpdateBoardUi()
		{
			var boardState = gameClientController.CurrentBoardState;
			var sessionConfiguration = gameClientController.CurrentGameSessionConfiguration;

			if (boardState == null || sessionConfiguration == null)
			{
				ClearAllListBoxes();
				LocalPlayerStrengthTextBlock.Text = "Strength: 0";
				OpponentPlayerStrengthTextBlock.Text = "Strength: 0";
				RoundNumberTextBlock.Text = "Round -";
				WeatherTextBlock.Text = "Weather: -";
				LocalLivesTextBlock.Text = "Lives: -";
				OpponentLivesTextBlock.Text = "Lives: -";
				LocalLeaderTextBlock.Text = "Leader: -";
				OpponentLeaderTextBlock.Text = "Leader: -";
				LocalDeckTextBlock.Text = "Deck: -";
				OpponentDeckTextBlock.Text = "Deck: -";
				LocalGraveyardTextBlock.Text = "Graveyard: -";
				OpponentGraveyardTextBlock.Text = "Graveyard: -";
				LocalMulliganTextBlock.Text = "Mulligans: -";
				LocalPassStatusTextBlock.Text = string.Empty;
				OpponentPassStatusTextBlock.Text = string.Empty;
				PlayCardButton.IsEnabled = false;
				PassButton.IsEnabled = false;
				MulliganButton.IsEnabled = false;
				LeaderAbilityButton.IsEnabled = false;
				SurrenderButton.IsEnabled = false;
				return;
			}

			PlayerBoardState localBoard;
			PlayerBoardState opponentBoard;

			if (gameClientController.LocalPlayerRole == GameRole.Host)
			{
				localBoard = boardState.HostPlayerBoard;
				opponentBoard = boardState.GuestPlayerBoard;
			}
			else
			{
				localBoard = boardState.GuestPlayerBoard;
				opponentBoard = boardState.HostPlayerBoard;
			}

			RoundNumberTextBlock.Text = $"Round {boardState.CurrentRoundNumber}";

			LocalPlayerStrengthTextBlock.Text = $"Strength: {localBoard.GetTotalStrength()}";
			OpponentPlayerStrengthTextBlock.Text = $"Strength: {opponentBoard.GetTotalStrength()}";

			LocalMeleeStrengthTextBlock.Text = localBoard.GetRowStrength(CardRow.Melee).ToString();
			LocalRangedStrengthTextBlock.Text = localBoard.GetRowStrength(CardRow.Ranged).ToString();
			LocalSiegeStrengthTextBlock.Text = localBoard.GetRowStrength(CardRow.Siege).ToString();

			OpponentMeleeStrengthTextBlock.Text = opponentBoard.GetRowStrength(CardRow.Melee).ToString();
			OpponentRangedStrengthTextBlock.Text = opponentBoard.GetRowStrength(CardRow.Ranged).ToString();
			OpponentSiegeStrengthTextBlock.Text = opponentBoard.GetRowStrength(CardRow.Siege).ToString();

			LocalLivesTextBlock.Text = $"Lives: {localBoard.LifeTokensRemaining}";
			OpponentLivesTextBlock.Text = $"Lives: {opponentBoard.LifeTokensRemaining}";

			LocalLeaderTextBlock.Text = $"Leader: {(localBoard.LeaderCard?.Name ?? "-")}";
			OpponentLeaderTextBlock.Text = $"Leader: {(opponentBoard.LeaderCard?.Name ?? "-")}";

			LocalDeckTextBlock.Text = $"Deck: {localBoard.Deck.Count}";
			OpponentDeckTextBlock.Text = $"Deck: {opponentBoard.Deck.Count}";
			LocalGraveyardTextBlock.Text = $"Graveyard: {localBoard.Graveyard.Count}";
			OpponentGraveyardTextBlock.Text = $"Graveyard: {opponentBoard.Graveyard.Count}";

			LocalMulliganTextBlock.Text = $"Mulligans: {localBoard.MulligansRemaining}";

			LocalPassStatusTextBlock.Text = localBoard.HasPassedCurrentRound ? "Passed" : string.Empty;
			OpponentPassStatusTextBlock.Text = opponentBoard.HasPassedCurrentRound ? "Passed" : string.Empty;

			if (boardState.WeatherCards != null && boardState.WeatherCards.Any())
			{
				string weatherNames = string.Join(", ", boardState.WeatherCards.Select(c => c.Name));
				WeatherTextBlock.Text = $"Weather: {weatherNames}";
			}
			else
			{
				WeatherTextBlock.Text = "Weather: none";
			}

			LocalMeleeRowListBox.ItemsSource = localBoard.MeleeRow;
			LocalRangedRowListBox.ItemsSource = localBoard.RangedRow;
			LocalSiegeRowListBox.ItemsSource = localBoard.SiegeRow;

			OpponentMeleeRowListBox.ItemsSource = opponentBoard.MeleeRow;
			OpponentRangedRowListBox.ItemsSource = opponentBoard.RangedRow;
			OpponentSiegeRowListBox.ItemsSource = opponentBoard.SiegeRow;

			LocalGraveyardListBox.ItemsSource = localBoard.Graveyard;

			HandListBox.ItemsSource = localBoard.Hand;

			bool isLocalTurn = boardState.ActivePlayerNickname == localBoard.PlayerNickname;

			bool isMulliganPhaseForLocal =
				boardState.CurrentRoundNumber == 1 &&
				localBoard.MulligansRemaining > 0;

			bool canPlayOrPass =
				isLocalTurn &&
				!localBoard.HasPassedCurrentRound &&
				!boardState.IsGameFinished &&
				!isMulliganPhaseForLocal;

			PlayCardButton.IsEnabled = canPlayOrPass;   // Play dla kart globalnych (pogoda, Scorch)
			PassButton.IsEnabled = canPlayOrPass;

			MulliganButton.IsEnabled =
				boardState.CurrentRoundNumber == 1 &&
				!boardState.IsGameFinished &&
				localBoard.MulligansRemaining > 0;

			LeaderAbilityButton.IsEnabled =
				localBoard.LeaderCard != null &&
				!localBoard.LeaderAbilityUsed &&
				!boardState.IsGameFinished &&
				isLocalTurn &&
				!isMulliganPhaseForLocal;

			SurrenderButton.IsEnabled = !boardState.IsGameFinished;

			if (boardState.IsGameFinished && !gameResultAlreadyShown)
			{
				gameResultAlreadyShown = true;

				string message;

				if (boardState.WinnerNickname == null)
				{
					message = "The game ended in a draw.";
				}
				else if (boardState.WinnerNickname == localBoard.PlayerNickname)
				{
					message = "You won the game!";
				}
				else
				{
					message = "You lost the game.";
				}

				MessageBox.Show(message, "Game finished",
					MessageBoxButton.OK, MessageBoxImage.Information);

				mainWindow.CurrentGameClientController?.TryStopServerProcess();
				mainWindow.CurrentGameClientController = null;
				mainWindow.NavigateToMainMenuPage();
			}

			UpdateSelectedCardPanel();
		}

		private void ClearAllListBoxes()
		{
			LocalMeleeRowListBox.ItemsSource = null;
			LocalRangedRowListBox.ItemsSource = null;
			LocalSiegeRowListBox.ItemsSource = null;
			OpponentMeleeRowListBox.ItemsSource = null;
			OpponentRangedRowListBox.ItemsSource = null;
			OpponentSiegeRowListBox.ItemsSource = null;
			LocalGraveyardListBox.ItemsSource = null;
			HandListBox.ItemsSource = null;
		}

		#endregion

		#region Panel szczegółów karty

		private void UpdateSelectedCardPanel()
		{
			if (selectedHandCard == null)
			{
				SelectedCardNameTextBlock.Text = "(no card selected)";
				SelectedCardFactionTextBlock.Text = string.Empty;
				SelectedCardCategoryTextBlock.Text = string.Empty;
				SelectedCardStrengthTextBlock.Text = string.Empty;
				SelectedCardAbilitiesTextBlock.Text = string.Empty;
				return;
			}

			SelectedCardNameTextBlock.Text = selectedHandCard.Name;
			SelectedCardFactionTextBlock.Text = $"Faction: {selectedHandCard.Faction}";
			SelectedCardCategoryTextBlock.Text = $"Category: {selectedHandCard.Category}";
			SelectedCardStrengthTextBlock.Text =
				$"Strength: {selectedHandCard.BaseStrength} (current {selectedHandCard.CurrentStrength})";

			if (selectedHandCard.Abilities != null && selectedHandCard.Abilities.Any())
			{
				string abilitiesString = string.Join(", ", selectedHandCard.Abilities);
				SelectedCardAbilitiesTextBlock.Text = $"Abilities: {abilitiesString}";
			}
			else
			{
				SelectedCardAbilitiesTextBlock.Text = "Abilities: none";
			}
		}

		#endregion

		#region Hand – wybór karty

		private void HandListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			selectedHandCard = HandListBox.SelectedItem as GwentCard;
			UpdateSelectedCardPanel();

			// reset trybów targetowania przy wyborze nowej karty
			pendingTargetType = PendingTargetType.None;
			pendingSourceCard = null;
			pendingMedicRow = null;
		}

		#endregion

		#region Klikanie w rzędy (GroupBoxy)

		private async void LocalRowGroupBox_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			if (selectedHandCard == null)
				return;

			var groupBox = sender as GroupBox;
			if (groupBox?.Tag is not string tag)
				return;

			CardRow row = tag switch
			{
				"Melee" => CardRow.Melee,
				"Ranged" => CardRow.Ranged,
				"Siege" => CardRow.Siege,
				_ => CardRow.Melee
			};

			await PlayHandCardOnRowAsync(selectedHandCard, row, isOpponentRow: false);
		}

		private async void OpponentRowGroupBox_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			if (selectedHandCard == null)
				return;

			var groupBox = sender as GroupBox;
			if (groupBox?.Tag is not string tag)
				return;

			CardRow row = tag switch
			{
				"Melee" => CardRow.Melee,
				"Ranged" => CardRow.Ranged,
				"Siege" => CardRow.Siege,
				_ => CardRow.Melee
			};

			await PlayHandCardOnRowAsync(selectedHandCard, row, isOpponentRow: true);
		}

		#endregion

		#region ListBoxy rzędów – tylko targety (Decoy/Mardroeme) + czyszczenie zaznaczenia

		private async void LocalBoardRow_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			var listBox = sender as ListBox;
			if (listBox == null)
				return;

			var clickedCard = listBox.SelectedItem as GwentCard;

			if ((pendingTargetType == PendingTargetType.DecoyFromBoard ||
				 pendingTargetType == PendingTargetType.MardroemeFromBoard) &&
				pendingSourceCard != null &&
				clickedCard != null)
			{
				var boardState = gameClientController.CurrentBoardState;
				if (boardState == null)
					return;

				var localBoard =
					gameClientController.LocalPlayerRole == GameRole.Host
						? boardState.HostPlayerBoard
						: boardState.GuestPlayerBoard;

				var actionPayload = new GameActionPayload
				{
					ActionType = GameActionType.PlayCard,
					ActingPlayerNickname = localBoard.PlayerNickname,
					CardInstanceId = pendingSourceCard.InstanceId,
					TargetInstanceId = clickedCard.InstanceId
				};

				pendingSourceCard = null;
				pendingTargetType = PendingTargetType.None;
				listBox.SelectedItem = null;

				await gameClientController.SendGameActionAsync(actionPayload);
				return;
			}

			listBox.SelectedItem = null;
		}

		private void OpponentBoardRow_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			var listBox = sender as ListBox;
			if (listBox == null)
				return;

			listBox.SelectedItem = null;
		}

		#endregion

		#region Cmentarz – Medic

		private async void LocalGraveyardListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (pendingTargetType != PendingTargetType.MedicFromGraveyard || pendingSourceCard == null)
				return;

			var targetCard = LocalGraveyardListBox.SelectedItem as GwentCard;
			if (targetCard == null)
				return;

			var boardState = gameClientController.CurrentBoardState;
			if (boardState == null)
				return;

			var localBoard =
				gameClientController.LocalPlayerRole == GameRole.Host
					? boardState.HostPlayerBoard
					: boardState.GuestPlayerBoard;

			var actionPayload = new GameActionPayload
			{
				ActionType = GameActionType.PlayCard,
				ActingPlayerNickname = localBoard.PlayerNickname,
				CardInstanceId = pendingSourceCard.InstanceId,
				TargetInstanceId = targetCard.InstanceId,
				TargetRow = pendingMedicRow
			};

			pendingSourceCard = null;
			pendingTargetType = PendingTargetType.None;
			pendingMedicRow = null;
			LocalGraveyardListBox.SelectedItem = null;

			await gameClientController.SendGameActionAsync(actionPayload);
		}

		#endregion

		#region Zagrywanie karty z ręki na rząd / globalnie

		private async Task PlayHandCardOnRowAsync(GwentCard card, CardRow row, bool isOpponentRow)
		{
			if (gameClientController.CurrentBoardState == null)
				return;

			var boardState = gameClientController.CurrentBoardState;

			var localBoard =
				gameClientController.LocalPlayerRole == GameRole.Host
					? boardState.HostPlayerBoard
					: boardState.GuestPlayerBoard;

			if (boardState.ActivePlayerNickname != localBoard.PlayerNickname)
			{
				MessageBox.Show("It is not your turn.", "Info",
					MessageBoxButton.OK, MessageBoxImage.Information);
				return;
			}

			if (localBoard.HasPassedCurrentRound)
			{
				MessageBox.Show("You have already passed this round.", "Info",
					MessageBoxButton.OK, MessageBoxImage.Information);
				return;
			}

			bool isMulliganPhaseForLocal =
				boardState.CurrentRoundNumber == 1 &&
				localBoard.MulligansRemaining > 0;

			if (isMulliganPhaseForLocal)
			{
				MessageBox.Show("You must finish your mulligans before playing cards.", "Info",
					MessageBoxButton.OK, MessageBoxImage.Information);
				return;
			}

			// Globalne karty (pogoda, ClearWeather, globalny Scorch) powinny być grane bez wybierania kolumny.
			// Tu zabezpieczamy się – jeśli ktoś kliknie rząd taką kartą, i tak gramy globalnie.
			bool isGlobalSpecial =
				card.Category == CardCategory.Weather ||
				(card.Category == CardCategory.Special &&
				 (card.HasAbility(CardAbilityType.Scorch) ||
				  card.HasAbility(CardAbilityType.ClearWeather)));

			if (isGlobalSpecial)
			{
				var actionPayloadGlobal = new GameActionPayload
				{
					ActionType = GameActionType.PlayCard,
					ActingPlayerNickname = localBoard.PlayerNickname,
					CardInstanceId = card.InstanceId
					// TargetRow / TargetInstanceId niepotrzebne
				};

				await gameClientController.SendGameActionAsync(actionPayloadGlobal);
				return;
			}

			// Szpieg – tylko na rzędy przeciwnika
			if (card.HasAbility(CardAbilityType.Spy))
			{
				if (!isOpponentRow)
				{
					MessageBox.Show("Spy cards must be placed on an opponent row.", "Info",
						MessageBoxButton.OK, MessageBoxImage.Information);
					return;
				}

				var actionPayloadSpy = new GameActionPayload
				{
					ActionType = GameActionType.PlayCard,
					ActingPlayerNickname = localBoard.PlayerNickname,
					CardInstanceId = card.InstanceId,
					TargetRow = row
				};

				await gameClientController.SendGameActionAsync(actionPayloadSpy);
				return;
			}

			// Inne karty – nie wolno na rząd przeciwnika
			if (isOpponentRow)
			{
				MessageBox.Show("You can only place Spy cards on opponent rows.", "Info",
					MessageBoxButton.OK, MessageBoxImage.Information);
				return;
			}

			// Medic – wybieramy rząd, potem jednostkę w cmentarzu
			if (card.HasAbility(CardAbilityType.Medic))
			{
				bool hasTarget =
					localBoard.Graveyard.Any(c => c.Category == CardCategory.Unit && !c.IsHero);

				if (!hasTarget)
				{
					MessageBox.Show("No valid Medic targets in your graveyard.", "Info",
						MessageBoxButton.OK, MessageBoxImage.Information);
					return;
				}

				pendingSourceCard = card;
				pendingTargetType = PendingTargetType.MedicFromGraveyard;
				pendingMedicRow = row;

				MessageBox.Show("Select a unit in your graveyard as Medic target.", "Target selection",
					MessageBoxButton.OK, MessageBoxImage.Information);
				return;
			}

			// Decoy / Mardroeme – logika przez klik jednostki na planszy
			if (card.HasAbility(CardAbilityType.Decoy) || card.HasAbility(CardAbilityType.Mardroeme))
			{
				pendingSourceCard = card;
				pendingTargetType = card.HasAbility(CardAbilityType.Decoy)
					? PendingTargetType.DecoyFromBoard
					: PendingTargetType.MardroemeFromBoard;

				MessageBox.Show("Select a unit on your board as target for this card.", "Target selection",
					MessageBoxButton.OK, MessageBoxImage.Information);
				return;
			}

			// Normalne jednostki / rogi / morale / bond itd. – zwykłe zagranie na rząd
			var actionPayload = new GameActionPayload
			{
				ActionType = GameActionType.PlayCard,
				ActingPlayerNickname = localBoard.PlayerNickname,
				CardInstanceId = card.InstanceId,
				TargetRow = row
			};

			await gameClientController.SendGameActionAsync(actionPayload);
		}

		#endregion

		#region Przyciski: Play (globalne karty), Mulligan, Leader, Pass, Surrender

		private async void PlayCardButton_Click(object sender, RoutedEventArgs e)
		{
			if (selectedHandCard == null)
			{
				MessageBox.Show("Select a card in your hand first.", "Info",
					MessageBoxButton.OK, MessageBoxImage.Information);
				return;
			}

			if (gameClientController.CurrentBoardState == null)
				return;

			var boardState = gameClientController.CurrentBoardState;

			var localBoard =
				gameClientController.LocalPlayerRole == GameRole.Host
					? boardState.HostPlayerBoard
					: boardState.GuestPlayerBoard;

			bool isMulliganPhaseForLocal =
				boardState.CurrentRoundNumber == 1 &&
				localBoard.MulligansRemaining > 0;

			if (isMulliganPhaseForLocal)
			{
				MessageBox.Show("You must finish your mulligans before playing cards.", "Info",
					MessageBoxButton.OK, MessageBoxImage.Information);
				return;
			}

			bool isGlobalSpecial =
				selectedHandCard.Category == CardCategory.Weather ||
				(selectedHandCard.Category == CardCategory.Special &&
				 (selectedHandCard.HasAbility(CardAbilityType.Scorch) ||
				  selectedHandCard.HasAbility(CardAbilityType.ClearWeather)));

			if (!isGlobalSpecial)
			{
				MessageBox.Show("To play this card: click a row on the board.", "Info",
					MessageBoxButton.OK, MessageBoxImage.Information);
				return;
			}

			// Pogoda / globalny Scorch / ClearWeather – PLAY bez wybierania rzędu
			var actionPayload = new GameActionPayload
			{
				ActionType = GameActionType.PlayCard,
				ActingPlayerNickname = localBoard.PlayerNickname,
				CardInstanceId = selectedHandCard.InstanceId
			};

			await gameClientController.SendGameActionAsync(actionPayload);
		}

		private async void MulliganButton_Click(object sender, RoutedEventArgs e)
		{
			if (gameClientController.CurrentBoardState == null)
				return;

			var boardState = gameClientController.CurrentBoardState;

			var localBoard =
				gameClientController.LocalPlayerRole == GameRole.Host
					? boardState.HostPlayerBoard
					: boardState.GuestPlayerBoard;

			if (boardState.CurrentRoundNumber != 1)
			{
				MessageBox.Show("Mulligan is only available in round 1.", "Info",
					MessageBoxButton.OK, MessageBoxImage.Information);
				return;
			}

			if (localBoard.MulligansRemaining <= 0)
			{
				MessageBox.Show("You have no mulligans left.", "Info",
					MessageBoxButton.OK, MessageBoxImage.Information);
				return;
			}

			var card = HandListBox.SelectedItem as GwentCard;
			if (card == null)
			{
				MessageBox.Show("Select a card in your hand to mulligan.", "Info",
					MessageBoxButton.OK, MessageBoxImage.Information);
				return;
			}

			var actionPayload = new GameActionPayload
			{
				ActionType = GameActionType.Mulligan,
				ActingPlayerNickname = localBoard.PlayerNickname,
				CardInstanceId = card.InstanceId
			};

			await gameClientController.SendGameActionAsync(actionPayload);
		}

		private async void LeaderAbilityButton_Click(object sender, RoutedEventArgs e)
		{
			if (gameClientController.CurrentBoardState == null)
				return;

			var boardState = gameClientController.CurrentBoardState;

			var localBoard =
				gameClientController.LocalPlayerRole == GameRole.Host
					? boardState.HostPlayerBoard
					: boardState.GuestPlayerBoard;

			if (localBoard.LeaderCard == null || localBoard.LeaderAbilityUsed)
				return;

			var actionPayload = new GameActionPayload
			{
				ActionType = GameActionType.UseLeaderAbility,
				ActingPlayerNickname = localBoard.PlayerNickname,
				CardInstanceId = localBoard.LeaderCard.InstanceId
			};

			await gameClientController.SendGameActionAsync(actionPayload);
		}

		private async void PassButton_Click(object sender, RoutedEventArgs e)
		{
			if (gameClientController.CurrentBoardState == null)
				return;

			var boardState = gameClientController.CurrentBoardState;

			var localBoard =
				gameClientController.LocalPlayerRole == GameRole.Host
					? boardState.HostPlayerBoard
					: boardState.GuestPlayerBoard;

			if (boardState.ActivePlayerNickname != localBoard.PlayerNickname)
			{
				MessageBox.Show("It is not your turn.", "Info",
					MessageBoxButton.OK, MessageBoxImage.Information);
				return;
			}

			if (localBoard.HasPassedCurrentRound)
			{
				MessageBox.Show("You have already passed this round.", "Info",
					MessageBoxButton.OK, MessageBoxImage.Information);
				return;
			}

			bool isMulliganPhaseForLocal =
				boardState.CurrentRoundNumber == 1 &&
				localBoard.MulligansRemaining > 0;

			if (isMulliganPhaseForLocal)
			{
				MessageBox.Show("You must finish your mulligans before passing.", "Info",
					MessageBoxButton.OK, MessageBoxImage.Information);
				return;
			}

			var actionPayload = new GameActionPayload
			{
				ActionType = GameActionType.PassTurn,
				ActingPlayerNickname = localBoard.PlayerNickname
			};

			await gameClientController.SendGameActionAsync(actionPayload);
		}

		private async void SurrenderButton_Click(object sender, RoutedEventArgs e)
		{
			if (gameClientController.CurrentBoardState == null)
				return;

			var boardState = gameClientController.CurrentBoardState;

			var localBoard =
				gameClientController.LocalPlayerRole == GameRole.Host
					? boardState.HostPlayerBoard
					: boardState.GuestPlayerBoard;

			var result = MessageBox.Show(
				"Do you really want to surrender?",
				"Confirm surrender",
				MessageBoxButton.YesNo,
				MessageBoxImage.Question);

			if (result != MessageBoxResult.Yes)
				return;

			var actionPayload = new GameActionPayload
			{
				ActionType = GameActionType.Resign,
				ActingPlayerNickname = localBoard.PlayerNickname
			};

			await gameClientController.SendGameActionAsync(actionPayload);
		}

		#endregion
	}
}
