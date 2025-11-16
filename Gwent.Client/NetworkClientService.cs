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
	/// </summary>
	public class NetworkClientService : IDisposable
	{
		private TcpClient? tcpClient;
		private NetworkStream? networkStream;
		private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

		public event EventHandler<NetworkMessage>? NetworkMessageReceived;

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

				_ = Task.Run(() => ReceiveLoopAsync(cancellationTokenSource.Token));
				return true;
			}
			catch
			{
				return false;
			}
		}

		/// <summary>
		/// Wysyła pojedynczą wiadomość do serwera.
		/// </summary>
		public async Task SendMessageAsync(NetworkMessage networkMessage)
		{
			if (networkStream == null)
			{
				return;
			}

			string serializedMessage = networkMessage.Serialize();
			byte[] data = Encoding.UTF8.GetBytes(serializedMessage);

			await networkStream.WriteAsync(data, 0, data.Length);
		}

		/// <summary>
		/// Pętla nasłuchująca – odbiera wiadomości z serwera i wyzwala zdarzenie NetworkMessageReceived.
		/// </summary>
		private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
		{
			if (networkStream == null)
			{
				return;
			}

			byte[] readBuffer = new byte[4096];

			try
			{
				while (!cancellationToken.IsCancellationRequested)
				{
					if (!networkStream.DataAvailable)
					{
						await Task.Delay(20, cancellationToken);
						continue;
					}

					int bytesRead = await networkStream.ReadAsync(readBuffer, 0, readBuffer.Length, cancellationToken);
					if (bytesRead <= 0)
					{
						break;
					}

					string rawMessage = Encoding.UTF8.GetString(readBuffer, 0, bytesRead);
					NetworkMessage? networkMessage = NetworkMessage.Deserialize(rawMessage);

					if (networkMessage != null)
					{
						NetworkMessageReceived?.Invoke(this, networkMessage);
					}
				}
			}
			catch (OperationCanceledException)
			{
				// Normalne zakończenie pętli.
			}
		}

		/// <summary>
		/// Zamyka połączenie z serwerem.
		/// </summary>
		public void Disconnect()
		{
			cancellationTokenSource.Cancel();

			networkStream?.Close();
			tcpClient?.Close();
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
