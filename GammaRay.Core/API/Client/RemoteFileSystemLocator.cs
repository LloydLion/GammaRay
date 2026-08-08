using GammaRay.Core.API.Services.Proto;
using GammaRay.Core.Utils.FileSystem;

namespace GammaRay.Core.API.Client;

public sealed class RemoteFileSystemLocator(FileSystemService.FileSystemServiceClient _fileSystemService) : IFileSystemLocator
{
	public async ValueTask<string?> GetFileContentAsync(string path)
	{
		var result = await _fileSystemService.GetFileContentAsync(new GetFileContentRequest { Path = path });
		return result.HasContent ? result.Content : null;
	}

	public async ValueTask<IEnumerable<string>> ListDirectoryAsync(string path, bool recursive)
	{
		var result = await _fileSystemService.ListDirectoryAsync(new ListDirectoryRequest { DirectoryPath = path, Recursive = recursive });
		return result.Entries;
	}

	public async ValueTask<bool> MoveFileAsync(string path, string newPath)
	{
		var result = await _fileSystemService.MoveFileAsync(new MoveFileRequest { SourcePath = path, DestinationPath = newPath });
		return result.Success;
	}

	public async ValueTask SetFileContentAsync(string path, string? content)
	{
		await _fileSystemService.SetFileContentAsync(new SetFileContentRequest { Path = path, Content = content });
	}
}
