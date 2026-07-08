using Dapper;
using GammaRay.Core.InternetAccess;
using GammaRay.Core.InternetAccess.Channels;
using GammaRay.Core.InternetAccess.Channels.Testing;
using GammaRay.Core.Network.Profiles;
using System.Data;

namespace GammaRay.Core.Persistence;

public sealed class DbIAPChannelObservedDataRepository(
	InternetAccessPointProvider _internetAccessPointProvider,
	NetworkProfileProvider _networkProfileProvider,
	IDbConnectionFactory connectionFactory
) : AsyncDbRepository<DbIAPChannelObservedDataRepository.State, DbIAPChannelObservedDataRepository.Mutation>(connectionFactory), IIAPChannelObservedDataRepository
{
	public IAPChannelObservedData? TryGetObservedData(InternetAccessPoint IAP, IAPChannel channel, NetworkProfile network)
	{
		CurrentState.Data.TryGetValue((IAP, channel, network), out var value);
		return value;
	}

	public void SaveObservedData(InternetAccessPoint IAP, IAPChannel channel, NetworkProfile network, IAPChannelObservedData data)
	{
		Write(new Mutation((IAP, channel, network), data));
	}


	protected override void ApplyMutation(State state, Mutation mutation)
	{
		state.Data[mutation.Key] = mutation.ObservedData;
	}

	protected override async ValueTask ExecuteWriteAsync(IDbConnection connection, Mutation item)
	{
		var observationRow = SerializeObservationRow(item.ObservedData.ObservationRow);
		var model = new Model()
		{
			InternetAccessPoint = item.Key.IAP.Name,
			ChannelName = item.Key.IAP.InverseChannels[item.Key.Channel],
			NetworkProfile = item.Key.Network.Name,
			IsAvailable = item.ObservedData.IsAvailable ? 1 : 0,
			ObservationRow = observationRow
		};

		await connection.ExecuteAsync(
			"""
			INSERT INTO ChannelObservedData (InternetAccessPoint, ChannelName, NetworkProfile, IsAvailable, ObservationRow)
			VALUES (@InternetAccessPoint, @ChannelName, @NetworkProfile, @IsAvailable, @ObservationRow)
			ON CONFLICT(InternetAccessPoint, ChannelName, NetworkProfile) DO UPDATE SET
				ObservationRow = excluded.ObservationRow
			""", model
		);
	}

	protected override State PreloadData(IDbConnection connection)
	{
		var rawModels = connection.Query<Model>(
			"""
			SELECT * FROM ChannelObservedData
			"""
		);
		return new State()
		{
			Data = rawModels.Select(s =>
			{
				var IAP = _internetAccessPointProvider.InternetAccessPoints[s.InternetAccessPoint];
				var channel = IAP.Channels[s.ChannelName];
				var network = _networkProfileProvider.Profiles[s.NetworkProfile];
				var observedData = new IAPChannelObservedData()
				{
					ObservationRow = DeserializeObservationRow(s.ObservationRow),
					IsAvailable = s.IsAvailable != 0
				};

				return KeyValuePair.Create((IAP, channel, network), observedData);
			}).ToDictionary()
		};
	}

	protected override void PerformDatabaseMigration(IDbConnection connection)
	{
		connection.Execute(
			"""
			CREATE TABLE IF NOT EXISTS ChannelObservedData (
				InternetAccessPoint TEXT NOT NULL,
				ChannelName INTEGER NOT NULL,
				NetworkProfile INTEGER NOT NULL,

				ObservationRow BLOB NOT NULL,
				IsAvailable INTEGER NOT NULL,

				PRIMARY KEY (InternetAccessPoint, ChannelName, NetworkProfile)
			)
			"""
		);
	}

	private static byte[] SerializeObservationRow(TimeSpan[] values)
	{
		var bytes = new byte[values.Length * sizeof(long)];

		for (int i = 0; i < values.Length; i++)
			BitConverter.TryWriteBytes(bytes.AsSpan(i * sizeof(long), sizeof(long)), values[i].Ticks);

		return bytes;
	}

	private static TimeSpan[] DeserializeObservationRow(byte[] bytes)
	{
		var result = new TimeSpan[bytes.Length / sizeof(long)];

		for (int i = 0; i < result.Length; i++)
			result[i] = new TimeSpan(BitConverter.ToInt64(bytes, i * sizeof(long)));

		return result;
	}


	public class State
	{
		public required Dictionary<(InternetAccessPoint IAP, IAPChannel Channel, NetworkProfile Network), IAPChannelObservedData> Data { get; init; }
	}

	public readonly record struct Mutation((InternetAccessPoint IAP, IAPChannel Channel, NetworkProfile Network) Key, IAPChannelObservedData ObservedData);

	private class Model
	{
		public required string InternetAccessPoint { get; init; }

		public required string ChannelName { get; init; }

		public required string NetworkProfile { get; init; }

		public required int IsAvailable { get; init; }

		public required byte[] ObservationRow { get; init; }
	}
}
