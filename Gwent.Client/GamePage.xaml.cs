using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
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
		}

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
				MessageBox.Show("Connection to server was lost.", "Disconnected", MessageBoxButton.OK, MessageBoxImage.Warning);
				mainWindow.CurrentGameClientController?.TryStopServerProcess();
				mainWindow.CurrentGameClientController = null;
				mainWindow.NavigateToMainMenuPage();
			});
		}

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
			bool canPlayOrPass = isLocalTurn && !localBoard.HasPassedCurrentRound && !boardState.IsGameFinished;

			PlayCardButton.IsEnabled = canPlayOrPass;
			PassButton.IsEnabled = canPlayOrPass;

			MulliganButton.IsEnabled =
				boardState.CurrentRoundNumber == 1 &&
				!boardState.IsGameFinished &&
				localBoard.MulligansRemaining > 0;

			LeaderAbilityButton.IsEnabled =
				localBoard.LeaderCard != null &&
				!localBoard.LeaderAbilityUsed &&
				!boardState.IsGameFinished &&
				isLocalTurn;

			SurrenderButton.IsEnabled = !boardState.IsGameFinished;

			if (boardState.IsGameFinished && !gameResultAlreadyShown && boardState.WinnerNickname != null)
			{
				gameResultAlreadyShown = true;

				string message = boardState.WinnerNickname == localBoard.PlayerNickname
					? "You won the game!"
					: "You lost the game.";

				MessageBox.Show(message, "Game finished", MessageBoxButton.OK, MessageBoxImage.Information);

				mainWindow.CurrentGameClientController?.TryStopServerProcess();
				mainWindow.CurrentGameClientController = null;
				mainWindow.NavigateToMainMenuPage();
			}
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

		private async void PlayCardButton_Click(object sender, RoutedEventArgs e)
		{
			if (gameClientController.CurrentBoardState == null)
				return;

			var selectedCard = HandListBox.SelectedItem as GwentCard;
			if (selectedCard == null)
			{
				MessageBox.Show("Select a card in your hand first.", "Info",
					MessageBoxButton.OK, MessageBoxImage.Information);
				return;
			}

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

			// targetowalne:
			if (selectedCard.HasAbility(CardAbilityType.Medic))
			{
				if (!localBoard.Graveyard.Any(c => c.Category == CardCategory.Unit && !c.IsHero))
				{
					MessageBox.Show("No valid Medic targets in your graveyard.", "Info",
						MessageBoxButton.OK, MessageBoxImage.Information);
					return;
				}

				pendingSourceCard = selectedCard;
				pendingTargetType = PendingTargetType.MedicFromGraveyard;
				MessageBox.Show("Select a unit in your graveyard as Medic target.", "Target selection",
					MessageBoxButton.OK, MessageBoxImage.Information);
				return;
			}

			if (selectedCard.HasAbility(CardAbilityType.Decoy))
			{
				bool hasTarget =
					localBoard.MeleeRow.Any(c => c.Category == CardCategory.Unit && !c.IsHero) ||
					localBoard.RangedRow.Any(c => c.Category == CardCategory.Unit && !c.IsHero) ||
					localBoard.SiegeRow.Any(c => c.Category == CardCategory.Unit && !c.IsHero);

				if (!hasTarget)
				{
					MessageBox.Show("No valid Decoy targets on your board.", "Info",
						MessageBoxButton.OK, MessageBoxImage.Information);
					return;
				}

				pendingSourceCard = selectedCard;
				pendingTargetType = PendingTargetType.DecoyFromBoard;
				MessageBox.Show("Select a unit on your board as Decoy target.", "Target selection",
					MessageBoxButton.OK, MessageBoxImage.Information);
				return;
			}

			if (selectedCard.HasAbility(CardAbilityType.Mardroeme))
			{
				bool hasTarget =
					localBoard.MeleeRow.Any(c => c.Category == CardCategory.Unit && !c.IsHero) ||
					localBoard.RangedRow.Any(c => c.Category == CardCategory.Unit && !c.IsHero) ||
					localBoard.SiegeRow.Any(c => c.Category == CardCategory.Unit && !c.IsHero);

				if (!hasTarget)
				{
					MessageBox.Show("No valid Mardroeme targets on your board.", "Info",
						MessageBoxButton.OK, MessageBoxImage.Information);
					return;
				}

				pendingSourceCard = selectedCard;
				pendingTargetType = PendingTargetType.MardroemeFromBoard;
				MessageBox.Show("Select a unit on your board as Mardroeme target.", "Target selection",
					MessageBoxButton.OK, MessageBoxImage.Information);
				return;
			}

			var actionPayload = new GameActionPayload
			{
				ActionType = GameActionType.PlayCard,
				ActingPlayerNickname = localBoard.PlayerNickname,
				CardInstanceId = selectedCard.InstanceId,
				TargetRow = selectedCard.DefaultRow == CardRow.Agile ? CardRow.Melee : null
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

			var selectedCard = HandListBox.SelectedItem as GwentCard;
			if (selectedCard == null)
			{
				MessageBox.Show("Select a card in your hand to mulligan.", "Info",
					MessageBoxButton.OK, MessageBoxImage.Information);
				return;
			}

			var actionPayload = new GameActionPayload
			{
				ActionType = GameActionType.Mulligan,
				ActingPlayerNickname = localBoard.PlayerNickname,
				CardInstanceId = selectedCard.InstanceId
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
				TargetInstanceId = targetCard.InstanceId
			};

			pendingSourceCard = null;
			pendingTargetType = PendingTargetType.None;
			LocalGraveyardListBox.SelectedItem = null;

			await gameClientController.SendGameActionAsync(actionPayload);
		}

		private async void LocalBoardRow_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if ((pendingTargetType != PendingTargetType.DecoyFromBoard &&
				 pendingTargetType != PendingTargetType.MardroemeFromBoard) ||
				pendingSourceCard == null)
			{
				return;
			}

			var listBox = sender as ListBox;
			if (listBox == null)
				return;

			var targetCard = listBox.SelectedItem as GwentCard;
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
				TargetInstanceId = targetCard.InstanceId
			};

			pendingSourceCard = null;
			pendingTargetType = PendingTargetType.None;
			listBox.SelectedItem = null;

			await gameClientController.SendGameActionAsync(actionPayload);
		}
	}
}
