using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

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

			_ = ConnectAndSignalReadyAsync();
		}

		/// <summary>
		/// Nawiązuje połączenie z serwerem, wysyła żądanie dołączenia i sygnał gotowości.
		/// </summary>
		private async Task ConnectAndSignalReadyAsync()
		{
			bool isConnected = await gameClientController.ConnectAndJoinAsync();
			if (!isConnected)
			{
				connectionAttemptFinished = true;

				MessageBox.Show("Failed to connect to server.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				mainWindow.CurrentGameClientController?.TryStopServerProcess();
				mainWindow.CurrentGameClientController = null;
				mainWindow.NavigateToMainMenuPage();
				return;
			}

			await gameClientController.SendPlayerReadyAsync();
			connectionAttemptFinished = true;
		}

		/// <summary>
		/// Reaguje na zdarzenie informujące, że gra powinna się rozpocząć – przełącza na GamePage.
		/// </summary>
		private void OnGameShouldStart(object? sender, EventArgs e)
		{
			Dispatcher.Invoke(() =>
			{
				mainWindow.NavigateToGamePage();
			});
		}

		/// <summary>
		/// Obsługuje kliknięcie przycisku "Cancel" – przerywa oczekiwanie i wraca do menu.
		/// </summary>
		private void CancelButton_Click(object sender, RoutedEventArgs e)
		{
			if (!connectionAttemptFinished)
			{
				// Na szkielecie pomijamy dodatkowe synchronizacje.
			}

			mainWindow.CurrentGameClientController?.TryStopServerProcess();
			mainWindow.CurrentGameClientController = null;
			mainWindow.NavigateToMainMenuPage();
		}
	}
}
