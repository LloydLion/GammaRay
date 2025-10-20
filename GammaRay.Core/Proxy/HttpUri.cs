using System.Text;

namespace GammaRay.Core.Proxy;

public record HttpUri(string? Schema, HttpEndPoint EndPoint, string? Path, string? Query)
{
	public override string ToString()
	{
		var sb = new StringBuilder();
		if (Schema != null)
			sb.Append(Schema).Append("://");
		sb.Append(EndPoint.ToString());
		if (Path != null)
			sb.Append('/').Append(Path);
		if (Query != null)
			sb.Append('?').Append(Query);
		return sb.ToString();
	}

	public static HttpUri Parse(string requestUri)
	{
		string? schema = null;
		int port = -1;
		if (requestUri.Contains("://"))
		{
			var split = requestUri.Split("://", 2);
			schema = split[0];
			requestUri = split[1];
			port = schema switch
			{
				"http" => 80,
				"https" => 443,
				_ => throw new FormatException("Unsupported URI scheme")
			};
		}

		var parts = requestUri.Split('/', 2);
		var endpointParts = parts[0].Split(':', 2);
		if (endpointParts.Length == 2)
			port = int.Parse(endpointParts[1]);

		if (port == -1)
			throw new FormatException("Port missing in URI");

		string? path = null;
		string? query = null;

		if (parts.Length == 2)
		{
			var pathAndMore = parts[1];
			var queryParts = pathAndMore.Split('?', 2);
			path = queryParts[0];
			if (queryParts.Length == 2)
				query = queryParts[1];
		}

		return new HttpUri(schema, new HttpEndPoint(new Site(endpointParts[0]), port), path, query);
	}
}