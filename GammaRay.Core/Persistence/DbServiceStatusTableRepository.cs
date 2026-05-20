using Dapper;
using GammaRay.Core.InternetAccess;
using GammaRay.Core.Network;
using GammaRay.Core.Services;
using GammaRay.Core.Services.Probing;
using GammaRay.Core.Utils;
using Microsoft.Extensions.Options;
using System.Data;
using System.Text.Json;

namespace GammaRay.Core.Persistence;

public sealed class DbServiceStatusTableRepository(
	IOptions<DbServiceStatusTableRepository.Options> options,
	IServiceRepository _serviceRepository,
	InternetAccessPointProvider _internetAccessPointProvider,
	TimeProvider _time,
	IDbConnectionFactory connectionFactory
) : AsyncDbRepository<DbServiceStatusTableRepository.State, DbServiceStatusTableRepository.Mutation>(connectionFactory), IServiceStatusTableRepository
{
	private readonly Options _options = options.Value;


	public void UpdateTable(ServiceStatusTable route)
	{
		Write(new Mutation(new Decayable<ServiceStatusTable>(route, _time.GetUtcNow().UtcDateTime + _options.DecayTime)));
	}

	public Decayable<ServiceStatusTable>? TryGetTable(Service service)
	{
		if (CurrentState.Data.TryGetValue(service, out var tableDecayable))
			return tableDecayable;
		return null;		
	}

	protected override void ApplyMutation(State state, Mutation mutation)
	{
		state.Data[mutation.TableDecayable.Value.Service] = mutation.TableDecayable;
	}

	protected override async ValueTask ExecuteWriteAsync(IDbConnection connection, Mutation item)
	{
		var tableJSON = JsonSerializer.Serialize(item.TableDecayable.Value.Table.ToDictionary(kv => kv.Key.Name, kv => kv.Value.Serialize()));
		var model = new Model()
		{
			WebHost = item.TableDecayable.Value.Service.EndPoint.Host.Domain,
			Port = item.TableDecayable.Value.Service.EndPoint.Port,
			Protocol = (int)item.TableDecayable.Value.Service.EndPoint.Protocol,
			ValidUntil = item.TableDecayable.ValidUntil.Ticks,
			TableData = tableJSON,
		};

		await connection.ExecuteAsync(
			"""
			INSERT INTO ServiceStatusTables (WebHost, Port, Protocol, ValidUntil, TableData)
			VALUES (@WebHost, @Port, @Protocol, @ValidUntil, @TableData)
			ON CONFLICT(WebHost, Port, Protocol) DO UPDATE SET
				ValidUntil = excluded.ValidUntil,
				TableData = excluded.TableData
			""", model
		);
	}

	protected override void PerformDatabaseMigration(IDbConnection connection)
	{
		/*
		 * ServiceStatusTable.Service.WebEndPoint is mapped to (WebHost, Port, Protocol) Primary key
		 *   Protocol is mapped to integer according enum values
		 * ServiceStatusTable.Table is mapped to TableData ('Table' is reserved SQL keyword) as JSON dictionary
		 * Decayable.ValidUntil is mapped to ValidUntil as Unix timestamp
		 */
		connection.Execute(
			"""
			CREATE TABLE IF NOT EXISTS ServiceStatusTables (
				WebHost TEXT NOT NULL,
				Port INTEGER NOT NULL,
				Protocol INTEGER NOT NULL,

				ValidUntil INTEGER NOT NULL,

				TableData TEXT NOT NULL,

				PRIMARY KEY (WebHost, Port, Protocol)
			)
			"""
		);
	}

	protected override State PreloadData(IDbConnection connection)
	{
		var rawData = connection.Query<Model>(
			"""
			SELECT * FROM ServiceStatusTables
			"""
		);

		var tables = rawData.Select(m =>
		{
			var endPoint = new WebEndPoint(new WebHost(m.WebHost), m.Port, (TransportType)m.Protocol);
			var service = _serviceRepository.TryGetService(endPoint)?.Value;
			if (service is null)
				return default;

			var rawTable = JsonSerializer.Deserialize<Dictionary<string, string>>(m.TableData) ?? [];
			var table = rawTable.ToDictionary(
				kv => _internetAccessPointProvider.InternetAccessPoints[kv.Key],
				kv => ServiceIAPStatus.Deserialize(kv.Value)
			);

			var statusTable = new ServiceStatusTable(service, table);

			var validUntil = new DateTime(m.ValidUntil);
			return new Decayable<ServiceStatusTable>(statusTable, validUntil);
		}).Where(s => s != default);

		return new State()
		{
			Data = tables.ToDictionary(s => s.Value.Service)
		};
	}


	public class Options
	{
		public TimeSpan DecayTime { get; init; } = TimeSpan.FromDays(1);
	}

	public class State
	{
		public required Dictionary<Service, Decayable<ServiceStatusTable>> Data { get; init; }
	}

	public readonly record struct Mutation(Decayable<ServiceStatusTable> TableDecayable);

	private class Model
	{
		public required string WebHost { get; init; }

		public required int Port { get; init; }

		public required int Protocol { get; init; }

		public required long ValidUntil { get; init; }

		public required string TableData { get; init; }
	}
}
