namespace GammaRay.Core.Persistence.Models;

public class RouteModel
{
	public required string SiteDomain { get; init; }

	public required string ProfileName { get; init; }

	public DateTime ValidUntil { get; set; }

	public required string ConfigurationsString { get; set; }
}
