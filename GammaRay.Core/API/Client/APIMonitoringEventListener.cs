using GammaRay.Core.API.Proto;
using GammaRay.Core.Monitoring;
using GammaRay.Core.Monitoring.Converters;
using Google.Protobuf;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace GammaRay.Core.API.Client;

public sealed class APIMonitoringEventListener(IMonitoringSystem _targetSystem, MonitoringSerializerOptionsSource serializerOptionsSource) : IAPIEventListener
{
	private static readonly Dictionary<Type, SetSystemReportPropertyDelegate> ConstructedSetSystemReportPropertyMethods = [];

	private readonly Dictionary<Guid, StateFullOpenMonitoringContext> _openMonitoringContexts = [];
	private readonly JsonSerializerOptions _serializationOptions = serializerOptionsSource.JsonOptions;


	public bool HandleEvent(IGammaRayAPIClient sender, MonitoringEvent eventData)
	{
		switch (eventData.EventCase)
		{
			case MonitoringEvent.EventOneofCase.NewContext:
				{
					var data = eventData.NewContext;
					var id = GuidFromByteString(data.ContextId);

					var context = new MonitoringContext(data.Type, data.CreationTime.ToDateTime(), _targetSystem, id);
					var openContext = new StateFullOpenMonitoringContext(context);
					_openMonitoringContexts[id] = openContext;
					return true;
				}
			case MonitoringEvent.EventOneofCase.CloseContext:
				{
					var data = eventData.CloseContext;
					var id = GuidFromByteString(data.ContextId);
					if (_openMonitoringContexts.Remove(id, out var context))
						context.MonitoringContext.Close();
					return true; ;
				}
			case MonitoringEvent.EventOneofCase.NewReport:
				{
					var data = eventData.NewReport;
					var contextId = GuidFromByteString(data.ContextId);
					if (_openMonitoringContexts.TryGetValue(contextId, out var context) == false)
						throw new Exception($"Invalid event: unknown context id {contextId}");

					var typeFullName = data.ReportType;
					var reportType = typeof(GammaRayAPIClient).Assembly.GetType(typeFullName, false)
						?? throw new Exception($"Invalid event: unknown report type {typeFullName}");

					var newReport = context.MonitoringContext.NewReport(reportType);
					context.OpenReports[newReport.Component] = newReport;
					return true;
				}
			case MonitoringEvent.EventOneofCase.FinishReport:
				{
					var data = eventData.FinishReport;
					var contextId = GuidFromByteString(data.ContextId);
					if (_openMonitoringContexts.TryGetValue(contextId, out var context) == false)
						throw new Exception($"Invalid event: unknown context id {contextId}");

					var component = data.Component;
					if (context.OpenReports.Remove(component, out var report))
					{
						report.ControlContextNotification(enableOnChangedNotification: false);

						foreach (var property in data.JsonReport)
						{
							var declaration = report.ListProperties()[property.Key];
							GetSetSystemReportPropertyMethod(declaration.ValueType)(this, report, declaration, property.Value);
						}

						report.Finish();
					}
					return true;
				}
		}

		return false;
	}

	private static Guid GuidFromByteString(ByteString bytes)
	{
		var id = new Guid();
		var span = MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref id, 1));
		bytes.Span.CopyTo(span);
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

	private class StateFullOpenMonitoringContext(MonitoringContext monitoringContext)
	{
		public MonitoringContext MonitoringContext { get; } = monitoringContext;

		public Dictionary<string, SystemReport> OpenReports { get; } = [];
	}
}
