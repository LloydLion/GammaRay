namespace GammaRay.Core.Utils;

public static class MathExtensions
{
	extension<TComparable>(TComparable)
		where TComparable : IComparable<TComparable>
	{
		public static bool operator<(TComparable a, TComparable b) => a.CompareTo(b) < 0;
		public static bool operator >(TComparable a, TComparable b) => a.CompareTo(b) > 0;
		public static bool operator >=(TComparable a, TComparable b) => a.CompareTo(b) >= 0;
		public static bool operator <=(TComparable a, TComparable b) => a.CompareTo(b) <= 0;
		public static bool operator ==(TComparable a, TComparable b) => a.CompareTo(b) == 0;
		public static bool operator !=(TComparable a, TComparable b) => a.CompareTo(b) != 0;
	}

	extension(Math)
	{
		public static TComparable Clamp<TComparable>(TComparable value, TComparable min, TComparable max)
			where TComparable : IComparable<TComparable>
		{
			if (value > max) return max;
			else if (value < min) return min;
			else return value;
		}
	}
}
