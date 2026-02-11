namespace GammaRay.Core.Inbound;

public delegate ValueTask IncomingRequestCallback(IInbound sender, RequestContext context);

public interface IInbound
{
	public Task Run(CancellationToken stopToken = default);

	public void OnNewRequest(IncomingRequestCallback callback);
}
