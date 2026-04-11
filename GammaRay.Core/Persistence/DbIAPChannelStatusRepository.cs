using Dapper;
using GammaRay.Core.InternetAccess;
using GammaRay.Core.InternetAccess.Channels;
using GammaRay.Core.Routing.NetworkProfiles;
using System.Data;

namespace GammaRay.Core.Persistence;

public sealed class DbIAPChannelStatusRepository(
	InternetAccessPointProvider _internetAccessPointProvider,
	NetworkProfileProvider _networkProfileProvider,
	TimeProvider _time,
	IDbConnectionFactory connectionFactory
) : AsyncDbRepository<DbIAPChannelStatusRepository.State, DbIAPChannelStatusRepository.Mutation>(connectionFactory), IIAPChannelStatusRepository
{
	public IAPChannelStatus? TryGetStatus(InternetAccessPoint point, IAPChannel channel, NetworkProfile currentNetworkProfile)
	{
		if (CurrentState.Statues.TryGetValue((point, channel, currentNetworkProfile), out var channelStatus))
			return channelStatus;
		return null;
	}

	public void UpdateStatuses(IEnumerable<IAPChannelStatus> statusTable)
	{
		var copy = statusTable.ToArray();
		var networkProfile = copy[0].Network;
		if (copy.Any(s => s.Network != networkProfile))
			throw new InvalidOperationException("All statues must be from single network");

		Write(new Mutation(copy, networkProfile, _time.GetUtcNow().UtcDateTime));
	}

	public DateTime GetLastStatusUpdateTime(NetworkProfile networkProfile)
	{
		if (CurrentState.LastUpdateTimes.TryGetValue(networkProfile, out var lastUpdateTime))
			return lastUpdateTime;
		return DateTime.MinValue;
	}

	protected override void ApplyMutation(State state, Mutation mutation)
	{
		foreach (var status in mutation.NewStatues)
			state.Statues[(status.InternetAccessPoint, status.Channel, mutation.Network)] = status;
		state.LastUpdateTimes[mutation.Network] = mutation.UpdatedAt;
	}

	protected override async ValueTask ExecuteWriteAsync(IDbConnection connection, Mutation item)
	{
		var statusModels = item.NewStatues.Select(s => new StatusModel()
		{
			InternetAccessPoint = s.InternetAccessPoint.Name,
			Channel = s.InternetAccessPoint.Channels.Single(kv => kv.Value == s.Channel).Key,
			Network = s.Network.Name,
			AverageAccessTime = s.AverageAccessTime.Ticks,
		});

		foreach (var statusModel in statusModels)
		{
			await connection.ExecuteAsync(
				"""
				INSERT INTO IAPChannelStatuses (InternetAccessPoint, Channel, Network, AverageAccessTime)
				VALUES (@InternetAccessPoint, @Channel, @Network, @AverageAccessTime)
				ON CONFLICT(InternetAccessPoint, Channel, Network) DO UPDATE SET
					AverageAccessTime = excluded.AverageAccessTime
				""", statusModel
			);
		}

		var lastUpdateTimeModel = new LastUpdateTimeModel()
		{
			Network = item.Network.Name,
			LastUpdateTime = item.UpdatedAt.Ticks,
		};

		await connection.ExecuteAsync(
			"""
			INSERT INTO IAPChannelStatusesUpdateTimes (Network, LastUpdateTime)
			VALUES (@Network, @LastUpdateTime)
			ON CONFLICT(Network) DO UPDATE SET
				LastUpdateTime = excluded.LastUpdateTime
			""", lastUpdateTimeModel
		);
	}

	protected override void PerformDatabaseMigration(IDbConnection connection)
	{
		/*
		 * IAPChannelStatus.InternetAccessPoint|Channel|Network is mapped to
		 *	 InternetAccessPoint|Channel|Network as their names together they form Primary key
		 * IAPChannelStatus.AverageAccessTime is mapped to AverageAccessTime as dotnet ticks
		 */
		connection.Execute(
			"""
			CREATE TABLE IF NOT EXISTS IAPChannelStatuses (
				InternetAccessPoint TEXT NOT NULL,
				Channel TEXT NOT NULL,
				Network TEXT NOT NULL,

				AverageAccessTime INTEGER NOT NULL,

				PRIMARY KEY (InternetAccessPoint, Channel, Network)
			)
			"""
		);

		/*
		 * Last update times stored in different table and keyed by NetworkProfile
		 */
		connection.Execute(
			"""
			CREATE TABLE IF NOT EXISTS IAPChannelStatusesUpdateTimes (
				Network TEXT NOT NULL PRIMARY KEY,
				LastUpdateTime INTEGER NOT NULL
			)
			"""
		);
	}

	protected override State PreloadData(IDbConnection connection)
	{
		var rawStatues = connection.Query<StatusModel>(
			"""
			SELECT * FROM IAPChannelStatuses
			"""
		);

		var statuses = rawStatues.Select(m =>
		{
			var IAP = _internetAccessPointProvider.InternetAccessPoints[m.InternetAccessPoint];
			var channel = IAP.Channels[m.Channel];
			var network = _networkProfileProvider.Profiles[m.Network];
			var accessTime = new TimeSpan(m.AverageAccessTime);

			return new IAPChannelStatus(IAP, channel, network, accessTime);
		});

		var rawLastUpdateTimes = connection.Query<LastUpdateTimeModel>(
			"""
			SELECT * FROM IAPChannelStatusesUpdateTimes
			"""
		);

		var lastUpdateTimes = rawLastUpdateTimes.Select(m =>
			KeyValuePair.Create(_networkProfileProvider.Profiles[m.Network], new DateTime(m.LastUpdateTime))
		);

		return new State()
		{
			Statues = statuses.ToDictionary(s => (s.InternetAccessPoint, s.Channel, s.Network)),
			LastUpdateTimes = lastUpdateTimes.ToDictionary()
		};
	}


	public class State
	{
		public required Dictionary<(InternetAccessPoint, IAPChannel, NetworkProfile), IAPChannelStatus> Statues { get; init; }

		public required Dictionary<NetworkProfile, DateTime> LastUpdateTimes { get; init; }
	}

	public readonly record struct Mutation(IAPChannelStatus[] NewStatues, NetworkProfile Network, DateTime UpdatedAt);

	private class StatusModel
	{
		public required string InternetAccessPoint { get; init; }

		public required string Channel { get; init; }

		public required string Network { get; init; }

		public required long AverageAccessTime { get; init; }
	}

	private class LastUpdateTimeModel
	{
		public required string Network { get; init; }

		public required long LastUpdateTime { get; init; }
	}
}
