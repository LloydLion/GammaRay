using Dapper;
using GammaRay.Core.Network.Identity;
using GammaRay.Core.Network.Profiles;
using System.Data;

namespace GammaRay.Core.Persistence;

public sealed class DbNetworkProfileMappingRepository(
	IDbConnectionFactory connectionFactory,
	NetworkProfileProvider _profiles
) : AsyncDbRepository<DbNetworkProfileMappingRepository.State, DbNetworkProfileMappingRepository.Mutation>(connectionFactory), INetworkProfileMappingRepository
{
	public NetworkProfile GetProfileFor(NetworkIdentity identity)
	{
		if (CurrentState.Mapping.TryGetValue(identity, out var profile))
			return profile ?? _profiles.DefaultProfile;

		Write(new Mutation(identity, null));
		return _profiles.DefaultProfile;
	}

	public void SetProfileFor(NetworkIdentity identity, NetworkProfile profile)
	{
		Write(new Mutation(identity, profile));
	}

	public IReadOnlyDictionary<NetworkIdentity, NetworkProfile?> GetMapping()
	{
		return CurrentState.Mapping;
	}

	protected override void ApplyMutation(State state, Mutation mutation)
	{
		state.Mapping[mutation.Identity] = mutation.Profile;
	}

	protected override async ValueTask ExecuteWriteAsync(IDbConnection connection, Mutation item)
	{
		var model = new Model { NetworkIdentity = item.Identity.SerializedForm, Profile = item.Profile?.Name };
		await connection.ExecuteAsync(
			"""
			INSERT INTO NetworkMapping (NetworkIdentity, Profile)
			VALUES (@NetworkIdentity, @Profile)
			ON CONFLICT(NetworkIdentity) DO UPDATE SET
				Profile = excluded.Profile
			""", model
		);
	}

	protected override void PerformDatabaseMigration(IDbConnection connection)
	{
		connection.Execute(
			"""
			CREATE TABLE IF NOT EXISTS NetworkMapping (
				NetworkIdentity TEXT NOT NULL,
				Profile TEXT,
			
				PRIMARY KEY (NetworkIdentity)
			)
			"""
		);
	}

	protected override State PreloadData(IDbConnection connection)
	{
		var models = connection.Query<Model>(
			"""
			SELECT * FROM NetworkMapping
			"""
		);

		var state = new State();
		foreach (var model in models)
			state.Mapping[new NetworkIdentity(model.NetworkIdentity)] = model.Profile is null ? null : _profiles.Profiles[model.Profile];

		return state;
	}


	public record struct Mutation(NetworkIdentity Identity, NetworkProfile? Profile);

	public class State
	{
		public Dictionary<NetworkIdentity, NetworkProfile?> Mapping { get; } = [];
	}

	public class Model
	{
		public required string NetworkIdentity { get; set; }

		public required string? Profile { get; set; }
	}
}
