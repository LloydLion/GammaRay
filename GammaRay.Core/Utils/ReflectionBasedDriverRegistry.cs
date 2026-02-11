using System.Reflection;

namespace GammaRay.Core.Utils;

public sealed class ReflectionBasedDriverRegistry<T>(IEnumerable<T> drivers) : IDriverRegistry<T>
{
	private readonly Dictionary<string, T> _drivers = drivers.ToDictionary(s =>
		{
			var attribute = s.GetType().GetCustomAttribute<RecommendedDriverNameAttribute>()
				?? throw new ArgumentException($"Driver (type {s.GetType()}) has not required RecommendedDriverNameAttribute", nameof(drivers));
			return attribute.RecommendedName;
		}, StringComparer.OrdinalIgnoreCase);

	public T ProvideDriver(string name) => _drivers[name];
}
