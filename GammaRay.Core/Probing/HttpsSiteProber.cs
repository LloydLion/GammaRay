using Serilog;
using System.Diagnostics;
using System.Net;

namespace GammaRay.Core.Probing;

public class HttpsSiteProber : ISiteProber
{
	private static readonly ILogger _logger = Log.ForContext<HttpsSiteProber>();

	private readonly Dictionary<string, ConfiguratedClient> _configurations;


	public HttpsSiteProber(IEnumerable<NetClientConfiguration> configurations)
	{
		_configurations = configurations.ToDictionary(s => s.Name, s =>
		{
			var handler = new HttpClientHandler()
			{
				UseProxy = s.ProxyServer is not null,
				Proxy = s.ProxyServer is not null ? new WebProxy(s.ProxyServer.Address.ToString(), s.ProxyServer.Port) : null
			};

			var client = new HttpClient(handler, true)
			{
				Timeout = s.Timeout
			};

			return new ConfiguratedClient(s, client);
		});

		_logger.Information("New HttpSiteProber. Configurations={Configurations}", configurations.Select(s => s.Name));
	}

	public void Dispose()
	{
		foreach (var client in _configurations.Values)
			client.Client.Dispose();
	}

	public async Task<ProbeResult> ProbeAsync(Site target, string configName, CancellationToken token = default)
	{
		var probeId = Convert.ToHexString(BitConverter.GetBytes(Random.Shared.Next()));
		var logger = _logger.ForContext("ProbeId", probeId, destructureObjects: false);
		logger.Debug("Started probing for {Site} using {Configuration}", target, configName);

		try
		{
			var (config, client) = _configurations[configName];
			var uri = new Uri($"https://{target.DomainName}");

			List<HitResult> results = [];

			for (int i = 0; config.MaxRequestCount != -1 && i < config.MaxRequestCount; i++)
			{
				var hitResult = await PerformHitAsync(logger, uri, client, token);
				results.Add(hitResult);

				var result = TryCreateResultIfPossible(results, config.RequestCount);
				if (result is not null)
				{
					logger.Debug("Probing finished with result: {Result}", result);
					return result;
				}

				await Task.Delay(config.RequestInternal, token);
			}

			logger.Warning("Reached maximum number of request, got inconsistent result");
			return new ProbeInconsistentResult();
		}
		catch (Exception ex)
		{
			if (ex is not TaskCanceledException)
				logger.Error(ex, "Error while probing[{ProbeId}] site {Site} using {Configuration}", probeId, target, configName);
			throw;
		}
	}

	public IEnumerable<NetClientConfiguration> ListAvailableConfigurations() => _configurations.Values.Select(s => s.Configuration);

	private async Task<HitResult> PerformHitAsync(ILogger logger, Uri target, HttpClient client, CancellationToken token)
	{
		var start = Stopwatch.GetTimestamp();
		try
		{
			var response = await client.GetAsync(target, token);
			var responseTime = Stopwatch.GetElapsedTime(start);

			logger.Verbose("During probing hit succeed with response time = {ResponseTime}", responseTime);
			return new HitResult(HitType.Success, responseTime, null);
		}
		catch (OperationCanceledException)
		{
			logger.Verbose("During probing hit timed out");
			return new HitResult(HitType.Timeout, TimeSpan.Zero, null);
		}
		catch (Exception ex)
		{
			logger.Verbose(ex, "During probing hit failed");
			return new HitResult(HitType.Failure, TimeSpan.Zero, ex);
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
