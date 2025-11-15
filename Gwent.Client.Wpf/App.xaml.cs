using System.Windows;

namespace Gwent.Client.Wpf
{
	public partial class App : Application
	{
		private void Application_Startup(object sender, StartupEventArgs e)
		{
			var login = new LoginWindow();
			var result = login.ShowDialog();

			if (result == true)
			{
				var main = new MainWindow(login.Nick, login.ServerAddress, login.Port);
				main.Show();
			}
			else
			{
				Shutdown();
			}
		}
	}
}
