namespace GammaRay.Core.API;

public interface IAPIClientConnection : IAsyncDisposable
{
	public ValueTask CloseAsync();


	public string Name { get; }

	public Stream Stream { get; }

	public bool IsOpen { get; }
}
