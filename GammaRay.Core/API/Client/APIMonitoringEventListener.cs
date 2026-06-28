using GammaRay.Core.API.Services.Proto;
using GammaRay.Core.Monitoring;
using GammaRay.Core.Monitoring.Converters;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using Guid = System.Guid;
using GGuid = GammaRay.Core.API.Services.Proto.Guid;

namespace GammaRay.Core.API.Client;

public sealed class APIMonitoringEventListener(MonitoringSystem _targetSystem, MonitoringSerializerOptionsSource serializerOptionsSource) : IAPIEventListener
{
	private static readonly Dictionary<Type, SetSystemReportPropertyDelegate> ConstructedSetSystemReportPropertyMethods = [];


	private readonly JsonSerializerOptions _serializationOptions = serializerOptionsSource.JsonOptions;


	public bool HandleEvent(IGammaRayAPIClient sender, MonitoringEvent eventData)
	{
		switch (eventData.EventCase)
		{
			case MonitoringEvent.EventOneofCase.NewProcedure:
				{
					var data = eventData.NewProcedure;
					var id = GuidFromGGuid(data.ProcedureId);

					TrackableProcedure.New(data.Type, data.CreationTime.ToDateTime(), _targetSystem, id);

					return true;
				}
			case MonitoringEvent.EventOneofCase.FinishProcedure:
				{
					var data = eventData.FinishProcedure;
					var id = GuidFromGGuid(data.ProcedureId);

					var procedure = _targetSystem.Context.Procedures[id];

					if (data.IsSuccessful == false)
						procedure.SetFatalException(new RemoteException(data.ExceptionMessage));
					procedure.Finish();

					return true;
				}
			case MonitoringEvent.EventOneofCase.CommitReport:
				{
					var data = eventData.CommitReport;
					var id = GuidFromGGuid(data.ProcedureId);

					var procedure = _targetSystem.Context.Procedures[id];

					var classId = data.ClassIdentification;
					var report = SystemReport.CreateNewReportByClassIdentification(classId);

					foreach (var property in data.JsonReport)
					{
						var declaration = report.ListProperties()[property.Key];
						GetSetSystemReportPropertyMethod(declaration.ValueType)(this, report, declaration, property.Value);
					}

					procedure.CommitReport(report);

					return true;
				}
		}

		return false;
	}

	private static Guid GuidFromGGuid(GGuid gGuid)
	{
		var id = new Guid();
		var span = MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref id, 1));
		BitConverter.TryWriteBytes(span[..8], gGuid.Ac);
		BitConverter.TryWriteBytes(span[8..], gGuid.Dk);
		return id;
	}

	private static SetSystemReportPropertyDelegate GetSetSystemReportPropertyMethod(Type propertyValueType)
	{
		if (ConstructedSetSystemReportPropertyMethods.TryGetValue(propertyValueType, out var method) == false)
		{
			method = typeof(APIMonitoringEventListener)
				.GetMethod(
					name: nameof(SetSystemReportProperty),
					genericParameterCount: 1,
					bindingAttr: BindingFlags.Static | BindingFlags.NonPublic,
					types: [typeof(APIMonitoringEventListener), typeof(SystemReport), typeof(SystemReportPropertyDeclaration), typeof(ReadOnlySpan<char>)]
				)!
				.MakeGenericMethod([propertyValueType])
				.CreateDelegate<SetSystemReportPropertyDelegate>(target: null);
			ConstructedSetSystemReportPropertyMethods.Add(propertyValueType, method);
		}
		return method;
	}

	private static void SetSystemReportProperty<TProperty>(APIMonitoringEventListener self, SystemReport report, SystemReportPropertyDeclaration property, ReadOnlySpan<char> jsonValue)
	{
		var value = JsonSerializer.Deserialize<TProperty>(jsonValue, self._serializationOptions);
		report.WriteProperty(property.Name, value);
	}


	private delegate void SetSystemReportPropertyDelegate(APIMonitoringEventListener self, SystemReport report, SystemReportPropertyDeclaration property, ReadOnlySpan<char> jsonValue);

	public class RemoteException(string message) : Exception($"Procedure finished with exception on remote, received message: {(string.IsNullOrEmpty(message) ? "No message" : message)}");
}
