namespace GammaRay.Core.Network;

public readonly record struct WebEndPoint(GenericWebEndPoint GenericEndPoint, TransportType Protocol)
{
	public WebEndPoint(WebHost host, int port, TransportType protocol)
		: this(new GenericWebEndPoint(host, port), protocol)
	{ }


	public WebHost Host => GenericEndPoint.Host;

	public int Port => GenericEndPoint.Port;


	public override string ToString()
	{
		return $"{Host}:{Port}/{Protocol}";
	}

	public static WebEndPoint Parse(string value, int defaultPort, TransportType protocol)
	{
		return new WebEndPoint(GenericWebEndPoint.Parse(value, defaultPort), protocol);
	}
}
