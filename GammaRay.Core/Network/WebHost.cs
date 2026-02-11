namespace GammaRay.Core.Network;

public readonly record struct WebHost(string Domain)
{
	public override string ToString()
	{
		return Domain;
	}
}
