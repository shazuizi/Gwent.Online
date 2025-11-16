using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Gwent.Core;

namespace Gwent.Client
{
	/// <summary>
	/// Odpowiada za komunikację sieciową klienta z serwerem (TCP).
	/// Wysyła i odbiera wiadomości JSON zakończone znakiem nowej linii ('\n').
	/// </summary>
	public class NetworkClientService : IDisposable
	{
		private TcpClient? tcpClient;
		private NetworkStream? networkStream;
		private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

		/// <summary>
		/// Zdarzenie wywoływane, gdy odebrano poprawną wiadomość sieciową z serwera.
		/// </summary>
		public event EventHandler<NetworkMessage>? NetworkMessageReceived;

		/// <summary>
		/// Zdarzenie wywoływane, gdy połączenie z serwerem zostało utracone
		/// (serwer zamknął socket lub wystąpił błąd połączenia).
		/// </summary>
		public event EventHandler? Disconnected;

		/// <summary>
		/// Nawiązuje połączenie z serwerem pod podanym adresem i portem.
		/// </summary>
		public async Task<bool> ConnectAsync(string serverAddress, int serverPort)
		{
			try
			{
				tcpClient = new TcpClient();
				await tcpClient.ConnectAsync(serverAddress, serverPort);
				networkStream = tcpClient.GetStream();

				// Startujemy pętlę odbioru w tle.
				_ = Task.Run(() => ReceiveLoopAsync(cancellationTokenSource.Token));
				return true;
			}
			catch
			{
				return false;
			}
		}

		/// <summary>
		/// Wysyła pojedynczą wiadomość do serwera jako JSON zakończony '\n'.
		/// </summary>
		public async Task SendMessageAsync(NetworkMessage networkMessage)
		{
			if (networkStream == null)
			{
				return;
			}

			string serializedMessage = networkMessage.Serialize() + "\n";
			byte[] data = Encoding.UTF8.GetBytes(serializedMessage);

			await networkStream.WriteAsync(data, 0, data.Length);
		}

		/// <summary>
		/// Pętla nasłuchująca – blokuje się na ReadAsync, zbiera dane tekstowe,
		/// dzieli po '\n' i każdą linię próbuje zdeserializować do NetworkMessage.
		/// </summary>
		private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
		{
			if (networkStream == null)
			{
				return;
			}

			byte[] readBuffer = new byte[4096];
			string pendingTextBuffer = string.Empty;

			try
			{
				while (!cancellationToken.IsCancellationRequested)
				{
					int bytesRead = await networkStream.ReadAsync(readBuffer, 0, readBuffer.Length, cancellationToken);

					// 0 bajtów = serwer zamknął połączenie (FIN)
					if (bytesRead <= 0)
					{
						Disconnected?.Invoke(this, EventArgs.Empty);
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
						catch
						{
							// Błędny JSON – ignorujemy tę linię, ale nie zrywamy połączenia.
						}

						if (networkMessage != null)
						{
							NetworkMessageReceived?.Invoke(this, networkMessage);
						}
					}
				}
			}
			catch (OperationCanceledException)
			{
				// Normalne wyjście przy Dispose/Disconnect.
			}
			catch
			{
				// Jakikolwiek inny wyjątek traktujemy jako utratę połączenia.
				Disconnected?.Invoke(this, EventArgs.Empty);
			}
		}

		/// <summary>
		/// Zamyka połączenie z serwerem i zatrzymuje pętlę odbioru.
		/// </summary>
		public void Disconnect()
		{
			cancellationTokenSource.Cancel();

			try
			{
				networkStream?.Close();
			}
			catch { }

			try
			{
				tcpClient?.Close();
			}
			catch { }
		}

		/// <summary>
		/// Zwalnia zasoby klienta sieciowego.
		/// </summary>
		public void Dispose()
		{
			Disconnect();
			cancellationTokenSource.Dispose();
		}
	}
}
