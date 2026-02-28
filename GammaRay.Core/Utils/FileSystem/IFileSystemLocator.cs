namespace GammaRay.Core.Utils.FileSystem;

public interface IFileSystemLocator
{
	public Stream OpenFile(string path);
}
