using Dapper;
using GammaRay.Core.Network;
using GammaRay.Core.Services;
using GammaRay.Core.Utils;
using Microsoft.Extensions.Options;
using System.Data;
using System.Text.Json;

namespace GammaRay.Core.Persistence;

public sealed class DbServiceRepository(
	IOptions<DbServiceRepository.Options> options,
	CapabilityClassProvider _capabilityClassProvider,
	TimeProvider _time,
	IDbConnectionFactory connectionFactory
) : AsyncDbRepository<DbServiceRepository.State, DbServiceRepository.Mutation>(connectionFactory), IServiceRepository
{
	private readonly Options _options = options.Value;


	public void RegisterService(Service service)
	{
		Write(new Mutation(new Decayable<Service>(service, _time.GetUtcNow().UtcDateTime + _options.DecayTime)));
	}

	public Decayable<Service>? TryGetService(WebEndPoint webEndPoint)
	{
		if (CurrentState.Data.TryGetValue(webEndPoint, out var serviceDecayable))
			return serviceDecayable;
		return null;
	}

	public IReadOnlyCollection<Decayable<Service>> ListServices()
	{
		return CurrentState.Data.Values;
	}

	protected override void ApplyMutation(State state, Mutation mutation)
	{
		state.Data[mutation.ServiceDecayable.Value.EndPoint] = mutation.ServiceDecayable;
	}

	protected override async ValueTask ExecuteWriteAsync(IDbConnection connection, Mutation item)
	{
		var model = new Model()
		{
			WebHost = item.ServiceDecayable.Value.EndPoint.Host.Domain,
			Port = item.ServiceDecayable.Value.EndPoint.Port,
			Protocol = (int)item.ServiceDecayable.Value.EndPoint.Protocol,
			ValidUntil = item.ServiceDecayable.ValidUntil.Ticks,
			CapabilityClass = _capabilityClassProvider.InverseLookupTable[item.ServiceDecayable.Value.Capability.Class].Name,
			CapabilityProperties = JsonSerializer.Serialize(item.ServiceDecayable.Value.Capability.Properties)
		};

		await connection.ExecuteAsync(
			"""
			INSERT INTO Services (WebHost, Port, Protocol, ValidUntil, CapabilityClass, CapabilityProperties)
			VALUES (@WebHost, @Port, @Protocol, @ValidUntil, @CapabilityClass, @CapabilityProperties)
			ON CONFLICT(WebHost, Port, Protocol) DO UPDATE SET
				ValidUntil = excluded.ValidUntil,
				CapabilityClass = excluded.CapabilityClass,
				CapabilityProperties = excluded.CapabilityProperties
			""", model
		);
	}

	protected override void PerformDatabaseMigration(IDbConnection connection)
	{
		/*
		 * Service.WebEndPoint is mapped to (WebHost, Port, Protocol) Primary key
		 *   Protocol is mapped to integer according enum values
		 * Service.Capability is mapped to (CapabilityClass, CapabilityProperties)
		 *   CapabilityProperties is mapped as JSON dictionary
		 * Decayable.ValidUntil is mapped to ValidUntil as dotnet ticks
		 */
		connection.Execute(
			"""
			CREATE TABLE IF NOT EXISTS Services (
				WebHost TEXT NOT NULL,
				Port INTEGER NOT NULL,
				Protocol INTEGER NOT NULL,

				ValidUntil INTEGER NOT NULL,

				CapabilityClass TEXT NOT NULL,
				CapabilityProperties TEXT NOT NULL,

				PRIMARY KEY (WebHost, Port, Protocol)
			)
			"""
		);
	}

	protected override State PreloadData(IDbConnection connection)
	{
		var rawData = connection.Query<Model>(
			"""
			SELECT * FROM Services
			"""
		);

		var services = rawData.Select(m =>
		{
			var endPoint = new WebEndPoint(new WebHost(m.WebHost), m.Port, (TransportType)m.Protocol);

			var capabilityProperties = JsonSerializer.Deserialize<Dictionary<string, string>>(m.CapabilityProperties) ?? [];
			var capability = new Capability(_capabilityClassProvider.GetClassByName(m.CapabilityClass), capabilityProperties);

			var service = new Service(endPoint, capability);

			var validUntil = new DateTime(m.ValidUntil);
			return new Decayable<Service>(service, validUntil);
		});

		return new State()
		{
			Data = services.ToDictionary(s => s.Value.EndPoint)
		};
	}


	public class Options
	{
		public TimeSpan DecayTime { get; init; } = TimeSpan.FromDays(1);
	}

	public class State
	{
		public required Dictionary<WebEndPoint, Decayable<Service>> Data { get; init; }
	}

	public readonly record struct Mutation(Decayable<Service> ServiceDecayable);

	private class Model
	{
		public required string WebHost { get; init; }

		public required int Port { get; init; }

		public required int Protocol { get; init; }

		public required long ValidUntil { get; init; }

		public required string CapabilityClass { get; init; }

		public required string CapabilityProperties { get; init; }
	}
}
