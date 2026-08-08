using GammaRay.Core.API.Services.Proto;
using GammaRay.Core.Utils.FileSystem;
using Grpc.Core;

namespace GammaRay.Core.API.Services;

public sealed class APIFileSystemService(IFileSystemLocator _fileSystem) : FileSystemService.FileSystemServiceBase
{
	public override async Task<GetFileContentResponse> GetFileContent(GetFileContentRequest request, ServerCallContext context)
	{
		var content = await _fileSystem.GetFileContentAsync(request.Path);
		return new GetFileContentResponse { Content = content };
	}

	public override async Task<ListDirectoryResponse> ListDirectory(ListDirectoryRequest request, ServerCallContext context)
	{
		var directories = await _fileSystem.ListDirectoryAsync(request.DirectoryPath, request.Recursive);
		var response = new ListDirectoryResponse();
		response.Entries.AddRange(directories);
		return response;
	}

	public override async Task<MoveFileResponse> MoveFile(MoveFileRequest request, ServerCallContext context)
	{
		var success = await _fileSystem.MoveFileAsync(request.SourcePath, request.DestinationPath);
		return new MoveFileResponse() { Success = success };
	}

	public override async Task<Empty> SetFileContent(SetFileContentRequest request, ServerCallContext context)
	{
		await _fileSystem.SetFileContentAsync(request.Path, request.Content);
		return new Empty();
	}
}
