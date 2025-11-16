using System;
using System.Threading.Tasks;

namespace Gwent.Server
{
	internal class Program
	{
		static async Task Main(string[] args)
		{
			Console.Title = "Gwent Server";

			try
			{
				ServerConfiguration serverConfiguration = ServerConfiguration.ParseFromArguments(args);

				using GwentServer gwentServer = new GwentServer(serverConfiguration);

				Console.WriteLine($"Starting server on port {serverConfiguration.ListeningPort}...");
				await gwentServer.StartAsync();

				Console.WriteLine("Server has been stopped. Press any key to exit.");
				Console.ReadKey();
			}
			catch (Exception ex)
			{
				Console.WriteLine("Fatal error in server:");
				Console.WriteLine(ex);
				Console.WriteLine("Press any key to exit.");
				Console.ReadKey();
			}
		}
	}
}
