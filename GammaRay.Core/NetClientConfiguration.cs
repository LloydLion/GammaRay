using System.Net;

namespace GammaRay.Core;

public class NetClientConfiguration(string name)
{
	public string Name { get; } = name;

	public IPEndPoint? ProxyServer { get; init; }

	public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(10);

	public TimeSpan RequestInternal { get; init; } = TimeSpan.FromSeconds(3);

	public int RequestCount { get; init; } = 3;

	public int MaxRequestCount { get; init; } = 5;
}
