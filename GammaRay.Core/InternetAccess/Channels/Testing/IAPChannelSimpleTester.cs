using GammaRay.Core.Network;
using GammaRay.Core.Network.Flow;
using GammaRay.Core.Protocols.HTTP;
using GammaRay.Core.Protocols.TLS;
using Microsoft.Extensions.Options;
using System.Text;
using static GammaRay.Core.InternetAccess.Channels.Testing.IAPChannelSimpleTestResult;

namespace GammaRay.Core.InternetAccess.Channels.Testing;

public sealed class IAPChannelSimpleTester(
	IChannelDriverRegistry _driverRegistry,
	IOptions<IAPChannelSimpleTester.Options> options,
	TimeProvider _time
) : IIAPChannelSimpleTester
{
	private readonly HttpUri _uri = HttpUri.Parse(options.Value.TestUri);


	public async ValueTask<IAPChannelSimpleTestResult> PerformTestAsync(IAPChannel channel, CancellationToken cancellationToken)
	{
		var start = _time.GetTimestamp();

		try
		{
			var endPoint = new WebEndPoint(_uri.EndPoint!.Value, TransportType.StreamBased);
			var openChannel = await _driverRegistry
				.ProvideDriver(channel.DriverName)
				.TryOpenChannelAsync(channel, endPoint);
			if (openChannel is null)
				return new(_time.GetElapsedTime(start), TestStatus.SocketFailure);

			await using (openChannel)
			{
				var dataFlow = (IStreamDataFlow)openChannel.GetFlow();

				if (_uri.Schema == "https")
				{
					var tlsDataFlow = new TLSDataFlowWrapper(dataFlow);
					await tlsDataFlow.BeginConnectionAsync(endPoint.Host.Domain, TimeSpan.FromSeconds(5));
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

				await dataFlow.WriteAsync(binMessage, new DataFlowWritingOptions(), cancellationToken);

				var rawResponse = await HttpMessageHeader.ReadRawHeaderAsync(dataFlow, new DataFlowReadingOptions());
				var response = HttpResponseHeader.Parse(rawResponse);
				if (response.Code != 204)
					return new(_time.GetElapsedTime(start), TestStatus.UnexceptedData);
			}

			return new(_time.GetElapsedTime(start), TestStatus.Success);
		}
		catch (Exception)
		{
			return new(_time.GetElapsedTime(start), TestStatus.SocketFailure);
		}
	}


	public class Options
	{
		public string TestUri { get; init; } = "http://www.gstatic.com/generate_204";

		public Dictionary<string, string> Headers { get; init; } = [];
	}
}
