using System.Reflection;
using System.Runtime.InteropServices;

namespace GammaRay.Core.Monitoring;

public sealed class ConsoleMonitoringSystem : IMonitoringSystem
{
	public void NewContext(MonitoringContext context)
	{

	}

	public void CloseContext(MonitoringContext context)
	{

	}

	public void FinishReport(SystemReport report)
	{
		PrintReportPrefix(report);
		Console.WriteLine();
		report.ReadProperties(new ConsoleReportReader());
	}

	public void NewReport(SystemReport report)
	{
		PrintReportPrefix(report);
		Console.WriteLine(" New report");
	}

	public void SetReportProperty<TProperty>(SystemReport report, string propertyName, ReportProperty<TProperty> oldValue, TProperty newValue)
	{

	}

	private static void PrintReportPrefix(SystemReport report)
	{
		PrintColoredRaw("[", null);
		PrintColoredString(report.MonitoringContext.Type);
		PrintColoredRaw("][", null);
		PrintColoredRaw(report.MonitoringContext.Id.GetHashCode().ToString("X4"),
			ObjectColorizer.ColorForGuid(report.MonitoringContext.Id));
		PrintColoredRaw("][", null);
		PrintColoredString(report.Component);
		PrintColoredRaw("]", null);
	}

	private static void PrintColoredRaw(string text, ConsoleColor? color)
	{
		if (color is not null)
			Console.ForegroundColor = color.Value;
		else Console.ResetColor();
		Console.Write(text);
	}

	private static void PrintColoredString(string text) => PrintColoredRaw(text, ObjectColorizer.ColorForString(text));


	private readonly struct ConsoleReportReader() : ISystemReportReader
	{
		public void FeedProperty<TProperty>(string propertyName, ReportProperty<TProperty> property)
		{
			PrintColoredRaw("\t+ ", ConsoleColor.Yellow);
			PrintColoredRaw(propertyName, null);
			PrintColoredRaw(" = ", ConsoleColor.Yellow);

			Console.ResetColor();
			if (property.IsSet == false)
				Console.Write("Unset");
			else
				ObjectPrinter.Print(property.Value);
			Console.WriteLine();
		}
	}

	private static class ObjectPrinter
	{
		private static readonly Dictionary<Type, Action<object>> _classPrinters = [];


		public static void Print<T>(T value)
		{
			if (typeof(T).IsValueType)
				Console.Write(value!.ToString());
			else if (value is null)
				Console.Write("Null");
			else
			{
				if (_classPrinters.TryGetValue(typeof(T), out var printer) == false)
					_classPrinters.Add(typeof(T), printer = CreateClassPrinter(typeof(T)));

				printer(value);
			}
		}

		private static Action<object> CreateClassPrinter(Type type)
		{
			if (type == typeof(string))
				return obj => Console.Write((string)obj);

			var enumerableInterface = type.GetInterfaces().FirstOrDefault(inf =>
				inf.IsGenericType && inf.GetGenericTypeDefinition() == typeof(IEnumerable<>)
			);

			if (enumerableInterface is not null)
			{
				var collectionType = enumerableInterface.GetGenericArguments()[0];
				var delegateType = typeof(Action<>).MakeGenericType([enumerableInterface]);
				var printer = typeof(ObjectPrinter)
					.GetMethod(nameof(PrintGenericCollection), BindingFlags.Static | BindingFlags.NonPublic)!
					.MakeGenericMethod(collectionType).CreateDelegate(delegateType);

				return Wrap(enumerableInterface, printer);
			}

			return obj => Console.Write(obj.ToString());
		}

		private static Action<object> Wrap(Type genericType, object actionObj)
		{
			var wrapper = typeof(ObjectPrinter)
				.GetMethod(nameof(WrapGeneric), BindingFlags.Static | BindingFlags.NonPublic)!
				.MakeGenericMethod(genericType);
			return (Action<object>)wrapper.Invoke(null, [actionObj])!;
		}

		private static Action<object> WrapGeneric<T>(object actionObj)
		{
			var action = (Action<T>)actionObj;
			return o => action((T)o);
		}

		private static void PrintGenericCollection<T>(IEnumerable<T> collection)
		{
			bool first = true;
			Console.Write('[');
			foreach (var item in collection)
			{
				if (first == false) Console.Write(", ");
				first = false;

				Console.Write(item?.ToString() ?? "Null");
			}
			Console.Write(']');
		}
	}

	private static class ObjectColorizer
	{
		private static readonly Dictionary<string, ConsoleColor> _consistentColorCache = [];


		public static ConsoleColor ColorForString(string str)
		{
			if (_consistentColorCache.TryGetValue(str, out var color) == false)
			{
				var hash = GetDeterministicHashCode(str);
				color = (ConsoleColor)(hash & 0xF);
				color = NormalizeColor(color);
				_consistentColorCache.Add(str, color);
			}

			return color;
		}

		public static ConsoleColor ColorForGuid(Guid id)
		{
			var bytes = MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref id, 1));
			var color = (ConsoleColor)(bytes[0] & 0xF);
			return NormalizeColor(color);
		}

		private static int GetDeterministicHashCode(string str)
		{
			unchecked
			{
				int hash1 = (5381 << 16) + 5381;
				int hash2 = hash1;

				for (int i = 0; i < str.Length; i += 2)
				{
					hash1 = ((hash1 << 5) + hash1) ^ str[i];
					if (i == str.Length - 1)
						break;
					hash2 = ((hash2 << 5) + hash2) ^ str[i + 1];
				}

				return hash1 + (hash2 * 1566083941);
			}
		}

		private static ConsoleColor NormalizeColor(ConsoleColor color) => color switch
		{
			ConsoleColor.Black => ConsoleColor.White,
			ConsoleColor.DarkBlue => ConsoleColor.Blue,
			ConsoleColor.DarkGreen => ConsoleColor.Green,
			ConsoleColor.DarkCyan => ConsoleColor.Cyan,
			ConsoleColor.DarkRed => ConsoleColor.Red,
			ConsoleColor.DarkMagenta => ConsoleColor.Magenta,
			ConsoleColor.DarkYellow => ConsoleColor.Yellow,
			ConsoleColor.DarkGray => ConsoleColor.Gray,
			_ => color
		};
	}
}
