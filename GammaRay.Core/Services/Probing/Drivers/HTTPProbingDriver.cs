using GammaRay.Core.Network;
using GammaRay.Core.Network.Flow;
using GammaRay.Core.Protocols.HTTP;
using GammaRay.Core.Protocols.TLS;
using GammaRay.Core.Utils;
using System.Reflection;
using System.Text;

namespace GammaRay.Core.Services.Probing.Drivers;

[RecommendedDriverName("HTTP")]
public sealed class HTTPProbingDriver(TimeProvider _time) : IProbeDriver
{
	public async Task<ProbeResult> ProbeAsync(
		IDataFlow targetOutcomingFlow,
		WebEndPoint endPoint,
		IReadOnlyDictionary<string, string> parameters,
		CommonProbeDriverOptions options
	)
	{
		if (targetOutcomingFlow is not IStreamDataFlow streamDataFlow)
			throw new ArgumentException("Only stream based data flows supported", nameof(targetOutcomingFlow));

		var strongParameters = ParseParameters(parameters);

		var readingOptions = new DataFlowReadingOptions() { Timeout = options.RTTTimeout };
		var writingOptions = new DataFlowWritingOptions() { Timeout = options.RTTTimeout };

		var startTimestamp = _time.GetTimestamp();

		try
		{
			streamDataFlow = await ApplyTLSOptionsAsync(streamDataFlow, strongParameters, endPoint.Host, options.RTTTimeout);

			var uri = new HttpUri(null, null, strongParameters.Path, null);

			var headers = new HttpHeadersCollection
			{
				{ "Host",				endPoint.Host.Domain		},
				{ "User-Agent",			strongParameters.UserAgent	},
				{ "Accept",				"*/*"						},
				{ "Accept-Encoding",	"gzip, deflate, br"			},
				{ "Connection",			"close"						}
			};

			var message = new HttpRequestHeader(strongParameters.Method, uri, HttpMessageHeader.HTTP11, headers);
			var stringMessage = message.Serialize();
			var binMessage = Encoding.ASCII.GetBytes(stringMessage);

			await streamDataFlow.WriteAsync(binMessage, writingOptions);

			var rawResponse = HttpMessageHeader.ReadRawHeader(streamDataFlow, readingOptions);
			var response = HttpResponseHeader.Parse(rawResponse);
			var isStatusCodeSatisfied = strongParameters.RequireNonErrorStatusCode is false || (response.Code / 100) is 1 or 2 or 3;

			if (isStatusCodeSatisfied == false)
				return new ProbeResult(ProbeResult.ProbeStatus.UnexceptedData, _time.GetElapsedTime(startTimestamp));

			var buffer = new byte[1024];
			var data = HttpBodyReader.ReadBodyAsync(buffer, streamDataFlow, readingOptions with { Timeout = options.ContinuousDataTimeout }, response.Headers);
			if (data is not null)
				await foreach (var item in data) { }

			return new ProbeResult(ProbeResult.ProbeStatus.Success, _time.GetElapsedTime(startTimestamp));
		}
		catch (TimeoutException)
		{
			return new ProbeResult(ProbeResult.ProbeStatus.Timeout, _time.GetElapsedTime(startTimestamp));
		}
		catch (Exception)
		{
			return new ProbeResult(ProbeResult.ProbeStatus.SocketFailure, _time.GetElapsedTime(startTimestamp));
		}
	}

	private async static ValueTask<IStreamDataFlow> ApplyTLSOptionsAsync(IStreamDataFlow originalDataFlow, StrongParameters parameters, WebHost targetHost, TimeSpan timeout)
	{
		var dataFlow = originalDataFlow;
		if (parameters.UseTLS)
		{
			var tlsFlow = new TLSDataFlowWrapper(originalDataFlow);
			await tlsFlow.BeginConnectionAsync(targetHost.Domain, timeout);
			dataFlow = tlsFlow;
		}

		return dataFlow; 
	}

	private static StrongParameters ParseParameters(IReadOnlyDictionary<string, string> parameters)
	{
		var useTLS = bool.Parse(parameters.GetValueOrDefault("useTLS") ?? "true");
		var path = parameters.GetValueOrDefault("path") ?? "";
		var method = parameters.GetValueOrDefault("method") ?? "GET";
		var userAgent = parameters.GetValueOrDefault("userAgent") ??
			"GammaRay/" + (Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0");
		var requireNonErrorStatusCode = bool.Parse(parameters.GetValueOrDefault("requireNonErrorStatusCode") ?? "false");

		return new StrongParameters(useTLS, path, method, userAgent, requireNonErrorStatusCode);
	}

	private record StrongParameters(bool UseTLS, string Path, string Method, string UserAgent, bool RequireNonErrorStatusCode);
}
