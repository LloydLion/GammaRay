namespace GammaRay.Core.InternetAccess;

public struct InternetAccessPointChain(IReadOnlyList<InternetAccessPointBlob> blobs)
{
	public static readonly InternetAccessPointChain Empty = new([]);

	public IReadOnlyList<InternetAccessPointBlob> Blobs { get; } = blobs;

	public IReadOnlyList<InternetAccessPoint> PlainListOfPoints
	{
		get { field ??= Blobs.SelectMany(s => s.Points).ToArray(); return field; }
	}


	public readonly InternetAccessPointChain Reverse()
	{
		return new InternetAccessPointChain(Blobs.Reverse().ToArray());
	}

	public InternetAccessPointChain Extend(InternetAccessPointProvider internetAccessPointProvider)
	{
		var IAPs = internetAccessPointProvider.PlainInternetAccessPoints;
		var lastBlob = new InternetAccessPointBlob(IAPs.Except(PlainListOfPoints).ToArray());
		var extendedChain = new InternetAccessPointChain([.. Blobs, lastBlob]);
		return extendedChain;
	}
}
