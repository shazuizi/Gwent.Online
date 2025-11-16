using System.Windows;

namespace Gwent.Client.Wpf
{
	public partial class App : Application
	{
		protected override void OnStartup(StartupEventArgs e)
		{
			base.OnStartup(e);

			var login = new LoginWindow();
			var result = login.ShowDialog();

		}
	}
}
