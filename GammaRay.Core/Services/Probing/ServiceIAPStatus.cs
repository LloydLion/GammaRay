using System.Diagnostics;
using System.Globalization;

namespace GammaRay.Core.Services.Probing;

public readonly record struct ServiceIAPStatus(ServiceIAPStatus.StatusType Type, TimeSpan AverageProbeTime)
{
	public static ServiceIAPStatus Blocked { get; } = new ServiceIAPStatus(StatusType.Blocked, TimeSpan.Zero);


	public override string ToString()
	{
		return Type switch
		{
			StatusType.Available => $"Available ({AverageProbeTime.TotalMilliseconds}ms)",
			StatusType.ServerSideBan => $"ServerSideBan ({AverageProbeTime.TotalMilliseconds}ms)",
			StatusType.Blocked => "Blocked",
			_ => throw new UnreachableException()
		};
	}

	public string Serialize()
	{
		var letter = Type switch
		{
			StatusType.Available => 'a',
			StatusType.ServerSideBan => 's',
			StatusType.Blocked => 'b',
			_ => throw new UnreachableException()
		};

		return $"{letter}{AverageProbeTime.Ticks}";
	}

	public static ServiceIAPStatus Deserialize(string serialized)
	{
		var letter = serialized[0];
		var ticks = long.Parse(serialized.AsSpan(1..), CultureInfo.InvariantCulture);

		var averageProbeTime = TimeSpan.FromTicks(ticks);
		var type = letter switch
		{
			'a' => StatusType.Available,
			's' => StatusType.ServerSideBan,
			'b' => StatusType.Blocked,
			_ => throw new ArgumentException("Invalid status type in serialized string", nameof(serialized))
		};

		return new ServiceIAPStatus(type, averageProbeTime);
	}


	public enum StatusType
	{
		Available,
		ServerSideBan,
		Blocked
	}
}
