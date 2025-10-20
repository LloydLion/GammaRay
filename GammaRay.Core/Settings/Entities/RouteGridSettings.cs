using System.Text.Json.Serialization;

namespace GammaRay.Core.Settings.Entities;

public sealed class RouteGridSettings
{
	[JsonPropertyName("profilesOrder")]
	public required string[] ProfilesOrder { get; init; }

	[JsonPropertyName("grid")]
	public required Dictionary<string, string[]> Grid { get; init; }
}
