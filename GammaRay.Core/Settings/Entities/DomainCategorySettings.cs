using System.Text.Json.Serialization;

namespace GammaRay.Core.Settings.Entities;

public sealed class DomainCategorySettings
{
	[JsonPropertyName("list")]
	public string? List { get; init; }

	[JsonPropertyName("isDefault")]
	public bool IsDefault { get; init; } = false;
}
