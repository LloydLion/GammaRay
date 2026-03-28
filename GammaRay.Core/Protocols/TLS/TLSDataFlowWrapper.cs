using GammaRay.Core.Network.Flow;
using System.Net.Security;

namespace GammaRay.Core.Protocols.TLS;

public sealed class TLSDataFlowWrapper : IStreamDataFlow
{
	private readonly SslStream _sslStream;


	public TLSDataFlowWrapper(IStreamDataFlow inner)
	{
		var streamWrapper = new DataFlowStreamWrapper(inner);
		_sslStream = new SslStream(streamWrapper);
	}


	public async ValueTask BeginConnectionAsync(string hostName, TimeSpan timeout)
	{
		_sslStream.ReadTimeout = _sslStream.WriteTimeout = (int)timeout.TotalMilliseconds;
		await _sslStream.AuthenticateAsClientAsync(hostName);
	}

	public int Read(Span<byte> buffer, DataFlowReadingOptions readingOptions)
	{
		DataFlowReadingOptions.InitializeWithDefaultsIfNeed(ref readingOptions);
		_sslStream.ReadTimeout = (int)readingOptions.Timeout.TotalMilliseconds;
		return _sslStream.Read(buffer);
	}

	public ValueTask<int> ReadAsync(Memory<byte> buffer, DataFlowReadingOptions readingOptions, CancellationToken cancellationToken = default)
	{
		DataFlowReadingOptions.InitializeWithDefaultsIfNeed(ref readingOptions);
		_sslStream.ReadTimeout = (int)readingOptions.Timeout.TotalMilliseconds;
		return _sslStream.ReadAsync(buffer, cancellationToken);
	}

	public async ValueTask<int> WriteAsync(ReadOnlyMemory<byte> buffer, DataFlowWritingOptions writingOptions, CancellationToken cancellationToken = default)
	{
		DataFlowWritingOptions.InitializeWithDefaultsIfNeed(ref writingOptions);
		_sslStream.WriteTimeout = (int)writingOptions.Timeout.TotalMilliseconds;
		await _sslStream.WriteAsync(buffer, cancellationToken);
		return buffer.Length;
	}
}
