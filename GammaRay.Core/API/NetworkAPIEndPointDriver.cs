using GammaRay.Core.Utils;
using System.Net;
using System.Net.Sockets;

namespace GammaRay.Core.API;

[RecommendedDriverName("network")]
public sealed class NetworkAPIEndPointDriver : IAPIEndPointDriver
{
	public IAPIListeningEndPoint CreateListening(string configurationString)
	{
		var ipEndPoint = IPEndPoint.Parse(configurationString);
		return new EndPoint(ipEndPoint);
	}

	public async ValueTask<IAPIClientConnection> ConnectAsClientAsync(string configurationString)
	{
		var ipEndPoint = IPEndPoint.Parse(configurationString);
		var socket = new Socket(ipEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
		await socket.ConnectAsync(ipEndPoint);
		return new APIClientConnection(socket);
	}


	private class APIClientConnection(Socket _socket) : IAPIClientConnection
	{
		public string Name { get; } = _socket.RemoteEndPoint?.ToString() ?? "";

		public Stream Stream { get; } = new NetworkStream(_socket, ownsSocket: false);

		public bool IsOpen => _socket.Connected;


		public async ValueTask CloseAsync()
		{
			try { await _socket.DisconnectAsync(false); }
			catch (Exception) { }
			_socket.Dispose();
			Stream.Dispose();
		}

		public ValueTask DisposeAsync() => CloseAsync();
	}

	private class EndPoint(IPEndPoint _endPoint) : IAPIListeningEndPoint
	{
		private APIConnectionHandler? _handler;


		public async Task Run(CancellationToken stopToken = default)
		{
			if (_handler is null)
				throw new InvalidOperationException("Set connection handler first");

			var aliveConnections = new Dictionary<Guid, Task>();

			using var listener = new Socket(_endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
			listener.Bind(_endPoint);
			listener.Listen();

			while (stopToken.IsCancellationRequested == false)
			{
				Socket? client = null;
				bool success = false;
				try
				{
					client = await listener.AcceptAsync(stopToken);
					var stream = new NetworkStream(client, ownsSocket: false);
					var id = Guid.NewGuid();

					var connection = new APIConnection(client.RemoteEndPoint?.ToString() ?? "", this, stream, id);

					var task = _handler(connection, stopToken).ContinueWith(async (task) =>
					{
						aliveConnections.Remove(id);

						try { await client.DisconnectAsync(false); }
						catch (Exception) { }

						client.Dispose();
					}, stopToken, TaskContinuationOptions.None, TaskScheduler.FromCurrentSynchronizationContext());

					aliveConnections.Add(id, task);

				}
				catch (OperationCanceledException)
				{
					if (success == false)
						client?.Dispose();
					break;
				}
			}

			try
			{
				await Task.WhenAll(aliveConnections.Values);
			}
			catch (Exception) { }
		}

		public void SetConnectionHandler(APIConnectionHandler handler)
		{
			_handler = handler;
		}
	}
}
