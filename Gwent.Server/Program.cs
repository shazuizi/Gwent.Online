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
		// Wspólny stan gry
		private static readonly GameState _gameState = new GameState
		{
			Player1 = new PlayerState { PlayerId = "P1", Name = string.Empty },
			Player2 = new PlayerState { PlayerId = "P2", Name = string.Empty }
		};

		private static readonly List<TcpClient> _clients = new();
		private static readonly Dictionary<TcpClient, string> _clientToPlayerId = new();
		private static readonly object _lock = new();

		static async Task Main(string[] args)
		{
			// PORT z argumentu, domyślnie 9000
			int port = 9000;
			if (args.Length > 0 && int.TryParse(args[0], out var p))
				port = p;

			var listener = new TcpListener(IPAddress.Any, port);
			listener.Start();
			Console.WriteLine($"[SERVER] Gwint startuje na porcie {port}...");

			while (true)
			{
				var client = await listener.AcceptTcpClientAsync();
				lock (_lock)
				{
					_clients.Add(client);
				}

				Console.WriteLine("[SERVER] Nowe połączenie przyjęte.");
				_ = HandleClient(client);
			}
		}

		private static async Task HandleClient(TcpClient client)
		{
			using var stream = client.GetStream();
			var buffer = new byte[4096];

			string? playerId = null;

			// 1) ODBIÓR PIERWSZEJ WIADOMOŚCI "join" Z NICKIEM
			int bytes;
			try
			{
				bytes = await stream.ReadAsync(buffer);
				if (bytes == 0) return;
			}
			catch
			{
				return;
			}

			var json = Encoding.UTF8.GetString(buffer, 0, bytes);
			var joinMsg = JsonSerializer.Deserialize<NetMessage>(json);

			if (joinMsg == null || joinMsg.Type != "join")
			{
				await SendError(stream, "Nieprawidłowa pierwsza wiadomość (oczekiwano 'join').");
				client.Close();
				return;
			}

			string nick = string.IsNullOrWhiteSpace(joinMsg.Nick) ? "Gracz" : joinMsg.Nick;

			// 2) PRZYDZIELENIE SLOTU P1/P2
			lock (_lock)
			{
				if (string.IsNullOrEmpty(_gameState.Player1.Name))
				{
					playerId = "P1";
					_gameState.Player1.Name = nick;
				}
				else if (string.IsNullOrEmpty(_gameState.Player2.Name))
				{
					playerId = "P2";
					_gameState.Player2.Name = nick;
				}
				else
				{
					// Serwer pełny
					playerId = null;
				}

				if (playerId != null)
				{
					_clientToPlayerId[client] = playerId;
					Console.WriteLine($"[SERVER] {nick} dołącza jako {playerId}");
				}
			}

			if (playerId == null)
			{
				await SendError(stream, "Serwer jest pełny (2 graczy).");
				client.Close();
				return;
			}

			// 3) Wyślij informację "joined" z PlayerId i aktualnym stanem
			await SendMessage(stream, new NetMessage
			{
				Type = "joined",
				PlayerId = playerId,
				GameState = _gameState
			});

			// 4) Jeśli mamy już 2 nicki, a gra jeszcze nie wystartowała -> start
			lock (_lock)
			{
				if (_gameState.Phase == GamePhase.WaitingForPlayers &&
					!string.IsNullOrEmpty(_gameState.Player1.Name) &&
					!string.IsNullOrEmpty(_gameState.Player2.Name))
				{
					Console.WriteLine("[SERVER] Obaj gracze połączeni – start gry.");
					GameLogic.StartGame(_gameState);
					_ = SendStateToAll(); // broadcast nowego stanu
				}
			}

			// 5) Główna pętla odbioru wiadomości od tego klienta
			while (true)
			{
				try
				{
					bytes = await stream.ReadAsync(buffer);
					if (bytes == 0) break; // rozłączony
				}
				catch
				{
					break;
				}

				json = Encoding.UTF8.GetString(buffer, 0, bytes);
				var msg = JsonSerializer.Deserialize<NetMessage>(json);
				if (msg == null) continue;

				// We wszystkich wiadomościach po join spodziewamy się PlayerId od klienta
				var pid = msg.PlayerId ?? playerId;
				if (pid == null) continue;

				if (msg.Type == "playCard" && msg.CardId != null)
				{
					bool ok;
					lock (_lock)
					{
						ok = GameLogic.PlayCard(_gameState, pid, msg.CardId, msg.TargetRow);
					}

					if (!ok)
					{
						await SendError(stream, "Nie można zagrać tej karty.");
					}
					else
					{
						await SendStateToAll();
					}
				}
				else if (msg.Type == "pass")
				{
					lock (_lock)
					{
						GameLogic.Pass(_gameState, pid);
					}
					await SendStateToAll();
				}
			}

			// 6) Rozłączenie
			lock (_lock)
			{
				Console.WriteLine("[SERVER] Gracz rozłączony.");
				_clients.Remove(client);
				_clientToPlayerId.Remove(client);
			}
		}

		private static async ValueTask SendError(NetworkStream stream, string error)
		{
			var msg = new NetMessage { Type = "error", Error = error };
			await SendMessage(stream, msg);
		}

		private static async ValueTask SendMessage(NetworkStream stream, NetMessage msg)
		{
			var json = JsonSerializer.Serialize(msg);
			var bytes = Encoding.UTF8.GetBytes(json);
			await stream.WriteAsync(bytes);
		}

		private static async ValueTask SendStateToAll()
		{
			var msg = new NetMessage
			{
				Type = "state",
				GameState = _gameState
			};

			var json = JsonSerializer.Serialize(msg);
			var bytes = Encoding.UTF8.GetBytes(json);

			List<TcpClient> clientsCopy;
			lock (_lock)
			{
				clientsCopy = new List<TcpClient>(_clients);
			}

			foreach (var client in clientsCopy)
			{
				try
				{
					var stream = client.GetStream();
					await stream.WriteAsync(bytes);
				}
				catch
				{
					// ignoruj błędy pojedynczych klientów
				}
			}
		}
	}
}
