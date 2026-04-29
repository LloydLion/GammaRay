namespace GammaRay.Core.Utils.FileSystem;

public interface IFileSystemLocator
{
	public void Move(string originalFilePath, string newFilePath, bool overwrite = false);

	public bool Exists(string filePath);
	
	public Stream Open(string path, FileMode mode = FileMode.Open, FileAccess access = FileAccess.Read, FileShare share = FileShare.None);
}
