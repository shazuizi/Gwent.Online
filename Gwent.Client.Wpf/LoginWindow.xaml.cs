using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text.Json;
using System.Windows;

namespace Gwent.Client.Wpf
{
	public partial class LoginWindow : Window
	{
		private class Config
		{
			public string Nickname { get; set; } = "Gracz";
			public string ServerAddress { get; set; } = "127.0.0.1";
			public int Port { get; set; } = 9000;
			public bool IsHost { get; set; } = true;
		}

		private const string ConfigFileName = "gwent.config.json";

		public LoginWindow()
		{
			InitializeComponent();
			LoadConfig();
		}

		private void LoadConfig()
		{
			try
			{
				if (File.Exists(ConfigFileName))
				{
					var json = File.ReadAllText(ConfigFileName);
					var cfg = JsonSerializer.Deserialize<Config>(json);
					if (cfg != null)
					{
						TxtNickname.Text = cfg.Nickname;
						TxtServerAddress.Text = cfg.ServerAddress;
						TxtPort.Text = cfg.Port.ToString();
						RbHost.IsChecked = cfg.IsHost;
						RbJoin.IsChecked = !cfg.IsHost;
					}
				}
			}
			catch
			{
				// jak coś pójdzie nie tak z configiem – olewamy
			}
		}

		private void SaveConfig(Config cfg)
		{
			try
			{
				var json = JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true });
				File.WriteAllText(ConfigFileName, json);
			}
			catch
			{
				// brak uprawnień itp. – nie zabijamy przez to aplikacji
			}
		}
		private void BtnOk_Click(object sender, RoutedEventArgs e)
		{
			string nick = TxtNickname.Text;
			string ip = TxtServerAddress.Text;
			int port = int.Parse(TxtPort.Text);

			// np. jeśli zaznaczone "Hostuj" → jesteś P1, jeśli "Dołącz" → P2
			string playerId = RbHost.IsChecked == true ? "P1" : "P2";

			var main = new MainWindow(playerId, nick, ip, port);
			main.Show();
			this.Close();
		}

		private async void Ok_Click(object sender, RoutedEventArgs e)
		{
			TxtError.Text = "";

			if (string.IsNullOrWhiteSpace(TxtNickname.Text))
			{
				TxtError.Text = "Podaj nick.";
				return;
			}

			if (!int.TryParse(TxtPort.Text, out int port) || port <= 0 || port > 65535)
			{
				TxtError.Text = "Niepoprawny port.";
				return;
			}

			var isHost = RbHost.IsChecked == true;
			var serverAddress = TxtServerAddress.Text.Trim();

			var cfg = new Config
			{
				Nickname = TxtNickname.Text.Trim(),
				ServerAddress = serverAddress,
				Port = port,
				IsHost = isHost
			};
			SaveConfig(cfg);

			// Jeśli hostujemy, spróbuj uruchomić serwer
			if (isHost)
			{
				// sprawdź, czy serwer już przypadkiem nie działa na tym porcie
				bool serverAlreadyRunning = await IsServerReachable("127.0.0.1", port);

				if (!serverAlreadyRunning)
				{
					try
					{
						StartServerProcess(port);
					}
					catch (Exception ex)
					{
						TxtError.Text = "Błąd startu serwera: " + ex.Message;
						return;
					}
				}

				// klient łączy się zawsze na localhost
				serverAddress = "127.0.0.1";
			}

			// Otwórz okno gry i przekaż ustawienia
			var gameWindow = new MainWindow(
				playerId: isHost ? "P1" : "P2",
				myNick: cfg.Nickname,
				serverAddress: serverAddress,
				serverPort: port);

			gameWindow.Show();
			Close();
		}

		private void StartServerProcess(int port)
		{
			// Ścieżka do Gwent.Server.exe – względna względem folderu klienta (Debug)
			// Zakładam strukturę:
			//  Gwent.Online/
			//    Gwent.Client.Wpf/bin/Debug/net8.0-windows/...
			//    Gwent.Server/bin/Debug/net8.0/...

			var baseDir = AppDomain.CurrentDomain.BaseDirectory;
			var serverPathRelative = Path.Combine(
				baseDir,
				@"..\..\..\..\Gwent.Server\bin\Debug\net8.0\Gwent.Server.exe");

			var serverExe = Path.GetFullPath(serverPathRelative);

			if (!File.Exists(serverExe))
				throw new FileNotFoundException("Nie znaleziono Gwent.Server.exe", serverExe);

			var startInfo = new ProcessStartInfo
			{
				FileName = serverExe,
				Arguments = port.ToString(),
				UseShellExecute = false,
				CreateNoWindow = true
			};

			Process.Start(startInfo);
		}

		private static async System.Threading.Tasks.Task<bool> IsServerReachable(string host, int port)
		{
			try
			{
				using var client = new TcpClient();
				var connectTask = client.ConnectAsync(host, port);
				var timeoutTask = System.Threading.Tasks.Task.Delay(500);

				var finished = await System.Threading.Tasks.Task.WhenAny(connectTask, timeoutTask);
				return finished == connectTask && client.Connected;
			}
			catch
			{
				return false;
			}
		}
	}
}
