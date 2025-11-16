using System.IO;
using System.Text.Json;

namespace Gwent.Client.Wpf
{
	public class ClientConfig
	{
		public string Nick { get; set; } = "Gracz";
		public bool LastIsHost { get; set; } = true;
		public string LastServerAddress { get; set; } = "127.0.0.1";
		public int LastPort { get; set; } = 9000;

		private static string ConfigPath =>
			Path.Combine(
				Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
				"GwentPvP",
				"clientconfig.json");

		public static ClientConfig Load()
		{
			try
			{
				if (File.Exists(ConfigPath))
				{
					var json = File.ReadAllText(ConfigPath);
					var cfg = JsonSerializer.Deserialize<ClientConfig>(json);
					if (cfg != null) return cfg;
				}
			}
			catch { }

			return new ClientConfig();
		}

		public void Save()
		{
			try
			{
				var dir = Path.GetDirectoryName(ConfigPath)!;
				if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

				var json = JsonSerializer.Serialize(this, new JsonSerializerOptions
				{
					WriteIndented = true
				});

				File.WriteAllText(ConfigPath, json);
			}
			catch { }
		}
	}
}
