using System;
using System.IO;
using System.Text.Json;

namespace Gwent.Client.Wpf
{
	public class ClientConfig
	{
		public string Nickname { get; set; } = "Gracz";
		public string ServerAddress { get; set; } = "127.0.0.1";
		public int ServerPort { get; set; } = 9000;
		public bool LastWasHost { get; set; } = true;

		public static string ConfigPath =>
			Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "gwent.config.json");

		public static ClientConfig Load()
		{
			try
			{
				if (!File.Exists(ConfigPath))
					return new ClientConfig();

				var json = File.ReadAllText(ConfigPath);
				var cfg = JsonSerializer.Deserialize<ClientConfig>(json);
				return cfg ?? new ClientConfig();
			}
			catch
			{
				return new ClientConfig();
			}
		}

		public void Save()
		{
			var json = JsonSerializer.Serialize(this, new JsonSerializerOptions
			{
				WriteIndented = true
			});
			File.WriteAllText(ConfigPath, json);
		}
	}
}
