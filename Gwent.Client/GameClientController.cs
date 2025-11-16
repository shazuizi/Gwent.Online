using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Gwent.Core;

namespace Gwent.Client
{
	/// <summary>
	/// Spina logikę klienta:
	/// - zarządza połączeniem z serwerem,
	/// - wysyła żądanie dołączenia i gotowości,
	/// - przechowuje konfigurację sesji gry i rolę gracza,
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

		/// <summary>
		/// Bieżąca konfiguracja sesji gry otrzymana z serwera.
		/// </summary>
		public GameSessionConfiguration? CurrentGameSessionConfiguration { get; private set; }

		/// <summary>
		/// Rzeczywista rola przydzielona przez serwer (Host / Guest).
		/// </summary>
		public GameRole LocalPlayerRole { get; private set; }

		/// <summary>
		/// Rola, którą wybrał użytkownik w menu (Host / Guest).
		/// </summary>
		public GameRole RequestedGameRole => gameRoleRequestedByUser;

		/// <summary>
		/// Zdarzenie wywoływane, gdy konfiguracja sesji została zaktualizowana.
		/// </summary>
		public event EventHandler? GameSessionUpdated;

		/// <summary>
		/// Zdarzenie wywoływane, gdy serwer nakazuje rozpocząć rozgrywkę.
		/// </summary>
		public event EventHandler? GameShouldStart;

		/// <summary>
		/// Zdarzenie wywoływane, gdy połączenie z serwerem zostało utracone.
		/// </summary>
		public event EventHandler? ServerDisconnected;

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

			LocalPlayerRole = gameRole; // zostanie nadpisane po PlayerJoinAccepted.

			networkClientService = new NetworkClientService();
			networkClientService.NetworkMessageReceived += OnNetworkMessageReceived;
			networkClientService.Disconnected += OnDisconnectedFromServer;
		}

		/// <summary>
		/// Nawiązuje połączenie z serwerem i wysyła żądanie dołączenia.
		/// Dla zwiększenia niezawodności wykonuje kilka prób połączenia z krótkim opóźnieniem
		/// (np. gdy host uruchamia lokalny serwer, który jeszcze nie zdążył zacząć nasłuchiwać).
		/// </summary>
		/// <returns>True, jeśli połączenie i wysłanie żądania dołączenia się powiodło; w przeciwnym razie false.</returns>
		public async Task<bool> ConnectAndJoinAsync()
		{
			const int maxConnectionAttempts = 20;      // 20 prób
			const int delayBetweenAttemptsMs = 250;    // co 250 ms => ~5 sekund łącznie

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

		/// <summary>
		/// Obsługuje przychodzące wiadomości z serwera i reaguje na zmiany sesji oraz start gry.
		/// </summary>
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

				default:
					// TODO: logika innych typów wiadomości.
					break;
			}
		}

		/// <summary>
		/// Przetwarza komunikat o akceptacji dołączenia – zapisuje konfigurację sesji oraz rolę gracza.
		/// </summary>
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

		/// <summary>
		/// Przetwarza komunikat o starcie gry – aktualizuje konfigurację sesji (jeśli przysłana)
		/// i wywołuje zdarzenie GameShouldStart.
		/// </summary>
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

		/// <summary>
		/// Reaguje na utratę połączenia z serwerem – przekazuje informację dalej przez zdarzenie ServerDisconnected.
		/// </summary>
		private void OnDisconnectedFromServer(object? sender, EventArgs e)
		{
			ServerDisconnected?.Invoke(this, EventArgs.Empty);
		}

		/// <summary>
		/// Wysyła do serwera wiadomość, że lokalny gracz jest gotowy do rozpoczęcia gry.
		/// </summary>
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
		/// Ustawia referencję do procesu serwera (jeżeli był uruchomiony przez hosta).
		/// </summary>
		public void SetServerProcess(Process process)
		{
			serverProcess = process;
		}

		/// <summary>
		/// Próbuje bezpiecznie zakończyć proces serwera, jeżeli jeszcze działa.
		/// </summary>
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
					// Ignorujemy problemy z zabiciem procesu.
				}
			}
		}

		/// <summary>
		/// Zwalnia zasoby kontrolera klienta (połączenie sieciowe i proces serwera).
		/// </summary>
		public void Dispose()
		{
			networkClientService.Dispose();
			TryStopServerProcess();
		}
	}
}
