namespace GammaRay.Core.InternetAccess;

public sealed class InternetAccessPointBlob(IReadOnlyCollection<InternetAccessPoint> internetAccessPoints)
{
	public static readonly InternetAccessPointBlob Empty = new([]);

	public IReadOnlyCollection<InternetAccessPoint> Points { get; } = internetAccessPoints;
}
