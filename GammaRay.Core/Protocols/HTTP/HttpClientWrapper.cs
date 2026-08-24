using System.Diagnostics;
using GammaRay.Core.Network;
using GammaRay.Core.Network.Flow;

namespace GammaRay.Core.Protocols.HTTP;

public class HttpClientWrapper : IDisposable
{
	private readonly ConnectCallbackSource _connectCallbackSource;
	private readonly HttpClient _client;


	public HttpClientWrapper()
	{
		_connectCallbackSource = new();
		_client = new HttpClient(new SocketsHttpHandler()
		{
			ConnectCallback = _connectCallbackSource.Callback,
			AllowAutoRedirect = false,
			EnableMultipleHttp2Connections = false,
			EnableMultipleHttp3Connections = false,
			UseCookies = false,
			UseProxy = false,
			PooledConnectionLifetime = TimeSpan.Zero,
			PooledConnectionIdleTimeout = TimeSpan.Zero,
		}, disposeHandler: true);
	}
	
	
	public ConfigurationHandle Configure(IStreamDataFlow dataFlow, WebEndPoint? targetEndPoint)
	{
		_connectCallbackSource.FlowWrapper.ReInit(dataFlow);
#if DEBUG
		_connectCallbackSource.TargetEndPoint = targetEndPoint;
#endif
		return new ConfigurationHandle(this);
	}

	public HttpClient AccessClient()
	{
		return _client;
	}
	
	public void SetWritingOptions(DataFlowWritingOptions options) => _connectCallbackSource.FlowWrapper.WritingOptions = options;
	
	public void SetReadingOptions(DataFlowReadingOptions options) => _connectCallbackSource.FlowWrapper.ReadingOptions = options;

	public void Dispose()
	{
		_client.Dispose();
	}
	
	
	private class ConnectCallbackSource
	{
		private DataFlowStreamWrapper _wrapper = new();
#if DEBUG
		public WebEndPoint? TargetEndPoint = null;
#endif


		public ValueTask<Stream> Callback(SocketsHttpConnectionContext ctx, CancellationToken _)
		{
			if (_wrapper is null)
				throw new InvalidOperationException("Set data flow first");
#if DEBUG
			if (TargetEndPoint is not null)
			{
				Debug.Assert(ctx.DnsEndPoint.Port == TargetEndPoint.Value.Port);
				Debug.Assert(ctx.DnsEndPoint.Host == TargetEndPoint.Value.Host);
			}
#endif
			return ValueTask.FromResult<Stream>(_wrapper);
		}


		public DataFlowStreamWrapper FlowWrapper => _wrapper;
	}
	
	public readonly struct ConfigurationHandle(HttpClientWrapper _owner) : IDisposable
	{
		public void Dispose()
		{
			_owner._connectCallbackSource.FlowWrapper.ReInit(newDataFlow: null);
#if DEBUG
			_owner._connectCallbackSource.TargetEndPoint = null;
#endif
		}
	}
}
