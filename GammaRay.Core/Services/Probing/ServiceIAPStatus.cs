namespace GammaRay.Core.Services.Probing;

public readonly record struct ServiceIAPStatus(TimeSpan AverageProbeTime)
{
	public static ServiceIAPStatus Unavailable { get; } = new(TimeSpan.MaxValue);


	public bool IsAvailable => this != Unavailable;

	public bool IsUnavailable => this == Unavailable;


	public ServiceIAPStatus Match<TContext>(
		TContext ctx,
		Func<TContext, ServiceIAPStatus, ServiceIAPStatus> availableStatusSelector
	) => Match(ctx, availableStatusSelector, _ => Unavailable);

	public ServiceIAPStatus Match<TContext>(
		TContext ctx,
		Func<TContext, ServiceIAPStatus, ServiceIAPStatus> availableStatusSelector,
		Func<TContext, ServiceIAPStatus> unavailableStatusSelector
	)
	{
		if (IsUnavailable)
			return unavailableStatusSelector(ctx);
		else
			return availableStatusSelector(ctx, this);
	}

	public override string ToString()
	{
		if (IsUnavailable)
			return "Unavailable";
		return $"{AverageProbeTime.TotalMilliseconds}ms";	
	}
}
