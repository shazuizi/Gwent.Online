using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Gwent.Core;

namespace Gwent.Client
{
	/// <summary>
	/// Strona pokazywana podczas oczekiwania na drugiego gracza.
	/// </summary>
	public partial class WaitingForPlayersPage : Page
	{
		private readonly MainWindow mainWindow;
		private readonly GameClientController gameClientController;

		private bool connectionAttemptFinished;

		public WaitingForPlayersPage(MainWindow mainWindow, GameClientController gameClientController)
		{
			InitializeComponent();
			this.mainWindow = mainWindow;
			this.gameClientController = gameClientController;

			this.gameClientController.GameShouldStart += OnGameShouldStart;
			this.gameClientController.ServerDisconnected += OnServerDisconnected;

			_ = ConnectAndSignalReadyAsync();
		}

		/// <summary>
		/// Nawiązuje połączenie z serwerem, wysyła żądanie dołączenia i sygnał gotowości.
		/// Aktualizuje status w UI informujący, co aktualnie się dzieje.
		/// </summary>
		private async Task ConnectAndSignalReadyAsync()
		{
			// Jeśli to host – dajmy serwerowi chwilę na odpalenie się.
			if (gameClientController.RequestedGameRole == GameRole.Host)
			{
				Dispatcher.Invoke(() =>
				{
					ConnectionStatusTextBlock.Text = "Starting local server...";
				});

				// Krótkie opóźnienie na wystartowanie procesu serwera i rozpoczęcie nasłuchu.
				await Task.Delay(1500);
			}

			Dispatcher.Invoke(() =>
			{
				ConnectionStatusTextBlock.Text = "Connecting to server...";
			});

			bool isConnected = await gameClientController.ConnectAndJoinAsync();
			if (!isConnected)
			{
				connectionAttemptFinished = true;

				Dispatcher.Invoke(() =>
				{
					ConnectionStatusTextBlock.Text = "Failed to connect.";
				});

				MessageBox.Show("Failed to connect to server.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);

				mainWindow.CurrentGameClientController?.TryStopServerProcess();
				mainWindow.CurrentGameClientController = null;

				mainWindow.NavigateToMainMenuPage();
				return;
			}

			Dispatcher.Invoke(() =>
			{
				ConnectionStatusTextBlock.Text = "Connected. Waiting for other player...";
			});

			await gameClientController.SendPlayerReadyAsync();
			connectionAttemptFinished = true;
		}

		/// <summary>
		/// Reakcja na zdarzenie informujące, że gra powinna się rozpocząć – przełącza na GamePage.
		/// </summary>
		private void OnGameShouldStart(object? sender, EventArgs e)
		{
			Dispatcher.Invoke(() =>
			{
				mainWindow.NavigateToGamePage();
			});
		}

		/// <summary>
		/// Reakcja na utratę połączenia z serwerem – informuje gracza i wraca do menu głównego.
		/// </summary>
		private void OnServerDisconnected(object? sender, EventArgs e)
		{
			Dispatcher.Invoke(() =>
			{
				ConnectionStatusTextBlock.Text = "Lost connection to server.";
				MessageBox.Show("Connection to server was lost.", "Disconnected", MessageBoxButton.OK, MessageBoxImage.Warning);

				mainWindow.CurrentGameClientController?.TryStopServerProcess();
				mainWindow.CurrentGameClientController = null;

				mainWindow.NavigateToMainMenuPage();
			});
		}

		/// <summary>
		/// Obsługuje kliknięcie przycisku "Cancel" – przerywa oczekiwanie i wraca do menu.
		/// </summary>
		private void CancelButton_Click(object sender, RoutedEventArgs e)
		{
			mainWindow.CurrentGameClientController?.TryStopServerProcess();
			mainWindow.CurrentGameClientController = null;

			mainWindow.NavigateToMainMenuPage();
		}
	}
}
