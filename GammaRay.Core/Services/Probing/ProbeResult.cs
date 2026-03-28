namespace GammaRay.Core.Services.Probing;

public class ProbeResult(ProbeResult.ProbeStatus status, TimeSpan probeDuration)
{
	public ProbeStatus Status { get; } = status;

	public TimeSpan ProbeDuration { get; } = probeDuration;


	public override string ToString()
	{
		return $"ProbeResult ({Status} in {ProbeDuration.Milliseconds}ms)";
	}


	public enum ProbeStatus
	{
		Success,
		Timeout,
		SocketFailure,
		UnexceptedData
	}
}
