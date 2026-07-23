using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;

namespace GammaRay.Core.Monitoring;

public abstract class SystemReport : IDisposable
{
	private static readonly Dictionary<Type, ReportClassData> GeneratedClassData = [];


	private readonly ReportClassData _myClass;
	private readonly TrackableProcedure? _autoBindProcedure;
	private TrackableProcedure? _boundProcedure;
	private SystemReportBindingParameters _bindingParameters;


	protected SystemReport(TrackableProcedure? autoBindProcedure = null)
	{
		_myClass = GetReportClassData(GetType());
		_autoBindProcedure = autoBindProcedure;
	}


	public TrackableProcedure Procedure => _boundProcedure ?? throw new InvalidOperationException("Report is not bound to a procedure");

	public SystemReportBindingParameters BindingParameters => IsBound ? _bindingParameters : throw new InvalidOperationException("Report is not bound to a procedure");

	[MemberNotNullWhen(true, nameof(_boundProcedure))]
	public bool IsBound => _boundProcedure is not null;

	public string ClassIdentification => GetType().FullName ?? throw new UnreachableException();

	public SystemReportMetadata Metadata => _myClass.Metadata;


	internal void BindProcedure(TrackableProcedure procedure, SystemReportBindingParameters binding)
	{
		_boundProcedure = procedure;
		_bindingParameters = binding;
	}

	public IReadOnlyDictionary<string, SystemReportPropertyDeclaration> ListProperties() => _myClass.PropertyDeclarations;

	public void ReadProperties<TReader>(TReader reader) where TReader : ISystemReportReader, allows ref struct
	{
		if (_myClass.GeneratedPropertyReaders.TryGetValue(typeof(TReader), out var propertyReaderDelegate) == false)
		{
			propertyReaderDelegate = GenerateReaderDelegate(typeof(TReader));
			_myClass.GeneratedPropertyReaders.Add(typeof(TReader), propertyReaderDelegate);
		}

		((ReportReaderDelegate<TReader>)propertyReaderDelegate).Invoke(this, reader);
	}

	public ReportProperty<TProperty> ReadProperty<TProperty>(string propertyName) =>
		((ReportPropertyGetter<TProperty>)_myClass.Properties[propertyName].Reader)(this);

	public void WriteProperty<TProperty>(string propertyName, TProperty newValue) =>
		((ReportPropertySetter<TProperty>)_myClass.Properties[propertyName].Writer)(this, ReportProperty.Create(newValue));

	void IDisposable.Dispose()
	{
		if (_autoBindProcedure is null)
			throw new InvalidOperationException("No system to bind, in case of manual bind remove Dispose call");
		_autoBindProcedure.CommitReport(this);
		GC.SuppressFinalize(this);
	}


	public static Type GetTypeByClassIdentification(string classIdentification) => typeof(SystemReport).Assembly.GetType(classIdentification, throwOnError: true)!;

	public static SystemReport CreateNewReportByType(Type type) => GetReportClassData(type).FactoryMethod();

	public static SystemReport CreateNewReportByClassIdentification(string classIdentification) => CreateNewReportByType(GetTypeByClassIdentification(classIdentification));

	private static ReportClassData GetReportClassData(Type reportType)
	{
		if (GeneratedClassData.TryGetValue(reportType, out var reportClassData) == false)
			GeneratedClassData.Add(reportType, reportClassData = GenerateReportClassData(reportType));
		return reportClassData;
	}

	private static ReportClassData GenerateReportClassData(Type reportType)
	{
		var properties = reportType.GetProperties(BindingFlags.Instance | BindingFlags.Public).Where(prop =>
			prop.CanRead && prop.CanWrite && prop.PropertyType.IsGenericType && prop.PropertyType.GetGenericTypeDefinition() == typeof(ReportProperty<>)
		);

		var result = new List<ReportPropertyInfo>();
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

			result.Add(new ReportPropertyInfo(new SystemReportPropertyDeclaration(name, valueType, property), getterDelegate, setterDelegate));
		}


		var constructor = reportType.GetConstructors().OrderBy(s => s.GetParameters().Length).First(s => s.GetParameters().All(p => p.HasDefaultValue));
		var constructorCallParameters = constructor.GetParameters().Select(p =>
			Expression.Constant(p.ParameterType.IsValueType && p.DefaultValue is null ? Activator.CreateInstance(p.ParameterType) : p.DefaultValue, p.ParameterType)
		).ToArray();
		var factoryExpression = Expression.New(constructor, constructorCallParameters);
		var factoryMethod = Expression.Lambda<Func<SystemReport>>(factoryExpression, []).Compile();


		var metadata = reportType.GetCustomAttribute<SystemReportMetadataAttribute>(inherit: false)?.Metadata
			?? throw new NullReferenceException($"{reportType} has no required SystemReportMetadata attribute");
		Debug.Assert(metadata is not null);


		return new ReportClassData(result.ToArray(), factoryMethod, metadata);
	}

	private Delegate GenerateReaderDelegate(Type readerType)
	{
		var reportParam = Expression.Parameter(typeof(object), "report");
		var readerParam = Expression.Parameter(readerType, "reader");

		var typedReport = Expression.Convert(reportParam, GetType());
		
		var expressions = new List<Expression>();

		var feedMethodGeneric = readerType.GetMethod(nameof(ISystemReportReader.FeedProperty), BindingFlags.Instance | BindingFlags.Public)!;

		foreach (var property in _myClass.Properties.Values)
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


	private class ReportClassData(ReportPropertyInfo[] properties, Func<SystemReport> factoryMethod, SystemReportMetadata metadata)
	{
		public Func<SystemReport> FactoryMethod { get; } = factoryMethod;

		public Dictionary<Type, Delegate> GeneratedPropertyReaders { get; } = [];

		public Dictionary<string, ReportPropertyInfo> Properties { get; } = properties.ToDictionary(s => s.Declaration.Name);

		public IReadOnlyDictionary<string, SystemReportPropertyDeclaration> PropertyDeclarations { get; } = properties.Select(s => s.Declaration).ToDictionary(s => s.Name);

		public SystemReportMetadata Metadata { get; } = metadata;
	}

	private class ReportPropertyInfo(SystemReportPropertyDeclaration declaration, Delegate reader, Delegate writer)
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
