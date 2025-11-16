namespace Gwent.Client.Wpf
{
	public class AppConfig
	{
		public string Nick { get; set; } = "Player";
		public bool IsHost { get; set; } = true;
		public string ServerAddress { get; set; } = "127.0.0.1";
		public int Port { get; set; } = 9000;
	}
}
