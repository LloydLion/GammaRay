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
	}
}
