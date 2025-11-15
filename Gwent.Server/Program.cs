using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Gwent.Core;

namespace Gwent.Server;

class Program
{
	static async Task Main()
	{
		var listener = new TcpListener(IPAddress.Any, 9000);
		listener.Start();
		Console.WriteLine("Serwer Gwinta: nasłuchuję na porcie 9000...");

		var clientSockets = new List<TcpClient>();

		while (clientSockets.Count < 2)
		{
			var client = await listener.AcceptTcpClientAsync();
			clientSockets.Add(client);
			Console.WriteLine($"Gracz {clientSockets.Count} połączony.");
		}

		var gameState = new GameState
		{
			Player1 = new PlayerState { PlayerId = "P1", Name = "Gracz 1" },
			Player2 = new PlayerState { PlayerId = "P2", Name = "Gracz 2" }
		};

		GameLogic.StartGame(gameState);
		Console.WriteLine("Gra rozpoczęta.");

		_ = HandleClient("P1", clientSockets[0], gameState, clientSockets);
		_ = HandleClient("P2", clientSockets[1], gameState, clientSockets);

		await Task.Delay(Timeout.Infinite);
	}

	static async Task HandleClient(string playerId, TcpClient client, GameState gameState, List<TcpClient> allClients)
	{
		using var stream = client.GetStream();
		await SendStateToAll(allClients, gameState);

		var buffer = new byte[4096];
		while (true)
		{
			int bytes;
			try
			{
				bytes = await stream.ReadAsync(buffer); 
				if (bytes == 0) break;
			}
			catch
			{
				break;
			}
			//Obsługa wiadomości
			var json = Encoding.UTF8.GetString(buffer, 0, bytes);
			var msg = JsonSerializer.Deserialize<NetMessage>(json);
			if (msg == null) continue;

			if (msg.Type == "join" && !string.IsNullOrWhiteSpace(msg.Nick))
			{
				// Ustawiamy nick gracza po stronie serwera
				if (playerId == "P1")
					gameState.Player1.Name = msg.Nick;
				else
					gameState.Player2.Name = msg.Nick;

				Console.WriteLine($"Gracz {playerId} podał nick: {msg.Nick}");
			}
			else if (msg.Type == "playCard" && msg.CardId != null)
			{
				bool ok = GameLogic.PlayCard(gameState, playerId, msg.CardId, msg.TargetRow);
				if (!ok)
				{
					await SendError(stream, "Nie można zagrać tej karty.");
				}
			}
			else if (msg.Type == "pass")
			{
				GameLogic.Pass(gameState, playerId);
			}

			// Po każdej akcji odświeżamy stan u obu
			await SendStateToAll(allClients, gameState);

		}
	}

	static async ValueTask SendError(NetworkStream stream, string error)
	{
		var msg = new NetMessage { Type = "error", Error = error };
		var json = JsonSerializer.Serialize(msg);
		var bytes = Encoding.UTF8.GetBytes(json);
		await stream.WriteAsync(bytes);
	}

	static async ValueTask SendStateToAll(List<TcpClient> clients, GameState gameState)
	{
		var msg = new NetMessage
		{
			Type = "state",
			GameState = gameState
		};
		var json = JsonSerializer.Serialize(msg);
		var bytes = Encoding.UTF8.GetBytes(json);

		foreach (var c in clients)
		{
			var s = c.GetStream();
			await s.WriteAsync(bytes);
		}
	}
}
