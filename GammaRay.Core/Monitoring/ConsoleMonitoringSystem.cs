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

		var contextId = report.MonitoringContext.Id;
		var contextIdBytes = MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref contextId, 1));
		var printableId = BitConverter.ToInt32(contextIdBytes);

		PrintColoredRaw(printableId.ToString("X4"), ObjectColorizer.ColorForByte(contextIdBytes[0]));
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
				MonitoringObjectPrinter.PrintObject(property.Value, Console.Out);
			Console.WriteLine();
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

		public static ConsoleColor ColorForByte(byte number)
		{
			var color = (ConsoleColor)(number & 0xF);
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
