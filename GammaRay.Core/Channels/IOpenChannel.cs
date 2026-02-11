using GammaRay.Core.Network.Flow;

namespace GammaRay.Core.Channels;

public interface IOpenChannel : IAsyncDisposable
{
	public IDataFlow GetFlow();
}
