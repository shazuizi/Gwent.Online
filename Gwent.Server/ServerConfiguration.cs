namespace Gwent.Server
{
	/// <summary>
	/// Konfiguracja serwera, np. port nasłuchiwania.
	/// </summary>
	public class ServerConfiguration
	{
		public int ListeningPort { get; set; } = 5000;

		/// <summary>
		/// Odczytuje konfigurację z argumentów linii komend (np. port).
		/// </summary>
		public static ServerConfiguration ParseFromArguments(string[] args)
		{
			ServerConfiguration serverConfiguration = new ServerConfiguration();

			if (args.Length > 0 && int.TryParse(args[0], out int parsedPort))
			{
				serverConfiguration.ListeningPort = parsedPort;
			}

			return serverConfiguration;
		}
	}
}
