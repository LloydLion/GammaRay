namespace GammaRay.Core.API.Client;

public interface IAPIEventListener
{
	public bool HandleEvent(IGammaRayAPIClient sender, ReadOnlySpan<byte> eventData);
}
