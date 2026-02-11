namespace GammaRay.Core.Inbound
{
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
	internal sealed class RecommendedDriverNameAttribute(string recommendedName) : Attribute
	{
		public string RecommendedName { get; } = recommendedName;
	}
}
