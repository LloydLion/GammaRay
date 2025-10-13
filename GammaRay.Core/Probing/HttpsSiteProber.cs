using System.Diagnostics;
using System.Net;

namespace GammaRay.Core.Probing;

internal class HttpsSiteProber : ISiteProber
{
	private readonly Dictionary<string, ConfiguratedClient> _configurations;


	public HttpsSiteProber(IEnumerable<NetClientConfiguration> configurations)
	{
		_configurations = configurations.ToDictionary(s => s.Name, s =>
		{
			var handler = new HttpClientHandler()
			{
				UseProxy = s.ProxyServer is not null,
				Proxy = s.ProxyServer is not null ? new WebProxy(s.ProxyServer) : null
			};

			var client = new HttpClient(handler, true)
			{
				Timeout = s.Timeout
			};

			return new ConfiguratedClient(s, client);
		});
	}

	public void Dispose()
	{
		foreach (var client in _configurations.Values)
			client.Client.Dispose();
	}

	public async Task<ProbeResult> ProbeAsync(Site target, string configName, CancellationToken token = default)
	{
		var (config, client) = _configurations[configName];
		var uri = new Uri($"https://{target.DomainName}");

		List<HitResult> results = [];

		for (int i = 0; config.MaxRequestCount != -1 && i < config.MaxRequestCount; i++)
		{
			var hitResult = await PerformHitAsync(uri, client, token);
			results.Add(hitResult);

			var result = TryCreateResultIfPossible(results, config.RequestCount);
			if (result is not null)
				return result;

			await Task.Delay(config.RequestInternal, token);
		}

		return new ProbeInconsistentResult();
	}

	public IEnumerable<NetClientConfiguration> ListAvailableConfigurations() => _configurations.Values.Select(s => s.Configuration);

	private async Task<HitResult> PerformHitAsync(Uri target, HttpClient client, CancellationToken token)
	{
		var start = Stopwatch.GetTimestamp();
		try
		{
			var response = await client.GetAsync(target, token);
			var responseTime = Stopwatch.GetElapsedTime(start);

			return new HitResult(HitType.Success, responseTime, null);
		}
		catch (OperationCanceledException)
		{
			return new HitResult(HitType.Timeout, TimeSpan.Zero, null);
		}
		catch (Exception ex)
		{
			return new HitResult(HitType.Timeout, TimeSpan.Zero, ex);
		}
	}

	private ProbeResult? TryCreateResultIfPossible(List<HitResult> results, int minHitCount)
	{
		HitType type = results[0].Type;
		int consistencyStartIndex = 0;
		for (int i = 1; i < results.Count; i++)
		{
			var result = results[i];
			if (result.Type != type)
			{
				consistencyStartIndex = i;
				type = result.Type;
			}
		}

		var count = results.Count - consistencyStartIndex;

		if (count < minHitCount)
			return null;

		var slice = results.Skip(consistencyStartIndex);

		if (type == HitType.Success)
		{
			return new ProbeSuccessResult(slice.Aggregate(TimeSpan.Zero, (acc, s) => acc + s.Time) / count);
		}
		else if (type == HitType.Failure)
		{
			return new ProbeFailureResult(slice.Select(s => s.Exception).ToArray()!);
		}
		else
		{
			return new ProbeTimeoutResult();
		}
	}


	private record ConfiguratedClient(NetClientConfiguration Configuration, HttpClient Client);

	private enum HitType
	{
		Success,
		Failure,
		Timeout
	}

	private record HitResult(HitType Type, TimeSpan Time, Exception? Exception);
}
