using GammaRay.Core.Network.Flow;

namespace GammaRay.Core.InternetAccess.Channels;

public interface IOpenChannel : IAsyncDisposable
{
	public IDataFlow GetFlow();
}
