namespace GammaRay.Core.Utils.FileSystem;

public interface IFileSystemLocator
{
	public ValueTask<string?> GetFileContentAsync(string path);

	public ValueTask SetFileContentAsync(string path, string? content);

	public ValueTask<bool> MoveFileAsync(string path, string newPath);

	public ValueTask<IEnumerable<string>> ListDirectoryAsync(string path, bool recursive);
}
