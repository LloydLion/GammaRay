namespace GammaRay.Core;

public readonly record struct Site(string DomainName)
{
	public override string ToString()
	{
		return DomainName;
	}
}
