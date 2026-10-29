
using System;
using System.IO;
using System.Text.Json;

namespace Shared;


public class AppSettings
{
	public string Host { get; set; } = "127.0.0.1";
	public int Port { get; set; } = 9000;
	public string UploadDir { get; set; } = "uploads";
	public int MaxFileSizeMB { get; set; } = 500;
	public int MaxConnections { get; set; } = 100;
	public int ParallelUploads { get; set; } = 3;
	public int TimeoutSeconds { get; set; } = 30;

	public static AppSettings Load()
	{
		string path = Path.Combine(
			AppContext.BaseDirectory, "appsettings.json");

		if (!File.Exists(path))
			return new AppSettings();

		try
		{
			var settings = JsonSerializer.Deserialize<AppSettings>(
				File.ReadAllText(path));

			if (settings == null)
				throw new InvalidDataException(
					"File cấu hình không có dữ liệu hợp lệ.");

			if (settings.Port < 1 || settings.Port > 65535)
				throw new InvalidDataException("Port phải từ 1 đến 65535.");

			if (settings.MaxFileSizeMB <= 0 ||
				settings.MaxConnections <= 0 ||
				settings.ParallelUploads <= 0 ||
				settings.TimeoutSeconds <= 0)
			{
				throw new InvalidDataException(
					"Các giới hạn và timeout phải lớn hơn 0.");
			}

			if (string.IsNullOrWhiteSpace(settings.Host))
				throw new InvalidDataException("Host không được để trống.");

			return settings;
		}
		catch (JsonException ex)
		{
			throw new InvalidDataException(
				"Nội dung appsettings.json không đúng định dạng JSON.", ex);
		}
	}
}
