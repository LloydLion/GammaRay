using System.Runtime.InteropServices;

namespace GammaRay.Core.Monitoring;

public sealed class ConsoleMonitoringProvider : IMonitoringProvider
{
	public void NotifyNewProcedure(TrackableProcedure procedure) { }

	public void NotifyProcedureFinished(TrackableProcedure procedure)
	{
		PrintColoredRaw("[", null);
		PrintColoredString(procedure.Type);
		PrintColoredRaw("][", null);

		var procId = procedure.Id;
		var procIdBytes = MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref procId, 1));
		var printableId = BitConverter.ToInt32(procIdBytes);

		PrintColoredRaw(printableId.ToString("X4"), ObjectColorizer.ColorForByte(procIdBytes[0]));
		PrintColoredRaw("] Procedure finished", null);

		if (procedure.IsFailed)
		{
			Console.WriteLine();
			var exception = procedure.FatalException;
			PrintColoredRaw(exception.ToString(), ConsoleColor.Red);
		}
		Console.WriteLine();
	}

	public void NotifyNewCommit(TrackableProcedure procedure, SystemReport newReport)
	{
		PrintReportPrefix(newReport);
		Console.WriteLine();
		newReport.ReadProperties(new ConsoleReportReader());
	}

	private static void PrintReportPrefix(SystemReport report)
	{
		PrintColoredRaw("[", null);
		PrintColoredString(report.Procedure.Type);
		PrintColoredRaw("][", null);

		var procId = report.Procedure.Id;
		var procIdBytes = MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref procId, 1));
		var printableId = BitConverter.ToInt32(procIdBytes);

		PrintColoredRaw(printableId.ToString("X4"), ObjectColorizer.ColorForByte(procIdBytes[0]));
		PrintColoredRaw("][", null);
		PrintColoredString(report.Metadata.Role);
		PrintColoredRaw("|", null);
		PrintColoredString(report.Metadata.Component);
		PrintColoredRaw("|", null);
		PrintColoredString(report.Metadata.Task);
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
