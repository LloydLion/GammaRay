using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using YamlDotNet.Core.Tokens;
using YamlDotNet.RepresentationModel;

namespace GammaRay.Core.Settings;

internal static class YAMLNodeExtensions
{
	extension(YamlMappingNode mappingNode)
	{
		public IEnumerable<KeyValuePair<string, YamlNode>> ScalarChildrenMap =>
			mappingNode.Children
				.Where(s => s.Key is YamlScalarNode)
				.Select(kv => KeyValuePair.Create(((YamlScalarNode)kv.Key).Value!, kv.Value));

		public bool TryGet<TChild>(string key, [NotNullWhen(true)] out TChild? value) where TChild : YamlNode
		{
			if (mappingNode.Children.TryGetValue(key, out var rawValue) && rawValue is TChild child)
			{
				value = child;
				return true;
			}
			value = null;
			return false;
		}

		public TChild? TryGet<TChild>(string key) where TChild : YamlNode
			=> mappingNode.TryGet<TChild>(key, out var child) ? child : null;

		public bool TryBindChild<TValue>(string key, [NotNullWhen(true)] out TValue? value) where TValue : notnull
		{
			if (mappingNode.TryGet<YamlNode>(key, out var node))
			{
				value = node.Bind<TValue>();
				return true;
			}
			value = default;
			return false;
		}

		public TValue? TryBindChild<TValue>(string key) where TValue : notnull
			=> mappingNode.TryBindChild<TValue>(key, out var value) ? value : default;

		public TChild ExceptChild<TChild>(string key) where TChild : YamlNode
		{
			return mappingNode.TryGet<TChild>(key) ?? throw new Exception($"Missing required key '{key}'");
		}

		public YamlMappingNode ExceptMappingChild(string key) => mappingNode.ExceptChild<YamlMappingNode>(key);
	}

	extension(YamlNode node)
	{
		public YamlMappingNode AsMapping() => node as YamlMappingNode ?? throw new Exception("Expected mapping node");

		public object? Bind(Type targetType)
		{
			switch (node)
			{
				case YamlScalarNode scalar:
					var s = scalar.Value;

					if (s is null) return defaultValueForType(targetType);

					if (targetType == typeof(string)) return s;
					if (targetType.IsEnum) return Enum.Parse(targetType, s, ignoreCase: true);
					if (targetType == typeof(bool)) return bool.Parse(s);

					var parseMethod = targetType.GetMethod("Parse", BindingFlags.Static | BindingFlags.Public,
						[typeof(string), typeof(IFormatProvider)]);
					if (parseMethod is not null) return parseMethod.Invoke(null, [s, CultureInfo.InvariantCulture]);

					parseMethod = targetType.GetMethod("Parse", BindingFlags.Static | BindingFlags.Public, [typeof(string)]);
					if (parseMethod is not null) return parseMethod.Invoke(null, [s]);

					throw new Exception("Parsing error");


				case YamlSequenceNode seq:
					var children = seq.Children;
					var count = children.Count;
					if (targetType.IsArray)
					{
						var elementType = targetType.GetElementType()!;
						var array = Array.CreateInstance(elementType, count);
						for (int i = 0; i < count; i++)
							array.SetValue(Bind(children[i], elementType), i);
						return array;
					}

					var genericEnumerableInterface = targetType
						.GetInterfaces()
						.FirstOrDefault(ife => ife.IsGenericType && ife.GetGenericTypeDefinition() == typeof(IEnumerable<>));
					if (genericEnumerableInterface is not null)
					{
						var elementType = genericEnumerableInterface.GetGenericArguments()[0];

						var array = Array.CreateInstance(elementType, count);
						for (int i = 0; i < count; i++)
							array.SetValue(Bind(children[i], elementType), i);

						Type typeToCreate;
						if (targetType.IsInterface)
						{
							if (array.GetType().GetInterfaces().Any(s => s == targetType))
								return array;
							throw new Exception("No type to create");
						}

						typeToCreate = targetType;
						return typeToCreate.GetConstructor([genericEnumerableInterface])!.Invoke([array]);
					}

					throw new Exception("Unsupported collection");

				case YamlMappingNode map:
					var values = map.Children;

					var constructors = targetType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);

					object? createdObject = null;
					foreach (var constructor in constructors)
					{
						try
						{
							var parameters = constructor.GetParameters();
							var arguments = parameters.Select(p => values[convertCasing(p.Name!)].Bind(p.ParameterType)).ToArray();
							createdObject = constructor.Invoke(arguments);
						}
						catch (Exception) { }
					}

					if (createdObject is null)
						throw new Exception("Enable to construct");

					var props = targetType.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanWrite);

					foreach (var prop in props)
					{
						if (values.TryGetValue(convertCasing(prop.Name), out var child))
						{
							var val = child.Bind(prop.PropertyType);
							prop.SetValue(createdObject, val);
						}
					}

					return createdObject;
			}

			throw new Exception("Unsupported node type");


			static string convertCasing(string value)
			{
				return string.Create(value.Length, value, (span, value) =>
				{
					value.CopyTo(span);
					span[0] = char.ToLowerInvariant(span[0]);
				});
			}

			static object? defaultValueForType(Type targetType) => targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
		}

		public T Bind<T>()
			where T : notnull
		{
			return (T)(node.Bind(typeof(T)) ?? throw new NullReferenceException());
		}

		public T? BindNullable<T>()
		{
			return (T?)node.Bind(typeof(T));
		}
	}
}
