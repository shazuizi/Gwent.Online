using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Gwent.Core;

namespace Gwent.Client
{
	/// <summary>
	/// Strona głównego menu: wybór nicka, roli (host/guest), adresu serwera, portu i zarządzanie taliami.
	/// </summary>
	public partial class MainMenuPage : Page
	{
		private readonly MainWindow mainWindow;
		private readonly ClientConfigurationManager clientConfigurationManager;
		private ClientConfiguration currentConfiguration;

		private readonly string serverExecutablePath = "Gwent.Server.exe"; // do zmiany, jeśli inna ścieżka

		public MainMenuPage(MainWindow mainWindow)
		{
			InitializeComponent();
			this.mainWindow = mainWindow;

			clientConfigurationManager = new ClientConfigurationManager();
			currentConfiguration = clientConfigurationManager.LoadConfiguration();

			LoadConfigurationToUi();
		}

		/// <summary>
		/// Wczytuje zapisane ustawienia z konfiguracji do pól w UI.
		/// </summary>
		private void LoadConfigurationToUi()
		{
			NicknameTextBox.Text = currentConfiguration.LastUsedNickname;
			ServerAddressTextBox.Text = currentConfiguration.LastUsedServerAddress;
			PortTextBox.Text = currentConfiguration.LastUsedServerPort.ToString();

			if (currentConfiguration.LastUsedRole == GameRole.Host)
			{
				HostRadioButton.IsChecked = true;
			}
			else
			{
				GuestRadioButton.IsChecked = true;
			}
		}

		/// <summary>
		/// Zapisuje aktualne wartości z UI do obiektu konfiguracji i pliku JSON.
		/// </summary>
		private void SaveConfigurationFromUi()
		{
			currentConfiguration.LastUsedNickname = NicknameTextBox.Text;
			currentConfiguration.LastUsedServerAddress = ServerAddressTextBox.Text;
			currentConfiguration.LastUsedRole = HostRadioButton.IsChecked == true ? GameRole.Host : GameRole.Guest;

			if (int.TryParse(PortTextBox.Text, out int parsedPort))
			{
				currentConfiguration.LastUsedServerPort = parsedPort;
			}

			clientConfigurationManager.SaveConfiguration(currentConfiguration);
		}

		/// <summary>
		/// Obsługuje kliknięcie przycisku "Manage Decks" – nawigacja do strony zarządzania taliami.
		/// </summary>
		private void ManageDecksButton_Click(object sender, RoutedEventArgs e)
		{
			mainWindow.NavigateToDeckManagementPage();
		}

		/// <summary>
		/// Obsługuje kliknięcie przycisku "Start Game" – zapisuje konfigurację,
		/// opcjonalnie uruchamia serwer i tworzy GameClientController, po czym przechodzi do strony oczekiwania.
		/// </summary>
		private void StartGameButton_Click(object sender, RoutedEventArgs e)
		{
			SaveConfigurationFromUi();

			string playerNickname = NicknameTextBox.Text;
			string serverAddress = ServerAddressTextBox.Text;
			bool isHost = HostRadioButton.IsChecked == true;

			if (!int.TryParse(PortTextBox.Text, out int serverPort))
			{
				MessageBox.Show("Invalid port number.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}

			if (string.IsNullOrWhiteSpace(playerNickname))
			{
				MessageBox.Show("Please enter a nickname.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}

			Process? startedServerProcess = null;

			if (isHost)
			{
				startedServerProcess = StartServerProcess(serverPort);
				if (startedServerProcess == null)
				{
					// Nie udało się uruchomić serwera.
					return;
				}
			}

			PlayerIdentity playerIdentity = new PlayerIdentity
			{
				Nickname = playerNickname,
				SelectedDeckId = currentConfiguration.LastSelectedDeckId
			};

			GameClientController gameClientController = new GameClientController(
				isHost ? GameRole.Host : GameRole.Guest,
				serverAddress,
				serverPort,
				playerIdentity);

			if (startedServerProcess != null)
			{
				gameClientController.SetServerProcess(startedServerProcess);
			}

			mainWindow.CurrentGameClientController = gameClientController;
			mainWindow.NavigateToWaitingForPlayersPage();
		}

		/// <summary>
		/// Uruchamia proces serwera Gwinta jako osobne EXE z podanym portem i zwraca proces.
		/// </summary>
		private Process? StartServerProcess(int serverPort)
		{
			if (!File.Exists(serverExecutablePath))
			{
				MessageBox.Show($"Cannot find server executable at path: {serverExecutablePath}",
					"Server not found",
					MessageBoxButton.OK,
					MessageBoxImage.Error);
				return null;
			}

			ProcessStartInfo processStartInfo = new ProcessStartInfo
			{
				FileName = serverExecutablePath,
				Arguments = serverPort.ToString(),
				UseShellExecute = false,
				CreateNoWindow = true
			};

			try
			{
				Process startedProcess = Process.Start(processStartInfo);
				return startedProcess;
			}
			catch
			{
				MessageBox.Show("Failed to start server process.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				return null;
			}
		}
	}
}
