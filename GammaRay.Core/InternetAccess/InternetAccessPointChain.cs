namespace GammaRay.Core.InternetAccess;

public sealed class InternetAccessPointChain(IReadOnlyList<InternetAccessPointBlob> blobs)
{
	public static readonly InternetAccessPointChain Empty = new([]);

	public IReadOnlyList<InternetAccessPointBlob> Blobs { get; } = blobs;


	public InternetAccessPointChain Reverse()
	{
		return new InternetAccessPointChain(Blobs.Reverse().ToArray());
	}

	public InternetAccessPointChain Extend(IInternetAccessPointProvider internetAccessPointProvider)
	{
		var IAPs = internetAccessPointProvider.GetAll();
		var lastBlob = new InternetAccessPointBlob(IAPs.Where(s => Blobs.Any(blob => blob.Points.Contains(s)) == false).ToArray());
		var extendedChain = new InternetAccessPointChain([.. Blobs, lastBlob]);
		return extendedChain;
	}
}
