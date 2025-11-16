using System;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using Gwent.Core;

namespace Gwent.Client.Wpf
{
	public partial class MainWindow : Window
	{
		private readonly string _playerId;
		private readonly string _myNick;
		private readonly string _serverAddress;
		private readonly int _serverPort;

		private TcpClient? _client;
		private NetworkStream? _stream;
		private GameState? _gameState;

		/// <summary>
		/// Konstruktor wywoływany z LoginWindow:
		/// new MainWindow(playerId, nick, ip, port).Show();
		/// </summary>
		public MainWindow(string playerId, string myNick, string serverAddress, int serverPort)
		{
			InitializeComponent();

			_playerId = playerId;          // np. "P1" lub "P2"
			_myNick = myNick;              // nick z okna logowania
			_serverAddress = serverAddress;
			_serverPort = serverPort;

			TxtMyNick.Text = _myNick;

			ConnectToServer();
		}

		private async void ConnectToServer()
		{
			try
			{
				_client = new TcpClient();
				await _client.ConnectAsync(_serverAddress, _serverPort);
				_stream = _client.GetStream();
				TxtStatus.Text = "Połączono z serwerem.";

				// Wyślij wiadomość powitalną z nickiem (serwer może ją wykorzystać
				// do ustawienia GameState.Player1/Player2.Name)
				var hello = new NetMessage
				{
					Type = "hello",
					PlayerId = _playerId,
					Error = _myNick // wykorzystujemy pole Error jako prosty string, albo dodaj własne pole Nick
				};
				var helloJson = JsonSerializer.Serialize(hello);
				var helloBytes = Encoding.UTF8.GetBytes(helloJson);
				await _stream.WriteAsync(helloBytes);

				_ = ReceiveLoop();
			}
			catch (Exception ex)
			{
				TxtStatus.Text = "Błąd połączenia: " + ex.Message;
			}
		}

		private async Task ReceiveLoop()
		{
			if (_stream == null) return;

			var buffer = new byte[4096];

			while (true)
			{
				int bytes;
				try
				{
					bytes = await _stream.ReadAsync(buffer);
					if (bytes == 0) break;
				}
				catch
				{
					break;
				}

				var json = Encoding.UTF8.GetString(buffer, 0, bytes);
				NetMessage? msg;
				try
				{
					msg = JsonSerializer.Deserialize<NetMessage>(json);
				}
				catch
				{
					continue;
				}

				if (msg == null) continue;

				if (msg.Type == "state" && msg.GameState != null)
				{
					_gameState = msg.GameState;
					Dispatcher.Invoke(UpdateUI);
				}
				else if (msg.Type == "error" && msg.Error != null)
				{
					Dispatcher.Invoke(() => TxtStatus.Text = "Błąd: " + msg.Error);
				}
			}
		}

		private void UpdateUI()
		{
			if (_gameState == null) return;

			var me = _gameState.GetPlayer(_playerId);
			var opp = _gameState.GetOpponent(_playerId);

			// Nicki z GameState (jeśli serwer je ustawił)
			if (!string.IsNullOrWhiteSpace(me.Name))
				TxtMyNick.Text = me.Name;

			if (!string.IsNullOrWhiteSpace(opp.Name))
				TxtEnemyNick.Text = opp.Name;

			HandList.ItemsSource = null;
			HandList.ItemsSource = me.Hand;

			TxtStatus.Text =
				$"Runda: {_gameState.RoundNumber} | Tura: {_gameState.CurrentPlayerId} | " +
				$"HP: {_gameState.Player1.Name}={_gameState.Player1.Lives}  {_gameState.Player2.Name}={_gameState.Player2.Lives}";

			var meleeRow = _gameState.Board.Rows.First(r => r.Row == Row.Melee);
			var rangedRow = _gameState.Board.Rows.First(r => r.Row == Row.Ranged);
			var siegeRow = _gameState.Board.Rows.First(r => r.Row == Row.Siege);

			var myMelee = me == _gameState.Player1 ? meleeRow.Player1Cards : meleeRow.Player2Cards;
			var myRanged = me == _gameState.Player1 ? rangedRow.Player1Cards : rangedRow.Player2Cards;
			var mySiege = me == _gameState.Player1 ? siegeRow.Player1Cards : siegeRow.Player2Cards;

			BoardMelee.ItemsSource = myMelee.Select(c => c.ToString());
			BoardRanged.ItemsSource = myRanged.Select(c => c.ToString());
			BoardSiege.ItemsSource = mySiege.Select(c => c.ToString());
		}

		private async void BtnPlay_Click(object sender, RoutedEventArgs e)
		{
			if (_stream == null || _gameState == null) return;
			if (HandList.SelectedItem is not Card card) return;

			var msg = new NetMessage
			{
				Type = "playCard",
				PlayerId = _playerId,
				CardId = card.Id,
				TargetRow = card.Row
			};

			var json = JsonSerializer.Serialize(msg);
			var bytes = Encoding.UTF8.GetBytes(json);
			await _stream.WriteAsync(bytes);
		}

		private async void BtnPass_Click(object sender, RoutedEventArgs e)
		{
			if (_stream == null) return;

			var msg = new NetMessage
			{
				Type = "pass",
				PlayerId = _playerId
			};

			var json = JsonSerializer.Serialize(msg);
			var bytes = Encoding.UTF8.GetBytes(json);
			await _stream.WriteAsync(bytes);
		}
	}
}
