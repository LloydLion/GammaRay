namespace GammaRay.Core.Probing;

public abstract record ProbeResult();

public sealed record ProbeSuccessResult(TimeSpan AverageResponseTime) : ProbeResult
{
	public override string ToString() => $"{{Success({AverageResponseTime:ss\\.fff})}}";
}

public sealed record ProbeFailureResult(Exception[] ErrorList) : ProbeResult
{
	public override string ToString() => $"{{Failure({ErrorList.Length} Errors)}}";
}

public sealed record ProbeTimeoutResult() : ProbeResult
{
	public override string ToString() => "{Timeout}";
}

public sealed record ProbeInconsistentResult() : ProbeResult
{
	public override string ToString() => "{Inconsistent}";
}
