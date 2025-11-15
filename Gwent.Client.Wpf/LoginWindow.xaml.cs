using System;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace Gwent.Client.Wpf
{
	public partial class LoginWindow : Window
	{
		public string Nick { get; private set; } = "";
		public string ServerAddress { get; private set; } = "";
		public int Port { get; private set; } = 9000;
		public bool IsHost { get; private set; } = true;

		private ClientConfig _config = ClientConfig.Load();

		public LoginWindow()
		{
			InitializeComponent();

			// wczytujemy z configa
			TxtNick.Text = _config.Nick;
			TxtAddress.Text = _config.LastServerAddress;
			TxtPort.Text = _config.LastPort.ToString();
			RbHost.IsChecked = _config.LastIsHost;
			RbJoin.IsChecked = !_config.LastIsHost;
		}

		private void Ok_Click(object sender, RoutedEventArgs e)
		{
			if (string.IsNullOrWhiteSpace(TxtNick.Text))
			{
				MessageBox.Show("Podaj nick.", "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
				return;
			}

			if (!int.TryParse(TxtPort.Text, out var port) || port <= 0 || port > 65535)
			{
				MessageBox.Show("Podaj poprawny port (1–65535).", "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
				return;
			}

			Nick = TxtNick.Text.Trim();
			ServerAddress = TxtAddress.Text.Trim();
			Port = port;
			IsHost = RbHost.IsChecked == true;

			// zapis configa
			_config.Nick = Nick;
			_config.LastServerAddress = ServerAddress;
			_config.LastPort = Port;
			_config.LastIsHost = IsHost;
			_config.Save();

			// jeśli hostujemy – uruchom serwer (zakładamy, że Gwent.Server.exe jest obok klienta)
			if (IsHost)
			{
				try
				{
					var exeDir = AppDomain.CurrentDomain.BaseDirectory;
					var serverPath = Path.Combine(exeDir, "Gwent.Server.exe");

					if (!File.Exists(serverPath))
					{
						MessageBox.Show(
							"Nie znaleziono Gwent.Server.exe w katalogu aplikacji.\n" +
							"Upewnij się, że serwer jest skopiowany obok klienta.",
							"Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
					}
					else
					{
						Process.Start(new ProcessStartInfo
						{
							FileName = serverPath,
							Arguments = Port.ToString(),
							UseShellExecute = false,
							CreateNoWindow = true
						});
					}
				}
				catch (Exception ex)
				{
					MessageBox.Show("Błąd przy uruchamianiu serwera: " + ex.Message);
				}
			}

			DialogResult = true;
			Close();
		}

		private void Cancel_Click(object sender, RoutedEventArgs e)
		{
			DialogResult = false;
			Close();
		}
	}
}
