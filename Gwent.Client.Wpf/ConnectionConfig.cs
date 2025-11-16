using System;

namespace Gwent.Client.Wpf
{
	public class ConnectionConfig
	{
		public string Nick { get; set; } = "Gracz";
		public bool IsHost { get; set; } = true;
		public string ServerAddress { get; set; } = "127.0.0.1";
		public int Port { get; set; } = 9000;
	}
}
