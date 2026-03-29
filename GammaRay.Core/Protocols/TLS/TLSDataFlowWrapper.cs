using GammaRay.Core.Network.Flow;
using System.Net.Security;

namespace GammaRay.Core.Protocols.TLS;

public sealed class TLSDataFlowWrapper : IStreamDataFlow
{
	private readonly DataFlowStreamWrapper _streamWrapper;
	private readonly SslStream _sslStream;


	public TLSDataFlowWrapper(IStreamDataFlow inner)
	{
		_streamWrapper = new DataFlowStreamWrapper(inner);
		_sslStream = new SslStream(_streamWrapper);
	}


	public async ValueTask BeginConnectionAsync(string hostName, TimeSpan timeout)
	{
		_streamWrapper.ReadingOptions = new() { Timeout = timeout };
		_streamWrapper.WritingOptions = new() { Timeout = timeout };
		await _sslStream.AuthenticateAsClientAsync(hostName);
	}

	public int Read(Span<byte> buffer, DataFlowReadingOptions readingOptions)
	{
		DataFlowReadingOptions.InitializeWithDefaultsIfNeed(ref readingOptions);
		_streamWrapper.ReadingOptions = readingOptions;
		return _sslStream.Read(buffer);
	}

	public ValueTask<int> ReadAsync(Memory<byte> buffer, DataFlowReadingOptions readingOptions, CancellationToken cancellationToken = default)
	{
		DataFlowReadingOptions.InitializeWithDefaultsIfNeed(ref readingOptions);
		_streamWrapper.ReadingOptions = readingOptions;
		return _sslStream.ReadAsync(buffer, cancellationToken);
	}

	public async ValueTask<int> WriteAsync(ReadOnlyMemory<byte> buffer, DataFlowWritingOptions writingOptions, CancellationToken cancellationToken = default)
	{
		DataFlowWritingOptions.InitializeWithDefaultsIfNeed(ref writingOptions);
		_streamWrapper.WritingOptions = writingOptions;
		await _sslStream.WriteAsync(buffer, cancellationToken);
		return buffer.Length;
	}
}
