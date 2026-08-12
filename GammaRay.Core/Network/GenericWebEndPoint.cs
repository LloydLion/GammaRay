namespace GammaRay.Core.Network;

public readonly record struct GenericWebEndPoint(WebHost Host, int Port)
{
	public override string ToString()
	{
		return $"{Host}:{Port}";
	}

	public string ToString(int defaultPort)
	{
		if (Port == defaultPort)
			return Host.ToString();
		return $"{Host}:{Port}";
	}

	public static GenericWebEndPoint Parse(string value, int defaultPort)
	{
		if (TryParse(value, defaultPort, out var result) == false)
			throw new FormatException($"'{value}' is not valid web endpoint. Format is 'domain.com:1111'");
		return result;
	}

	public static GenericWebEndPoint Parse(string value) => Parse(value, -1);

	public static bool TryParse(string value, int defaultPort, out GenericWebEndPoint result)
	{
		var idx = value.IndexOf(':');
		if (idx == -1)
		{
			if (defaultPort == -1)
			{
				result = default;
				return false;
			}

			result = new GenericWebEndPoint(new WebHost(value), defaultPort);
			return true;
		}

		result = new GenericWebEndPoint(new WebHost(value[..idx]), int.Parse(value[(idx + 1)..]));
		return true;
	}

	public static bool TryParse(string value, out GenericWebEndPoint result) => TryParse(value, -1, out result);
}
