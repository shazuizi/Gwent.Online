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
		private readonly int _serverPort;

		private TcpClient? _client;
		private NetworkStream? _stream;
		private GameState? _gameState;

		public MainWindow(string nick, string serverAddress, int serverPort)
		{
			InitializeComponent();

			_nick = nick;
			_serverAddress = serverAddress;
			_serverPort = serverPort;

			Loaded += async (_, __) => await ConnectAndJoinAsync();
		}

		private async Task ConnectAndJoinAsync()
		{
			try
			{
				_client = new TcpClient();
				await _client.ConnectAsync(_serverAddress, _serverPort);
				_stream = _client.GetStream();
				TxtStatus.Text = "Połączono z serwerem, wysyłam JOIN...";

				// PIERWSZA WIADOMOŚĆ: join
				var join = new NetMessage
				{
					Type = "join",
					PlayerName = _nick
				};

				var json = JsonSerializer.Serialize(join);
				var bytes = Encoding.UTF8.GetBytes(json);
				await _stream.WriteAsync(bytes);

				TxtStatus.Text = "JOIN wysłany, oczekiwanie na przeciwnika...";

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
					msg = null;
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

			// Który PlayerState jest "mój"? – po nicku
			bool iAmP1 = string.Equals(_gameState.Player1.Name, _nick, StringComparison.Ordinal);
			var me = iAmP1 ? _gameState.Player1 : _gameState.Player2;

			HandList.ItemsSource = null;
			HandList.ItemsSource = me.Hand;

			TxtStatus.Text =
				$"Ty: {me.Name} | Runda: {_gameState.RoundNumber} | Tura: {_gameState.CurrentPlayerId} | " +
				$"HP: P1={_gameState.Player1.Lives} P2={_gameState.Player2.Lives}";

			var meleeRow = _gameState.Board.Rows.First(r => r.Row == Row.Melee);
			var rangedRow = _gameState.Board.Rows.First(r => r.Row == Row.Ranged);
			var siegeRow = _gameState.Board.Rows.First(r => r.Row == Row.Siege);

			var myMelee = iAmP1 ? meleeRow.Player1Cards : meleeRow.Player2Cards;
			var myRanged = iAmP1 ? rangedRow.Player1Cards : rangedRow.Player2Cards;
			var mySiege = iAmP1 ? siegeRow.Player1Cards : siegeRow.Player2Cards;

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
