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
		private readonly string _nick;
		private readonly string _serverAddress;
		private readonly int _port;

		private TcpClient? _client;
		private NetworkStream? _stream;
		private GameState? _gameState;

		public MainWindow(string nick, string serverAddress, int port)
		{
			_nick = nick;
			_serverAddress = serverAddress;
			_port = port;

			InitializeComponent();
			TxtMeName.Text = _nick;
			ConnectToServer();
		}

		private async void ConnectToServer()
		{
			try
			{
				_client = new TcpClient();
				await _client.ConnectAsync(_serverAddress, _port);
				_stream = _client.GetStream();
				TxtStatus.Text = $"Połączono z serwerem {_serverAddress}:{_port}";

				// Wyślij "hello" z nickiem
				var hello = new NetMessage
				{
					Type = "hello",
					Nickname = _nick
				};
				var jsonHello = JsonSerializer.Serialize(hello);
				var bytesHello = Encoding.UTF8.GetBytes(jsonHello);
				await _stream.WriteAsync(bytesHello);

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

			// Ustalamy, który gracz to "ja" po nicku
			PlayerState me;
			PlayerState opp;

			if (string.Equals(_gameState.Player1.Name, _nick, StringComparison.OrdinalIgnoreCase))
			{
				me = _gameState.Player1;
				opp = _gameState.Player2;
			}
			else if (string.Equals(_gameState.Player2.Name, _nick, StringComparison.OrdinalIgnoreCase))
			{
				me = _gameState.Player2;
				opp = _gameState.Player1;
			}
			else
			{
				// fallback – jakby nick nie był jeszcze zsynchronizowany
				me = _gameState.Player1;
				opp = _gameState.Player2;
			}

			TxtMeName.Text = me.Name;
			TxtOppName.Text = opp.Name;

			// Ręka
			HandList.ItemsSource = null;
			HandList.ItemsSource = me.Hand;

			// Status
			TxtStatus.Text =
				$"Runda: {_gameState.RoundNumber} | Tura: {_gameState.CurrentPlayerId} | " +
				$"HP: {_gameState.Player1.Name}={_gameState.Player1.Lives}  {_gameState.Player2.Name}={_gameState.Player2.Lives}";

			// Stół – wybieramy odpowiednią listę kart
			var meleeRow = _gameState.Board.Rows.First(r => r.Row == Row.Melee);
			var rangedRow = _gameState.Board.Rows.First(r => r.Row == Row.Ranged);
			var siegeRow = _gameState.Board.Rows.First(r => r.Row == Row.Siege);

			bool meIsP1 = ReferenceEquals(me, _gameState.Player1);

			var myMelee = meIsP1 ? meleeRow.Player1Cards : meleeRow.Player2Cards;
			var myRanged = meIsP1 ? rangedRow.Player1Cards : rangedRow.Player2Cards;
			var mySiege = meIsP1 ? siegeRow.Player1Cards : siegeRow.Player2Cards;

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
				Type = "pass"
			};

			var json = JsonSerializer.Serialize(msg);
			var bytes = Encoding.UTF8.GetBytes(json);
			await _stream.WriteAsync(bytes);
		}
	}
}
