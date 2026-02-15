using GammaRay.Core.Network;

namespace GammaRay.Core.Routing;

public interface IEndpointCategorizer
{
	public EndpointCategory Categorize(WebEndPoint endPoint);
}
