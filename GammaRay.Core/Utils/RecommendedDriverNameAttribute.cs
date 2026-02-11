namespace GammaRay.Core.Utils
{
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
	public sealed class RecommendedDriverNameAttribute(string recommendedName) : Attribute
	{
		public string RecommendedName { get; } = recommendedName;
	}
}
