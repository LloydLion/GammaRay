namespace GammaRay.Core.Settings.Model;

public class SD<T> : Dictionary<string, T>
{
	public SD() : base(StringComparer.OrdinalIgnoreCase) { }

	public SD(IReadOnlyDictionary<string, T> dictionary) : base(dictionary, StringComparer.OrdinalIgnoreCase) { }
}
