using GammaRay.Core.Utils.FileSystem;
using Microsoft.Extensions.Options;
using System.Text;

namespace GammaRay.Core.Settings;

public class SettingsFileHolder(IOptions<SettingsFileHolder.Options> options, IFileSystemLocator _fileSystem)
{
	private readonly Options _options = options.Value;


	public TextReader ReadConfigurationFile(bool readBackupFile = false)
	{
		if (readBackupFile)
			goto readBackup;
		try
		{
			if (_fileSystem.Exists(_options.FileName) == false)
				goto readBackup;
			var stream = _fileSystem.Open(_options.FileName, FileMode.Open, FileAccess.Read, FileShare.None);
			if (stream.Length == 0)
				goto readBackup;
			return new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, -1, leaveOpen: false);
		}
		catch (IOException) { }

	readBackup: // Try open backup file
		var backupStream = _fileSystem.Open(_options.BackupFileName, FileMode.Open, FileAccess.Read, FileShare.None);
		return new StreamReader(backupStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, -1, leaveOpen: false);
	}

	public TextWriter WriteConfigurationFile(bool clearExisting = true)
	{
		if (clearExisting && _fileSystem.Exists(_options.FileName))
			_fileSystem.Move(_options.FileName, _options.BackupFileName, overwrite: true);

		var stream = _fileSystem.Open(_options.FileName, clearExisting ? FileMode.CreateNew : FileMode.Open, FileAccess.Write, FileShare.None);
		return new StreamWriter(stream, Encoding.UTF8, -1, leaveOpen: false);
	}


	public class Options
	{
		public string FileName { get; set; } = "settings.yaml";

		public string BackupFileName { get; set; } = "settings.yaml.bak";
	}
}
