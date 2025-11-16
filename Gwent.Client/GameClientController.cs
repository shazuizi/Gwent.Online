using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Gwent.Core;

namespace Gwent.Client
{
	/// <summary>
	/// Spina logikę klienta:
	/// - zarządza połączeniem z serwerem,
	/// - wysyła żądanie dołączenia / gotowości,
	/// - wysyła akcje w grze,
	/// - przechowuje konfigurację sesji, stan planszy i role gracza,
	/// - reaguje na start gry i rozłączenie z serwerem.
	/// </summary>
	public class GameClientController : IDisposable
	{
		private readonly GameRole gameRoleRequestedByUser;
		private readonly string serverAddress;
		private readonly int serverPort;
		private readonly PlayerIdentity localPlayerIdentity;

		private readonly NetworkClientService networkClientService;
		private Process? serverProcess;

		public GameSessionConfiguration? CurrentGameSessionConfiguration { get; private set; }
		public GameBoardState? CurrentBoardState { get; private set; }

		public GameRole LocalPlayerRole { get; private set; }
		public GameRole RequestedGameRole => gameRoleRequestedByUser;

		public event EventHandler? GameSessionUpdated;
		public event EventHandler? GameShouldStart;
		public event EventHandler? ServerDisconnected;

		/// <summary>
		/// Wywoływane, gdy serwer przysłał nowy stan planszy.
		/// </summary>
		public event EventHandler? GameStateUpdated;

		public GameClientController(
			GameRole gameRole,
			string serverAddress,
			int serverPort,
			PlayerIdentity localPlayerIdentity)
		{
			this.gameRoleRequestedByUser = gameRole;
			this.serverAddress = serverAddress;
			this.serverPort = serverPort;
			this.localPlayerIdentity = localPlayerIdentity;

			LocalPlayerRole = gameRole;

			networkClientService = new NetworkClientService();
			networkClientService.NetworkMessageReceived += OnNetworkMessageReceived;
			networkClientService.Disconnected += OnDisconnectedFromServer;
		}

		public async Task<bool> ConnectAndJoinAsync()
		{
			const int maxConnectionAttempts = 20;
			const int delayBetweenAttemptsMs = 250;

			bool isConnected = false;

			for (int attemptIndex = 1; attemptIndex <= maxConnectionAttempts; attemptIndex++)
			{
				isConnected = await networkClientService.ConnectAsync(serverAddress, serverPort);
				if (isConnected)
				{
					Debug.WriteLine($"[GameClientController] Connected on attempt {attemptIndex}.");
					break;
				}

				Debug.WriteLine($"[GameClientController] Connect attempt {attemptIndex} failed. Retrying...");
				await Task.Delay(delayBetweenAttemptsMs);
			}

			if (!isConnected)
			{
				return false;
			}

			PlayerJoinRequestPayload joinRequestPayload = new PlayerJoinRequestPayload
			{
				JoiningPlayer = localPlayerIdentity
			};

			NetworkMessage joinRequestMessage = NetworkMessage.Create(
				NetworkMessageType.PlayerJoinRequest,
				joinRequestPayload);

			await networkClientService.SendMessageAsync(joinRequestMessage);

			return true;
		}

		private void OnNetworkMessageReceived(object? sender, NetworkMessage networkMessage)
		{
			switch (networkMessage.MessageType)
			{
				case NetworkMessageType.PlayerJoinAccepted:
					HandlePlayerJoinAccepted(networkMessage);
					break;

				case NetworkMessageType.BothPlayersReadyStartGame:
					HandleGameStart(networkMessage);
					break;

				case NetworkMessageType.GameStateUpdate:
					HandleGameStateUpdate(networkMessage);
					break;

				default:
					break;
			}
		}

		private void HandlePlayerJoinAccepted(NetworkMessage networkMessage)
		{
			PlayerJoinAcceptedPayload? payload = networkMessage.DeserializePayload<PlayerJoinAcceptedPayload>();
			if (payload == null)
			{
				return;
			}

			CurrentGameSessionConfiguration = payload.CurrentGameSessionConfiguration;
			LocalPlayerRole = payload.AssignedRole;

			GameSessionUpdated?.Invoke(this, EventArgs.Empty);
		}

		private void HandleGameStart(NetworkMessage networkMessage)
		{
			GameStartPayload? startPayload = networkMessage.DeserializePayload<GameStartPayload>();
			if (startPayload != null)
			{
				CurrentGameSessionConfiguration = startPayload.GameSessionConfiguration;
				GameSessionUpdated?.Invoke(this, EventArgs.Empty);
			}

			GameShouldStart?.Invoke(this, EventArgs.Empty);
		}

		private void HandleGameStateUpdate(NetworkMessage networkMessage)
		{
			GameStateUpdatePayload? updatePayload = networkMessage.DeserializePayload<GameStateUpdatePayload>();
			if (updatePayload == null)
			{
				return;
			}

			CurrentBoardState = updatePayload.BoardState;
			GameStateUpdated?.Invoke(this, EventArgs.Empty);
		}

		private void OnDisconnectedFromServer(object? sender, EventArgs e)
		{
			ServerDisconnected?.Invoke(this, EventArgs.Empty);
		}

		public async Task SendPlayerReadyAsync()
		{
			PlayerReadyPayload readyPayload = new PlayerReadyPayload
			{
				Nickname = localPlayerIdentity.Nickname
			};

			NetworkMessage readyMessage = NetworkMessage.Create(
				NetworkMessageType.PlayerReady,
				readyPayload);

			await networkClientService.SendMessageAsync(readyMessage);
		}

		/// <summary>
		/// Wysyła na serwer akcję w grze (zagranie karty, pass).
		/// </summary>
		public async Task SendGameActionAsync(GameActionPayload gameActionPayload)
		{
			NetworkMessage networkMessage = NetworkMessage.Create(
				NetworkMessageType.GameAction,
				gameActionPayload);

			await networkClientService.SendMessageAsync(networkMessage);
		}

		public void SetServerProcess(Process process)
		{
			serverProcess = process;
		}

		public void TryStopServerProcess()
		{
			if (serverProcess != null && !serverProcess.HasExited)
			{
				try
				{
					serverProcess.Kill();
				}
				catch
				{
				}
			}
		}

		public void Dispose()
		{
			networkClientService.Dispose();
			TryStopServerProcess();
		}
	}
}
