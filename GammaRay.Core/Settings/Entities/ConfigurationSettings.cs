using System.Text.Json.Serialization;

namespace GammaRay.Core.Settings.Entities;

public sealed class ConfigurationSettings
{
	[JsonPropertyName("proxyServer")]
	public string? ProxyServer { get; init; }

	[JsonPropertyName("timeoutMs")]
	public int TimeoutMs { get; init; } = 10_000;

	[JsonPropertyName("requestInternalMs")]
	public int RequestInternalMs { get; init; } = 3_000;

	[JsonPropertyName("requestCount")]
	public int RequestCount { get; init; } = 3;

	[JsonPropertyName("maxRequestCount")]
	public int MaxRequestCount { get; init; } = 5;
}
