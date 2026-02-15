namespace GammaRay.Core.InternetAccess;

public interface IInternetAccessPointProvider
{
	public IReadOnlyCollection<InternetAccessPoint> GetAll();
}
