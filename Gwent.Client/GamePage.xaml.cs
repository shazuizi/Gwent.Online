using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Gwent.Core;

namespace Gwent.Client
{
	/// <summary>
	/// Główna strona rozgrywki – pokazuje stan planszy i pozwala grać karty / passować / poddać grę.
	/// </summary>
	public partial class GamePage : Page
	{
		private readonly MainWindow mainWindow;
		private readonly GameClientController gameClientController;

		private bool gameResultAlreadyShown;

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
			GameSessionConfiguration? sessionConfiguration = gameClientController.CurrentGameSessionConfiguration;
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
			{
				localNickname = "Unknown";
			}

			if (string.IsNullOrWhiteSpace(opponentNickname))
			{
				opponentNickname = "Waiting...";
			}

			LocalPlayerNicknameTextBlock.Text = localNickname;
			OpponentNicknameTextBlock.Text = opponentNickname;
			LocalPlayerRoleTextBlock.Text = localRoleText;
			OpponentRoleTextBlock.Text = opponentRoleText;
		}

		/// <summary>
		/// Odświeża UI planszy na podstawie aktualnego stanu w GameClientController.
		/// Pokazuje rundy, życia, pass, siłę itd.
		/// </summary>
		private void UpdateBoardUi()
		{
			GameBoardState? boardState = gameClientController.CurrentBoardState;
			GameSessionConfiguration? sessionConfiguration = gameClientController.CurrentGameSessionConfiguration;

			if (boardState == null || sessionConfiguration == null)
			{
				ClearAllListBoxes();
				LocalPlayerStrengthTextBlock.Text = "Strength: 0";
				OpponentStrengthTextBlock.Text = "Strength: 0";
				RoundNumberTextBlock.Text = "Round -";
				LocalLivesTextBlock.Text = "Lives: -";
				OpponentLivesTextBlock.Text = "Lives: -";
				LocalPassStatusTextBlock.Text = string.Empty;
				OpponentPassStatusTextBlock.Text = string.Empty;
				PlayCardButton.IsEnabled = false;
				PassButton.IsEnabled = false;
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
			OpponentStrengthTextBlock.Text = $"Strength: {opponentBoard.GetTotalStrength()}";

			LocalLivesTextBlock.Text = $"Lives: {localBoard.LifeTokensRemaining}";
			OpponentLivesTextBlock.Text = $"Lives: {opponentBoard.LifeTokensRemaining}";

			LocalPassStatusTextBlock.Text = localBoard.HasPassedCurrentRound ? "Passed" : string.Empty;
			OpponentPassStatusTextBlock.Text = opponentBoard.HasPassedCurrentRound ? "Passed" : string.Empty;

			LocalMeleeRowListBox.ItemsSource = localBoard.MeleeRow.Select(c => $"{c.Name} ({c.CurrentStrength})");
			LocalRangedRowListBox.ItemsSource = localBoard.RangedRow.Select(c => $"{c.Name} ({c.CurrentStrength})");
			LocalSiegeRowListBox.ItemsSource = localBoard.SiegeRow.Select(c => $"{c.Name} ({c.CurrentStrength})");

			OpponentMeleeRowListBox.ItemsSource = opponentBoard.MeleeRow.Select(c => $"{c.Name} ({c.CurrentStrength})");
			OpponentRangedRowListBox.ItemsSource = opponentBoard.RangedRow.Select(c => $"{c.Name} ({c.CurrentStrength})");
			OpponentSiegeRowListBox.ItemsSource = opponentBoard.SiegeRow.Select(c => $"{c.Name} ({c.CurrentStrength})");

			HandListBox.ItemsSource = localBoard.Hand;

			bool isLocalTurn = boardState.ActivePlayerNickname == localBoard.PlayerNickname;
			bool canPlayOrPass = isLocalTurn && !localBoard.HasPassedCurrentRound && !boardState.IsGameFinished;

			PlayCardButton.IsEnabled = canPlayOrPass;
			PassButton.IsEnabled = canPlayOrPass;
			SurrenderButton.IsEnabled = !boardState.IsGameFinished;

			if (boardState.IsGameFinished && !gameResultAlreadyShown && boardState.WinnerNickname != null)
			{
				gameResultAlreadyShown = true;

				string message;
				if (boardState.WinnerNickname == localBoard.PlayerNickname)
				{
					message = "You won the game!";
				}
				else
				{
					message = "You lost the game.";
				}

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
			HandListBox.ItemsSource = null;
		}

		private async void PlayCardButton_Click(object sender, RoutedEventArgs e)
		{
			if (gameClientController.CurrentBoardState == null)
			{
				return;
			}

			GwentCard? selectedCard = HandListBox.SelectedItem as GwentCard;
			if (selectedCard == null)
			{
				MessageBox.Show("Select a card in your hand first.", "Info",
					MessageBoxButton.OK, MessageBoxImage.Information);
				return;
			}

			GameBoardState boardState = gameClientController.CurrentBoardState;

			PlayerBoardState localBoard =
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

			GameActionPayload actionPayload = new GameActionPayload
			{
				ActionType = GameActionType.PlayCard,
				ActingPlayerNickname = localBoard.PlayerNickname,
				CardInstanceId = selectedCard.InstanceId,
				TargetRow = selectedCard.DefaultRow == CardRow.Agile ? CardRow.Melee : null
			};

			await gameClientController.SendGameActionAsync(actionPayload);
		}

		private async void PassButton_Click(object sender, RoutedEventArgs e)
		{
			if (gameClientController.CurrentBoardState == null)
			{
				return;
			}

			GameBoardState boardState = gameClientController.CurrentBoardState;

			PlayerBoardState localBoard =
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

			GameActionPayload actionPayload = new GameActionPayload
			{
				ActionType = GameActionType.PassTurn,
				ActingPlayerNickname = localBoard.PlayerNickname
			};

			await gameClientController.SendGameActionAsync(actionPayload);
		}

		private async void SurrenderButton_Click(object sender, RoutedEventArgs e)
		{
			if (gameClientController.CurrentBoardState == null)
			{
				return;
			}

			GameBoardState boardState = gameClientController.CurrentBoardState;

			PlayerBoardState localBoard =
				gameClientController.LocalPlayerRole == GameRole.Host
					? boardState.HostPlayerBoard
					: boardState.GuestPlayerBoard;

			var result = MessageBox.Show(
				"Do you really want to surrender?",
				"Confirm surrender",
				MessageBoxButton.YesNo,
				MessageBoxImage.Question);

			if (result != MessageBoxResult.Yes)
			{
				return;
			}

			GameActionPayload actionPayload = new GameActionPayload
			{
				ActionType = GameActionType.Resign,
				ActingPlayerNickname = localBoard.PlayerNickname
			};

			await gameClientController.SendGameActionAsync(actionPayload);
		}
	}
}
