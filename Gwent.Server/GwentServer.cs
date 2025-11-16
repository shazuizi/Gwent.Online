using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Gwent.Core;

namespace Gwent.Server
{
	/// <summary>
	/// Klasa odpowiedzialna za logikę serwera Gwinta:
	/// nasłuchiwanie, przyjmowanie połączeń i start rozgrywki, gdy dwóch graczy jest połączonych i gotowych.
	/// Dodatkowo trzyma GameEngine i przetwarza akcje graczy.
	/// </summary>
	public class GwentServer : IDisposable
	{
		private readonly ServerConfiguration serverConfiguration;
		private readonly TcpListener tcpListener;
		private readonly List<TcpClient> connectedClients = new List<TcpClient>();
		private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

		private GameSessionConfiguration currentGameSessionConfiguration = new GameSessionConfiguration();

		private bool isHostPlayerReady;
		private bool isGuestPlayerReady;

		private GameEngine? gameEngine;

		public GwentServer(ServerConfiguration serverConfiguration)
		{
			this.serverConfiguration = serverConfiguration;
			tcpListener = new TcpListener(IPAddress.Any, serverConfiguration.ListeningPort);
		}

		public async Task StartAsync()
		{
			tcpListener.Start();
			Console.WriteLine("Server listening...");

			try
			{
				while (!cancellationTokenSource.IsCancellationRequested)
				{
					if (tcpListener.Pending())
					{
						TcpClient acceptedClient = await tcpListener.AcceptTcpClientAsync(cancellationTokenSource.Token);
						Console.WriteLine("Client connected.");

						_ = HandleClientAsync(acceptedClient, cancellationTokenSource.Token);
					}

					await Task.Delay(50, cancellationTokenSource.Token);
				}
			}
			catch (OperationCanceledException)
			{
				// Normalne wyjście z pętli.
			}
			finally
			{
				tcpListener.Stop();
			}
		}

		private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
		{
			if (connectedClients.Count >= 2)
			{
				client.Close();
				return;
			}

			connectedClients.Add(client);

			using NetworkStream clientNetworkStream = client.GetStream();

			byte[] readBuffer = new byte[4096];
			string pendingTextBuffer = string.Empty;

			try
			{
				while (!cancellationToken.IsCancellationRequested && client.Connected)
				{
					int bytesRead = await clientNetworkStream.ReadAsync(readBuffer, 0, readBuffer.Length, cancellationToken);
					if (bytesRead <= 0)
					{
						break;
					}

					string receivedChunk = Encoding.UTF8.GetString(readBuffer, 0, bytesRead);
					pendingTextBuffer += receivedChunk;

					int newlineIndex;
					while ((newlineIndex = pendingTextBuffer.IndexOf('\n')) >= 0)
					{
						string rawLine = pendingTextBuffer.Substring(0, newlineIndex).Trim();
						pendingTextBuffer = pendingTextBuffer.Substring(newlineIndex + 1);

						if (string.IsNullOrWhiteSpace(rawLine))
						{
							continue;
						}

						NetworkMessage? networkMessage = null;

						try
						{
							networkMessage = NetworkMessage.Deserialize(rawLine);
						}
						catch (Exception ex)
						{
							Console.WriteLine($"[Server] Failed to deserialize message: {ex.Message}");
							continue;
						}

						if (networkMessage == null)
						{
							Console.WriteLine("[Server] Received null or invalid message.");
							continue;
						}

						try
						{
							await HandleNetworkMessageAsync(client, clientNetworkStream, networkMessage, cancellationToken);
						}
						catch (Exception ex)
						{
							Console.WriteLine($"[Server] Error while handling message {networkMessage.MessageType}: {ex}");
						}
					}
				}
			}
			catch (OperationCanceledException)
			{
				// zignoruj
			}
			catch (Exception ex)
			{
				Console.WriteLine($"[Server] Unhandled exception in HandleClientAsync: {ex}");
			}
			finally
			{
				connectedClients.Remove(client);
				client.Close();
				Console.WriteLine("Client disconnected.");
			}
		}

		private async Task HandleNetworkMessageAsync(
			TcpClient client,
			NetworkStream clientNetworkStream,
			NetworkMessage networkMessage,
			CancellationToken cancellationToken)
		{
			switch (networkMessage.MessageType)
			{
				case NetworkMessageType.PlayerJoinRequest:
					await HandlePlayerJoinRequestAsync(client, clientNetworkStream, networkMessage, cancellationToken);
					break;

				case NetworkMessageType.PlayerReady:
					await HandlePlayerReadyAsync(networkMessage, cancellationToken);
					break;

				case NetworkMessageType.GameAction:
					await HandleGameActionAsync(networkMessage, cancellationToken);
					break;

				default:
					break;
			}
		}

