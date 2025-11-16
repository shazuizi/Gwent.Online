using System.IO;
using System.Text.Json;

namespace Gwent.Client.Wpf
{
	public static class AppConfigManager
	{
		private static readonly string FolderPath = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
			"GwentClone");

		private static readonly string FilePath = Path.Combine(FolderPath, "config.json");

		public static AppConfig Load()
		{
			try
			{
				if (File.Exists(FilePath))
				{
					var json = File.ReadAllText(FilePath);
					return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
				}
			}
			catch { }

			return new AppConfig();
		}

		public static void Save(AppConfig config)
		{
			Directory.CreateDirectory(FolderPath);
			var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
			File.WriteAllText(FilePath, json);
		}
	}
}
