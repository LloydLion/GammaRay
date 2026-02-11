using System.Reflection;

namespace GammaRay.Core.Inbound;

internal sealed class ReflectionBasedDriverRegistry(IEnumerable<IInboundDriver> drivers) : IInboundDriverRegistry
{
	private readonly Dictionary<string, IInboundDriver> _drivers = drivers.ToDictionary(s =>
		{
			var attribute = s.GetType().GetCustomAttribute<RecommendedDriverNameAttribute>()
				?? throw new ArgumentException($"Driver (type {s.GetType()}) has not required RecommendedDriverNameAttribute", nameof(drivers));
			return attribute.RecommendedName;
		}, StringComparer.OrdinalIgnoreCase);

	public IInboundDriver ProvideDriver(string name) => _drivers[name];
}
