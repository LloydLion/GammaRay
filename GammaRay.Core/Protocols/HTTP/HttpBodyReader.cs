using GammaRay.Core.Network.Flow;
using System.Net.Sockets;
using System.Text;

namespace GammaRay.Core.Protocols.HTTP;

public static class HttpBodyReader
{
	public static IAsyncEnumerable<ReadOnlyMemory<byte>>? ReadBodyAsync(Memory<byte> buffer, IStreamDataFlow dataFlow, DataFlowReadingOptions readingOptions, HttpHeadersCollection headers)
	{
		var transferEncoding = headers.TryGetSingle("Transfer-Encoding");
		if (transferEncoding == "chunked")
			return ReadChunkedBodyAsync(buffer, dataFlow, readingOptions);

		var contentLengthStr = headers.TryGetSingle("Content-Length");
		if (contentLengthStr is not null && long.TryParse(contentLengthStr, out var contentLength))
			return ReadFixedBodyAsync(buffer, dataFlow, readingOptions, contentLength);

		return null;
	}

	public static async IAsyncEnumerable<ReadOnlyMemory<byte>> ReadChunkedBodyAsync(Memory<byte> buffer, IStreamDataFlow dataFlow, DataFlowReadingOptions readingOptions)
	{
		const int Terminator1 = '\r';
		const int Terminator2 = '\n';

		while (true)
		{
			long chunkLength = 0;
			while (true)
			{
				var lengthByte = dataFlow.ReadByte();
				if (lengthByte == -1)
					throw new EndOfStreamException();
				if (lengthByte == Terminator1)
				{
					lengthByte = dataFlow.ReadByte();
					if (lengthByte == Terminator2)
						break;
					else throw new SocketException();
				}

				chunkLength *= 16;
				if (lengthByte is >= '0' and <= '9')
					chunkLength += lengthByte - '0';
				else if (lengthByte is >= 'A' and <= 'F')
					chunkLength += lengthByte - 'A' + 10;
				else if (lengthByte is >= 'a' and <= 'f')
					chunkLength += lengthByte - 'a' + 10;
				else throw new SocketException();
			}

			if (chunkLength == 0)
				yield break;

			await foreach (var item in ReadFixedBodyAsync(buffer, dataFlow, readingOptions, chunkLength))
				yield return item;


			var terminatorByte = dataFlow.ReadByte();
			if (terminatorByte != Terminator1)
				throw new SocketException();
			terminatorByte = dataFlow.ReadByte();
			if (terminatorByte != Terminator2)
				throw new SocketException();
		}
	}

	public static async IAsyncEnumerable<ReadOnlyMemory<byte>> ReadFixedBodyAsync(Memory<byte> buffer, IStreamDataFlow dataFlow, DataFlowReadingOptions readingOptions, long length)
	{
		var remainingLength = length;
		while (true)
		{
			if (remainingLength <= 0)
				yield break;

			var read = await dataFlow.ReadAsync(buffer[..(int)Math.Min(remainingLength, 1024)], readingOptions);
			if (read == 0)
				throw new EndOfStreamException();

			yield return buffer[..read];

			remainingLength -= read;
		}
	}
}
