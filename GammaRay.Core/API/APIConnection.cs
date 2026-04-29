namespace GammaRay.Core.API;

public class APIConnection(string name, IAPIListeningEndPoint source, Stream stream, Guid id)
{
	public string Name { get; } = name;

	public IAPIListeningEndPoint Source { get; } = source;

	public Stream Stream { get; } = stream;

	public Guid Id { get; } = id;
}
