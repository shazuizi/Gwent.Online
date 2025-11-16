using System;
using System.IO;
using System.Text.Json;

namespace Gwent.Client
{
	/// <summary>
	/// Odpowiada za zapis i odczyt konfiguracji klienta do/z pliku JSON.
	/// </summary>
	public class ClientConfigurationManager
	{
		private readonly string configurationFilePath;

		public ClientConfigurationManager()
		{
			string applicationFolder = Path.Combine(
				Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
				"GwentPvP");

			if (!Directory.Exists(applicationFolder))
			{
				Directory.CreateDirectory(applicationFolder);
			}

			configurationFilePath = Path.Combine(applicationFolder, "clientConfig.json");
		}

		/// <summary>
		/// Wczytuje konfigurację z pliku JSON (lub zwraca domyślną, jeśli plik nie istnieje).
		/// </summary>
		public ClientConfiguration LoadConfiguration()
		{
			if (!File.Exists(configurationFilePath))
			{
				return new ClientConfiguration();
			}

			try
			{
				string jsonContent = File.ReadAllText(configurationFilePath);
				ClientConfiguration? deserializedConfiguration = JsonSerializer.Deserialize<ClientConfiguration>(jsonContent);
				return deserializedConfiguration ?? new ClientConfiguration();
			}
			catch
			{
				return new ClientConfiguration();
			}
		}

		/// <summary>
		/// Zapisuje konfigurację do pliku JSON.
		/// </summary>
		public void SaveConfiguration(ClientConfiguration configuration)
		{
			string jsonContent = JsonSerializer.Serialize(configuration, new JsonSerializerOptions
			{
				WriteIndented = true
			});

			File.WriteAllText(configurationFilePath, jsonContent);
		}
	}
}
