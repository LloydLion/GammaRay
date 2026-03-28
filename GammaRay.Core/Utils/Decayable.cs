namespace GammaRay.Core.Utils;

public readonly record struct Decayable<T>(T Value, DateTime ValidUntil)
{
	public bool IsValid(DateTime now) => now <= ValidUntil;

	public T? GetValueOrDefault(DateTime now) => IsValid(now) ? Value : default;

	public T GetValueOrSpecial(DateTime now, T specialValue) => IsValid(now) ? Value : specialValue;
}
