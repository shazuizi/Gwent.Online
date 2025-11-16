using System.Windows;

namespace Gwent.Client.Wpf
{
	public partial class App : Application
	{
		private void Application_Startup(object sender, StartupEventArgs e)
		{
			var login = new LoginWindow();
			login.Show();
			
		}
	}
}
