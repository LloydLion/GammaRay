namespace GammaRay.Core.Persistence.Models;

public sealed class NetworkModel
{
	public required string Identity { get; init; }

	public string? UsedProfile { get; set; }
}
