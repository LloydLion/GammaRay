namespace GammaRay.Core.InternetAccess;

public readonly struct InternetAccessPointBlob(IReadOnlyCollection<InternetAccessPoint> internetAccessPoints)
{
	public static readonly InternetAccessPointBlob Empty = new([]);

	public IReadOnlyCollection<InternetAccessPoint> Points { get; } = internetAccessPoints;
}
