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
		private TcpClient? _client;
		private NetworkStream? _stream;
		private GameState? _gameState;

		// Na razie na sztywno: zmień na "P2" w drugim kliencie
		private string _playerId = "P2";

		public MainWindow()
		{
			InitializeComponent();
			ConnectToServer();
		}

		private async void ConnectToServer()
		{
			try
			{
				_client = new TcpClient();
				// jeśli serwer na tym samym kompie:
				await _client.ConnectAsync("192.168.1.12", 9000);
				_stream = _client.GetStream();
				TxtStatus.Text = "Połączono z serwerem.";

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
					if (bytes == 0) break; // serwer rozłączył
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

			var me = _gameState.Player1.PlayerId == _playerId
				? _gameState.Player1
				: _gameState.Player2;

			// Ręka
			HandList.ItemsSource = null;
			HandList.ItemsSource = me.Hand;

			// Status
			TxtStatus.Text =
				$"Runda: {_gameState.RoundNumber} | Tura: {_gameState.CurrentPlayerId} | " +
				$"HP: P1={_gameState.Player1.Lives} P2={_gameState.Player2.Lives}";

			// Stół (pokazujemy tylko swoje karty – na start)
			var meleeRow = _gameState.Board.Rows.First(r => r.Row == Row.Melee);
			var rangedRow = _gameState.Board.Rows.First(r => r.Row == Row.Ranged);
			var siegeRow = _gameState.Board.Rows.First(r => r.Row == Row.Siege);

			var myMelee = _gameState.Player1.PlayerId == _playerId ? meleeRow.Player1Cards : meleeRow.Player2Cards;
			var myRanged = _gameState.Player1.PlayerId == _playerId ? rangedRow.Player1Cards : rangedRow.Player2Cards;
			var mySiege = _gameState.Player1.PlayerId == _playerId ? siegeRow.Player1Cards : siegeRow.Player2Cards;

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
				TargetRow = card.Row  // na razie automatycznie w odpowiedni rząd
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
