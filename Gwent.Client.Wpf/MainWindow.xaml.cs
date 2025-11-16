using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Windows;
using Gwent.Core;
using System.Diagnostics;

namespace Gwent.Client.Wpf;

public partial class MainWindow : Window
{
	private TcpClient _client = null!;
	private NetworkStream _stream = null!;
	private GameState? _game;
	private readonly string _playerName;
	private readonly string _address;
	private readonly string _port;
	private readonly bool _isHost;

	public MainWindow(string address, string port, string playerName, bool isHost)
	{
		InitializeComponent();
		_address = address;
		_port = port;
		_playerName = playerName;
		_isHost = isHost;

		Loaded += async (_, __) => await ConnectAndJoinAsync();
	}

	private async Task ConnectAndJoinAsync()
	{
		try
		{
			_client = new TcpClient();
			await _client.ConnectAsync(_address, int.Parse(_port));
			_stream = _client.GetStream();

			// JOIN
			var join = new NetMessage
			{
				Type = "join",
				Name = _playerName
			};

			var json = JsonSerializer.Serialize(join);
			var bytes = Encoding.UTF8.GetBytes(json);

			await _stream.WriteAsync(bytes);

			// START RECEIVE LOOP
			_ = Task.Run(ReceiveLoop);
		}
		catch (Exception ex)
		{
			MessageBox.Show("Nie można połączyć się z serwerem: " + ex.Message);
		}
	}

	private async Task ReceiveLoop()
	{
		var buffer = new byte[4096];

		while (true)
		{
			int bytes = await _stream.ReadAsync(buffer);
			if (bytes == 0) continue;

			string json = Encoding.UTF8.GetString(buffer, 0, bytes);
			var msg = JsonSerializer.Deserialize<NetMessage>(json);
			if (msg == null) continue;

			if (msg.Type == "state" && msg.GameState != null)
			{
				_game = msg.GameState;
				Dispatcher.Invoke(UpdateUI);
			}
		}
	}

	private void UpdateUI()
	{
		TxtStatus.Text = $"You: {_playerName}\n" +
$"{_game!.Player1.Name} vs {_game.Player2.Name}";
	}
}
