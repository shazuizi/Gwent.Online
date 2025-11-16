using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Gwent.Core;

namespace Gwent.Client
{
	/// <summary>
	/// Główna strona rozgrywki – pokazuje stan planszy i pozwala grać karty / passować turę.
	/// </summary>
	public partial class GamePage : Page
	{
		private readonly MainWindow mainWindow;
		private readonly GameClientController gameClientController;

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

		/// <summary>
		/// Ustawia nicki i role graczy na podstawie konfiguracji sesji.
		/// </summary>
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
		/// </summary>
		private void UpdateBoardUi()
		{
			GameBoardState? boardState = gameClientController.CurrentBoardState;
			GameSessionConfiguration? sessionConfiguration = gameClientController.CurrentGameSessionConfiguration;

			if (boardState == null || sessionConfiguration == null)
			{
				// brak stanu – czyścimy listy
				ClearAllListBoxes();
				LocalPlayerStrengthTextBlock.Text = "Strength: 0";
				OpponentStrengthTextBlock.Text = "Strength: 0";
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

			LocalPlayerStrengthTextBlock.Text = $"Strength: {localBoard.GetTotalStrength()}";
			OpponentStrengthTextBlock.Text = $"Strength: {opponentBoard.GetTotalStrength()}";

			LocalMeleeRowListBox.ItemsSource = localBoard.MeleeRow.Select(c => $"{c.Name} ({c.CurrentStrength})");
			LocalRangedRowListBox.ItemsSource = localBoard.RangedRow.Select(c => $"{c.Name} ({c.CurrentStrength})");
			LocalSiegeRowListBox.ItemsSource = localBoard.SiegeRow.Select(c => $"{c.Name} ({c.CurrentStrength})");

			OpponentMeleeRowListBox.ItemsSource = opponentBoard.MeleeRow.Select(c => $"{c.Name} ({c.CurrentStrength})");
			OpponentRangedRowListBox.ItemsSource = opponentBoard.RangedRow.Select(c => $"{c.Name} ({c.CurrentStrength})");
			OpponentSiegeRowListBox.ItemsSource = opponentBoard.SiegeRow.Select(c => $"{c.Name} ({c.CurrentStrength})");

			HandListBox.ItemsSource = localBoard.Hand;
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

		/// <summary>
		/// Obsługa przycisku "Play selected card" – wysyła GameAction do serwera.
		/// </summary>
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

			GameSessionConfiguration? sessionConfiguration = gameClientController.CurrentGameSessionConfiguration;
			if (sessionConfiguration == null)
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

			GameActionPayload actionPayload = new GameActionPayload
			{
				ActionType = GameActionType.PlayCard,
				ActingPlayerNickname = localBoard.PlayerNickname,
				CardInstanceId = selectedCard.InstanceId,
				TargetRow = selectedCard.DefaultRow == CardRow.Agile ? CardRow.Melee : null
			};

			await gameClientController.SendGameActionAsync(actionPayload);
		}

		/// <summary>
		/// Obsługa przycisku "Pass" – wysyła akcję PassTurn.
		/// </summary>
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

			GameActionPayload actionPayload = new GameActionPayload
			{
				ActionType = GameActionType.PassTurn,
				ActingPlayerNickname = localBoard.PlayerNickname
			};

			await gameClientController.SendGameActionAsync(actionPayload);
		}
	}
}
