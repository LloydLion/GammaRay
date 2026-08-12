namespace GammaRay.Core.Utils.FileSystem;

public class NullFileSystemLocator : IFileSystemLocator
{
	public static NullFileSystemLocator Instance { get; } = new();

	
	private NullFileSystemLocator() { }


	public ValueTask<string?> GetFileContentAsync(string path) => ValueTask.FromResult<string?>(null);

	public ValueTask SetFileContentAsync(string path, string? content) => ValueTask.CompletedTask;

	public ValueTask<bool> MoveFileAsync(string path, string newPath) => ValueTask.FromResult(false);

	public ValueTask<IEnumerable<string>> ListDirectoryAsync(string path, bool recursive) => ValueTask.FromResult<IEnumerable<string>>([]);
}
