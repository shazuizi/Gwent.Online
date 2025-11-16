using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Windows;
using Gwent.Core;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Gwent.Core;

namespace Gwent.Client.Wpf;

public partial class LoginWindow : Window
{
	public LoginWindow() => InitializeComponent();
	private Process? _serverProcess;


	private async void BtnOk_Click(object sender, RoutedEventArgs e)
	{
		string playerName = TxtName.Text;
		string ip = TxtServerAddress.Text;
		int port = int.Parse(TxtPort.Text);
		bool hostStatus = (RbHost.IsChecked == true);

		if(hostStatus)
		{
			StartServer(port.ToString());

		}

		var client = new TcpClient();
		await client.ConnectAsync(ip, port);

		var stream = client.GetStream();

		var join = new NetMessage
		{
			Type = "join",
			Name = playerName,
			IsHost = hostStatus
		};

		string json = JsonSerializer.Serialize(join);
		await stream.WriteAsync(Encoding.UTF8.GetBytes(json));

		// Otwórz główne okno gry
		var game = new MainWindow(ip, port.ToString(), playerName, hostStatus);
		game.Show();
		Close();
	}

	private bool StartServer(string port)
	{
		try
		{
			string serverPath = System.IO.Path.Combine(
				AppDomain.CurrentDomain.BaseDirectory,
				"Gwent.Server.exe"
			);

			_serverProcess = new Process();
			_serverProcess.StartInfo.FileName = serverPath;
			_serverProcess.StartInfo.CreateNoWindow = false; //widocznosc konsoli servera
			_serverProcess.StartInfo.UseShellExecute = false; //.NET lub shell

			_serverProcess.StartInfo.Arguments = port;
			_serverProcess.Start();

			return true;
		}
		catch (Exception ex)
		{
			MessageBox.Show("Nie udało się uruchomić serwera: " + ex.Message);
			return false;
		}
	}
	public void StopServer()
	{
		try
		{
			if (_serverProcess != null && !_serverProcess.HasExited)
				_serverProcess.Kill();
		}
		catch { }
	}

}
