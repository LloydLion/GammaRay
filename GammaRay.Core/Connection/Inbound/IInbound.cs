namespace GammaRay.Core.Connection.Inbound;

public interface IInbound
{
	public Task Run(CancellationToken stopToken = default);

	public void SetMaster(IMasterServerInboundAgent master);
}
