using System;
using System.Threading.Tasks;

namespace Gwent.Server
{
	/// <summary>
	/// Punkt wejścia serwera Gwinta – uruchamia logikę serwera i czeka na zakończenie.
	/// </summary>
	internal class Program
	{
		static async Task Main(string[] args)
		{
			Console.Title = "Gwent Server";

			ServerConfiguration serverConfiguration = ServerConfiguration.ParseFromArguments(args);

			using GwentServer gwentServer = new GwentServer(serverConfiguration);

			Console.WriteLine($"Starting server on port {serverConfiguration.ListeningPort}...");
			await gwentServer.StartAsync();

			Console.WriteLine("Server has been stopped. Press any key to exit.");
			Console.ReadKey();
		}
	}
}
