using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace Gwent.Client.Wpf
{
	public partial class LoginWindow : Window
	{
		private const string ConfigFileName = "gwent_config.json";

		public LoginWindow()
		{
			InitializeComponent();
			LoadConfig();
		}

		private string GetConfigPath()
		{
			var folder = Path.Combine(
				Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
				"Gwent.Online");
			Directory.CreateDirectory(folder);
			return Path.Combine(folder, ConfigFileName);
		}

		private void LoadConfig()
		{
			try
			{
				var path = GetConfigPath();
				if (!File.Exists(path)) return;

				var json = File.ReadAllText(path);
				var config = JsonSerializer.Deserialize<LoginConfig>(json);
				if (config == null) return;

				TxtNick.Text = config.Nick ?? "";
				TxtAddress.Text = config.Address ?? "127.0.0.1";
				TxtPort.Text = config.Port.ToString();

				if (config.IsHost)
				{
					RbHost.IsChecked = true;
					RbJoin.IsChecked = false;
				}
				else
				{
					RbHost.IsChecked = false;
					RbJoin.IsChecked = true;
				}
			}
			catch
			{
				// jak się coś wywali przy odczycie – ignorujemy
			}
		}

		private void SaveConfig(LoginConfig config)
		{
			try
			{
				var path = GetConfigPath();
				var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
				{
					WriteIndented = true
				});
				File.WriteAllText(path, json);
			}
			catch
			{
				// brak zapisu = przeżyjemy
			}
		}

		private void Ok_Click(object sender, RoutedEventArgs e)
		{
			var nick = TxtNick.Text.Trim();
			var addr = TxtAddress.Text.Trim();
			var portText = TxtPort.Text.Trim();

			if (string.IsNullOrWhiteSpace(nick))
			{
				MessageBox.Show("Podaj nick.", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}
			if (string.IsNullOrWhiteSpace(addr))
			{
				MessageBox.Show("Podaj adres serwera.", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}
			if (!int.TryParse(portText, out var port) || port <= 0 || port > 65535)
			{
				MessageBox.Show("Nieprawidłowy port.", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}

			bool isHost = RbHost.IsChecked == true;

			// zapis do configu
			var cfg = new LoginConfig
			{
				Nick = nick,
				Address = addr,
				Port = port,
				IsHost = isHost
			};
			SaveConfig(cfg);

			if (isHost)
			{
				// HOST – odpalamy serwer na wybranym porcie (tylko jeśli nie działa)
				try
				{
					StartServerProcess(port);
				}
				catch (Exception ex)
				{
					MessageBox.Show("Nie udało się uruchomić serwera:\n" + ex.Message,
						"Błąd serwera", MessageBoxButton.OK, MessageBoxImage.Error);
					return;
				}

				// jako host zwykle łączysz się na localhost
				addr = "127.0.0.1";
			}

			// Tworzymy okno gry, przekazujemy nick, adres i port
			var main = new MainWindow(nick, addr, port);
			Application.Current.MainWindow = main;
			main.Show();

			Close();
		}

		private void StartServerProcess(int port)
		{
			// zakładamy, że Gwent.Server.exe jest obok klienta po publikacji
			// w czasie developmentu możesz podać pełną ścieżkę do bin\Debug\net8.0\Gwent.Server.exe
			var exeName = "Gwent.Server.exe";

			var startInfo = new ProcessStartInfo
			{
				FileName = exeName,
				Arguments = port.ToString(),
				UseShellExecute = true,
				WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
				CreateNoWindow = false
			};

			Process.Start(startInfo);
		}

		private void Cancel_Click(object sender, RoutedEventArgs e)
		{
			Application.Current.Shutdown();
		}
	}

	public class LoginConfig
	{
		public string? Nick { get; set; }
		public string? Address { get; set; }
		public int Port { get; set; }
		public bool IsHost { get; set; }
	}
}
