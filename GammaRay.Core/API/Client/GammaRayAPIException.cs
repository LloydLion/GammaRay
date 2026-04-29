namespace GammaRay.Core.API.Client;

public sealed class GammaRayAPIException : Exception
{
	public GammaRayAPIException(string message, Exception? innerException = null) : base(message, innerException) { }
}
