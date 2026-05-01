using GammaRay.Core.Network.Flow;
using System.Buffers;
using System.Net.Sockets;
using System.Text;

namespace GammaRay.Core.Protocols.HTTP;

public abstract class HttpMessageHeader(Version version, HttpHeadersCollection headers)
{
	public const string Terminator = "\r\n\r\n";
	public static readonly byte[] BinaryTerminator = Encoding.UTF8.GetBytes(Terminator);
	public static readonly Version HTTP11 = new(1, 1);
	private static readonly ArrayPool<byte> ReadArrayPool = ArrayPool<byte>.Create();


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

	public static ValueTask<string[]> ReadRawHeaderAsync(Socket socket) => ReadRawHeaderAsync((output, peekOnly, socket) =>
	{
		return socket.ReceiveAsync(output, peekOnly ? SocketFlags.Peek : SocketFlags.None);
	}, socket);

	public static ValueTask<string[]> ReadRawHeaderAsync(
		IReadOnlyStreamDataFlow dataFlow,
		DataFlowReadingOptions readingOptions = default
	) => ReadRawHeaderAsync((output, peekOnly, ctx) =>
	{
		var options = ctx.readingOptions with { PeekOnly = peekOnly };
		return ctx.dataFlow.ReadAsync(output, options);
	}, (dataFlow, readingOptions));

	public static async ValueTask<string[]> ReadRawHeaderAsync<TContext>(
		// output, peekOnly, context -> readBytes
		Func<Memory<byte>, bool, TContext, ValueTask<int>> reader, TContext context
	)
	{
		List<byte[]>? filledBuffers = null;
		byte[]? lastBuffer = null;
		try
		{
			int readInLastBuffer = 0;
			int matchedTerminatorBytes = 0;

			while (true)
			{
				if (filledBuffers is { Count: 64 })
					throw new Exception("Too big header");

				byte[] currentReadBuffer = ReadArrayPool.Rent(1024 * 16);

				if (lastBuffer is not null)
				{
					if (filledBuffers is null) filledBuffers = new();
					filledBuffers.Add(lastBuffer);
				}
				lastBuffer = currentReadBuffer;

				int usedInBuffer = 0;

				while (usedInBuffer < currentReadBuffer.Length)
				{
					var read = await reader(currentReadBuffer.AsMemory(usedInBuffer..), true, context);
					if (read == 0)
						throw new EndOfStreamException();

					for (int i = usedInBuffer; i < usedInBuffer + read; i++)
						if (currentReadBuffer[i] == BinaryTerminator[matchedTerminatorBytes])
						{
							matchedTerminatorBytes++;
							if (matchedTerminatorBytes == BinaryTerminator.Length)
							{
								readInLastBuffer = i + 1;
								await reader(currentReadBuffer.AsMemory(usedInBuffer..readInLastBuffer), false, context);
								goto exit;
							}
							// END!
						}
						else matchedTerminatorBytes = 0;

					await reader(currentReadBuffer.AsMemory(usedInBuffer..(usedInBuffer + read)), false, context);

					usedInBuffer += read;
				}
			}

		exit:

			int charCount = Encoding.UTF8.GetCharCount(lastBuffer.AsSpan(..readInLastBuffer));
			if (filledBuffers is not null)
				foreach (var subBuffer in filledBuffers)
					charCount += Encoding.UTF8.GetCharCount(subBuffer);

			var output = string.Create(charCount, 0, (output, _) =>
			{
				int wroteChars = 0;
				if (filledBuffers is not null)
					foreach (var subBuffer in filledBuffers)
						wroteChars += Encoding.UTF8.GetChars(subBuffer, output[wroteChars..]);
			
				Encoding.UTF8.GetChars(lastBuffer.AsSpan(..readInLastBuffer), output[wroteChars..]);
			});

			return output.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
		}
		finally
		{
			if (lastBuffer is not null)
				ReadArrayPool.Return(lastBuffer);
			if (filledBuffers is not null)
				foreach (var subBuffer in filledBuffers)
					ReadArrayPool.Return(subBuffer);
		}
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
