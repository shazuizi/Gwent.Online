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

		private readonly string _nick;
		private readonly string _serverAddress;
		private readonly int _port;

		public MainWindow(string nick, string serverAddress, int port)
		{
			InitializeComponent();

			_nick = nick;
			_serverAddress = serverAddress;
			_port = port;

			Title = $"Gwint Online – {_nick}";
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

				// wysyłamy JOIN z nickiem
				var joinMsg = new NetMessage
				{
					Type = "join",
					Nick = _nick
				};
				var jsonJoin = JsonSerializer.Serialize(joinMsg);
				var bytesJoin = Encoding.UTF8.GetBytes(jsonJoin);
				await _stream.WriteAsync(bytesJoin);

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

			// ustalamy, który gracz to "ja" po nicku
			PlayerState me, opp;

			if (string.Equals(_gameState.Player1.Name, _nick, StringComparison.OrdinalIgnoreCase))
			{
				me = _gameState.Player1;
				opp = _gameState.Player2;
			}
			else
			{
				me = _gameState.Player2;
				opp = _gameState.Player1;
			}

			HandList.ItemsSource = null;
			HandList.ItemsSource = me.Hand;

			TxtStatus.Text =
				$"Runda: {_gameState.RoundNumber} | Tura: {_gameState.CurrentPlayerId} " +
				$"| Ty: {me.Name} (HP: {me.Lives}) vs {opp.Name} (HP: {opp.Lives})";

			var meleeRow = _gameState.Board.Rows.First(r => r.Row == Row.Melee);
			var rangedRow = _gameState.Board.Rows.First(r => r.Row == Row.Ranged);
			var siegeRow = _gameState.Board.Rows.First(r => r.Row == Row.Siege);

			var myMelee = (me == _gameState.Player1) ? meleeRow.Player1Cards : meleeRow.Player2Cards;
			var myRanged = (me == _gameState.Player1) ? rangedRow.Player1Cards : rangedRow.Player2Cards;
			var mySiege = (me == _gameState.Player1) ? siegeRow.Player1Cards : siegeRow.Player2Cards;

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
