using System.Runtime.CompilerServices;

namespace GammaRay.Core.Monitoring;

public interface ISystemReportReader
{
	public void FeedProperty<TProperty>(string propertyName, ReportProperty<TProperty> property);
}


public static class SystemReportReaderExtensions
{
	extension<TReader>(TReader reader) where TReader : ISystemReportReader
	{
		public void FeedProperty<TProperty>(ReportProperty<TProperty> property,
			[CallerArgumentExpression(nameof(property))] string propertyName = "Captured from argument expression"
		)
		{
			reader.FeedProperty(propertyName, property);
		}
	}
}
