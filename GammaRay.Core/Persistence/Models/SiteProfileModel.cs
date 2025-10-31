namespace GammaRay.Core.Persistence.Models;

public class SiteProfileModel
{
	public required string SiteDomain { get; set; }

	public required string ProfileName { get; set; }

	public DateTime ValidUntil { get; set; }

	public required string ConfigurationName { get; set; }
}
