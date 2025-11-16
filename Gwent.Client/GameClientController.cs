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
	/// - reaguje na start gry.
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
		/// Zdarzenie wywoływane, gdy konfiguracja sesji została zaktualizowana.
		/// </summary>
		public event EventHandler? GameSessionUpdated;

		/// <summary>
		/// Zdarzenie wywoływane, gdy serwer nakazuje rozpocząć rozgrywkę.
		/// </summary>
		public event EventHandler? GameShouldStart;

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

			LocalPlayerRole = gameRole; // zostanie nadpisane po komunikacie z serwera.

			networkClientService = new NetworkClientService();
			networkClientService.NetworkMessageReceived += OnNetworkMessageReceived;
		}

		/// <summary>
		/// Nawiązuje połączenie z serwerem i wysyła żądanie dołączenia.
		/// </summary>
		public async Task<bool> ConnectAndJoinAsync()
		{
			bool isConnected = await networkClientService.ConnectAsync(serverAddress, serverPort);
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
		/// Próbuje bezpiecznie zakończyć proces serwera.
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
		/// Zwalnia zasoby kontrolera klienta.
		/// </summary>
		public void Dispose()
		{
			networkClientService.Dispose();
			TryStopServerProcess();
		}
	}
}
