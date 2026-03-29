namespace GammaRay.Core.Utils;

public static class TimeExtensions
{
	extension(TimeSpan time)
	{
		public int TotalMillisecondsInt => (int)time.TotalMilliseconds;
	}
}
