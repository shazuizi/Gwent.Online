using System;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace Gwent.Client.Wpf
{
	public partial class LoginWindow : Window
	{
		private ClientConfig _config = null!;

		public LoginWindow()
		{
			InitializeComponent();
			LoadConfig();
		}

		private void LoadConfig()
		{
			_config = ClientConfig.Load();

			TxtNick.Text = _config.Nickname;
			TxtAddress.Text = _config.ServerAddress;
			TxtPort.Text = _config.ServerPort.ToString();
			RbHost.IsChecked = _config.LastWasHost;
			RbJoin.IsChecked = !_config.LastWasHost;
		}

		private void SaveConfig(bool isHost, string nick, string addr, int port)
		{
			_config.Nickname = nick;
			_config.ServerAddress = addr;
			_config.ServerPort = port;
			_config.LastWasHost = isHost;
			_config.Save();
		}

		private void BtnCancel_Click(object sender, RoutedEventArgs e)
		{
			Application.Current.Shutdown();
		}

		private void BtnOk_Click(object sender, RoutedEventArgs e)
		{
			bool isHost = RbHost.IsChecked == true;
			string nick = TxtNick.Text.Trim();
			string addr = TxtAddress.Text.Trim();
			if (!int.TryParse(TxtPort.Text.Trim(), out int port))
			{
				MessageBox.Show("Nieprawidłowy port.", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}

			if (string.IsNullOrWhiteSpace(nick))
			{
				MessageBox.Show("Podaj nick.", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}

			if (string.IsNullOrWhiteSpace(addr))
			{
				addr = "127.0.0.1";
			}

			SaveConfig(isHost, nick, addr, port);

			if (isHost)
			{
				try
				{
					// ścieżka do serwera: ten sam folder, obok klienta – jeśli masz inaczej,
					// dostosuj ścieżkę.
					string baseDir = AppDomain.CurrentDomain.BaseDirectory;
					string serverPath = Path.Combine(baseDir, "..", "..", "..", "..",
						"Gwent.Server", "bin", "Debug", "net8.0", "Gwent.Server.exe");

					serverPath = Path.GetFullPath(serverPath);

					var psi = new ProcessStartInfo
					{
						FileName = serverPath,
						Arguments = port.ToString(),
						UseShellExecute = false,
						CreateNoWindow = false
					};

					Process.Start(psi);
				}
				catch (Exception ex)
				{
					MessageBox.Show("Nie udało się uruchomić serwera: " + ex.Message,
						"Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
					return;
				}
			}

			// uruchamiamy okno gry
			var main = new MainWindow(nick, addr, port);
			main.Show();

			this.Close();
		}
	}
}
