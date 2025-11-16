using System.Windows;

namespace Gwent.Client
{
	/// <summary>
	/// Główne okno aplikacji – zawiera Frame, w którym nawigujemy między stronami.
	/// </summary>
	public partial class MainWindow : Window
	{
		/// <summary>
		/// Aktualny kontroler klienta gry, współdzielony między stronami.
		/// </summary>
		public GameClientController? CurrentGameClientController { get; set; }

		public MainWindow()
		{
			InitializeComponent();
			NavigateToMainMenuPage();
		}

		/// <summary>
		/// Nawiguje do strony głównego menu.
		/// </summary>
		public void NavigateToMainMenuPage()
		{
			MainMenuPage mainMenuPage = new MainMenuPage(this);
			MainFrame.Navigate(mainMenuPage);
		}

		/// <summary>
		/// Nawiguje do strony oczekiwania na drugiego gracza.
		/// </summary>
		public void NavigateToWaitingForPlayersPage()
		{
			if (CurrentGameClientController == null)
			{
				NavigateToMainMenuPage();
				return;
			}

			WaitingForPlayersPage waitingForPlayersPage = new WaitingForPlayersPage(this, CurrentGameClientController);
			MainFrame.Navigate(waitingForPlayersPage);
		}

		/// <summary>
		/// Nawiguje do strony rozgrywki.
		/// </summary>
		public void NavigateToGamePage()
		{
			if (CurrentGameClientController == null)
			{
				NavigateToMainMenuPage();
				return;
			}

			GamePage gamePage = new GamePage(this, CurrentGameClientController);
			MainFrame.Navigate(gamePage);
		}

		/// <summary>
		/// Nawiguje do strony zarządzania taliami.
		/// </summary>
		public void NavigateToDeckManagementPage()
		{
			DeckManagementPage deckManagementPage = new DeckManagementPage(this);
			MainFrame.Navigate(deckManagementPage);
		}
	}
}
