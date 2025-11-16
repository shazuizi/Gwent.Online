using System.IO;
using System.Text.Json;

namespace Gwent.Client.Wpf
{
	public class GameConfig
	{
		public string Nickname { get; set; } = "Gracz";
		public bool IsHost { get; set; } = true;
		public string ServerAddress { get; set; } = "127.0.0.1";
		public int Port { get; set; } = 9000;

		private static string ConfigPath =>
			Path.Combine(Directory.GetCurrentDirectory(), "gwent.config.json");

		public static GameConfig Load()
		{
			try
			{
				if (File.Exists(ConfigPath))
				{
					var json = File.ReadAllText(ConfigPath);
					var cfg = JsonSerializer.Deserialize<GameConfig>(json);
					if (cfg != null) return cfg;
				}
			}
			catch { }

			return new GameConfig(); // domyślne
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
