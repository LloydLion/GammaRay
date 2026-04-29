namespace GammaRay.Core.API;

public interface IAPIEventSink
{
	public ValueTask SendEvent(Memory<byte> buffer);
}
