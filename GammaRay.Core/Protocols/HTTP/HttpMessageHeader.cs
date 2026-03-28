using GammaRay.Core.Network.Flow;
using System.Text;

namespace GammaRay.Core.Protocols.HTTP;

public abstract class HttpMessageHeader(Version version, HttpHeadersCollection headers)
{
	public const string Terminator = "\r\n\r\n";
	public static readonly int ASCIITerminator = BitConverter.ToInt32(Encoding.UTF8.GetBytes(Terminator));
	public static readonly Version HTTP11 = new(1, 1);

	public Version Version { get; } = version;

	public HttpHeadersCollection Headers { get; } = headers;


	protected static HttpHeadersCollection ParseHeaders(ReadOnlySpan<string> lines)
	{
		var headers = new HttpHeadersCollection();

		for (int i = 0; i < lines.Length; i++)
		{
			var line = lines[i].AsSpan();

			var idx = line.IndexOf(':');

			if (idx > 0 && idx < line.Length - 2)
			{
				var header = line[..idx];
				var value = line[(idx + 2)..];
				headers.Add(new string(header), new string(value));
			}
		}

		return headers;
	}

	protected static Version ParseVersion(string version)
	{
		if (version == "HTTP/1.1")
			return HTTP11;
		return Version.Parse(version["HTTP/".Length..]);
	}

	protected void SerializeHeaders(Span<string> output)
	{
		int i = 0;
		foreach (var (header, value) in Headers)
			output[i++] = $"{header}: {value}";
	}

	protected string SerializeVersion() => $"HTTP/{Version.Major}.{Version.Minor}";

	public abstract string Serialize();

	public static string[] ReadRawHeader(Stream stream) => ReadRawHeader(stream.ReadByte);

	public static string[] ReadRawHeader(IReadOnlyStreamDataFlow stream, DataFlowReadingOptions readingOptions = default) =>
		ReadRawHeader(() => stream.ReadByte(readingOptions));

	public static string[] ReadRawHeader(Func<int> syncByteReader)
	{
		var ms = new MemoryStream();

		while (true)
		{
			var readByte = syncByteReader();

			if (readByte == -1)
				break;

			ms.WriteByte((byte)readByte);

			if (ms.Length < 4)
				continue;

			var buffer = ms.GetBuffer();
			var usedBufferSegment = buffer.AsSpan(0, (int)ms.Length);
			var maybeTerminator = BitConverter.ToInt32(usedBufferSegment[^4..]);
			if (maybeTerminator == ASCIITerminator)
				break;
		}

		var raw = Encoding.UTF8.GetString(ms.GetBuffer().AsSpan(0, (int)ms.Length));
		return raw.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
	}
}

public class HttpRequestHeader(
	string method,
	HttpUri uri,
	Version version,
	HttpHeadersCollection headers
	) : HttpMessageHeader(version, headers)
{
	public string Method { get; } = method;

	public HttpUri Uri { get; } = uri;


	public static HttpRequestHeader Parse(string[] rawLines)
	{
		var requestLine = rawLines[0];

		var parts = requestLine.Split(' ', 3);
		if (parts is not [var method, var requestUri, var version])
			throw new FormatException("Invalid request line");

		return new HttpRequestHeader(method.ToUpperInvariant(), HttpUri.Parse(requestUri), ParseVersion(version), ParseHeaders(rawLines.AsSpan(1..)));
	}

	public override string Serialize()
	{
		var lines = new string[Headers.Count + 1 + 1];
		lines[0] = $"{Method} {Uri} {SerializeVersion()}";

		SerializeHeaders(lines.AsSpan(1..^1));

		lines[^1] = "\r\n";
		return string.Join("\r\n", lines);
	}

}

public class HttpResponseHeader(
	int code,
	string reason,
	Version version,
	HttpHeadersCollection headers
	) : HttpMessageHeader(version, headers)
{
	public int Code { get; } = code;

	public string Reason { get; } = reason;


	public static HttpResponseHeader Parse(string[] rawLines)
	{
		var requestLine = rawLines[0];

		var parts = requestLine.Split(' ', 3);
		if (parts is not [var version, var status, var reason])
			throw new FormatException("Invalid request line");

		return new HttpResponseHeader(int.Parse(status), reason, ParseVersion(version), ParseHeaders(rawLines.AsSpan(1..)));
	}

	public override string Serialize()
	{
		var lines = new string[Headers.Count + 1 + 1];
		lines[0] = $"{SerializeVersion()} {Code} {Reason}";

		SerializeHeaders(lines.AsSpan(1..^1));

		lines[^1] = "\r\n";
		return string.Join("\r\n", lines);
	}
}
