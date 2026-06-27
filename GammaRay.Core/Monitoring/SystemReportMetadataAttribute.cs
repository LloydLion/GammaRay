namespace GammaRay.Core.Monitoring;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class SystemReportMetadataAttribute(string role, string component, string task) : Attribute
{
	public SystemReportMetadata Metadata { get; } = new(role, component, task);
}
