using Microsoft.Extensions.Options;

namespace GammaRay.Core.Utils.FileSystem;

public sealed class LocalFileSystemLocator(IOptions<LocalFileSystemLocator.Options> options) : IFileSystemLocator
{
	private readonly Options _options = options.Value;


	public async ValueTask<string?> GetFileContentAsync(string path)
	{
		path = GetPath(path);
		return File.Exists(path) ? await File.ReadAllTextAsync(path) : null;
	}

	public ValueTask<IEnumerable<string>> ListDirectoryAsync(string path, bool recursive)
	{
		return ValueTask.FromResult(Directory.GetFiles(path, "", SearchOption.AllDirectories).Select(GetPath));
	}

	public ValueTask<bool> MoveFileAsync(string path, string newPath)
	{
		path = GetPath(path);
		if (File.Exists(path))
		{
			File.Move(path, GetPath(newPath));
			return ValueTask.FromResult(true);
		}
		return ValueTask.FromResult(false);
	}

	public async ValueTask SetFileContentAsync(string path, string? content)
	{
		path = GetPath(path);
		if (content is null)
			File.Delete(path);
		else
			await File.WriteAllTextAsync(path, content);
	}

	private string GetPath(string path) => Path.Combine(_options.BasePath, path);


	public class Options
	{
		public string BasePath { get; set; } = Environment.CurrentDirectory;
	}
}
