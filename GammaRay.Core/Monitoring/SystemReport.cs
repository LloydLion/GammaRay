using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace GammaRay.Core.Monitoring;

public abstract class SystemReport : IDisposable
{
	private static readonly Dictionary<Type, ReportMetadata> GeneratedMetadata = [];


	private readonly ReportMetadata _myMetadata;
	private MonitoringContext? _monitoringContext;
	private bool _onChangedNotificationEnabled = true;


	protected SystemReport(string component)
	{
		Component = component;

		var myType = GetType();
		if (GeneratedMetadata.TryGetValue(myType, out var myMetadata) == false)
		{
			myMetadata = GenerateReportMetadata(myType);
			GeneratedMetadata.Add(myType, myMetadata);
		}
		_myMetadata = myMetadata;
	}


	public MonitoringContext MonitoringContext => _monitoringContext ?? throw new InvalidOperationException(
		"No monitoring context. Do not create report directly, use MonitoringContext.NewReport<TReport>() method"
	);

	public string Component { get; }

	public bool Finished { get; private set; } = false;


	public void Finish()
	{
		Finished = true;
		MonitoringContext.NotifyReportFinished(this);
	}

	void IDisposable.Dispose() => Finish();

	internal void SetContext(MonitoringContext monitoringContext)
	{
		_monitoringContext = monitoringContext;
	}

	protected void SetProperty<T>(ref ReportProperty<T> property, T newValue, [CallerMemberName] string propertyName = "None")
	{
		var ctx = MonitoringContext;

		property = newValue;

		if (_onChangedNotificationEnabled)
			ctx.NotifyReportChanged(this, propertyName, property, newValue);
	}

	public IReadOnlyDictionary<string, SystemReportPropertyDeclaration> ListProperties() => _myMetadata.PropertyDeclarations;

	public void ReadProperties<TReader>(TReader reader) where TReader : ISystemReportReader, allows ref struct
	{
		if (_myMetadata.GeneratedPropertyReaders.TryGetValue(typeof(TReader), out var propertyReaderDelegate) == false)
		{
			propertyReaderDelegate = GenerateReaderDelegate(typeof(TReader));
			_myMetadata.GeneratedPropertyReaders.Add(typeof(TReader), propertyReaderDelegate);
		}

		((ReportReaderDelegate<TReader>)propertyReaderDelegate).Invoke(this, reader);
	}

	public ReportProperty<TProperty> ReadProperty<TProperty>(string propertyName) =>
		((ReportPropertyGetter<TProperty>)_myMetadata.Properties[propertyName].Reader)(this);

	public void WriteProperty<TProperty>(string propertyName, TProperty newValue) =>
		((ReportPropertySetter<TProperty>)_myMetadata.Properties[propertyName].Writer)(this, ReportProperty.Create(newValue));

	public void ControlContextNotification(bool enableOnChangedNotification) => _onChangedNotificationEnabled = enableOnChangedNotification;


	private static ReportMetadata GenerateReportMetadata(Type reportType)
	{
		var properties = reportType.GetProperties(BindingFlags.Instance | BindingFlags.Public).Where(prop =>
			prop.CanRead && prop.CanWrite && prop.PropertyType.IsGenericType && prop.PropertyType.GetGenericTypeDefinition() == typeof(ReportProperty<>)
		);

		var result = new List<ReportPropertyMetadata>();
		foreach (var property in properties)
		{
			var name = property.Name;
			var valueType = property.PropertyType.GetGenericArguments()[0];

			var reportParameter = Expression.Parameter(typeof(object), "report");
			var newValueParameter = Expression.Parameter(property.PropertyType, "newValue");
			var setExpression = Expression.Assign(Expression.Property(Expression.Convert(reportParameter, reportType), property), newValueParameter);
			var setterDelegateType = typeof(ReportPropertySetter<>).MakeGenericType([valueType]);
			var setterDelegate = Expression.Lambda(setterDelegateType, setExpression, reportParameter, newValueParameter).Compile();

			reportParameter = Expression.Parameter(typeof(object), "report");
			var getExpression = Expression.Property(Expression.Convert(reportParameter, reportType), property);
			var getterDelegateType = typeof(ReportPropertyGetter<>).MakeGenericType([valueType]);
			var getterDelegate = Expression.Lambda(getterDelegateType, getExpression, reportParameter).Compile();

			result.Add(new ReportPropertyMetadata(new SystemReportPropertyDeclaration(name, valueType, property), getterDelegate, setterDelegate));
		}

		return new ReportMetadata(result.ToArray());
	}

	private Delegate GenerateReaderDelegate(Type readerType)
	{
		var reportParam = Expression.Parameter(typeof(object), "report");
		var readerParam = Expression.Parameter(readerType, "reader");

		var typedReport = Expression.Convert(reportParam, GetType());
		
		var expressions = new List<Expression>();

		var feedMethodGeneric = readerType.GetMethod(nameof(ISystemReportReader.FeedProperty), BindingFlags.Instance | BindingFlags.Public)!;

		foreach (var property in _myMetadata.Properties.Values)
		{
			var propertyExpression = Expression.Property(typedReport, property.Declaration.PropertyInfo);
			var propertyName = Expression.Constant(property.Declaration.Name);

			var feedMethod = feedMethodGeneric.MakeGenericMethod([property.Declaration.ValueType]);

			var call = Expression.Call(readerParam, feedMethod, [propertyName, propertyExpression]);

			expressions.Add(call);
		}

		var body = Expression.Block(expressions);

		var lambda = Expression.Lambda(
			typeof(ReportReaderDelegate<>).MakeGenericType(readerType),
			body,
			[reportParam, readerParam]
		);

		return lambda.Compile();
	}


	private class ReportMetadata(ReportPropertyMetadata[] properties)
	{
		public Dictionary<Type, Delegate> GeneratedPropertyReaders { get; } = [];

		public Dictionary<string, ReportPropertyMetadata> Properties { get; } = properties.ToDictionary(s => s.Declaration.Name);

		public IReadOnlyDictionary<string, SystemReportPropertyDeclaration> PropertyDeclarations { get; } = properties.Select(s => s.Declaration).ToDictionary(s => s.Name);
	}

	private class ReportPropertyMetadata(SystemReportPropertyDeclaration declaration, Delegate reader, Delegate writer)
	{
		public SystemReportPropertyDeclaration Declaration { get; } = declaration;

		public Delegate Reader { get; } = reader;

		public Delegate Writer { get; } = writer;
	}

	private delegate void ReportReaderDelegate<TReader>(object report, TReader reader)
		where TReader : ISystemReportReader, allows ref struct;

	private delegate void ReportPropertySetter<TProperty>(object report, ReportProperty<TProperty> newValue);

	private delegate ReportProperty<TProperty> ReportPropertyGetter<TProperty>(object report);
}
