using System.Text.Json;

namespace Gwent.Core
{
	/// <summary>
	/// Bazowa klasa wiadomości sieciowej – zawiera typ oraz treść w formacie JSON.
	/// </summary>
	public class NetworkMessage
	{
		public NetworkMessageType MessageType { get; set; }
		public string PayloadJson { get; set; } = string.Empty;

		/// <summary>
		/// Tworzy wiadomość z dowolnego obiektu (serializacja do JSON).
		/// </summary>
		public static NetworkMessage Create<T>(NetworkMessageType messageType, T payloadObject)
		{
			return new NetworkMessage
			{
				MessageType = messageType,
				PayloadJson = JsonSerializer.Serialize(payloadObject)
			};
		}

		/// <summary>
		/// Deserializuje treść wiadomości do określonego typu.
		/// </summary>
		public T? DeserializePayload<T>()
		{
			if (string.IsNullOrWhiteSpace(PayloadJson))
			{
				return default;
			}

			return JsonSerializer.Deserialize<T>(PayloadJson);
		}

		/// <summary>
		/// Serializuje całą wiadomość do jednego stringa (np. do wysyłki po TCP).
		/// </summary>
		public string Serialize()
		{
			return JsonSerializer.Serialize(this);
		}

		/// <summary>
		/// Deserializuje string do obiektu NetworkMessage.
		/// </summary>
		public static NetworkMessage? Deserialize(string rawMessage)
		{
			return JsonSerializer.Deserialize<NetworkMessage>(rawMessage);
		}
	}
}