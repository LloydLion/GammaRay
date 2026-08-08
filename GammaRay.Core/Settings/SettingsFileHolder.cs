using GammaRay.Core.Utils.FileSystem;
using Microsoft.Extensions.Options;

namespace GammaRay.Core.Settings;

public class SettingsFileHolder(IOptions<SettingsFileHolder.Options> options, IFileSystemLocator _fileSystem)
{
	private readonly Options _options = options.Value;


	public async ValueTask<string> ReadConfigurationFileAsync(bool readBackupFile = false)
	{
		if (readBackupFile)
			goto readBackup;

		var content = await _fileSystem.GetFileContentAsync(_options.FileName);
		if (content is null)
			goto readBackup;
		return content;

	readBackup: // Try open backup file
		return (await _fileSystem.GetFileContentAsync(_options.BackupFileName)) ?? throw new FileNotFoundException("Configuration file not found (main and backup)");
	}

	public async ValueTask WriteConfigurationFileAsync(string content) => await _fileSystem.SetFileContentAsync(_options.FileName, content);


	public class Options
	{
		public string FileName { get; set; } = "settings.yaml";

		public string BackupFileName { get; set; } = "settings.yaml.bak";
	}
}
