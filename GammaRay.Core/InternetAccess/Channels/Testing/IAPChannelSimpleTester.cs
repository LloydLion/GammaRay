using GammaRay.Core.Monitoring;
using GammaRay.Core.Network;
using GammaRay.Core.Network.Flow;
using GammaRay.Core.Protocols.HTTP;
using GammaRay.Core.Protocols.TLS;
using Microsoft.Extensions.Options;
using System.Text;

namespace GammaRay.Core.InternetAccess.Channels.Testing;

public sealed class IAPChannelSimpleTester(
	IChannelDriverRegistry _driverRegistry,
	IOptions<IAPChannelSimpleTester.Options> options
) : IIAPChannelSimpleTester
{
	private readonly HttpUri _uri = HttpUri.Parse(options.Value.TestUri);


	public async ValueTask<bool> PerformTestAsync(IAPChannel channel, CancellationToken cancellation, MonitoringContext monitoring)
	{
		var endPoint = new WebEndPoint(_uri.EndPoint!.Value, TransportType.StreamBased);
		var openingResult = await _driverRegistry
			.ProvideDriver(channel.DriverName)
			.TryOpenChannelAsync(channel, endPoint);
		if (openingResult.Type != ChannelOpeningResult.ResultType.Success)
			return false;

		await using (openingResult)
		{
			var dataFlow = (IStreamDataFlow)openingResult.OpenChannel.GetFlow();

			if (_uri.Schema == "https")
			{
				var tlsDataFlow = new TLSDataFlowWrapper(dataFlow);
				await tlsDataFlow.BeginConnectionAsync(endPoint.Host.Domain, Timeout.InfiniteTimeSpan, cancellation);
				dataFlow = tlsDataFlow;
			}

			var headers = new HttpHeadersCollection
			{
				{ "Host",               endPoint.Host.Domain },
				{ "User-Agent",         "GammaRay/1.0.0"     },
				{ "Accept",             "*/*"                },
				{ "Accept-Encoding",    "gzip, deflate, br"  },
				{ "Connection",         "close"              }
			};

			foreach (var header in options.Value.Headers)
			{
				headers.RemoveAll(header.Key);
				headers.Add(header.Key, header.Value);
			}

			var message = new HttpRequestHeader("GET", _uri, HttpMessageHeader.HTTP11, headers);
			var stringMessage = message.Serialize();
			var binMessage = Encoding.ASCII.GetBytes(stringMessage);

			await dataFlow.WriteAsync(binMessage, new DataFlowWritingOptions() { Timeout = Timeout.InfiniteTimeSpan }, cancellation);

			var rawResponse = await HttpMessageHeader.ReadRawHeaderAsync(dataFlow, new DataFlowReadingOptions());
			var response = HttpResponseHeader.Parse(rawResponse);
			if (response.Code != 204)
				return false;
		}

		return true;
	}


	public class Options
	{
		public string TestUri { get; init; } = "http://www.gstatic.com/generate_204";

		public Dictionary<string, string> Headers { get; init; } = [];
	}
}
