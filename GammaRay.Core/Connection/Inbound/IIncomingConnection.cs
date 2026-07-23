using GammaRay.Core.Network.Flow;

namespace GammaRay.Core.Connection.Inbound;

public interface IIncomingConnection
{
	public void ResetConnection();

	public IDataFlow GetFlow();
}
