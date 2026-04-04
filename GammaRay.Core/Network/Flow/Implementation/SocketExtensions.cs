using GammaRay.Core.Utils;
using System.Net.Sockets;

namespace GammaRay.Core.Network.Flow.Implementation;

public static class SocketExtensions
{
	extension(Socket socket)
	{
		public void SetReceiveTimeout(TimeSpan time)
		{
			if (time == Timeout.InfiniteTimeSpan)
			{
				socket.ReceiveTimeout = -1;
				return;
			}

			var timeoutMs = time.TotalMillisecondsInt;
			if (timeoutMs == 0 && time != TimeSpan.Zero)
				timeoutMs = 1;
			socket.ReceiveTimeout = timeoutMs;
		}

		public async ValueTask ReceiveExactAsync(Memory<byte> destination)
		{
			int offset = 0;
			while (offset < destination.Length)
			{
				var segment = destination[offset..];
				int got = await socket.ReceiveAsync(segment);
				if (got == 0)
					throw new SocketException();
				offset += got;
			}
		}
	}
}
