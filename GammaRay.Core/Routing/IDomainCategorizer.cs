namespace GammaRay.Core.Routing;

public interface IDomainCategorizer
{
	public DomainCategory DefaultCategory { get; }


	public DomainCategory GetCategoryForDomain(string domainName);
}
