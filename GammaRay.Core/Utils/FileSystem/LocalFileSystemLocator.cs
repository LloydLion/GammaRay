using Microsoft.Extensions.Options;

namespace GammaRay.Core.Utils.FileSystem;

public sealed class LocalFileSystemLocator(IOptions<LocalFileSystemLocator.Options> options) : IFileSystemLocator
{
	private readonly Options _options = options.Value;


	public async ValueTask<string?> GetFileContentAsync(string path)
	{
		var pathInfo = GetPath(path);
		return File.Exists(pathInfo.AbsolutePath) ? await File.ReadAllTextAsync(pathInfo.AbsolutePath) : null;
	}

	public ValueTask<IEnumerable<string>> ListDirectoryAsync(string path, bool recursive)
	{
		var directoryPath = Path.Combine(_options.BasePath, path);
		return ValueTask.FromResult(
			Directory
				.GetFiles(directoryPath, "*", recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
				.Select(filePath => Path.GetRelativePath(directoryPath, filePath))
		);
	}

	public ValueTask<bool> MoveFileAsync(string path, string newPath)
	{
		var originalFile = GetPath(path);
		if (File.Exists(originalFile.AbsolutePath) == false)
			return ValueTask.FromResult(false);

		var destinationFile = GetPath(newPath);
		if (Directory.Exists(destinationFile.DirectoryAbsolutePath) == false)
			Directory.CreateDirectory(destinationFile.DirectoryAbsolutePath);
		File.Move(originalFile.AbsolutePath, destinationFile.AbsolutePath);
		return ValueTask.FromResult(true);
	}

	public async ValueTask SetFileContentAsync(string path, string? content)
	{
		var fileInfo = GetPath(path);
		if (content is null && Directory.Exists(fileInfo.DirectoryAbsolutePath))
			File.Delete(fileInfo.AbsolutePath);
		else
		{
			if (Directory.Exists(fileInfo.DirectoryAbsolutePath) == false)
				Directory.CreateDirectory(fileInfo.DirectoryAbsolutePath);
			await File.WriteAllTextAsync(fileInfo.AbsolutePath, content);
		}
	}

	private FilePathInformation GetPath(string path)
	{
		var fullFilePath = Path.GetFullPath(Path.Combine(_options.BasePath, path));
		return new FilePathInformation(fullFilePath, Path.GetDirectoryName(fullFilePath) ?? "");
	}


	public class Options
	{
		public string BasePath { get; set; } = Environment.CurrentDirectory;
	}
	
	private record struct FilePathInformation(string AbsolutePath, string DirectoryAbsolutePath);
}
