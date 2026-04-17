namespace GammaRay.Core.Monitoring;

public readonly struct ReportProperty<TProperty>
{
	private readonly bool _set;
	private readonly TProperty _value;


	public ReportProperty(TProperty value)
	{
		_set = true;
		_value = value;
	}


	public bool IsSet => _set;

	public TProperty Value => _set ? _value : throw new InvalidOperationException("Access to unset property");


	public static implicit operator ReportProperty<TProperty>(TProperty value) => new(value);

	public static explicit operator TProperty(ReportProperty<TProperty> property) => property.Value;
}

public static class ReportProperty
{
	public static ReportProperty<TProperty> Create<TProperty>(TProperty value) => new(value);
}
