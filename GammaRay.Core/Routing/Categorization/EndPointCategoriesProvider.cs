using GammaRay.Core.Network;
using GammaRay.Core.Settings;

namespace GammaRay.Core.Routing.Categorization;

public sealed class EndPointCategoriesProvider
{
	public const string DefaultCategoryName = "default";


	public EndPointCategoriesProvider(IRawSettingsProvider<IReadOnlyCollection<EndPointCategory>> rawProvider)
	{
		var rawCategories = rawProvider.Get();

		if (rawCategories.Any(s => s.Name == DefaultCategoryName))
			throw new ArgumentException($"Category name '{DefaultCategoryName}' is reserved");

		DefaultCategory = new EndPointCategory(DefaultCategoryName, [new EndPointPattern([])]);
		PlainCategories = rawCategories.Append(DefaultCategory).ToArray();
		Categories = PlainCategories.ToDictionary(c => c.Name);
	}


	public EndPointCategory DefaultCategory { get; }

	public IReadOnlyCollection<EndPointCategory> PlainCategories { get; }

	public IReadOnlyDictionary<string, EndPointCategory> Categories { get; }


	public EndPointCategory Categorize(WebEndPoint endPoint)
	{
		// Finding pattern with max level that matches end point among all categories
		// Return category of that pattern
		// If no match: return default category

		var categories = PlainCategories;

		(int Level, EndPointPattern Pattern, EndPointCategory Category)? match = null;

		foreach (var category in categories)
		{
			foreach (var pattern in category.Patterns)
			{
				if (match is not null && match.Value.Level >= pattern.Level)
					continue;

				if (pattern.IsMatch(endPoint))
					match = (pattern.Level, pattern, category);
			}
		}

		if (match is not null)
			return match.Value.Category;

		return DefaultCategory;
	}
}
