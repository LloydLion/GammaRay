namespace GammaRay.Core.Connection;

public enum ClientConnectionState
{
	Blank = 0,
	Requested = 1,
	Routed = 2,
	Established = 3,

	ClosedByClient = 4,
	ClosedByRemote = 5,
	Rerouted = 6
}

public static class ClientConnectionStateExtensions
{
	extension(ClientConnectionState state)
	{
		public bool IsClosed => state is ClientConnectionState.ClosedByClient
			or ClientConnectionState.ClosedByRemote
			or ClientConnectionState.Rerouted;
	}
}
