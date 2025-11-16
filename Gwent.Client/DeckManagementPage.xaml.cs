using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Gwent.Core;

namespace Gwent.Client
{
	/// <summary>
	/// Strona do zarządzania taliami – na razie skeleton z listą talii, szczegółami i ustawianiem aktywnej talii.
	/// </summary>
	public partial class DeckManagementPage : Page
	{
		private readonly MainWindow mainWindow;
		private readonly ClientConfigurationManager clientConfigurationManager;
		private ClientConfiguration currentConfiguration;

		private readonly List<DeckSummary> availableDecks = new List<DeckSummary>();

		public DeckManagementPage(MainWindow mainWindow)
		{
			InitializeComponent();
			this.mainWindow = mainWindow;

			clientConfigurationManager = new ClientConfigurationManager();
			currentConfiguration = clientConfigurationManager.LoadConfiguration();

			LoadAvailableDecks();
			DecksListBox.SelectionChanged += DecksListBox_SelectionChanged;

			SelectCurrentDeckInList();
		}

		/// <summary>
		/// Ładuje dostępne talie do listy (na razie przykładowe dane).
		/// </summary>
		private void LoadAvailableDecks()
		{
			availableDecks.Clear();

			availableDecks.Add(new DeckSummary
			{
				DeckId = "default-deck",
				DeckName = "Default Northern Realms",
				FactionName = "Northern Realms",
				CardsCount = 25
			});

			availableDecks.Add(new DeckSummary
			{
				DeckId = "scoiatael-deck",
				DeckName = "Scoia'tael Control",
				FactionName = "Scoia'tael",
				CardsCount = 25
			});

			availableDecks.Add(new DeckSummary
			{
				DeckId = "nilfgaard-deck",
				DeckName = "Nilfgaard Spies",
				FactionName = "Nilfgaard",
				CardsCount = 25
			});

			DecksListBox.ItemsSource = availableDecks;
		}

		/// <summary>
		/// Ustawia w UI aktualnie wybraną talię jako zaznaczoną.
		/// </summary>
		private void SelectCurrentDeckInList()
		{
			foreach (DeckSummary deckSummary in availableDecks)
			{
				if (deckSummary.DeckId == currentConfiguration.LastSelectedDeckId)
				{
					DecksListBox.SelectedItem = deckSummary;
					break;
				}
			}
		}

		/// <summary>
		/// Reaguje na zmianę zaznaczonej talii na liście – pokazuje jej szczegóły.
		/// </summary>
		private void DecksListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			DeckSummary? selectedDeck = DecksListBox.SelectedItem as DeckSummary;
			if (selectedDeck == null)
			{
				SelectedDeckNameTextBlock.Text = string.Empty;
				SelectedDeckDetailsTextBlock.Text = string.Empty;
				return;
			}

			SelectedDeckNameTextBlock.Text = selectedDeck.DeckName;
			SelectedDeckDetailsTextBlock.Text =
				$"Faction: {selectedDeck.FactionName}\nCards: {selectedDeck.CardsCount}";
		}

		/// <summary>
		/// Ustawia aktualnie zaznaczoną talię jako aktywną i zapisuje ją w konfiguracji.
		/// </summary>
		private void SetAsActiveDeckButton_Click(object sender, RoutedEventArgs e)
		{
			DeckSummary? selectedDeck = DecksListBox.SelectedItem as DeckSummary;
			if (selectedDeck == null)
			{
				MessageBox.Show("Please select a deck first.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
				return;
			}

			currentConfiguration.LastSelectedDeckId = selectedDeck.DeckId;
			clientConfigurationManager.SaveConfiguration(currentConfiguration);

			MessageBox.Show($"Active deck set to: {selectedDeck.DeckName}", "Deck selected",
				MessageBoxButton.OK, MessageBoxImage.Information);
		}

		/// <summary>
		/// Wraca do menu głównego.
		/// </summary>
		private void BackButton_Click(object sender, RoutedEventArgs e)
		{
			mainWindow.NavigateToMainMenuPage();
		}
	}
}
