using System.Text.Json.Serialization;

namespace GammaRay.Core.Settings.Entities;

public sealed class ApplicationSettings
{
	[JsonPropertyName("inbounds")]
	public required Dictionary<string, InboundSettings> Inbounds { get; init; }

	[JsonPropertyName("configurations")]
	public required Dictionary<string, ConfigurationSettings> Configurations { get; init; }

	[JsonPropertyName("priorityQueues")]
	public required Dictionary<string, string[]> PriorityQueues { get; init; }

	[JsonPropertyName("networkProfiles")]
	public required Dictionary<string, NetworkProfileSettings> NetworkProfiles { get; init; }

	[JsonPropertyName("categories")]
	public required Dictionary<string, DomainCategorySettings> Categories { get; init; }

	[JsonPropertyName("routeGrid")]
	public required RouteGridSettings RouteGrid { get; init; }
}
