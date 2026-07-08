namespace GammaRay.Core.Network.Profiles;

public sealed record NetworkProfile(string Name)
{
	public override string ToString()
	{
		return Name;
	}
}
