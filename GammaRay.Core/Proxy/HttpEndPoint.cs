namespace GammaRay.Core.Proxy;

public readonly record struct HttpEndPoint(string Host, int Port)
{
	public override string ToString()
	{
		return Host + ":" + Port;
	}

	public static HttpEndPoint FromString(string value, int defaultPort)
	{
		var idx = value.IndexOf(':');
		if (idx == -1)
			return new HttpEndPoint(value, defaultPort);
		return new HttpEndPoint(value[..idx], int.Parse(value[(idx + 1)..]));
	}
}
