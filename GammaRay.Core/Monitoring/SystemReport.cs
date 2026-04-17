using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace GammaRay.Core.Monitoring;

public abstract class SystemReport : IDisposable
{
	private static readonly Dictionary<(Type ReportType, Type ReaderType), Delegate> GeneratedPropertyReaders = [];


	private MonitoringContext? _monitoringContext;


	protected SystemReport(string component)
	{
		Component = component;
	}


	public MonitoringContext MonitoringContext => _monitoringContext ?? throw new InvalidOperationException(
		"No monitoring context. Do not create report directly, use MonitoringContext.NewReport<TReport>() method"
	);

	public string Component { get; }


	public void Finish()
	{
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
		ctx.NotifyReportChanged(this, propertyName, property, newValue);
	}

	public void ReadProperties<TReader>(TReader reader) where TReader : ISystemReportReader
	{
		var key = (ReportType: GetType(), ReaderType: typeof(TReader));
		if (GeneratedPropertyReaders.TryGetValue(key, out var propertyReaderDelegate) == false)
		{
			 propertyReaderDelegate = GenerateReaderDelegate(key.ReportType, key.ReaderType);
			 GeneratedPropertyReaders.Add(key, propertyReaderDelegate);
		}

		((Action<object, TReader>)propertyReaderDelegate).Invoke(this, reader);
	}

	private Delegate GenerateReaderDelegate(Type reportType, Type readerType)
	{
		var reportParam = Expression.Parameter(typeof(object), "report");
		var readerParam = Expression.Parameter(readerType, "reader");

		var typedReport = Expression.Convert(reportParam, reportType);

		var expressions = new List<Expression>();

		var feedMethodGeneric = readerType.GetMethod(nameof(ISystemReportReader.FeedProperty), BindingFlags.Instance | BindingFlags.Public)!;

		var properties = reportType.GetProperties(BindingFlags.Instance | BindingFlags.Public).Where(prop =>
			prop.CanRead && prop.PropertyType.IsGenericType && prop.PropertyType.GetGenericTypeDefinition() == typeof(ReportProperty<>)
		);

		foreach (var prop in properties)
		{
			var tProperty = prop.PropertyType.GetGenericArguments()[0];

			var propertyExpression = Expression.Property(typedReport, prop);
			var propertyName = Expression.Constant(prop.Name);

			var feedMethod = feedMethodGeneric.MakeGenericMethod([tProperty]);

			var call = Expression.Call(readerParam, feedMethod, [propertyName, propertyExpression]);

			expressions.Add(call);
		}

		var body = Expression.Block(expressions);

		var lambda = Expression.Lambda(
			typeof(Action<,>).MakeGenericType(typeof(object), readerType),
			body,
			[reportParam, readerParam]
		);

		return lambda.Compile();
	}
}
