using System.Text.Json.Serialization;

namespace GammaRay.Core.Settings.Entities;

public sealed class InboundSettings
{
	[JsonPropertyName("protocol")]
	public required string Protocol { get; init; }

	[JsonPropertyName("endpoint")]
	public required string EndPoint { get; init; }
}
