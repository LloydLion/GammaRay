namespace GammaRay.Core.Routing.NetworkProfiles;

public sealed record NetworkProfile(string Name)
{
	public override string ToString()
	{
		return Name;
	}
}
