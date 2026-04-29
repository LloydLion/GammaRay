using System.Linq.Expressions;
using System.Reflection;

namespace GammaRay.Core.Monitoring;

public static class MonitoringObjectPrinter
{
	private static readonly Dictionary<Type, ObjectPrinter> ObjectPrinters = [];


	public static void PrintObject<T>(T value, TextWriter textOutput)
	{
		if (value is null)
			textOutput.Write("Null");
		else if (typeof(T).IsValueType)
			textOutput.Write(value!.ToString());
		else
		{
			if (ObjectPrinters.TryGetValue(typeof(T), out var printer) == false)
				ObjectPrinters.Add(typeof(T), printer = CreateClassPrinter(typeof(T)));

			printer(value, textOutput);
		}
	}

	private static ObjectPrinter CreateClassPrinter(Type type)
	{
		if (type == typeof(string))
			goto returnDefault;

		var enumerableInterface = type.GetInterfaces().FirstOrDefault(inf =>
			inf.IsGenericType && inf.GetGenericTypeDefinition() == typeof(IEnumerable<>)
		);

		if (enumerableInterface is not null)
		{
			var collectionType = enumerableInterface.GetGenericArguments()[0];

			return CreateBundleFromGenericMethods(nameof(SerializeGenericCollectionText), collectionType, enumerableInterface);
		}

	returnDefault:
		return (obj, text) => text.Write(obj);
	}

	private static ObjectPrinter CreateBundleFromGenericMethods(string name, Type genericArgument, Type acceptingType)
	{
		var bastMethod = typeof(MonitoringObjectPrinter).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)!.MakeGenericMethod(genericArgument);
		var objParameter = Expression.Parameter(typeof(object), "obj");
		var outputParameter = Expression.Parameter(typeof(TextWriter), "output");
		var body = Expression.Call(null, bastMethod, Expression.Convert(objParameter, acceptingType), outputParameter);
		var objectPrinter = Expression.Lambda<ObjectPrinter>(body, objParameter, outputParameter).Compile();

		return objectPrinter;
	}

	private static void SerializeGenericCollectionText<T>(IEnumerable<T> collection, TextWriter output)
	{
		bool first = true;
		output.Write('[');
		foreach (var item in collection)
		{
			if (first == false) output.Write(", ");
			first = false;

			PrintObject(item, output);
		}
		output.Write(']');
	}


	private delegate void ObjectPrinter(object obj, TextWriter output);
}
