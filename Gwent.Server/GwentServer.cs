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

		public GwentServer(ServerConfiguration serverConfiguration)
		{
			this.serverConfiguration = serverConfiguration;
			tcpListener = new TcpListener(IPAddress.Any, serverConfiguration.ListeningPort);
		}

		/// <summary>
		/// Uruchamia serwer: startuje nasłuch TCP oraz pętlę akceptowania nowych klientów.
		/// </summary>
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
				// Normalne wyjście z pętli przy zatrzymaniu serwera.
			}
			finally
			{
				tcpListener.Stop();
			}
		}

		/// <summary>
		/// Obsługuje pojedynczego klienta: odbiera wiadomości, reaguje na żądania dołączenia i gotowości.
		/// Wiadomości są w formacie JSON zakończonym '\n'.
		/// </summary>
		private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
		{
			if (connectedClients.Count >= 2)
			{
				// Serwer zapełniony – można by tu odesłać info, ale na razie od razu rozłączamy.
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
						// Klient się odłączył
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
				// Normalne wyjście z pętli przy zatrzymaniu serwera.
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

		/// <summary>
		/// Przetwarza pojedynczą wiadomość sieciową otrzymaną od klienta.
		/// </summary>
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

				default:
					// TODO: obsługa innych typów wiadomości w przyszłości.
					break;
			}
		}

		/// <summary>
		/// Obsługuje żądanie dołączenia gracza – zapisuje gracza jako Host lub Guest
		/// oraz wysyła akceptację wraz z aktualną konfiguracją sesji oraz przypisaną rolą.
		/// </summary>
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

			// Jeżeli HostPlayer nie ma jeszcze nicka – pierwszy dołączający zostaje Hostem.
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

		/// <summary>
		/// Obsługuje informację o gotowości gracza. 
		/// Gdy Host i Guest są gotowi – wysyła do wszystkich komunikat startu gry
		/// wraz z pełną konfiguracją sesji.
		/// </summary>
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
				GameStartPayload startPayload = new GameStartPayload
				{
					GameSessionConfiguration = currentGameSessionConfiguration
				};

				NetworkMessage startGameMessage = NetworkMessage.Create(
					NetworkMessageType.BothPlayersReadyStartGame,
					startPayload);

				foreach (TcpClient connectedClient in connectedClients)
				{
					if (connectedClient.Connected)
					{
						NetworkStream networkStream = connectedClient.GetStream();
						await SendMessageToClientAsync(networkStream, startGameMessage, cancellationToken);
					}
				}

				Console.WriteLine("Both players are ready. Start game message sent.");
			}
		}

		/// <summary>
		/// Wysyła pojedynczą wiadomość do klienta po TCP jako JSON zakończony '\n'.
		/// </summary>
		private async Task SendMessageToClientAsync(
			NetworkStream clientNetworkStream,
			NetworkMessage message,
			CancellationToken cancellationToken)
		{
			string serializedMessage = message.Serialize() + "\n";
			byte[] data = Encoding.UTF8.GetBytes(serializedMessage);
			await clientNetworkStream.WriteAsync(data, 0, data.Length, cancellationToken);
		}

		/// <summary>
		/// Zatrzymuje serwer – zakańcza pętlę nasłuchiwania i rozłącza klientów.
		/// </summary>
		public void Stop()
		{
			cancellationTokenSource.Cancel();

			foreach (TcpClient connectedClient in connectedClients)
			{
				connectedClient.Close();
			}

			connectedClients.Clear();
		}

		/// <summary>
		/// Zwalnia zasoby serwera.
		/// </summary>
		public void Dispose()
		{
			Stop();
			cancellationTokenSource.Dispose();
		}
	}
}
