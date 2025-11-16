using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Gwent.Core;

namespace Gwent.Server
{
	internal class Program
	{
		static async Task Main(string[] args)
		{
			int port = 9000;
			if (args.Length > 0 && int.TryParse(args[0], out var p))
				port = p;

			var listener = new TcpListener(IPAddress.Any, port);
			listener.Start();
			Console.WriteLine($"Serwer Gwinta: nasłuchuję na porcie {port}...");

			var gameState = new GameState();

			// lista (PlayerId, TcpClient)
			var clients = new List<(string PlayerId, TcpClient Client)>();

			// --- ETAP 1: przyjmujemy 2 graczy i oczekujemy "join" ---
			while (clients.Count < 2)
			{
				var client = await listener.AcceptTcpClientAsync();
				Console.WriteLine("Nowe połączenie, czekam na JOIN...");

				var stream = client.GetStream();
				var buffer = new byte[4096];
				int bytes;

				try
				{
					bytes = await stream.ReadAsync(buffer);
					if (bytes == 0)
					{
						client.Close();
						continue;
					}
				}
				catch
				{
					client.Close();
					continue;
				}

				var json = Encoding.UTF8.GetString(buffer, 0, bytes);
				NetMessage? msg;
				try
				{
					msg = JsonSerializer.Deserialize<NetMessage>(json);
				}
				catch
				{
					msg = null;
				}

				if (msg == null || msg.Type != "join" || string.IsNullOrWhiteSpace(msg.PlayerName))
				{
					Console.WriteLine("Nieprawidłowa pierwsza wiadomość (oczekiwano 'join').");
					await SendError(stream, "Nieprawidłowa pierwsza wiadomość (oczekiwano 'join').");
					client.Close();
					continue;
				}

				// przypisujemy gracza jako P1 albo P2
				string playerId = clients.Count == 0 ? "P1" : "P2";

				if (playerId == "P1")
				{
					gameState.Player1.PlayerId = playerId;
					gameState.Player1.Name = msg.PlayerName;
				}
				else
				{
					gameState.Player2.PlayerId = playerId;
					gameState.Player2.Name = msg.PlayerName;
				}

				clients.Add((playerId, client));
				Console.WriteLine($"Gracz '{msg.PlayerName}' dołączył jako {playerId}.");
			}

			// --- ETAP 2: start gry ---
			GameLogic.StartGame(gameState);
			Console.WriteLine("Gra rozpoczęta.");

			// lista samych TcpClientów do broadcastu stanu
			var clientSockets = new List<TcpClient>(clients.Count);
			foreach (var c in clients)
				clientSockets.Add(c.Client);

			// start pętli dla każdego gracza
			foreach (var (playerId, client) in clients)
			{
				_ = HandleClient(playerId, client, gameState, clientSockets);
			}

			// trzymamy proces przy życiu
			await Task.Delay(Timeout.Infinite);
		}

		private static async Task HandleClient(
			string playerId,
			TcpClient client,
			GameState gameState,
			List<TcpClient> allClients)
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
					if (bytes == 0) break; // klient się rozłączył
				}
				catch
				{
					break;
				}

				var json = Encoding.UTF8.GetString(buffer, 0, bytes);
				NetMessage? msg;
				try
				{
					msg = JsonSerializer.Deserialize<NetMessage>(json);
				}
				catch
				{
					msg = null;
				}

				if (msg == null) continue;

				if (msg.Type == "playCard" && msg.CardId != null)
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

				await SendStateToAll(allClients, gameState);
			}
		}

		private static async ValueTask SendError(NetworkStream stream, string error)
		{
			var msg = new NetMessage { Type = "error", Error = error };
			var json = JsonSerializer.Serialize(msg);
			var bytes = Encoding.UTF8.GetBytes(json);
			await stream.WriteAsync(bytes);
		}

		private static async ValueTask SendStateToAll(List<TcpClient> clients, GameState gameState)
		{
			var msg = new NetMessage
			{
				Type = "state",
				GameState = gameState
			};

			var json = JsonSerializer.Serialize(msg);
			var bytes = Encoding.UTF8.GetBytes(json);

			foreach (var client in clients)
			{
				var stream = client.GetStream();
				await stream.WriteAsync(bytes);
			}
		}
	}
}
