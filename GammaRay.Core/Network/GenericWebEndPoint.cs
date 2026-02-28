namespace GammaRay.Core.Network;

public readonly record struct GenericWebEndPoint(WebHost Host, int Port)
{
	public override string ToString()
	{
		return $"{Host}:{Port}";
	}

	public static GenericWebEndPoint Parse(string value, int defaultPort)
	{
		var idx = value.IndexOf(':');
		if (idx == -1)
		{
			if (defaultPort == -1)
				throw new FormatException($"'{value}' is not valid web endpoint. Format is 'domain.com:1111'");
			return new GenericWebEndPoint(new WebHost(value), defaultPort);
		}
		return new GenericWebEndPoint(new WebHost(value[..idx]), int.Parse(value[(idx + 1)..]));
	}

	public static GenericWebEndPoint Parse(string value) => Parse(value, -1);
}