		private async Task HandlePlayerJoinRequestAsync(
			TcpClient client,
			NetworkStream clientNetworkStream,
			NetworkMessage networkMessage,
			CancellationToken cancellationToken)
		{
			PlayerJoinRequestPayload? joinPayload = networkMessage.DeserializePayload<PlayerJoinRequestPayload>();
			if (joinPayload == null)
			{
				return;
			}

			GameRole assignedRole;

			if (string.IsNullOrWhiteSpace(currentGameSessionConfiguration.HostPlayer.Nickname))
			{
				currentGameSessionConfiguration.HostPlayer = joinPayload.JoiningPlayer;
				assignedRole = GameRole.Host;
			}
			else
			{
				currentGameSessionConfiguration.GuestPlayer = joinPayload.JoiningPlayer;
				assignedRole = GameRole.Guest;
			}

			PlayerJoinAcceptedPayload acceptedPayload = new PlayerJoinAcceptedPayload
			{
				IsSessionFull = connectedClients.Count >= 2,
				CurrentGameSessionConfiguration = currentGameSessionConfiguration,
				AssignedRole = assignedRole
			};

			NetworkMessage acceptedMessage = NetworkMessage.Create(NetworkMessageType.PlayerJoinAccepted, acceptedPayload);
			await SendMessageToClientAsync(clientNetworkStream, acceptedMessage, cancellationToken);

			Console.WriteLine($"Player joined as {assignedRole}: {joinPayload.JoiningPlayer.Nickname}");
		}

		private async Task HandlePlayerReadyAsync(
			NetworkMessage networkMessage,
			CancellationToken cancellationToken)
		{
			PlayerReadyPayload? readyPayload = networkMessage.DeserializePayload<PlayerReadyPayload>();
			if (readyPayload == null)
			{
				return;
			}

			if (readyPayload.Nickname == currentGameSessionConfiguration.HostPlayer.Nickname)
			{
				isHostPlayerReady = true;
				Console.WriteLine($"Host player is ready: {readyPayload.Nickname}");
			}
			else if (readyPayload.Nickname == currentGameSessionConfiguration.GuestPlayer.Nickname)
			{
				isGuestPlayerReady = true;
				Console.WriteLine($"Guest player is ready: {readyPayload.Nickname}");
			}

			if (isHostPlayerReady && isGuestPlayerReady && connectedClients.Count == 2)
			{
				// Inicjujemy engine gry
				gameEngine = new GameEngine();
				gameEngine.InitializeNewGame(currentGameSessionConfiguration);

				// wysyłamy komunikat startu gry
				GameStartPayload startPayload = new GameStartPayload
				{
					GameSessionConfiguration = currentGameSessionConfiguration
				};

				NetworkMessage startGameMessage = NetworkMessage.Create(
					NetworkMessageType.BothPlayersReadyStartGame,
					startPayload);

				await BroadcastMessageAsync(startGameMessage, cancellationToken);

				// oraz pierwszy snapshot stanu planszy
				await BroadcastCurrentGameStateAsync(cancellationToken);

				Console.WriteLine("Both players are ready. Start game message + initial state sent.");
			}
		}

		private async Task HandleGameActionAsync(
			NetworkMessage networkMessage,
			CancellationToken cancellationToken)
		{
			if (gameEngine == null)
			{
				return;
			}

			GameActionPayload? actionPayload = networkMessage.DeserializePayload<GameActionPayload>();
			if (actionPayload == null)
			{
				return;
			}

			// stosujemy akcję
			gameEngine.ApplyGameAction(actionPayload);

			// wysyłamy aktualny stan do wszystkich
			await BroadcastCurrentGameStateAsync(cancellationToken);
		}

		private async Task BroadcastCurrentGameStateAsync(CancellationToken cancellationToken)
		{
			if (gameEngine == null)
			{
				return;
			}

			GameStateUpdatePayload updatePayload = new GameStateUpdatePayload
			{
				BoardState = gameEngine.GetCurrentBoardStateSnapshot()
			};

			NetworkMessage updateMessage = NetworkMessage.Create(
				NetworkMessageType.GameStateUpdate,
				updatePayload);

			await BroadcastMessageAsync(updateMessage, cancellationToken);
		}

		private async Task BroadcastMessageAsync(NetworkMessage message, CancellationToken cancellationToken)
		{
			foreach (TcpClient connectedClient in connectedClients)
			{
				if (!connectedClient.Connected)
				{
					continue;
				}

				NetworkStream stream = connectedClient.GetStream();
				await SendMessageToClientAsync(stream, message, cancellationToken);
			}
		}

		private async Task SendMessageToClientAsync(
			NetworkStream clientNetworkStream,
			NetworkMessage message,
			CancellationToken cancellationToken)
		{
			string serializedMessage = message.Serialize() + "\n";
			byte[] data = Encoding.UTF8.GetBytes(serializedMessage);
			await clientNetworkStream.WriteAsync(data, 0, data.Length, cancellationToken);
		}

		public void Stop()
		{
			cancellationTokenSource.Cancel();

			foreach (TcpClient connectedClient in connectedClients)
			{
				connectedClient.Close();
			}

			connectedClients.Clear();
		}

		public void Dispose()
		{
			Stop();
			cancellationTokenSource.Dispose();
		}
	}
}
