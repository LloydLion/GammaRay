using GammaRay.Core.Monitoring;
using GammaRay.Core.Monitoring.Converters;
using GammaRay.Core.Utils;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace GammaRay.Core.API.Client;

public sealed class APIMonitoringEventListener(IMonitoringSystem _targetSystem, MonitoringSerializerOptionsSource serializerOptionsSource) : IAPIEventListener
{
	private static readonly Dictionary<Type, SetSystemReportPropertyDelegate> ConstructedSetSystemReportPropertyMethods = [];

	private readonly Dictionary<Guid, StateFullOpenMonitoringContext> _openMonitoringContexts = [];
	private readonly JsonSerializerOptions _serializationOptions = serializerOptionsSource.JsonOptions;


	public bool HandleEvent(IGammaRayAPIClient sender, ReadOnlySpan<byte> eventData)
	{
		var eventType = (APIConstants.EventType)eventData[0];
		var reader = new BufferReader(eventData, initialReadLength: 1);
		switch (eventType)
		{
			case APIConstants.EventType.MonitoringNewContext:
				{
					var creationTime = reader.ReadDateTime();
					var id = reader.ReadGuid();
					var type = reader.ReadStringToEnd(Encoding.UTF8);
					
					var context = new MonitoringContext(type, creationTime, _targetSystem, id);
					var openContext = new StateFullOpenMonitoringContext(context);
					_openMonitoringContexts[id] = openContext;
					break;
				}
			case APIConstants.EventType.MonitoringCloseContext:
				{
					var id = reader.ReadGuid();
					if (_openMonitoringContexts.Remove(id, out var context))
						context.MonitoringContext.Close();
					break;
				}
			case APIConstants.EventType.MonitoringNewReport:
				{
					var contextId = reader.ReadGuid();
					if (_openMonitoringContexts.TryGetValue(contextId, out var context) == false)
						throw new Exception($"Invalid event: unknown context id {contextId}");


					var typeFullName = reader.ReadStringToEnd(Encoding.UTF8);
					var reportType = typeof(GammaRayAPIClient).Assembly.GetType(typeFullName, false)
						?? throw new Exception($"Invalid event: unknown report type {typeFullName}");

					var newReport = context.MonitoringContext.NewReport(reportType);
					context.OpenReports[newReport.Component] = newReport;
				}
				break;
			case APIConstants.EventType.MonitoringFinishReport:
				{
					var contextId = reader.ReadGuid();
					if (_openMonitoringContexts.TryGetValue(contextId, out var context) == false)
						throw new Exception($"Invalid event: unknown context id {contextId}");

					var component = reader.ReadStringWithLength(Encoding.UTF8);
					if (context.OpenReports.Remove(component, out var report))
					{
						report.ControlContextNotification(enableOnChangedNotification: false);
						while (reader.RemainingLength != 0)
						{
							var propertyName = reader.ReadStringWithLength(Encoding.UTF8);
							var declaration = report.ListProperties()[propertyName];

							var propLength = reader.ReadInt();
							if (propLength != 0)
							{
								var jsonValue = reader.UnreadBufferPart[..propLength];
								reader.Advance(propLength);

								GetSetSystemReportPropertyMethod(declaration.ValueType)(this, report, declaration, jsonValue);
							}
						}

						report.Finish();
					}

				}
				break;
			case APIConstants.EventType.MonitoringSetReportProperty:
				{
					var contextId = reader.ReadGuid();
					if (_openMonitoringContexts.TryGetValue(contextId, out var context) == false)
						throw new Exception($"Invalid event: unknown context id {contextId}");

					var component = reader.ReadStringWithLength(Encoding.UTF8);
					if (context.OpenReports.TryGetValue(component, out var report) == false)
						throw new Exception($"Invalid event: unknown report {contextId}/{component}");

					var propertyName = reader.ReadStringWithLength(Encoding.ASCII);
					var declaration = report.ListProperties()[propertyName];
					var jsonValue = reader.UnreadBufferPart;

					GetSetSystemReportPropertyMethod(declaration.ValueType)(this, report, declaration, jsonValue);
				}
				break;
			default:
				return false;
		}

		return true;
	}

	public void FitPendingEvents(Span<byte> span)
	{
		var reader = new BufferReader(span);
		while (reader.RemainingLength > 0)
		{
			var contextCreationTime = reader.ReadDateTime();
			var contextId = reader.ReadGuid();
			var contextType = reader.ReadStringWithLength(Encoding.UTF8);

			var context = new MonitoringContext(contextType, contextCreationTime, _targetSystem, contextId);
			var openContext = new StateFullOpenMonitoringContext(context);
			_openMonitoringContexts[contextId] = openContext;

			var reportCount = reader.ReadInt();
			for (int i = 0; i < reportCount; i++)
			{
				var reportTypeName = reader.ReadStringWithLength(Encoding.UTF8);
				var reportType = typeof(GammaRayAPIClient).Assembly.GetType(reportTypeName, false)
					?? throw new Exception($"Invalid event: unknown report type {reportTypeName}");
				var report = context.NewReport(reportType);
				var finished = reader.ReadBoolean();

				while (true)
				{
					var propertyNameLengthOrTerminator = reader.ReadInt();
					if (propertyNameLengthOrTerminator == -1)
						break;
					var propertyName = reader.ReadString(Encoding.UTF8, propertyNameLengthOrTerminator);
					var declaration = report.ListProperties()[propertyName];

					var propLength = reader.ReadInt();
					if (propLength != 0)
					{
						var jsonValue = reader.UnreadBufferPart[..propLength];
						reader.Advance(propLength);

						GetSetSystemReportPropertyMethod(declaration.ValueType)(this, report, declaration, jsonValue);
					}
				}

				if (finished)
					report.Finish();
				else
					openContext.OpenReports[report.Component] = report;
			}

			Debug.WriteLine($"CONTEXT restored: {contextType}/{contextId} with {reportCount} reports");
		}
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
					types: [typeof(APIMonitoringEventListener), typeof(SystemReport), typeof(SystemReportPropertyDeclaration), typeof(ReadOnlySpan<byte>)]
				)!
				.MakeGenericMethod([propertyValueType])
				.CreateDelegate<SetSystemReportPropertyDelegate>(target: null);
			ConstructedSetSystemReportPropertyMethods.Add(propertyValueType, method);
		}
		return method;
	}

	private static void SetSystemReportProperty<TProperty>(APIMonitoringEventListener self, SystemReport report, SystemReportPropertyDeclaration property, ReadOnlySpan<byte> jsonValue)
	{
		var value = JsonSerializer.Deserialize<TProperty>(jsonValue, self._serializationOptions);
		report.WriteProperty(property.Name, value);
	}


	private delegate void SetSystemReportPropertyDelegate(APIMonitoringEventListener self, SystemReport report, SystemReportPropertyDeclaration property, ReadOnlySpan<byte> jsonValue);

	private class StateFullOpenMonitoringContext(MonitoringContext monitoringContext)
	{
		public MonitoringContext MonitoringContext { get; } = monitoringContext;

		public Dictionary<string, SystemReport> OpenReports { get; } = [];
	}
}
